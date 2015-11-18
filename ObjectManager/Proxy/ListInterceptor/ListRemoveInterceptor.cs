using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy.ListInterceptor
{
    public class ListRemoveInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public ListRemoveInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var prop = ((IProxyTargetAccessor)invocation.Proxy).GetTargetPropertyInfo();
            var listKey = new RedisKeyObject(prop, _commonData.Id);

            var original = invocation.Arguments[0];
            // 1 Figure out if this is removing a IRedisObject
            if (original is IRedisObject)
            {
                var deleteCascade = prop.GetCustomAttribute<RedisDeleteCascade>();

                var objectKey = new RedisKeyObject(original.GetType(), string.Empty);
                _commonData.RedisDatabase.GenerateId(objectKey, original, _commonData.RedisObjectManager.RedisBackup);

                if (!(deleteCascade != null && !deleteCascade.Cascade))
                {
                    _commonData.RedisObjectManager.DeleteObject(original, objectKey.Id, _commonData.RedisDatabase);
                }

                _commonData.RedisObjectManager.RedisBackup?.RemoveListItem(listKey, objectKey.RedisKey);
                _commonData.RedisDatabase.ListRemove(listKey.RedisKey, objectKey.RedisKey, 1);
            }
            else
            {
                _commonData.RedisObjectManager.RedisBackup?.RemoveListItem(listKey, (RedisValue)original);
                _commonData.RedisDatabase.ListRemove(listKey.RedisKey, (RedisValue)original, 1);
            }

            invocation.Proceed();
        }
    }
}