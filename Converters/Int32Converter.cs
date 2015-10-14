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
            if (obj.IsInteger)
            {
                return (int) obj;
            }

            throw new InvalidCastException("obj is not an Int32 value");
        }
    }
}