using System;
using System.Collections.Generic;
using DemgelRedis.ObjectManager.Attributes;

namespace DemgelRedis.Common
{
    public class RedisKeyObject
    {
        public string Prefix { get; set; }
        public string Id { get; set; }
        public string Suffix { get; set; }

        public string RedisKey
        {
            get
            {
                if (Prefix != null)
                {
                    return Suffix != null ? $"{Prefix}:{Id}:{Suffix}" : $"{Prefix}:{Id}";
                }
                return Suffix != null ? $"{Id}:{Suffix}" : Id;
            }
        }

        public RedisKeyObject(IEnumerable<Attribute> attributes, string id)
        {
            Id = id;
            ParseRedisKey(attributes);
        }

        public RedisKeyObject()
        {
        }

        private void ParseRedisKey(IEnumerable<Attribute> attributes)
        {
            foreach (var attr in attributes)
            {
                if (attr is RedisPrefix)
                {
                    Prefix = ((RedisPrefix)attr).Key;
                }
                else if (attr is RedisSuffix)
                {
                    Suffix = ((RedisSuffix)attr).Key;
                }
            }
        }
    }
}