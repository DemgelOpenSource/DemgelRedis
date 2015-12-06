using System;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor
{
    public class DictionaryAddInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public DictionaryAddInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var prop = ((IProxyTargetAccessor)invocation.Proxy).GetTargetPropertyInfo();
            var hashKey = new RedisKeyObject(prop, _commonData.Id);

            _commonData.RedisObjectManager.RedisBackup?.RestoreHash(_commonData.RedisDatabase, hashKey);

            // For now limit to Strings as dictionary key, later will implement any value that
            // can be converted to String (as in, Guid, string, redisvalue of string type)
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
                throw new NullReferenceException("Key or Value cannot be null");
            }

            // TODO this will go away - yay use converters to force RedisValue
            //if (!(dictKey is string) && !(dictKey is RedisValue))
            //{
            //    throw new InvalidOperationException("Dictionary Key can only be of type String");
            //}

            // Only IRedis Objects and RedisValue can be saved into dictionary (for now)
            var redisObject = dictValue as IRedisObject;
            if (redisObject != null)
            {
                RedisKeyObject key;
                if (!(dictValue is IProxyTargetAccessor))
                {
                    var proxy = redisObject.CreateProxy(_commonData, out key); // CreateProxy(redisObject, out key);
                    invocation.Arguments[1] = proxy;
                    dictValue = proxy;
                }
                else
                {
                    key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                    _commonData.RedisDatabase.GenerateId(key, dictValue, _commonData.RedisObjectManager.RedisBackup);
                }

                if (_commonData.Processing)
                {
                    invocation.Proceed();
                    return;
                }

                HashEntry hashEntry;
                if (dictKey is RedisValue)
                {
                    hashEntry = new HashEntry((RedisValue) dictKey, key.RedisKey);
                }
                else
                {
                    RedisValue redisKey;
                    if (!_commonData.RedisObjectManager.TryConvertToRedisValue(dictKey, out redisKey))
                    {
                        throw new Exception("Invalid Key Type...");
                    }
                    hashEntry = new HashEntry(redisKey, key.RedisKey);
                }

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

                // Converter can get rid of this too
                RedisValue newDictValue;
                if (!(dictValue is RedisValue))
                {
                    if (!_commonData.RedisObjectManager.TryConvertToRedisValue(dictValue, out newDictValue))
                    {
                        throw new Exception("Cannot convert to RedisValue");
                    }
                }
                else
                {
                    newDictValue = (RedisValue)dictValue;
                }

                HashEntry hashEntry;
                if (dictKey is RedisValue)
                {
                    hashEntry = new HashEntry((RedisValue) dictKey, newDictValue);
                }
                else
                {
                    RedisValue redisKey;
                    if (!_commonData.RedisObjectManager.TryConvertToRedisValue(dictKey, out redisKey))
                    {
                        throw new Exception("Cannot convert to RedisValue");
                    }
                    hashEntry = new HashEntry(redisKey, newDictValue);
                }

                _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(hashEntry, hashKey);
                _commonData.RedisDatabase.HashSet(hashKey.RedisKey, hashEntry.Name, hashEntry.Value);
            }

            invocation.Proceed();
        }
    }
}