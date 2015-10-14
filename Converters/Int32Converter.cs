using System;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.Converters
{
    public class Int32Converter : ITypeConverter
    {
        public RedisValue ToWrite(object prop)
        {
            return (int) prop;
        }

        public object OnRead(RedisValue obj)
        {
            int value;
            if (int.TryParse(obj, out value))
            {
                return value;
            }

            throw new InvalidCastException("obj is not an Int32 value");
        }
    }
}