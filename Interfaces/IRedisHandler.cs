using System;
using System.Reflection;
using StackExchange.Redis;

namespace DemgelRedis.Interfaces
{
    public interface IRedisHandler
    {
        bool CanHandle(object obj);
        object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
        bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
        bool Delete(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
    }
}