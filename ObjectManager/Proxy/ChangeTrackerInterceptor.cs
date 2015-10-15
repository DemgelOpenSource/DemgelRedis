using System;
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
        protected internal bool Transient { private get; set; }
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
            // Decide if we are to save automaticly
            var hasNoSave = invocation.Method.ReflectedType?.GetMembers().Any(x => x.HasAttribute<RedisNoAutoSave>());
            if (!hasNoSave.GetValueOrDefault()
                && !(invocation.Arguments[0] is IRedisObject)
                && Processed)
            {
                var cAttr =
                    ParentProxy?.GetType().BaseType?
                        .GetProperties()
                        .SingleOrDefault(x => x.GetValue(ParentProxy, null) == invocation.Proxy) ??
                    invocation.Proxy;

                RedisKeyObject key = null;
                if (cAttr.GetType().GetInterfaces().Contains(typeof(IRedisObject)))
                {
                    // This is a single item within an IRedisObject... it will be saved as a hash
                    key = new RedisKeyObject(cAttr.GetType().GetCustomAttributes(), _id);
                    // Need to get the property name of the IRedisObject this is being set in
                    var property =
                        invocation.Method.ReflectedType?.GetProperties()
                            .SingleOrDefault(x => x.SetMethod.Name == invocation.Method.Name);

                    ITypeConverter converter;
                    if (property != null && _redisObjectManager.TypeConverters.TryGetValue(property.PropertyType, out converter))
                    {
                        var ret = new HashEntry(property.Name, converter.ToWrite(invocation.Arguments[0]));
                        _database.HashSet(key.RedisKey, ret.Name, ret.Value);
                        _redisBackup?.UpdateHashValue(ret, key);
                    }
                }
                else
                {
                    var prop = cAttr as PropertyInfo;
                    if (prop != null)
                    {
                        key = new RedisKeyObject(prop.GetCustomAttributes(), _id);
                    }
                }

                Notify(invocation, key);

                Debug.WriteLine("ChangeTrackerInterceptor (Save attempting) " + invocation.Method.Name);
            }
            else
            {
                // this would result from a add, or set_Item
                // TODO refactor this to reflect that
                if (invocation.Arguments[0] is IRedisObject && !(invocation.Arguments[0] is IProxyTargetAccessor))
                {
                    var key = new RedisKeyObject(invocation.Arguments[0].GetType().GetCustomAttributes(), string.Empty);

                    GenerateId(invocation, key);

                    invocation.Arguments[0] = _redisObjectManager.RetrieveObjectProxy(invocation.Arguments[0].GetType(), key.Id, _database, invocation.Arguments[0], Transient);

                    var prop = invocation.Arguments[0].GetType()
                        .GetProperties()
                        .SingleOrDefault(x => x.GetCustomAttributes().Any(y => y is RedisIdKey));

                    if (prop != null && prop.PropertyType == typeof (string))
                    {
                        prop.SetValue(invocation.Arguments[0], key.Id);
                    } else if (prop != null && prop.PropertyType == typeof (Guid))
                    {
                        prop.SetValue(invocation.Arguments[0], Guid.Parse(key.Id));
                    }
                    // Don't save the objects added during processing
                    var cAttr =
                        ParentProxy?.GetType().BaseType?
                            .GetProperties()
                            .SingleOrDefault(x => x.GetValue(ParentProxy, null) == invocation.Proxy);
                    
                    if (Processed)
                    {
                        var listKey = new RedisKeyObject(cAttr.GetCustomAttributes(), _id);
                        _database.ListRightPush(listKey.RedisKey, key.RedisKey);
                        _redisObjectManager.SaveObject(invocation.Arguments[0], key.Id, _database);
                    }
                }
                Debug.WriteLine("No autosave attempted " + invocation.Method.Name);
            }

            invocation.Proceed();
        }

        private void GenerateId(IInvocation invocation, RedisKeyObject key)
        {
            var redisIdAttr =
                invocation.Arguments[0].GetType().GetProperties().SingleOrDefault(
                    x => x.GetCustomAttributes().Any(a => a is RedisIdKey));
            var value = redisIdAttr?.GetValue(invocation.Arguments[0], null);

            if (redisIdAttr != null && redisIdAttr.PropertyType == typeof(string))
            {
                if (((string) value).IsNullOrEmpty())
                {
                    var newId = _database.StringIncrement($"demgelcounter:{key.CounterKey}");
                    key.Id = newId.ToString();
                    redisIdAttr.SetValue(invocation.Arguments[0], key.Id);
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
                    redisIdAttr.SetValue(invocation.Arguments[0], guid);
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

        public void RecieveSub(RedisChannel channel, RedisValue value)
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