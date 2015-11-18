using System;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.ObjectManager;
using DemgelRedis.ObjectManager.Proxy;
using StackExchange.Redis;

namespace DemgelRedis.Interfaces
{
    public interface IRedisHandler
    {
        bool CanHandle(object obj);
        object Read<T>(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo, LimitObject<T> limits = null);
        bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
        bool Delete(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
        object BuildProxy(ProxyGenerator generator, Type objType, CommonData data, object baseObj);
    }
}