using System;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.Converters
{
    public class LongConverter : ITypeConverter
    {
        public RedisValue ToWrite(object prop)
        {
            return (long) prop;
        }

        public object OnRead(RedisValue obj)
        {
            long value;
            if (long.TryParse(obj, out value))
            {
                return value;
            }

            throw new InvalidCastException("obj is not an long value");
        }
    }
}