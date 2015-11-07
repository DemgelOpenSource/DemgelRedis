using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class AddSetInterceptor : IInterceptor
    {
        private readonly IDatabase _database;
        private readonly RedisObjectManager _redisObjectManager;
        private readonly IRedisBackup _redisBackup;
        private readonly string _id;

        private readonly Type _stringType = typeof (string);
        private readonly Type _guidType = typeof (Guid);

        protected internal bool Processed { get; set; }
        private bool Transient { get; }

        private object _parentProxy;
        protected internal object ParentProxy
        {
            private get { return _parentProxy; }
            set
            {
                Debug.WriteLine("Setting parent");
                _parentProxy = value;
            }
        }

        public AddSetInterceptor(
            IDatabase redisDatabase, 
            RedisObjectManager redisObjectManager,
            IRedisBackup redisBackup,
            string id,
            bool transient)
        {
            _database = redisDatabase;
            _redisObjectManager = redisObjectManager;
            _redisBackup = redisBackup;
            _id = id;

            Transient = transient;
        }

        public void Intercept(IInvocation invocation)
        {
            var cAttr =
                   ParentProxy?.GetType().BaseType?
                       .GetProperties()
                       .SingleOrDefault(x => x.GetValue(ParentProxy, null) == invocation.Proxy)
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
            if (!(invocation.Arguments[0] is IRedisObject)
                && Processed)
            {
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
                    if (property != null && _redisObjectManager.TypeConverters.TryGetValue(property.PropertyType, out converter))
                    {
                        var ret = new HashEntry(property.Name, converter.ToWrite(invocation.Arguments[0]));

                        _redisBackup?.RestoreHash(_database, key);
                        _redisBackup?.UpdateHashValue(ret, key);
                        
                        _database.HashSet(key.RedisKey, ret.Name, ret.Value);
                    }
                }
            } else if (invocation.Arguments[0] is IRedisObject)
            {
                var redisObject = (IRedisObject) invocation.Arguments[0];

                RedisKeyObject key;
                if (!(invocation.Arguments[0] is IProxyTargetAccessor))
                {
                    var proxy = CreateProxy(redisObject, out key);
                    invocation.Arguments[0] = proxy;
                }
                else
                {
                    // TODO this is an issue... ID needs to be set
                    key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                    _redisObjectManager.GenerateId(_database, key, invocation.Arguments[0]);
                }

                if (!Processed)
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
                    _redisBackup?.UpdateHashValue(new HashEntry(property.Name, key.RedisKey), objectKey);

                    _database.HashSet(objectKey.RedisKey, property.Name, key.RedisKey);
                    _redisObjectManager.SaveObject(invocation.Arguments[0], key.Id, _database);
                }

            }
            invocation.Proceed();
        }

        private object CreateProxy(IRedisObject argument, out RedisKeyObject key)
        {
            var argumentType = argument.GetType();
            key = new RedisKeyObject(argumentType, string.Empty);

            _redisObjectManager.GenerateId(_database, key, argument);

            var newArgument = _redisObjectManager.RetrieveObjectProxy(argumentType, key.Id, _database, argument, Transient);

            var prop = argumentType.GetProperties()
                .SingleOrDefault(x => x.GetCustomAttributes().Any(y => y is RedisIdKey));

            if (prop == null) { return newArgument; }

            if (prop.PropertyType == _stringType)
            {
                prop.SetValue(newArgument, key.Id);
            }
            else if (prop.PropertyType == _guidType)
            {
                prop.SetValue(newArgument, Guid.Parse(key.Id));
            }

            return newArgument;
        }

        private void DoAddDictionaryItem(IInvocation invocation, PropertyInfo prop)
        {
            var hashKey = new RedisKeyObject(prop, _id);

            _redisBackup?.RestoreHash(_database, hashKey);

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
                    _redisObjectManager.GenerateId(_database, key, dictValue);
                }

                if (!Processed) return;
                var hashEntry = new HashEntry((string)dictKey, key.RedisKey);
                _redisBackup?.UpdateHashValue(hashEntry, hashKey);
                _database.HashSet(hashKey.RedisKey, hashEntry.Name, hashEntry.Value);
                _redisObjectManager.SaveObject(dictValue, key.Id, _database);
            }
            else
            {
                if (!(dictValue is RedisValue))
                {
                    throw new InvalidOperationException("Dictionary Value can only be IRedisObject or RedisValue");
                }
                var hashEntry = new HashEntry((string) dictKey, (RedisValue) dictValue);

                _redisBackup?.UpdateHashValue(hashEntry, hashKey);
                _database.HashSet(hashKey.RedisKey, hashEntry.Name, hashEntry.Value);
            }
        }

        private void DoAddListItem(IInvocation invocation, PropertyInfo prop)
        {
            var listKey = new RedisKeyObject(prop, _id);

            // Make sure the list is Restored
            _redisBackup?.RestoreList(_database, listKey);

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
                    _redisObjectManager.GenerateId(_database, key, invocation.Arguments[0]);
                }

                if (!Processed) return;
                _redisBackup?.AddListItem(listKey, key.RedisKey);

                _database.ListRightPush(listKey.RedisKey, key.RedisKey);
                _redisObjectManager.SaveObject(invocation.Arguments[0], key.Id, _database);
            }
            else
            {
                // TODO to better checks for casting to RedisValue
                _redisBackup?.AddListItem(listKey, (RedisValue) invocation.Arguments[0]);
                _database.ListRightPush(listKey.RedisKey, (RedisValue) invocation.Arguments[0]);
            }
        }

        private void DoSetDictionaryItem(IInvocation invocation, PropertyInfo prop)
        {
            var hashKey = new RedisKeyObject(prop, _id);

            _redisBackup?.RestoreHash(_database, hashKey);

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
                    _redisObjectManager.GenerateId(_database, key, dictValue);
                }

                if (!Processed) return;
                // TODO we will need to try to remove the old RedisObject
                var hashEntry = new HashEntry((string)dictKey, key.RedisKey);
                _redisBackup?.UpdateHashValue(hashEntry, hashKey);

                _database.HashSet(hashKey.RedisKey, hashEntry.Name, hashEntry.Value);
                _redisObjectManager.SaveObject(dictValue, key.Id, _database);
            }
            else
            {
                var hashValue = new HashEntry((string) dictKey, (RedisValue) dictValue);
                _redisBackup?.UpdateHashValue(hashValue, hashKey);
                _database.HashSet(hashKey.RedisKey, hashValue.Name, hashValue.Value);
            }
        }

        private void DoSetListItem(IInvocation invocation, PropertyInfo prop)
        {
            var listKey = new RedisKeyObject(prop, _id);

            // Make sure the list is Restored
            _redisBackup?.RestoreList(_database, listKey);

            // We will need the Original value no matter what
            var accessor = (IProxyTargetAccessor)invocation.Proxy;
            var original = (accessor.DynProxyGetTarget() as IList)?[(int)invocation.Arguments[0]];
            if (original == null) return;

            // We are checking if the new item set to the list is actually a Proxy (if not created it)
            var redisObject = invocation.Arguments[1] as IRedisObject;
            if (redisObject != null)
            {
                var originalKey = new RedisKeyObject(original.GetType(), string.Empty);
                _redisObjectManager.GenerateId(_database, originalKey, original);

                RedisKeyObject key;
                if (!(invocation.Arguments[1] is IProxyTargetAccessor))
                {
                    var proxy = CreateProxy(redisObject, out key);
                    invocation.Arguments[1] = proxy;
                }
                else
                {
                    key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                    _redisObjectManager.GenerateId(_database, key, invocation.Arguments[1]);
                }

                if (!Processed) return;
                // TODO we will need to try to remove the old object
                _redisBackup?.UpdateListItem(listKey, originalKey.RedisKey, key.RedisKey);

                _database.ListRemove(listKey.RedisKey, originalKey.RedisKey, 1);
                _database.ListRightPush(listKey.RedisKey, key.RedisKey);
                _redisObjectManager.SaveObject(invocation.Arguments[1], key.Id, _database);
            }
            else
            {
                _redisBackup?.UpdateListItem(listKey, (RedisValue) original,
                    (RedisValue) invocation.Arguments[1]);
                _database.ListRemove(listKey.RedisKey, (RedisValue) original, 1);
                _database.ListRightPush(listKey.RedisKey, (RedisValue) invocation.Arguments[1]);
            }
        }
    }
}