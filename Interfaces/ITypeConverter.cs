using System.Reflection;
using StackExchange.Redis;

namespace DemgelRedis.Interfaces
{
    public interface ITypeConverter
    {
        /// <summary>
        /// Return a value that is acceptable to redis cache
        /// </summary>
        /// <returns></returns>
        RedisValue ToWrite(object prop);
        /// <summary>
        /// Reading from redis into an object you expect
        /// </summary>
        /// <returns></returns>
        object OnRead(RedisValue obj, PropertyInfo info);
    }
}