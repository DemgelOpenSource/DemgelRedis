using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Handlers
{
    public class RedisObjectHandler : RedisHandler
    {
        //private readonly IRedisBackup _redisBackup;
        private readonly bool _transient = true;

        public RedisObjectHandler(RedisObjectManager manager)
            : base(manager)
        {
        }

        public override bool CanHandle(object obj)
        {
            return obj is IRedisObject;
        }

        public override object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            if (id == null)
            {
                throw new Exception("Id can't be null");
            }
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
                            var baseObject = prop.GetValue(obj, null);
                            if (baseObject == null)
                            {
                                baseObject = Activator.CreateInstance(prop.PropertyType);
                            }
                            // If the target is an IRedisObject we need to get the ID differently
                            string objectKey;
                            if (prop.PropertyType.GetInterfaces().Any(x => x == typeof (IRedisObject)))
                            {
                                // Try to get the property value from ret
                                RedisValue propKey;
                                if (ret.ToDictionary().TryGetValue(prop.Name, out propKey))
                                {
                                    objectKey = propKey.ParseKey();
                                }
                                else
                                {
                                    // No key was found (this property has not value)
                                    continue;
                                }
                            }
                            else
                            {
                                objectKey = id;
                            }
                            // TODO create extension to handle setting RedisIdKey
                            foreach (var p in baseObject.GetType().GetProperties())
                            {
                                foreach (var att in p.GetCustomAttributes())
                                {
                                    if (att is RedisIdKey)
                                    {
                                        p.SetValue(baseObject, objectKey);
                                    }
                                }
                            }

                            if (!(baseObject is IProxyTargetAccessor))
                            {
                                var pr = RedisObjectManager.RetrieveObjectProxy(prop.PropertyType, objectKey, redisDatabase, baseObject, _transient);
                                obj.GetType().GetProperty(prop.Name).SetValue(obj, pr);
                            }
                           
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Error here - really?" + e);
                        }
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