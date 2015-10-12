using System;
using System.Collections.Generic;
using System.Reflection;
using StackExchange.Redis;

namespace Demgel.Redis.Interfaces
{
    public interface IRedisHandler
    {
        bool CanHandle(object obj);
        object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
        bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null);
    }
}