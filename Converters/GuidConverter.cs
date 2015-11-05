using System;
using System.Reflection;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.Converters
{
    public class GuidConverter : ITypeConverter
    {
        /// <summary>
        /// Used to convert Guids to a byte[] array ready to be stored in a Redis Cache
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public RedisValue ToWrite(object prop)
        {
            var guid = prop as Guid?;
            return guid?.ToByteArray() ?? Guid.Empty.ToByteArray();
        }

        public object OnRead(RedisValue value, PropertyInfo info)
        {
            Guid guid;
            if (value.IsByteArray()) return new Guid((byte[])value);
            if (Guid.TryParse(value, out guid)) return guid;
            
            throw new ArgumentException("Value is not a Guid");
        }
    }
}