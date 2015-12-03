using System;
using System.Reflection;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.Converters
{
    public class LogConverter : ITypeConverter
    {
        public RedisValue ToWrite(object prop)
        {
            return (long) prop;
        }

        public object OnRead(RedisValue obj, PropertyInfo info)
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