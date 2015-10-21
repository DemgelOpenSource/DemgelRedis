﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Converters;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Handlers;
using DemgelRedis.ObjectManager.Proxy;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager
{
    public class RedisObjectManager
    {
        private readonly ProxyGenerator _generator = new ProxyGenerator();
        protected internal readonly Dictionary<Type, ITypeConverter> TypeConverters;
        private readonly IList<IRedisHandler> _handlers;
        private readonly GeneralInterceptorSelector _generalInterceptorSelector;
        protected internal readonly IRedisBackup RedisBackup;

        public RedisObjectManager()
        {
            TypeConverters = new Dictionary<Type, ITypeConverter>
            {
                {typeof(Guid), new GuidConverter() },
                {typeof(string), new StringConverter() },
                {typeof(int), new Int32Converter() },
                {typeof(float), new FloatConverter() },
                {typeof(double), new DoubleConverter() }
            };

            _handlers = new List<IRedisHandler>
            {
                new ListHandler(this),
                new DictionaryHandler(this),
                new RedisObjectHandler(this, RedisBackup)
            };

            _generalInterceptorSelector = new GeneralInterceptorSelector();
        }

        public RedisObjectManager(IRedisBackup redisBackup)
            : this()
        {
            RedisBackup = redisBackup;
        }

        public IEnumerable<HashEntry> ConvertToRedisHash(object o)
        {
            foreach (var prop in o.GetType().GetProperties())
            {
                //if (prop.HasAttribute<RedisIdKey>()) continue;
                var type = prop.PropertyType;
                ITypeConverter converter;
                if (!TypeConverters.TryGetValue(type, out converter)) continue;
                var ret = new HashEntry(prop.Name, converter.ToWrite(prop.GetValue(o, null)));
                if (ret.Value.IsNull) continue;
                yield return ret;
            }
        }

        public object ConvertToObject(object obj, HashEntry[] hashEntries)
        {
            var testObj = obj;
            var hashDict = hashEntries.ToDictionary();

            foreach (var prop in obj.GetType().GetProperties())
            {
                RedisValue hashPair;
                if (!hashDict.TryGetValue(prop.Name, out hashPair)) continue;

                var type = prop.PropertyType;
                ITypeConverter converter;
                if (!TypeConverters.TryGetValue(type, out converter)) continue;
                var value = converter.OnRead(hashPair);
                prop.SetValue(testObj, value);
            }

            return testObj;
        }

        /// <summary>
        /// Retrieves an object from redis cache by id
        /// 
        /// usually assumes a key:id structure, but if no key field is supplied
        /// will just search by id
        /// 
        /// can use key:id:suffix
        /// </summary>
        /// <param name="id">The id of the object to find</param>
        /// <param name="redisDatabase"></param>
        /// <param name="isTransient">Short lived objects should be transient (prevent subscribing to events)</param>
        /// <returns></returns>
        public T RetrieveObjectProxy<T>(string id, IDatabase redisDatabase, bool isTransient = false)
            where T : class
        {
            var proxy = RetrieveObjectProxy(typeof(T), id, redisDatabase, null, isTransient);            
            var result = (RetrieveObject(proxy, id, redisDatabase)).Object as T;
            var changeTrackerInterceptor = (AddSetInterceptor) ((result as IProxyTargetAccessor)?.GetInterceptors())?.SingleOrDefault(x => x is AddSetInterceptor);
            if (changeTrackerInterceptor != null)
                changeTrackerInterceptor.Processed = true;
            return result;
        }

        protected internal object RetrieveObjectProxy(Type type, string id, IDatabase redisDatabase, object obj, bool isTransient)
        {
            object proxy;

            if (!type.IsInterface)
                if (obj != null)
                {
                    proxy = _generator.CreateClassProxyWithTarget(type, obj,
                        new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                        {
                            Selector = _generalInterceptorSelector
                        },
                        new GeneralInterceptor(id, redisDatabase, this),
                        new AddSetInterceptor(redisDatabase, this, RedisBackup, id, isTransient),
                        new RemoveInterceptor(id, redisDatabase, this));
                }
                else
                {
                    proxy = _generator.CreateClassProxy(type,
                        new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                        {
                            Selector = _generalInterceptorSelector
                        },
                        new GeneralInterceptor(id, redisDatabase, this),
                        new AddSetInterceptor(redisDatabase, this, RedisBackup, id, isTransient),
                        new RemoveInterceptor(id, redisDatabase, this));
                }
            else if (obj == null)
            {
                proxy = _generator.CreateInterfaceProxyWithoutTarget(type,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    new GeneralInterceptor(id, redisDatabase, this),
                    new AddSetInterceptor(redisDatabase, this, RedisBackup, id, isTransient),
                    new RemoveInterceptor(id, redisDatabase, this));
            }
            else
            {
                proxy = _generator.CreateInterfaceProxyWithTarget(type, obj,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    new GeneralInterceptor(id, redisDatabase, this),
                    new AddSetInterceptor(redisDatabase, this, RedisBackup, id, isTransient),
                    new RemoveInterceptor(id, redisDatabase, this));
            }

            return proxy;
        }

        /// <summary>
        /// Do not call this method directly, it is ment to be called from the Proxy, call RetrieveObjectProxy first.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="id"></param>
        /// <param name="redisDatabase"></param>
        /// <param name="basePropertyInfo">Optional PropertyInfo, only required is calling IEnumerable</param>
        /// <returns></returns>
        protected internal DemgelRedisResult RetrieveObject(object obj, string id, IDatabase redisDatabase, PropertyInfo basePropertyInfo = null)
        {
    
            var result = new DemgelRedisResult
            {
                Result = DemgelResult.Success,
                Object = obj
            };

            var objType = obj.GetType();

            // We might not be dealing with a Hash all the time.. maybe a set? or List?
            foreach (var handler in _handlers.Where(x => x.CanHandle(obj)))
            {
                result.Object = handler.Read(obj, objType, redisDatabase, id, basePropertyInfo);
                return result;
            }

            return result;
        }

        /// <summary>
        /// Will manually save an object and all underlying objects
        /// </summary>
        /// <param name="obj">Object to be saved</param>
        /// <param name="id">Id of the object to be saved</param>
        /// <param name="redisDatabase">RedisDatabase to save too</param>
        public bool SaveObject(object obj, string id, IDatabase redisDatabase)
        {
            var handlers =
                    _handlers.Where(x => x.CanHandle(obj));

            var handled = handlers.Where(handler => handler.Save(obj, obj.GetType(), redisDatabase, id)).ToArray();

            return handled.Any();
        }
        // DeleteObjectFromRedis (and backing)
    }   
}