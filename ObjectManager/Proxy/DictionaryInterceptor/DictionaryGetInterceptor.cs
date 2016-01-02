using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor
{
    public class DictionaryGetInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public DictionaryGetInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var prop = ((IProxyTargetAccessor)invocation.Proxy).GetTargetPropertyInfo();
            var hashKey = new RedisKeyObject(prop, _commonData.Id);

            var targetType = invocation.TargetType;
            Type keyType = null;
            Type itemType = null;

            if (targetType.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType &&
                      interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                if (targetType.GetGenericArguments().Any())
                {
                    keyType = targetType.GetGenericArguments()[0];
                    itemType = targetType.GetGenericArguments()[1];
                }
            }

            var method = invocation.Proxy.GetType().GetMethod("Add", new[] { keyType, itemType });

            _commonData.RedisObjectManager.RedisBackup?.RestoreHash(_commonData.RedisDatabase, hashKey);

            RedisValue dictKey;
            if (invocation.Arguments[0] is RedisValue)
            {
                dictKey = (RedisValue)invocation.Arguments[0];
            }
            else
            {
                if (!_commonData.RedisObjectManager.TryConvertToRedisValue(invocation.Arguments[0], out dictKey))
                {
                    throw new Exception("Invalid Key Type...");
                }
            }

            // This assumes string or RedisValue are the dictionary key - probably should check for sanitity
            if (_commonData.RedisDatabase.HashExists(hashKey.RedisKey, dictKey))
            {
                var redisKey = _commonData.RedisDatabase.HashGet(hashKey.RedisKey, dictKey);
                var key = redisKey.ParseKey();
                if (itemType.GetInterfaces().Contains(typeof(IRedisObject)))
                {
                    var newProxy = _commonData.RedisObjectManager.GetRedisObjectWithType(_commonData.RedisDatabase, (string)redisKey, key);

                    method.Invoke(invocation.Proxy, new[] { Convert.ChangeType(dictKey, keyType), newProxy });
                }
                else
                {
                    method.Invoke(invocation.Proxy, new[] { Convert.ChangeType(dictKey, keyType), redisKey });
                }
            }

            invocation.Proceed();
        }
    }
}
