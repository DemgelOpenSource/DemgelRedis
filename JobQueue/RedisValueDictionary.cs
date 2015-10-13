using System;
using System.Collections.Generic;
using StackExchange.Redis;

namespace DemgelRedis.JobQueue
{
    [Serializable]
    public class RedisValueDictionary : Dictionary<RedisValue, RedisValue>
    {
        public RedisValue Key { get; set; }        
    }
}