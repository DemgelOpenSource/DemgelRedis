using System.Collections.Generic;
using StackExchange.Redis;

namespace Demgel.Redis
{
    public class RedisValueDictionary : Dictionary<RedisValue, RedisValue>
    {
        public RedisValue Key { get; set; }        
    }
}