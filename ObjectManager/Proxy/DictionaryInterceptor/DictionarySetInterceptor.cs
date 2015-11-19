using System;
using System.Collections;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor
{
    public class DictionarySetInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public DictionarySetInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var prop = ((IProxyTargetAccessor)invocation.Proxy).GetTargetPropertyInfo();

            var hashKey = new RedisKeyObject(prop, _commonData.Id);

            _commonData.RedisObjectManager.RedisBackup?.RestoreHash(_commonData.RedisDatabase, hashKey);

            // We will need the Original value no matter what
            var accessor = (IProxyTargetAccessor)invocation.Proxy;
            var original = (accessor.DynProxyGetTarget() as IDictionary)?[invocation.Arguments[0]];
            if (original == null) return;

            object dictKey = null, dictValue = null;

            // Determine if this is a KeyValuePair or a 2 argument
            if (invocation.Arguments.Length == 2)
            {
                dictKey = invocation.Arguments[0];
                dictValue = invocation.Arguments[1];
            }
            else
            {
                var valuePairType = invocation.Arguments[0].GetType();
                if (valuePairType.Name.StartsWith("KeyValuePair", StringComparison.Ordinal))
                {
                    dictKey = valuePairType.GetProperty("Key").GetValue(invocation.Arguments[0]);
                    dictValue = valuePairType.GetProperty("Value").GetValue(invocation.Arguments[0]);
                }
            }

            if (dictKey == null || dictValue == null)
            {
                throw new NullReferenceException("Key or Value cannot be Null");
            }

            var valueRedis = dictValue as IRedisObject;
            if (valueRedis != null)
            {
                RedisKeyObject key;
                if (!(dictValue is IProxyTargetAccessor))
                {
                    //var proxy = CreateProxy(valueRedis, out key);
                    var proxy = valueRedis.CreateProxy(_commonData, out key);
                    invocation.Arguments[1] = proxy;
                    dictValue = proxy;
                }
                else
                {
                    key = new RedisKeyObject(valueRedis.GetType(), string.Empty);
                    _commonData.RedisDatabase.GenerateId(key, dictValue, _commonData.RedisObjectManager.RedisBackup);
                }

                if (_commonData.Processing)
                {
                    invocation.Proceed();
                    return;
                }

                // TODO we will need to try to remove the old RedisObject
                var hashEntry = new HashEntry((string)dictKey, key.RedisKey);
                _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(hashEntry, hashKey);

                _commonData.RedisDatabase.HashSet(hashKey.RedisKey, hashEntry.Name, hashEntry.Value);
                _commonData.RedisObjectManager.SaveObject(dictValue, key.Id, _commonData.RedisDatabase);
            }
            else
            {
                if (_commonData.Processing)
                {
                    invocation.Proceed();
                    return;
                }

                var hashValue = new HashEntry((string)dictKey, (RedisValue)dictValue);
                _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(hashValue, hashKey);
                _commonData.RedisDatabase.HashSet(hashKey.RedisKey, hashValue.Name, hashValue.Value);
            }

            invocation.Proceed();
        }
    }
}