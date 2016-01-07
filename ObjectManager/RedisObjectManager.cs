using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Converters;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
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
        protected internal readonly IRedisBackup RedisBackup;

        public RedisObjectManager()
        {
            TypeConverters = new Dictionary<Type, ITypeConverter>
            {
                {typeof(Guid), new GuidConverter() },
                {typeof(string), new StringConverter() },
                {typeof(int), new Int32Converter() },
                {typeof(float), new FloatConverter() },
                {typeof(double), new DoubleConverter() },
                {typeof(long), new LongConverter() },
                {typeof(DateTime), new DateTimeConverter() }
            };

            _handlers = new List<IRedisHandler>
            {
                new ListHandler(this),
                new DictionaryHandler(this),
                new SetHandler(this),
                new RedisObjectHandler(this)
            };
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
                HashEntry entry;
                //var type = prop.PropertyType;
                if (prop.PropertyType.GetInterfaces().Any(x => x == typeof(IRedisObject)))
                {
                    var redisObject = prop.GetValue(o, null);
                    var redisIdAttr =
                        redisObject?.GetType().GetProperties().SingleOrDefault(
                            x => x.HasAttribute<RedisIdKey>()) ??
                        redisObject?.GetType().BaseType?.GetProperties().SingleOrDefault(
                                x => x.HasAttribute<RedisIdKey>());

                    if (redisIdAttr != null)
                    {
                        var value = redisIdAttr.GetValue(redisObject, null);
                        if (value == null)
                        {
                            entry = new HashEntry();
                        }
                        else
                        {
                            var key = new RedisKeyObject(redisObject.GetType(), (string) value);
                            entry = new HashEntry(prop.Name, key.RedisKey);
                        }
                    }
                    else
                    {
                        entry = new HashEntry();
                    }

                }
                else
                {
                    RedisValue obj;
                    entry = TryConvertToRedisValue(prop.GetValue(o, null), out obj) 
                        ? new HashEntry(prop.Name, obj) 
                        : new HashEntry();
                }

                if (entry.Value.IsNull) continue;
                yield return entry;
            }

            // Need to start recording the type
            var accessor = o as IProxyTargetAccessor;
            var typeEntry = accessor != null 
                ? new HashEntry("Type", accessor.DynProxyGetTarget().GetType().ToString()) 
                : new HashEntry("Type", o.GetType().ToString());
            yield return typeEntry;
        }

        public object ConvertToObject(object obj, HashEntry[] hashEntries)
        {
            //var testObj = obj;
            var hashDict = hashEntries.ToDictionary();

            foreach (var prop in obj.GetType().GetProperties())
            {
                RedisValue hashPair;
                if (!hashDict.TryGetValue(prop.Name, out hashPair)) continue;

                var type = prop.PropertyType;
                object value;
                if (TryConvertFromRedisValue(type, hashPair, out value))
                {
                    prop.SetValue(obj, value);
                }
            }

            return obj;
        }

        public T RetrieveObjectProxy<T>(IDatabase redisDatabase)
            where T : class, new()
        {
            var key = new RedisKeyObject(typeof(T), string.Empty);
            var obj = new T();
            redisDatabase.GenerateId(key, obj, RedisBackup);
            var proxy = RetrieveObjectProxy(typeof(T), key.Id, redisDatabase, obj) as T;

            return proxy;
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
        public T RetrieveObjectProxy<T>(string id, IDatabase redisDatabase, T baseObject)
            where T : class, new()
        {
            var prop = baseObject.GetType().GetProperties().SingleOrDefault(p => p.HasAttribute<RedisIdKey>());

            if (prop == null)
            {
                throw new Exception("RedisIDkey Attribute is required on one property");
            }

            var proxy = RetrieveObjectProxy(typeof(T), id, redisDatabase, baseObject);

            if (prop.PropertyType.IsAssignableFrom(typeof(Guid)))
            {
                prop.SetValue(proxy, Guid.Parse(id));
            }
            else if (prop.PropertyType.IsAssignableFrom(typeof(string)))
            {
                prop.SetValue(proxy, id);
            }
            else
            {
                throw new Exception("Id can only be of type String or Guid");
            }

            return proxy as T;
        }

        public T RetrieveObjectProxy<T>(string id, IDatabase redisDatabase)
            where T : class, new()
        {
            var obj = new T();

            return RetrieveObjectProxy(id, redisDatabase, obj);
        }

        public T RetrieveObjectProxy<T>(IDatabase redisDatabase, T baseObject)
            where T : class, new()
        {
            var key = new RedisKeyObject(typeof(T), string.Empty);
            redisDatabase.GenerateId(key, baseObject, RedisBackup);

            return RetrieveObjectProxy(key.Id, redisDatabase, baseObject);
        }

        protected internal object RetrieveObjectProxy(Type type, string id, IDatabase redisDatabase, object obj, object parentProxy = null)
        {
            var commonData = new CommonData
            {
                RedisDatabase = redisDatabase,
                RedisObjectManager = this,
                Id = id,
                Created = false
            };

            var handler = _handlers.SingleOrDefault(x => x.CanHandle(obj));

            var proxy = handler?.BuildProxy(_generator, type, commonData, obj);

            if (proxy == null)
            {
                throw new Exception("Generated Proxy is Null");
            }
            commonData.Created = true;
            commonData.ParentProxy = parentProxy;

            return proxy;
        }

        /// <summary>
        /// Do not call this method directly, it is ment to be called from the Proxy, call RetrieveObjectProxy first.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="id"></param>
        /// <param name="redisDatabase"></param>
        /// <param name="basePropertyInfo">Optional PropertyInfo, only required is calling IEnumerable</param>
        /// <param name="limits"></param>
        /// <returns></returns>
        protected internal void RetrieveObject(object obj, string id, IDatabase redisDatabase, PropertyInfo basePropertyInfo, LimitObject limits = null)
        {
            var objType = obj.GetType();

            foreach (var handler in _handlers.Where(x => x.CanHandle(obj)))
            {
                handler.Read(obj, objType, redisDatabase, id, basePropertyInfo, limits);
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

        public bool TryConvertToRedisValue(object value, out RedisValue convertedRedisValue)
        {
            if (value is RedisValue)
            {
                convertedRedisValue = (RedisValue)value;
                return true;
            }

            if (value == null)
            {
                convertedRedisValue = RedisValue.Null;
                return false;
            }

            ITypeConverter converter;
            if (!TypeConverters.TryGetValue(value.GetType(),
                        out converter))
            {
                convertedRedisValue = RedisValue.Null;
                return false;
            }

            convertedRedisValue = converter.ToWrite(value);
            return true;
        }

        public bool TryConvertFromRedisValue(Type type, RedisValue value, out object convertedValue)
        {
            if (type.Name.StartsWith("RedisValue", StringComparison.Ordinal))
            {
                convertedValue = value;
                return true;
            }

            ITypeConverter converter;
            if (!TypeConverters.TryGetValue(type,
                        out converter))
            {
                convertedValue = null;
                return false;
            }

            convertedValue = converter.OnRead(value);
            return true;
        }

        public object GetRedisObjectWithType(IDatabase redisDatabase, RedisKey redisKey, string id)
        {
            var key = new RedisKeyObject(redisKey);
            RedisBackup?.RestoreHash(redisDatabase, key);

            if (!redisDatabase.KeyExists(redisKey))
            {
                return null;
            }

            var typeHash = redisDatabase.HashGet(redisKey, "Type");
            if (typeHash.IsNullOrEmpty) return null;

            Type finalItemType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { 
                finalItemType = Type.GetType(typeHash + "," + assembly.FullName);
                if (finalItemType != null) break;
            }

            if (finalItemType == null)
            {
                throw new ArgumentException("Type value is saved incorrectly and will lead to corruption.");
            }

            var newObj = Activator.CreateInstance(finalItemType);
            var keyProp = newObj.GetType().GetProperties().SingleOrDefault(x => x.HasAttribute<RedisIdKey>());
            if (keyProp == null) throw new Exception("RedisObjects need to have a RedisIdKey property.");
            if (keyProp.PropertyType.IsAssignableFrom(typeof(string)))
            {
                keyProp.SetValue(newObj, id);
            }
            else if (keyProp.PropertyType.IsAssignableFrom(typeof(Guid)))
            {
                keyProp.SetValue(newObj, Guid.Parse(id));
            }
            else
            {
                throw new Exception("RedisIdKey can only be of type String or Guid");
            }

            return RetrieveObjectProxy(finalItemType, id, redisDatabase, newObj);
        }
    }   
}