using System.Collections;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy.ListInterceptor
{
    public class ListSetInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public ListSetInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var prop = ((IProxyTargetAccessor)invocation.Proxy).GetTargetPropertyInfo();

            var listKey = new RedisKeyObject(prop, _commonData.Id);

            // Make sure the list is Restored
            _commonData.RedisObjectManager.RedisBackup?.RestoreList(_commonData.RedisDatabase, listKey);

            // We will need the Original value no matter what
            var accessor = (IProxyTargetAccessor) invocation.Proxy;
            var original = (accessor.DynProxyGetTarget() as IList)?[(int) invocation.Arguments[0]];
            if (original == null) return;

            // We are checking if the new item set to the list is actually a Proxy (if not created it)
            var redisObject = invocation.Arguments[1] as IRedisObject;
            if (redisObject != null)
            {
                var originalKey = new RedisKeyObject(original.GetType(), string.Empty);
                _commonData.RedisDatabase.GenerateId(originalKey, original, _commonData.RedisObjectManager.RedisBackup);

                RedisKeyObject key;
                if (!(invocation.Arguments[1] is IProxyTargetAccessor))
                {
                    var proxy = CreateProxy(redisObject, out key);
                    invocation.Arguments[1] = proxy;
                }
                else
                {
                    key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                    _commonData.RedisDatabase.GenerateId(key, invocation.Arguments[1],
                        _commonData.RedisObjectManager.RedisBackup);
                }

                if (_commonData.Processing)
                {
                    invocation.Proceed();
                    return;
                }

                // TODO we will need to try to remove the old object
                _commonData.RedisObjectManager.RedisBackup?.UpdateListItem(listKey, originalKey.RedisKey, key.RedisKey);

                _commonData.RedisDatabase.ListRemove(listKey.RedisKey, originalKey.RedisKey, 1);
                _commonData.RedisDatabase.ListRightPush(listKey.RedisKey, key.RedisKey);
                _commonData.RedisObjectManager.SaveObject(invocation.Arguments[1], key.Id, _commonData.RedisDatabase);
            }
            else
            {
                if (_commonData.Processing)
                {
                    invocation.Proceed();
                }

                _commonData.RedisObjectManager.RedisBackup?.UpdateListItem(listKey, (RedisValue) original,
                    (RedisValue) invocation.Arguments[1]);
                _commonData.RedisDatabase.ListRemove(listKey.RedisKey, (RedisValue) original, 1);
                _commonData.RedisDatabase.ListRightPush(listKey.RedisKey, (RedisValue) invocation.Arguments[1]);
            }
        invocation.Proceed();
        }

        private object CreateProxy(IRedisObject argument, out RedisKeyObject key)
        {
            var argumentType = argument.GetType();
            key = new RedisKeyObject(argumentType, string.Empty);

            _commonData.RedisDatabase.GenerateId(key, argument, _commonData.RedisObjectManager.RedisBackup);

            var newArgument = _commonData.RedisObjectManager.RetrieveObjectProxy(argumentType, key.Id, _commonData.RedisDatabase, argument);

            return newArgument;
        }
    }
}