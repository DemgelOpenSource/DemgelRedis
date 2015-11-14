using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class AddSetInterceptor : IInterceptor
    {
        private readonly string _id;
        private readonly CommonData _commonData;

        private readonly Type _stringType = typeof (string);
        private readonly Type _guidType = typeof (Guid);


        public AddSetInterceptor(
            string id,
            CommonData data)
        {
            _id = id;
            _commonData = data;
        }

        public void Intercept(IInvocation invocation)
        {
           
            var cAttr =
                   _commonData.ParentProxy?.GetType().BaseType?
                       .GetProperties()
                       .SingleOrDefault(x => x.GetValue(_commonData.ParentProxy, null) == invocation.Proxy)
                   ??
                   invocation.Proxy;

            var cPropertyInfo = cAttr as PropertyInfo;
            if (cPropertyInfo != null)
            {
                if (invocation.Method.Name.StartsWith("Add", StringComparison.Ordinal))
                {
                    if (cPropertyInfo.PropertyType.Name.StartsWith("IList", StringComparison.Ordinal))
                    {
                        DoAddListItem(invocation, cPropertyInfo);
                    }
                    else if (cPropertyInfo.PropertyType.Name.StartsWith("IDictionary", StringComparison.Ordinal))
                    {
                        DoAddDictionaryItem(invocation, cPropertyInfo);
                        
                    }
                    invocation.Proceed();
                    return;
                }

                if (invocation.Method.Name.StartsWith("set_Item"))
                {
                    if (cPropertyInfo.PropertyType.Name.StartsWith("IList"))
                    {
                        DoSetListItem(invocation, cPropertyInfo);
                    }
                    else if (cPropertyInfo.PropertyType.Name.StartsWith("IDictionary"))
                    {
                        DoSetDictionaryItem(invocation, cPropertyInfo);
                    }
                    invocation.Proceed();
                    return;
                }
            }

            // if it gets this far, we are likely to be setting an property in an IRedisObject
            // We cannot process IRedisObjects here, if we are trying to set a Proxies object
            // With a new IRedisObject, we need to handle that differently

            // TODO we need to do this better this is just a bandaid
            object checkObj;
            if (invocation.Arguments.Length == 2)
            {
                checkObj = invocation.Arguments[1];
            }
            else
            {
                checkObj = invocation.Arguments[0];
            }

            if (!(checkObj is IRedisObject)
                /*&& _commonData.Processed*/ && !_commonData.Processing)
            {
                if (!_commonData.Processed)
                {
                    _commonData.ProcessProxy(null, null, invocation.Proxy);
                } 

                if (cAttr.GetType().GetInterfaces().Contains(typeof(IRedisObject)))
                {
                    // This is a single item within an IRedisObject... it will be saved as a hash
                    var key = new RedisKeyObject(cAttr.GetType(), _id);
                    // Need to get the property name of the IRedisObject this is being set in
                    // I might be missing something here, but this works...
                    var property =
                        invocation.Method.ReflectedType?.GetProperties()
                            .SingleOrDefault(x => x.SetMethod.Name == invocation.Method.Name);

                    ITypeConverter converter;
                    if (property != null && _commonData.RedisObjectManager.TypeConverters.TryGetValue(property.PropertyType, out converter))
                    {
                        var ret = new HashEntry(property.Name, converter.ToWrite(checkObj));

                        _commonData.RedisObjectManager.RedisBackup?.RestoreHash(_commonData.RedisDatabase, key);
                        _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(ret, key);
                        
                        _commonData.RedisDatabase.HashSet(key.RedisKey, ret.Name, ret.Value);
                    }
                }
            } else if (checkObj is IRedisObject)
            {
                var redisObject = (IRedisObject) checkObj;

                var key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                //_commonData.RedisObjectManager.GenerateId(_commonData.RedisDatabase, key, invocation.Arguments[0]);
                _commonData.RedisDatabase.GenerateId(key, checkObj, _commonData.RedisObjectManager.RedisBackup);

                if (!_commonData.Processed)
                {
                    invocation.Proceed();
                    return;
                }
                var objectKey = new RedisKeyObject(cAttr.GetType(), _id);
                // FIX THIS...
                var property =
                        invocation.Method.ReflectedType?.GetProperties()
                            .SingleOrDefault(x => x.SetMethod != null && x.SetMethod.Name == invocation.Method.Name);

                if (property != null)
                {
                    _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(new HashEntry(property.Name, key.RedisKey), objectKey);

                    _commonData.RedisDatabase.HashSet(objectKey.RedisKey, property.Name, key.RedisKey);
                    _commonData.RedisObjectManager.SaveObject(checkObj, key.Id, _commonData.RedisDatabase);
                }

            }
            invocation.Proceed();
        }

        private object CreateProxy(IRedisObject argument, out RedisKeyObject key)
        {
            var argumentType = argument.GetType();
            key = new RedisKeyObject(argumentType, string.Empty);

            //_commonData.RedisObjectManager.GenerateId(_commonData.RedisDatabase, key, argument);
            _commonData.RedisDatabase.GenerateId(key, argument, _commonData.RedisObjectManager.RedisBackup);

            var newArgument = _commonData.RedisObjectManager.RetrieveObjectProxy(argumentType, key.Id, _commonData.RedisDatabase, argument);

            //var prop = argumentType.GetProperties()
            //    .SingleOrDefault(x => x.GetCustomAttributes().Any(y => y is RedisIdKey));

            //if (prop == null) { return newArgument; }

            //if (prop.PropertyType == _stringType)
            //{
            //    prop.SetValue(newArgument, key.Id);
            //}
            //else if (prop.PropertyType == _guidType)
            //{
            //    prop.SetValue(newArgument, Guid.Parse(key.Id));
            //}

            return newArgument;
        }

        private void DoAddDictionaryItem(IInvocation invocation, PropertyInfo prop)
        {
            var hashKey = new RedisKeyObject(prop, _id);

            _commonData.RedisObjectManager.RedisBackup?.RestoreHash(_commonData.RedisDatabase, hashKey);

            // For now limit to Strings as dictionary key, later will will implement any value that
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

            if (!(dictKey is string))
            {
                throw new InvalidOperationException("Dictionary Key can only be of type String");
            }

            // Only IRedis Objects and RedisValue can be saved into dictionary (for now)
            var redisObject = dictValue as IRedisObject;
            if (redisObject != null)
            {
                RedisKeyObject key;
                if (!(dictValue is IProxyTargetAccessor))
                {
                    var proxy = CreateProxy(redisObject, out key);
                    invocation.Arguments[1] = proxy;
                    dictValue = proxy;
                }
                else
                {
                    key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                    //_commonData.RedisObjectManager.GenerateId(_commonData.RedisDatabase, key, dictValue);
                    _commonData.RedisDatabase.GenerateId(key, dictValue, _commonData.RedisObjectManager.RedisBackup);
                }

                if (!_commonData.Processed) return;
                var hashEntry = new HashEntry((string)dictKey, key.RedisKey);
                _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(hashEntry, hashKey);
                _commonData.RedisDatabase.HashSet(hashKey.RedisKey, hashEntry.Name, hashEntry.Value);
                _commonData.RedisObjectManager.SaveObject(dictValue, key.Id, _commonData.RedisDatabase);
            }
            else
            {
                if (!(dictValue is RedisValue))
                {
                    throw new InvalidOperationException("Dictionary Value can only be IRedisObject or RedisValue");
                }
                var hashEntry = new HashEntry((string) dictKey, (RedisValue) dictValue);

                _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(hashEntry, hashKey);
                _commonData.RedisDatabase.HashSet(hashKey.RedisKey, hashEntry.Name, hashEntry.Value);
            }
        }

        private bool _restored;

        private void DoAddListItem(IInvocation invocation, PropertyInfo prop)
        {
            var listKey = new RedisKeyObject(prop, _id);

            // Make sure the list is Restored
            // we need to make sure to do this as a object variable to check for key existance
            if (!_restored)
            {
                _commonData.RedisObjectManager.RedisBackup?.RestoreList(_commonData.RedisDatabase, listKey);
                _restored = true;
            }

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

                if (!_commonData.Processed) return;
                _commonData.RedisObjectManager.RedisBackup?.AddListItem(listKey, key.RedisKey);

                _commonData.RedisDatabase.ListRightPush(listKey.RedisKey, key.RedisKey);
                _commonData.RedisObjectManager.SaveObject(invocation.Arguments[0], key.Id, _commonData.RedisDatabase);
            }
            else
            {
                if (!_commonData.Processed) return;

                // TODO to better checks for casting to RedisValue
                _commonData.RedisObjectManager.RedisBackup?.AddListItem(listKey, (RedisValue) invocation.Arguments[0]);
                _commonData.RedisDatabase.ListRightPush(listKey.RedisKey, (RedisValue) invocation.Arguments[0]);
            }
        }

        private void DoSetDictionaryItem(IInvocation invocation, PropertyInfo prop)
        {
            var hashKey = new RedisKeyObject(prop, _id);

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
                    var proxy = CreateProxy(valueRedis, out key);
                    invocation.Arguments[1] = proxy;
                    dictValue = proxy;
                }
                else
                {
                    key = new RedisKeyObject(valueRedis.GetType(), string.Empty);
                    _commonData.RedisDatabase.GenerateId(key, dictValue, _commonData.RedisObjectManager.RedisBackup);
                }

                if (!_commonData.Processed) return;
                // TODO we will need to try to remove the old RedisObject
                var hashEntry = new HashEntry((string)dictKey, key.RedisKey);
                _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(hashEntry, hashKey);

                _commonData.RedisDatabase.HashSet(hashKey.RedisKey, hashEntry.Name, hashEntry.Value);
                _commonData.RedisObjectManager.SaveObject(dictValue, key.Id, _commonData.RedisDatabase);
            }
            else
            {
                var hashValue = new HashEntry((string) dictKey, (RedisValue) dictValue);
                _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(hashValue, hashKey);
                _commonData.RedisDatabase.HashSet(hashKey.RedisKey, hashValue.Name, hashValue.Value);
            }
        }

        private void DoSetListItem(IInvocation invocation, PropertyInfo prop)
        {
            var listKey = new RedisKeyObject(prop, _id);

            // Make sure the list is Restored
            _commonData.RedisObjectManager.RedisBackup?.RestoreList(_commonData.RedisDatabase, listKey);

            // We will need the Original value no matter what
            var accessor = (IProxyTargetAccessor)invocation.Proxy;
            var original = (accessor.DynProxyGetTarget() as IList)?[(int)invocation.Arguments[0]];
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
                    _commonData.RedisDatabase.GenerateId(key, invocation.Arguments[1], _commonData.RedisObjectManager.RedisBackup);
                }

                if (!_commonData.Processed) return;
                // TODO we will need to try to remove the old object
                _commonData.RedisObjectManager.RedisBackup?.UpdateListItem(listKey, originalKey.RedisKey, key.RedisKey);

                _commonData.RedisDatabase.ListRemove(listKey.RedisKey, originalKey.RedisKey, 1);
                _commonData.RedisDatabase.ListRightPush(listKey.RedisKey, key.RedisKey);
                _commonData.RedisObjectManager.SaveObject(invocation.Arguments[1], key.Id, _commonData.RedisDatabase);
            }
            else
            {
                _commonData.RedisObjectManager.RedisBackup?.UpdateListItem(listKey, (RedisValue) original,
                    (RedisValue) invocation.Arguments[1]);
                _commonData.RedisDatabase.ListRemove(listKey.RedisKey, (RedisValue) original, 1);
                _commonData.RedisDatabase.ListRightPush(listKey.RedisKey, (RedisValue) invocation.Arguments[1]);
            }
        }
    }
}