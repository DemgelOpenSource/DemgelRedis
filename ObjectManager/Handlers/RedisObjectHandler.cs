﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Handlers
{
    public class RedisObjectHandler : RedisHandler
    {
        //private readonly IRedisBackup _redisBackup;
        private readonly bool _transient = true;

        public RedisObjectHandler(RedisObjectManager manager, IRedisBackup redisBackup)
            : base(manager)
        {
            //_redisBackup = redisBackup;
        }

        public override bool CanHandle(object obj)
        {
            return obj is IRedisObject;
        }

        public override object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var redisKey = new RedisKeyObject(objType, id);

            RedisObjectManager.RedisBackup?.RestoreHash(redisDatabase, redisKey);
            var ret = redisDatabase.HashGetAll(redisKey.RedisKey);

            // Attempt to set all given properties
            obj = RedisObjectManager.ConvertToObject(obj, ret);

            // Do we continue here if it is a base system class?
            if (!(obj is IRedisObject)) return obj;

            var props = obj.GetType().GetProperties();

            foreach (var prop in props)
            {
                if (prop.HasAttribute<RedisIdKey>())
                {
                    if (!prop.PropertyType.IsAssignableFrom(typeof(string))
                        && !prop.PropertyType.IsAssignableFrom(typeof(Guid)))
                    {
                        throw new InvalidOperationException("RedisIdKey can only be of type String or Guid");
                    }

                    if (prop.PropertyType.IsAssignableFrom(typeof(string)))
                    {
                        prop.SetValue(obj, id);
                    }
                    else
                    {
                        prop.SetValue(obj, Guid.Parse(id));
                    }
                }
                else
                {
                    // If value is virtual assume it is lazy
                    if (prop.GetMethod.IsVirtual)
                    {
                        // Create proxies here
                        if (prop.PropertyType.IsSealed) continue;
                        if (!prop.PropertyType.IsClass && !prop.PropertyType.IsInterface) continue;
                        try
                        {
                            object baseObject = prop.GetValue(obj, null);
                            if (baseObject == null)
                            {
                                baseObject = Activator.CreateInstance(prop.PropertyType);
                            }
                            var pr = RedisObjectManager.RetrieveObjectProxy(prop.PropertyType, id, redisDatabase, baseObject, _transient);
                            //var subresult = RedisObjectManager.RetrieveObject(pr, id, redisDatabase, prop).Object;
                            obj.GetType().GetProperty(prop.Name).SetValue(obj, pr);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Error here - really?", e);
                        }
                        continue;
                    }

                    // Not sure if this check is really needed, we are getting all new values
                    var value = prop.GetValue(obj, null);
                    if (value != null) continue;

                    try
                    {
                        var newObj = Activator.CreateInstance(prop.PropertyType);
                        var subresult = RedisObjectManager.RetrieveObject(newObj, id, redisDatabase, prop);
                        if (subresult.IsValid)
                        {
                            prop.SetValue(obj, subresult.Object);
                        }
                    }
                    catch
                    {
                        // TODO add something to log this better
                        Debug.WriteLine($"Exception handing '{prop.Name}'");
                    }
                }
            }

            return obj;
        }

        public override bool Save(object obj, 
            Type objType, 
            IDatabase redisDatabase, 
            string id, 
            PropertyInfo basePropertyInfo = null)
        {
            var redisKey = new RedisKeyObject(objType, id);

            var hashList = RedisObjectManager.ConvertToRedisHash(obj).ToArray();

            RedisObjectManager.RedisBackup?.UpdateHash(hashList, redisKey);
            RedisObjectManager.RedisBackup?.RestoreHash(redisDatabase, redisKey);
            redisDatabase.HashSet(redisKey.RedisKey, hashList);

            return true;
        }

        public override bool Delete(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var hashKey = new RedisKeyObject(objType, id);

            redisDatabase.KeyDelete(hashKey.RedisKey);

            return true;
        }
    }
}