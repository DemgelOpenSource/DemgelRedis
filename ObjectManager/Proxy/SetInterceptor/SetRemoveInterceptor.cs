using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;

namespace DemgelRedis.ObjectManager.Proxy.SetInterceptor
{
    public class SetRemoveInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public SetRemoveInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var prop = ((IProxyTargetAccessor)invocation.Proxy).GetTargetPropertyInfo();
            var setKey = new RedisKeyObject(prop, _commonData.Id);

            //var accessor = (IProxyTargetAccessor)invocation.Proxy;
            var original = invocation.Arguments[0];

            // Removed null check and return... should remove from rediscache
            var objectKey = new RedisKeyObject(original.GetType(), string.Empty);
            
            // 1 Figure out if this is removing a IRedisObject
            if (original is IRedisObject)
            {
                // Look for cascade, if cascade is false don't do anything for redisobject
                var deleteCascade = prop.GetCustomAttribute<RedisDeleteCascade>();

                if (!(deleteCascade != null && !deleteCascade.Cascade))
                {
                    _commonData.RedisObjectManager.RedisBackup?.DeleteHash(objectKey);
                    _commonData.RedisDatabase.GenerateId(objectKey, original, _commonData.RedisObjectManager.RedisBackup);
                    _commonData.RedisObjectManager.DeleteObject(original, objectKey.Id, _commonData.RedisDatabase);
                }
            }

            // Delete the keys
            // TODO work on Table backup for sets
            //_commonData.RedisObjectManager.RedisBackup?.DeleteSet((string)invocation.Arguments[0], setKey);
            _commonData.RedisDatabase.SortedSetRemove(setKey.RedisKey, objectKey.RedisKey);

            invocation.Arguments[0] = original;
            invocation.Proceed();
        }
    }
}