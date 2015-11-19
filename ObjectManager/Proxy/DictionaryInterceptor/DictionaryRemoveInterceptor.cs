using System.Collections;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;

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
            if (original == null) return;
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

            // Delete the keys
            _commonData.RedisObjectManager.RedisBackup?.DeleteHashValue((string)invocation.Arguments[0], hashKey);
            _commonData.RedisDatabase.HashDelete(hashKey.RedisKey, (string)invocation.Arguments[0]);
        }
    }
}