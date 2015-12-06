using System;
using System.Collections;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor
{
    public class DictionaryRemoveInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public DictionaryRemoveInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var prop = ((IProxyTargetAccessor)invocation.Proxy).GetTargetPropertyInfo();
            var hashKey = new RedisKeyObject(prop, _commonData.Id);

            var accessor = (IProxyTargetAccessor)invocation.Proxy;
            var original = (accessor.DynProxyGetTarget() as IDictionary)?[invocation.Arguments[0]];

            // Removed null check and return... should remove from rediscache

            // 1 Figure out if this is removing a IRedisObject
            if (original is IRedisObject)
            {
                // Look for cascade, if cascade is false don't do anything for redisobject
                var deleteCascade = prop.GetCustomAttribute<RedisDeleteCascade>();

                if (!(deleteCascade != null && !deleteCascade.Cascade))
                {
                    var objectKey = new RedisKeyObject(original.GetType(), string.Empty);
                    _commonData.RedisObjectManager.RedisBackup?.DeleteHash(objectKey);
                    _commonData.RedisDatabase.GenerateId(objectKey, original, _commonData.RedisObjectManager.RedisBackup);
                    _commonData.RedisObjectManager.DeleteObject(original, objectKey.Id, _commonData.RedisDatabase);
                }
            }

            RedisValue value;
            if (!(invocation.Arguments[0] is RedisValue))
            {
                if (!_commonData.RedisObjectManager.TryConvertToRedisValue(invocation.Arguments[0], out value))
                {
                    throw new Exception("Cannot convert to RedisValue");
                }
            }
            else
            {
                value = (RedisValue)invocation.Arguments[0];
            }

            // Delete the keys
            _commonData.RedisObjectManager.RedisBackup?.DeleteHashValue(value, hashKey);
            _commonData.RedisDatabase.HashDelete(hashKey.RedisKey, value);

            
            invocation.Proceed();
        }
    }
}