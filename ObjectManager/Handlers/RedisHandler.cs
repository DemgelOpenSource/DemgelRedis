using System;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Proxy;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Handlers
{
    public abstract class RedisHandler : IRedisHandler
    {
        protected readonly RedisObjectManager RedisObjectManager;

        protected RedisHandler(RedisObjectManager demgelRedis)
        {
            RedisObjectManager = demgelRedis;
        }

        protected virtual object GetTarget(object obj)
        {
            var accessor = obj as IProxyTargetAccessor;
            return accessor == null ? obj : accessor.DynProxyGetTarget();
        }

        public abstract bool CanHandle(object obj);
        public abstract object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo, LimitObject limits = null);
        public abstract bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
        public abstract bool Delete(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
        public abstract object BuildProxy(ProxyGenerator generator, Type objType, CommonData data, object baseObj);
    }
}