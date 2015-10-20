using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class ChangeTrackerInterceptor : IInterceptor
    {
        private readonly IDatabase _database;
        private readonly RedisObjectManager _redisObjectManager;
        private readonly IRedisBackup _redisBackup;
        private readonly string _id;

        private readonly Dictionary<string, object> _listeners; 

        protected internal bool Processed { private get; set; }
        private bool Transient { get; }
        protected internal object ParentProxy { private get; set; }

        public ChangeTrackerInterceptor(
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

            _listeners = new Dictionary<string, object>();
            Transient = transient;
        }

        public void Intercept(IInvocation invocation)
        {
            var cAttr =
                   ParentProxy?.GetType().BaseType?
                       .GetProperties()
                       .SingleOrDefault(x => x.GetValue(ParentProxy, null) == invocation.Proxy) ??
                   invocation.Proxy;

            if (invocation.Method.Name.StartsWith("Add", StringComparison.Ordinal))
            {
                var cPropertyInfo = cAttr as PropertyInfo;

                if (cPropertyInfo != null)
                {
                    if (cPropertyInfo.PropertyType.Name.StartsWith("IList"))
                    {
                        DoAddListItem(invocation, cPropertyInfo);
                        invocation.Proceed();
                        return;
                    }

                    // This code us currently irrelavent (WIP)
                    if (cPropertyInfo.PropertyType.Name.StartsWith("IDictionary"))
                    {
                        // Do Set Dictionary Item
                        invocation.Proceed();
                        return;
                    }
                }
            }
            else if (invocation.Method.Name.StartsWith("set_Item") && invocation.Arguments.Length == 2)
            {
                var cPropertyInfo = cAttr as PropertyInfo;

                if (cPropertyInfo != null)
                {
                    if (cPropertyInfo.PropertyType.Name.StartsWith("IList"))
                    {
                        DoSetListItem(invocation, cPropertyInfo);
                    }
                    else if (cPropertyInfo.PropertyType.Name.StartsWith("IDictionary"))
                    {
                        // This code is irrelavent (WIP)
                        // Do Set Dictionary Item
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
                    // I might be missing something here, but this works... TODO look for faster lookup
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

                //Notify(invocation, key);
            }

            invocation.Proceed();
        }

        private object CreateProxy(IRedisObject argument, out RedisKeyObject key)
        {
            var argumentType = argument.GetType();
            key = new RedisKeyObject(argumentType, string.Empty);

            GenerateId(key, argument);

            var newArgument = _redisObjectManager.RetrieveObjectProxy(argumentType, key.Id, _database, argument, Transient);

            var prop = argumentType.GetProperties()
                .SingleOrDefault(x => x.GetCustomAttributes().Any(y => y is RedisIdKey));

            if (prop != null && prop.PropertyType == typeof(string))
            {
                prop.SetValue(newArgument, key.Id);
            }
            else if (prop != null && prop.PropertyType == typeof(Guid))
            {
                prop.SetValue(newArgument, Guid.Parse(key.Id));
            }

            return newArgument;
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
                    // Create the Proxy 
                    var proxy = CreateProxy(redisObject, out key);
                    invocation.Arguments[0] = proxy;
                }
                else
                {
                    key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                    GenerateId(key, invocation.Arguments[0]);
                }

                if (!Processed) return;
                _redisBackup?.AddListItem(_database, listKey, key.RedisKey);

                _database.ListRightPush(listKey.RedisKey, key.RedisKey);
                _redisObjectManager.SaveObject(invocation.Arguments[0], key.Id, _database);
            }
            else
            {
                // TODO to better checks for casting to RedisValue
                _redisBackup?.AddListItem(_database, listKey, (RedisValue) invocation.Arguments[0]);
                _database.ListRightPush(listKey.RedisKey, (RedisValue) invocation.Arguments[0]);
            }
        }

        private void DoSetDictionaryItem(IInvocation invocation, PropertyInfo prop)
        {
            var key = new RedisKeyObject(prop, _id);
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
                GenerateId(originalKey, original);

                RedisKeyObject key;
                if (!(invocation.Arguments[1] is IProxyTargetAccessor))
                {
                    // Create the Proxy 
                    var proxy = CreateProxy(redisObject, out key);
                    invocation.Arguments[1] = proxy;
                }
                else
                {
                    key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                    GenerateId(key, invocation.Arguments[1]);
                }

                if (!Processed) return;
                _redisBackup?.UpdateListItem(_database, listKey, originalKey.RedisKey, key.RedisKey);

                _database.ListRemove(listKey.RedisKey, originalKey.RedisKey, 1);
                _database.ListRightPush(listKey.RedisKey, key.RedisKey);
                _redisObjectManager.SaveObject(invocation.Arguments[1], key.Id, _database);
            }
            else
            {
                _redisBackup?.UpdateListItem(_database, listKey, (RedisValue) original,
                    (RedisValue) invocation.Arguments[1]);
                _database.ListRemove(listKey.RedisKey, (RedisValue) original, 1);
                _database.ListRightPush(listKey.RedisKey, (RedisValue) invocation.Arguments[1]);
            }
        }

        private void GenerateId(RedisKeyObject key, object argument)
        {
            var redisIdAttr =
                argument.GetType().GetProperties().SingleOrDefault(
                    x => x.GetCustomAttributes().Any(a => a is RedisIdKey));
            var value = redisIdAttr?.GetValue(argument, null);

            if (redisIdAttr != null && redisIdAttr.PropertyType == typeof(string))
            {
                if (((string) value).IsNullOrEmpty())
                {
                    var newId = _database.StringIncrement($"demgelcounter:{key.CounterKey}");
                    key.Id = newId.ToString();
                    redisIdAttr.SetValue(argument, key.Id);
                }
                else
                {
                    key.Id = (string) value;
                }
            }
            else if (redisIdAttr != null && redisIdAttr.PropertyType == typeof(Guid))
            {
                if ((Guid) value == new Guid())
                {
                    var guid = Guid.NewGuid();
                    key.Id = guid.ToString();
                    redisIdAttr.SetValue(argument, guid);
                }
                else
                {
                    key.Id = ((Guid) value).ToString();
                }
            }
            else
            {
                throw new ArgumentException("RedisIdKey needs to be either Guid or String");
            }
        }

        private void Notify(IInvocation invocation, RedisKeyObject key)
        {
            // Check if we have a listener for this object
            if (key == null) return;
            object listener;
            if (Transient && !_listeners.TryGetValue(key.RedisKey, out listener))
            {
                // Create the listener
                var subscriber = _database.Multiplexer.GetSubscriber();
                subscriber.Subscribe($"demgelom:{key.RedisKey}", RecieveSub);
                Debug.WriteLine("Creating Channel " + key.RedisKey);
                _listeners.Add(key.RedisKey, invocation.Proxy);
            }
            _database.Multiplexer.GetSubscriber().Publish($"demgelom:{key.RedisKey}", "somevalue changed...");
        }

        private void RecieveSub(RedisChannel channel, RedisValue value)
        {
            Debug.WriteLine(channel + " -- " + value);
            // All we are going to do is mark the GeneralIntercepter as dirty and start over
            var chan = (string) channel;
            var index = chan.IndexOf(":", StringComparison.Ordinal);
            var key = ((string) channel).Substring(index + 1);
            Debug.WriteLine("RedisKey: " + key);
        }
    }
}