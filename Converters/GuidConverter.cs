using System;
using System.Reflection;
using Demgel.Redis.Interfaces;
using StackExchange.Redis;

namespace Demgel.Redis.Converters
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

        public object OnRead(RedisValue value)
        {
            Guid guid;
            if (Guid.TryParse(value, out guid)) return guid;
            try
            {
                guid = new Guid((byte[])value);
            }
            catch
            {
                guid = Guid.Empty;
            }
            return guid;
        }
    }
}