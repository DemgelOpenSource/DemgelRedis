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
                Debug.WriteLine("No autosave attempted");
            }

            invocation.Proceed();
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