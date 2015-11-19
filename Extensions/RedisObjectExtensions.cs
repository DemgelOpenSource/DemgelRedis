
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Proxy;

namespace DemgelRedis.Extensions
{
    public static class RedisObjectExtensions
    {
        public static object CreateProxy(this IRedisObject redisObect, CommonData commonData, out RedisKeyObject key)
        {
            var argumentType = redisObect.GetType();
            key = new RedisKeyObject(argumentType, string.Empty);

            commonData.RedisDatabase.GenerateId(key, redisObect, commonData.RedisObjectManager.RedisBackup);

            var newArgument = commonData.RedisObjectManager.RetrieveObjectProxy(argumentType, key.Id, commonData.RedisDatabase, redisObect);

            return newArgument;
        }
    }
}