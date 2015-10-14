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

        protected internal bool Processed { private get; set; }
        protected internal object ParentProxy { private get; set; }

        public ChangeTrackerInterceptor(
            IDatabase redisDatabase, 
            RedisObjectManager redisObjectManager,
            IRedisBackup redisBackup,
            string id)
        {
            _database = redisDatabase;
            _redisObjectManager = redisObjectManager;
            _redisBackup = redisBackup;
            _id = id;
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
                    ParentProxy?.GetType()
                        .GetProperties()
                        .SingleOrDefault(x => x.GetValue(ParentProxy, null) == invocation.Proxy)?.PropertyType ??
                    invocation.Proxy.GetType();

                RedisKeyObject key;
                if (cAttr != null && cAttr.GetInterfaces().Contains(typeof(IRedisObject)))
                {
                    // This is a single item within an IRedisObject... it will be saved as a hash
                    key = new RedisKeyObject(cAttr.GetCustomAttributes(), _id);
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
                    key = new RedisKeyObject(cAttr.GetCustomAttributes(), _id);
                }

                object targetObject = null;
                var accessor = invocation.Proxy as IProxyTargetAccessor;
                targetObject = accessor?.DynProxyGetTarget();

                Debug.WriteLine("ChangeTrackerInterceptor (Save attempting) " + invocation.Method.Name);
            }
            else
            {
                Debug.WriteLine("No autosave attempted");
            }

            invocation.Proceed();
        }
    }
}