using System.Reflection;
using StackExchange.Redis;

namespace Demgel.Redis.Interfaces
{
    public interface ITypeConverter
    {
        /// <summary>
        /// Return a value that is exceptable to redis cache
        /// </summary>
        /// <returns></returns>
        RedisValue ToWrite(object prop);
        /// <summary>
        /// Reading from redis into an object you expect
        /// </summary>
        /// <returns></returns>
        object OnRead(RedisValue obj);
    }
}