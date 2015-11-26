using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using DemgelRedis.ObjectManager.Proxy;
using DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor;
using DemgelRedis.ObjectManager.Proxy.Selectors;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Handlers
{
    public class DictionaryHandler : RedisHandler
    {
        private readonly DictionarySelector _dictionarySelector;

        public DictionaryHandler(RedisObjectManager demgelRedis) : base(demgelRedis)
        {
            _dictionarySelector = new DictionarySelector();
        }

        /// <summary>
        /// Need to pass in the Proxy object of the list/enumerable
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool CanHandle(object obj)
        {
            var targetObject = GetTarget(obj);
            return targetObject is IDictionary;
        }

        public override object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo, ILimitObject limits = null)
        {
            var hashKey = new RedisKeyObject(basePropertyInfo, id);
            RedisObjectManager.RedisBackup?.RestoreHash(redisDatabase, hashKey);

            if (limits != null && limits.RestoreOnly)
            {
                return obj;
            }

            var targetType = GetTarget(obj).GetType();
            Type keyType = null;
            Type itemType = null;

            if (targetType.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType &&
                      interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                if (targetType.GetGenericArguments().Any())
                {
                    keyType = targetType.GetGenericArguments()[0];
                    itemType = targetType.GetGenericArguments()[1];
                }
            }

            var method = objType.GetMethod("Add", new []{keyType, itemType});

            if (itemType != null && itemType.GetInterfaces().Contains(typeof(IRedisObject)))
            {
                // TODO this all needs to be changed to handle IRedisObjects in a Dictionary, shouldn't be to hard
                List<HashEntry> retlist;

                if (limits != null)
                {
                    if (limits.KeyLimit != null && !limits.KeyLimit.IsNullOrEmpty())
                    {
                        retlist = new List<HashEntry>();
                        foreach (var item in limits.KeyLimit)
                        {
                            var ret = redisDatabase.HashGet(hashKey.RedisKey, (string) item);
                            if (!ret.IsNullOrEmpty)
                            {
                                retlist.Add(new HashEntry((string) item, ret));
                            }
                        }
                    } else if (limits.StartLimit != 0 || limits.TakeLimit != 0)
                    {
                        retlist =
                            redisDatabase.HashScan(hashKey.RedisKey, default(RedisValue), limits.TakeLimit,
                                limits.StartLimit).ToList();
                    }
                    else
                    {
                        retlist = new List<HashEntry>();
                    }
                }
                else
                {
                    retlist = redisDatabase.HashGetAll(hashKey.RedisKey).ToList();
                }

                foreach (var ret in retlist)
                {
                    // We need to check to make sure the object exists (Overhead... it happens)
                    if (!redisDatabase.KeyExists((string) ret.Value))
                    {
                        redisDatabase.HashDelete(hashKey.RedisKey, ret.Name);
                        continue;
                    }

                    var key = ret.Value.ParseKey();

                    var newObj = Activator.CreateInstance(itemType);
                    var keyProp = newObj.GetType().GetProperties().SingleOrDefault(x => x.HasAttribute<RedisIdKey>());
                    if (keyProp == null) throw new Exception("RedisObjects need to have a RedisIdKey property.");
                    if (keyProp.PropertyType.IsAssignableFrom(typeof(string)))
                    {
                        keyProp.SetValue(newObj, key);
                    } else if (keyProp.PropertyType.IsAssignableFrom(typeof (Guid)))
                    {
                        keyProp.SetValue(newObj, Guid.Parse(key));
                    }
                    else
                    {
                        throw new Exception("RedisIdKey can only be of type String or Guid");
                    }

                    var newProxy = RedisObjectManager.RetrieveObjectProxy(itemType, key, redisDatabase, newObj);

                    method.Invoke(obj, new[] { (string) ret.Name, newProxy });
                }
                return obj;
            }

            if (itemType != typeof (RedisValue))
            {
                // Try to process each entry as a proxy, or fail
                throw new InvalidCastException($"Use RedisValue instead of {itemType?.Name}.");
            }

            var retList = redisDatabase.HashGetAll(hashKey.RedisKey);
            foreach (var ret in retList)
            {
                method.Invoke(obj, new[] {(string)ret.Name, (object)ret.Value});
            }
            return obj;
        }

        public override bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            Debug.WriteLine("Save was called on this object...");
            return true;
        }

        public override bool Delete(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var hashKey = new RedisKeyObject(basePropertyInfo, id);

            redisDatabase.KeyDelete(hashKey.RedisKey);

            return true;
        }

        public override object BuildProxy(ProxyGenerator generator, Type objType, CommonData data, object baseObj)
        {
            if (!objType.IsInterface)
            {
                throw new Exception("Dictionary can only be created from IDictionary Interface");
            }

            object proxy;

            if (baseObj == null)
            {
                proxy = generator.CreateInterfaceProxyWithoutTarget(objType,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _dictionarySelector
                    },
                    new GeneralGetInterceptor(data), new DictionaryAddInterceptor(data), new DictionarySetInterceptor(data), new DictionaryRemoveInterceptor(data));
            }
            else
            {
                proxy = generator.CreateInterfaceProxyWithTarget(objType, baseObj,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _dictionarySelector
                    },
                    new GeneralGetInterceptor(data), new DictionaryAddInterceptor(data), new DictionarySetInterceptor(data), new DictionaryRemoveInterceptor(data));
            }
            return proxy;
        }
    }
}