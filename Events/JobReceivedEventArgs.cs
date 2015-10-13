using System;
using DemgelRedis;
using DemgelRedis.JobQueue;

namespace Demgel.Redis.Events
{
    public class JobReceivedEventArgs : EventArgs
    {
        public JobReceivedEventArgs(RedisValueDictionary dict, string key)
        {
            Dictionary = dict;
            Key = key;
        }

        public RedisValueDictionary Dictionary { get; }

        public string Key { get; }
    }
}