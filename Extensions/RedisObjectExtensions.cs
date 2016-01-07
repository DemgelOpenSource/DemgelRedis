using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using DemgelRedis.ObjectManager.Proxy;
using System;
using System.Threading.Tasks;

namespace DemgelRedis.Extensions
{
    public static class RedisObjectExtensions
    {
        internal static object CreateProxy(this IRedisObject redisObect, CommonData commonData, out RedisKeyObject key)
        {
            var argumentType = redisObect.GetType();
            key = new RedisKeyObject(argumentType, string.Empty);

            commonData.RedisDatabase.GenerateId(key, redisObect, commonData.RedisObjectManager.RedisBackup);

            var newArgument = commonData.RedisObjectManager.RetrieveObjectProxy(argumentType, key.Id, commonData.RedisDatabase, redisObect);

            return newArgument;
        }

        public static void DeleteRedisObject(this IRedisObject redisObject)
        {
            CommonData data;
            if (!redisObject.GetCommonData(out data))
            {
                throw new Exception("Objects needs to be a Proxy (call RetrieveObjectProxy first)");
            }

            data.Processing = true;
            var target = redisObject.GetTarget();
            foreach(var prop in target.GetType().GetProperties())
            {
                if (prop.HasAttribute<RedisDeleteCascade>())
                {
                    if (!prop.GetAttribute<RedisDeleteCascade>().Cascade) continue;
                }
                var value = prop.GetValue(redisObject, null);
                if (value is IRedisObject)
                {
                    ((IRedisObject)value).DeleteRedisObject();
                }
            }
            data.Processing = false;

            var key = new RedisKeyObject(redisObject.GetType(), string.Empty);
            data.RedisDatabase.GenerateId(key, redisObject, data.RedisObjectManager.RedisBackup);

            data.RedisDatabase.KeyDelete(key.RedisKey);
            data.RedisObjectManager.RedisBackup?.DeleteHash(key);
        }

        public static bool GetCommonData(this IRedisObject redisObject, out CommonData data)
        {
            if (!(redisObject is IProxyTargetAccessor))
            {
                data = null;
                return false;
            }

            data = ((IProxyTargetAccessor)redisObject).GetCommonData();
            return true;
        }

        public static T GetTarget<T>(this T redisObject)
            where T : IRedisObject
        {
            if (!(redisObject is IProxyTargetAccessor))
            {
                // There is not need to get the target, it already is one
                return redisObject;
            }

            var accessor = (IProxyTargetAccessor)redisObject;

            return (T)accessor.DynProxyGetTarget();
        }
    }
}