using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Converters;
using DemgelRedis.Extensions;
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
                new RedisObjectHandler(this)
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
                var value = converter.OnRead(hashPair, prop);
                prop.SetValue(testObj, value);
            }

            return testObj;
        }

        public T RetrieveObjectProxy<T>(IDatabase redisDatabase)
            where T : class, IRedisObject, new()
        {
            var key = new RedisKeyObject(typeof(T), string.Empty);
            redisDatabase.GenerateId(key, new T(), RedisBackup);
            //GenerateId(redisDatabase, key, new T());
            return RetrieveObjectProxy<T>(key.Id, redisDatabase);
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
        /// <returns></returns>
        public T RetrieveObjectProxy<T>(string id, IDatabase redisDatabase)
            where T : class
        {
            var proxy = RetrieveObjectProxy(typeof(T), id, redisDatabase, null);            
            return proxy as T;
        }

        protected internal object RetrieveObjectProxy(Type type, string id, IDatabase redisDatabase, object obj)
        {
            var commonData = new CommonData
            {
                RedisDatabase = redisDatabase,
                RedisObjectManager = this
            };

            var general = new GeneralInterceptor(id, commonData);
            var addset = new AddSetInterceptor(id, commonData);
            var remove = new RemoveInterceptor(id, commonData);

            if (!type.IsInterface)
            {
                if (obj != null)
                {
                    return _generator.CreateClassProxyWithTarget(type, obj,
                        new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                        {
                            Selector = _generalInterceptorSelector
                        },
                        general, addset, remove);
                }
                return _generator.CreateClassProxy(type,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    general, addset, remove);
            }
            if (obj == null)
            {
                return _generator.CreateInterfaceProxyWithoutTarget(type,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    general, addset, remove);
            }
            return _generator.CreateInterfaceProxyWithTarget(type, obj,
                new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                {
                    Selector = _generalInterceptorSelector
                },
                general, addset, remove);
        }

        /// <summary>
        /// Do not call this method directly, it is ment to be called from the Proxy, call RetrieveObjectProxy first.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="id"></param>
        /// <param name="redisDatabase"></param>
        /// <param name="basePropertyInfo">Optional PropertyInfo, only required is calling IEnumerable</param>
        /// <returns></returns>
        protected internal void RetrieveObject(object obj, string id, IDatabase redisDatabase, PropertyInfo basePropertyInfo = null)
        {
            var objType = obj.GetType();

            // We might not be dealing with a Hash all the time.. maybe a set? or List?
            foreach (var handler in _handlers.Where(x => x.CanHandle(obj)))
            {
                handler.Read(obj, objType, redisDatabase, id, basePropertyInfo);
            }
        }

        /// <summary>
        /// Will manually save an object and all underlying objects
        /// </summary>
        /// <param name="obj">Object to be saved</param>
        /// <param name="id">Id of the object to be saved</param>
        /// <param name="redisDatabase">RedisDatabase to save too</param>
        public bool SaveObject(object obj, string id, IDatabase redisDatabase)
        {
            return _handlers.Where(x => x.CanHandle(obj))
                .Where(handler => handler.Save(obj, obj.GetType(), redisDatabase, id))
                .ToArray().Any();
        }

        public bool DeleteObject(object obj, string id, IDatabase redisDatabase)
        {
            return _handlers.Where(x => x.CanHandle(obj))
                .Where(handler => handler.Delete(obj, obj.GetType(), redisDatabase, id))
                .ToArray().Any();
        }

        //protected internal void GenerateId(IDatabase database, RedisKeyObject key, object argument)
        //{
        //    var redisIdAttr =
        //        argument.GetType().GetProperties().SingleOrDefault(
        //            x => x.GetCustomAttributes().Any(a => a is RedisIdKey));

        //    if (redisIdAttr == null) return; // Throw error

        //    var value = redisIdAttr.GetValue(argument, null);

        //    if (redisIdAttr.PropertyType == _stringType)
        //    {
        //        var currentValue = (string)value;
        //        if (string.IsNullOrEmpty(currentValue))
        //        {
        //            RedisBackup.RestoreCounter(database, key);
        //            var newId = database.StringIncrement($"demgelcounter:{key.CounterKey}");
        //            RedisBackup.UpdateCounter(database, key);
        //            key.Id = newId.ToString();
        //            redisIdAttr.SetValue(argument, key.Id);
        //        }
        //        else
        //        {
        //            key.Id = currentValue;
        //        }
        //    }
        //    else if (redisIdAttr.PropertyType == _guidType)
        //    {
        //        var guid = (Guid)value;
        //        if (guid == Guid.Empty)
        //        {
        //            guid = Guid.NewGuid();
        //            key.Id = guid.ToString();
        //            redisIdAttr.SetValue(argument, guid);
        //        }
        //        else
        //        {
        //            key.Id = guid.ToString();
        //        }
        //    }
        //    else
        //    {
        //        throw new ArgumentException("RedisIdKey needs to be either Guid or String");
        //    }
        //}
    }   
}