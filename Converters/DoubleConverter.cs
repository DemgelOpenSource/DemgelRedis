using System;
using System.Reflection;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.Converters
{
    public class DoubleConverter : ITypeConverter
    {
        public RedisValue ToWrite(object prop)
        {
            return (double) prop;
        }

        public object OnRead(RedisValue obj, PropertyInfo info)
        {
            double value;
            if (double.TryParse(obj, out value))
            {
                return value;
            }

            throw new InvalidCastException("obj is not an double value");
        }
    }
}