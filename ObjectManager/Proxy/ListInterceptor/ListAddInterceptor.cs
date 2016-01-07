using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy.ListInterceptor
{
    public class ListAddInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public ListAddInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var prop = ((IProxyTargetAccessor) invocation.Proxy).GetTargetPropertyInfo();

            var listKey = new RedisKeyObject(prop, _commonData.Id);

            // Make sure the list is Restored
            // we need to make sure to do this as a object variable to check for key existance
            //if (!_restored)
            //{
                _commonData.RedisObjectManager.RedisBackup?.RestoreList(_commonData.RedisDatabase, listKey);
                //_restored = true;
            //}

            var redisObject = invocation.Arguments[0] as IRedisObject;
            if (redisObject != null)
            {
                RedisKeyObject key;
                if (!(invocation.Arguments[0] is IProxyTargetAccessor))
                {
                    var proxy = CreateProxy(redisObject, out key);
                    invocation.Arguments[0] = proxy;
                }
                else
                {
                    key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                    _commonData.RedisDatabase.GenerateId(key, invocation.Arguments[0], _commonData.RedisObjectManager.RedisBackup);
                }

                if (_commonData.Processing)
                {
                    invocation.Proceed();
                    return;
                }
                _commonData.RedisObjectManager.RedisBackup?.AddListItem(listKey, key.RedisKey);

                _commonData.RedisDatabase.ListRightPush(listKey.RedisKey, key.RedisKey);
                _commonData.RedisObjectManager.SaveObject(invocation.Arguments[0], key.Id, _commonData.RedisDatabase);
            }
            else
            {
                if (_commonData.Processing)
                {
                    invocation.Proceed();
                    return;
                }

                // TODO to better checks for casting to RedisValue
                RedisValue value;
                if (invocation.Arguments[0] is RedisValue)
                {
                    value = (RedisValue)invocation.Arguments[0];
                }
                else
                {
                    _commonData.RedisObjectManager.TryConvertToRedisValue(invocation.Arguments[0], out value);
                }
               
                _commonData.RedisObjectManager.RedisBackup?.AddListItem(listKey, value);
                _commonData.RedisDatabase.ListRightPush(listKey.RedisKey, value);
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