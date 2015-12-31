using System;
using System.Diagnostics;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;

namespace DemgelRedis.ObjectManager.Proxy.SetInterceptor
{
    public class SetAddInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public SetAddInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            // The property we are attempting to set
            var prop = ((IProxyTargetAccessor) invocation.Proxy).GetTargetPropertyInfo();
            var setKey = new RedisKeyObject(prop, _commonData.Id);

            // There should only be one argument we need to get the score
            var redisObject = invocation.Arguments[0] as IRedisObject;
            if (redisObject == null)
            {
                throw new Exception("Object needs to be an IRedisObject");
            }

            var score = redisObject.GetSetScore();

            RedisKeyObject key;

            // We need to process a proxy
            if (!(invocation.Arguments[0] is IProxyTargetAccessor))
            {
                var proxy = redisObject.CreateProxy(_commonData, out key);
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

            // TODO Redis Backup Entries
            _commonData.RedisObjectManager.RedisBackup?.AddSetItem(setKey, key.RedisKey, score);
            _commonData.RedisDatabase.SortedSetAdd(setKey.RedisKey, key.RedisKey, score);
            _commonData.RedisObjectManager.SaveObject(invocation.Arguments[0], key.Id, _commonData.RedisDatabase);

            invocation.Proceed();
        }
    }
}