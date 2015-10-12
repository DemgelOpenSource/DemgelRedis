using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Demgel.Redis.Events;
using StackExchange.Redis;

namespace Demgel.Redis.JobQueue
{
    /// <summary>
    /// Needed: (2) Redis queues 1 for new messages, 1 for currently processing messages
    /// Needed: processing messages list is FILO
    /// 
    /// The queues will only contain the key to the message in redis, which is stored as
    /// a single entity for quick lookup
    /// 
    /// jobQueue  -- processingQueue
    /// job:1        job:2
    /// 
    /// job:1 (job to do index 1)
    /// job:2 (job to do index 2)
    /// 
    /// Finish method, will LREM key, and Remove Key from database
    /// 
    /// ON adding a new job, send a Publish to say a new job is added
    /// 
    /// ON taking a job, RPOPLPUSH from jobQueue to processingQueue
    /// 
    /// Checking for failed jobs, experation time 10 seconds (this should be long enough 
    /// to process anything)
    /// If job stays in processingQueue for longer than timeout, RPOPLPUSH to jobQueue
    /// 
    /// </summary>
    public class RedisJobQueue
    {
        public delegate RedisJobQueue Factory(string jobName, CancellationToken cancellationToken = new CancellationToken());

        private IConnectionMultiplexer ConnectionMultiplexer => _lazyConnection.Value;
        private readonly Lazy<IConnectionMultiplexer> _lazyConnection;

        private readonly string _jobQueue;
        private readonly string _processingQueue;
        private readonly string _subChannel;
        private readonly string _jobName;
        private readonly string _deadMessage;

        private readonly string _luaTest = @"
                                            local timeToKill = tonumber(ARGV[1])
                                            local jobs = redis.call('LRANGE', KEYS[1], 0, -1)
                                            local result = {}
                                            local count = 1;
                                            for i, job in ipairs(jobs) do
                                                local active = tonumber(redis.call('HMGET', job, 'active'))
                                                if active < timeToKill then
                                                    result[count] = job
                                                    count = count + 1
                                                end
                                            end
                                            return result;
                                            ";

        private readonly CancellationToken _cancellationToken;

        // ReSharper disable once NotAccessedField.Local
        private Task _managementTask;

        private bool _receiving;

        public event EventHandler<JobReceivedEventArgs> OnJobReceived; 

        public RedisJobQueue(Lazy<IConnectionMultiplexer> multiplexer, string jobName, CancellationToken cancellationToken = new CancellationToken())
        {
            _lazyConnection = multiplexer;
            _jobQueue = $"{jobName}:jobs";
            _processingQueue = $"{jobName}:process";
            _subChannel = $"{jobName}:channel";
            _deadMessage = $"{jobName}:deadmessage";
            _jobName = jobName;
            _cancellationToken = cancellationToken;
        }

        private IDatabase Database => ConnectionMultiplexer.GetDatabase();

        /// <summary>
        /// When a job is finished, remove it from the processingQueue and from the
        /// cache database.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="success">if false requeue for another attempt</param>
        public async Task Finish(string key, bool success = true)
        {
            var db = Database;
            await db.ListRemoveAsync(_processingQueue, key, 0, CommandFlags.FireAndForget);

            if (success)
            {
                await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
                return;
            }

            // How many times to fail before dead
            if (await db.HashExistsAsync(key, "failedcount"))
            {
                var count = await db.HashGetAsync(key, "failedcount");
                if (count.IsInteger && (int) count >= 10)
                {
                    // for now, delete the key, later we might integrate a dead message
                    // queue
                    await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
                    return;
                }
            }

            await db.HashIncrementAsync(key, "failedcount", 1, CommandFlags.FireAndForget);
            await db.HashDeleteAsync(key, "active", CommandFlags.FireAndForget);
            await db.ListRightPushAsync(_jobQueue, key, When.Always, CommandFlags.FireAndForget);

            ConnectionMultiplexer.GetSubscriber().Publish(_subChannel, "", CommandFlags.FireAndForget);
        }

        /// <summary>
        /// Do we consume messages from the queue
        /// </summary>
        /// <returns></returns>
        public RedisJobQueue AsConsumer()
        {
            var sub = ConnectionMultiplexer.GetSubscriber();
            sub.Subscribe(_subChannel, async (channel, value) => await HandleNewJobs());

            // Assume on starting that we have jobs waiting to be handled
            Task.Factory.StartNew(async () => await HandleNewJobs(), TaskCreationOptions.PreferFairness);
            return this;
        }

        /// <summary>
        /// Runs a Task every 10 seconds to see if any remaining items are in
        /// processing queue
        /// </summary>
        /// <returns></returns>
        public RedisJobQueue AsManager()
        {
            _managementTask = Task.Factory.StartNew(async () =>
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(10000, _cancellationToken);
                        var timeToKill = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - 10000;
                        //RedisValue[] values = await Database.ListRangeAsync(_processingQueue);
                        //foreach (var value in from value in values let activeTime = (double) Database.HashGet((string)value, "active") where activeTime < timeToKill select value)
                        var jobs =
                            (RedisValue[])
                                await
                                    Database.ScriptEvaluateAsync(_luaTest,
                                        new List<RedisKey> {_processingQueue}.ToArray(),
                                        new List<RedisValue> {timeToKill}.ToArray());

                        foreach (var value in jobs)
                        {
                            await Finish(value, false);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        Trace.WriteLine("Management Thread Finished.");
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(e.Message);
                    }
                    
                }
            }, TaskCreationOptions.LongRunning);

            return this;
        }

        public RedisJobQueue PreserveAsyncOrder(bool preserveOrder)
        {
            ConnectionMultiplexer.PreserveAsyncOrder = preserveOrder;
            return this;
        }

        /// <summary>
        /// Move key from JobQueue to processingQueue, get key value from cache.
        /// 
        /// Also set the active field. Indicates when job was retrieved so we can monitor
        /// its time.
        /// </summary>
        /// <returns></returns>
        private async Task<RedisValueDictionary> GetJobAsync()
        {
            var db = Database;
            var value = new RedisValueDictionary();
            while (!_cancellationToken.IsCancellationRequested)
            {
                string key = await db.ListRightPopLeftPushAsync(_jobQueue, _processingQueue);
                // If key is null, then nothing was there to get, so no value is available
                if (string.IsNullOrEmpty(key))
                {
                    value.Clear();
                    break;
                }

                await db.HashSetAsync(key, "active", (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds);
                value = (RedisValueDictionary)(await db.HashGetAllAsync(key)).ToDictionary();

                // if Count is 0, remove it and check for the next job
                if (value.Count == 0)
                {
                    await db.ListRemoveAsync(_processingQueue, key, flags: CommandFlags.FireAndForget);
                    continue;
                }

                value.Key = key;
                
                break;
            }
            return value;
        }

        /// <summary>
        /// We have received an indicator that new jobs are available
        /// We process until we are out of jobs.
        /// </summary>
        private async Task HandleNewJobs()
        {
            if (_receiving) return;

            _receiving = true;

            var job = await GetJobAsync();
            // If a valid job cannot be found, it will return an empty Dictionary
            while (job.Count != 0)
            {
                // Fire the Event
                OnJobReceived?.Invoke(this, new JobReceivedEventArgs(job, job.Key));
                // Get a new job if there is one
                job = await GetJobAsync();
            }
            _receiving = false;
        }

        /// <summary>
        /// Add a job to the Queue
        /// </summary>
        /// <param name="job"></param>
        public void AddJob(RedisValue job)
        {
            Task.Run(() => AddJobAsync(job));
        }

        /// <summary>
        /// Add a job to the Queue (async)
        /// 
        /// the single RedisValue is marked as 'payload' in the hash
        /// </summary>
        /// <param name="job">payload</param>
        public async Task AddJobAsync(RedisValue job)
        {
            if (job.IsNullOrEmpty) return;

            var db = Database;
            var key = await GetNextJobId();
            await db.HashSetAsync(key, "payload", job);
            await db.ListLeftPushAsync(_jobQueue, key, flags: CommandFlags.FireAndForget);
            await ConnectionMultiplexer.GetSubscriber().PublishAsync(_subChannel, "", CommandFlags.FireAndForget);
        }

        /// <summary>
        /// Add a job to the Queue (async)
        /// 
        /// Adds a Dictionary to the message, both values are RedisValue.
        /// 
        /// Reserved names for dictionary keys are 'key', 'active', 'failedcount'
        /// </summary>
        /// <param name="parametersDictionary"></param>
        /// <returns></returns>
        public async Task AddJobAsync(RedisValueDictionary parametersDictionary)
        {
            if (parametersDictionary.Count == 0) return;
            if (parametersDictionary.ContainsKey("key") 
                || parametersDictionary.ContainsKey("active")
                || parametersDictionary.ContainsKey("failedcount"))
            {
                Trace.WriteLine("parameter 'key', 'active' or 'failedcount' are reserved.");
                return;
            }

            var db = Database;
            //var id = await db.StringIncrementAsync($"{_jobName}:jobid");
            var key = await GetNextJobId();

            await db.HashSetAsync(key, parametersDictionary.Select(entries => new HashEntry(entries.Key, entries.Value)).ToArray());

            await db.ListLeftPushAsync(_jobQueue, key, When.Always, CommandFlags.FireAndForget);
            await ConnectionMultiplexer.GetSubscriber().PublishAsync(_subChannel, "", CommandFlags.FireAndForget);
        }

        private async Task<string> GetNextJobId()
        {
            var db = Database;
            var id = await db.StringIncrementAsync($"{_jobName}:jobid");
            return $"{_jobName}:{id}";
        }
    }
}