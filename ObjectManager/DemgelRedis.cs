using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using Demgel.Redis;
using Demgel.Redis.Converters;
using Demgel.Redis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using DemgelRedis.ObjectManager.Handlers;
using DemgelRedis.ObjectManager.Proxy;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager
{
    public class DemgelRedis
    {
        private readonly ProxyGenerator _generator = new ProxyGenerator();
        private readonly Dictionary<Type, ITypeConverter> _typeConverters;
        private readonly IList<IRedisHandler> _handlers;
        private readonly GeneralInterceptorSelector _generalInterceptorSelector;

        public DemgelRedis()
        {
            _typeConverters = new Dictionary<Type, ITypeConverter>
            {
                {typeof(Guid), new GuidConverter() },
                {typeof(string), new StringConverter() }
            };

            _handlers = new List<IRedisHandler>
            {
                new EnumerableHandler(this)
            };

            _generalInterceptorSelector = new GeneralInterceptorSelector();
        }

        public IEnumerable<HashEntry> ConvertToRedisHash(object o, bool ignoreFail = false)
        {
            foreach (var prop in o.GetType().GetProperties())
            {
                var type = prop.PropertyType;
                ITypeConverter converter;
                if (_typeConverters.TryGetValue(type, out converter))
                {
                    yield return new HashEntry(prop.Name, converter.ToWrite(prop.GetValue(o, null)));
                }
            }
        }

        public object ConvertToObject(object obj, HashEntry[] hashEntries, bool ignoreFail = false)
        {
            var testObj = obj;
            var hashDict = hashEntries.ToDictionary();

            foreach (var prop in obj.GetType().GetProperties())
            {
                RedisValue hashPair;
                if (!hashDict.TryGetValue(prop.Name, out hashPair)) continue;

                var type = prop.PropertyType;
                ITypeConverter converter;
                if (!_typeConverters.TryGetValue(type, out converter)) continue;
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
        /// <returns></returns>
        public T RetrieveObjectProxy<T>(string id, IDatabase redisDatabase)
            where T : class
        {
            // TODO this needs to be moved down
            // TODO make this a redirect function
            //var proxy = _generator.CreateClassProxy<T>(
            //    new ProxyGenerationOptions(new GeneralProxyGenerationHook()) {Selector = _generalInterceptorSelector},
            //    new GeneralInterceptor(id, redisDatabase, this),
            //    new ChangeTrackerInterceptor(redisDatabase));

            var proxy = RetrieveObjectProxy(typeof(T), id, redisDatabase, null);
            // Lets try setting all Proxies...
            
            //foreach (var p in proxy.GetType().GetProperties())
            //{
            //    if (!p.GetMethod.IsVirtual) continue;
            //    var baseObject = p.GetValue(proxy, null);
            //    var pr = RetrieveObjectProxy(p.PropertyType, id, redisDatabase, baseObject);
            //    proxy.GetType().GetProperty(p.Name).SetValue(proxy, pr);
            //}
            
            var result = RetrieveObject(proxy, id, redisDatabase).Object as T;
            return result;
        }

        private object RetrieveObjectProxy(Type type, string id, IDatabase redisDatabase, object obj)
        {
            object proxy;

            if (!type.IsInterface)
                proxy = _generator.CreateClassProxy(type,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    new GeneralInterceptor(id, redisDatabase, this),
                    new ChangeTrackerInterceptor(redisDatabase));
            else if (obj == null)
            {
                proxy = _generator.CreateInterfaceProxyWithoutTarget(type,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    new GeneralInterceptor(id, redisDatabase, this),
                    new ChangeTrackerInterceptor(redisDatabase));
            }
            else
            {
                proxy = _generator.CreateInterfaceProxyWithTarget(type, obj,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    new GeneralInterceptor(id, redisDatabase, this),
                    new ChangeTrackerInterceptor(redisDatabase));
            }

            // Lets try setting all Proxies...

            foreach (var p in proxy.GetType().GetProperties())
            {
                if (!p.GetMethod.IsVirtual || p.PropertyType.IsSealed) continue;
                if (!p.PropertyType.IsClass && !p.PropertyType.IsInterface) continue;
                var baseObject = p.GetValue(proxy, null);
                var pr = RetrieveObjectProxy(p.PropertyType, id, redisDatabase, baseObject);
                proxy.GetType().GetProperty(p.Name).SetValue(proxy, pr);
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

            //var objType = obj.GetType();
            var objType = obj.GetType();

            // We might not be dealing with a Hash all the time.. maybe a set? or List?
            foreach (var handler in _handlers.Where(x => x.CanHandle(obj)))
            {
                result.Object = handler.Read(obj, objType, redisDatabase, id, basePropertyInfo);
                return result;
            }

            // If another type was not found, try to set all values normally.
            var redisKey = ParseRedisKey(objType.GetCustomAttributes(), id);

            var ret = redisDatabase.HashGetAll(redisKey);

            if (ret.Length == 0)
            {
                result.Result = DemgelResult.NotFound;
            }

            // Attempt to set all given properties
            result.Object = ConvertToObject(obj, ret, true);

            // Do we continue here if it is a base system class?
            if (!(result.Object is IRedisObject)) return result;

            var props = result.Object.GetType().GetProperties();

            foreach (var prop in props)
            {
                //if (prop.CustomAttributes.Any(x => x.AttributeType == typeof (RedisIdKey)))
                if(prop.HasAttribute<RedisIdKey>())
                {
                    if (!prop.PropertyType.IsAssignableFrom(typeof (string)))
                    {
                        throw new InvalidOperationException("RedisIdKey can only be of type String");
                    }
                    prop.SetValue(result.Object, id);
                }
                else
                {
                    // If value is virtual assume it is lazy
                    if (prop.GetMethod.IsVirtual)
                    {
                        //var t = RetrieveObjectProxy(prop.PropertyType, id, redisDatabase, obj);
                        //prop.SetValue(result.Object, t);
                        continue;
                    }

                    // If value is not set, then recursion
                    // Not sure if this check is really needed, we are getting all new values
                    var value = prop.GetValue(result.Object, null);
                    if (value != null) continue;

                    var newObj = Activator.CreateInstance(prop.PropertyType);
                    var subresult = RetrieveObject(newObj, id, redisDatabase, prop);
                    if (subresult.IsValid)
                    {
                        prop.SetValue(result.Object, subresult.Object);
                    }
                }
            }

            return result;
        }

        // SaveObjectToRedis (and tables)
        public void SaveObject(object obj, string id, IDatabase redisDatabase)
        {
            var objType = obj.GetType();

            // Handle all complex values, if a handler handles the property then it shouldn't be hashed
            foreach (var prop in obj.GetType().GetProperties())
            {
                var currentObject = prop.GetValue(obj);
                var saved = false;
                foreach (var handler in _handlers.Where(x => x.CanHandle(currentObject)))
                {
                    if (!handler.Save(currentObject, currentObject.GetType(), redisDatabase, id, prop)) continue;
                    saved = true;
                    break;
                }
                if (!saved && currentObject is IRedisObject)
                {
                    SaveObject(currentObject, id, redisDatabase);
                }
            }

            // If no other handler could handle this obj, parse it normally and save it.
            var redisKey = ParseRedisKey(objType.GetCustomAttributes(), id);

            var hashList = ConvertToRedisHash(obj);
            redisDatabase.HashSet(redisKey, hashList.ToArray());
        }
        // DeleteObjectFromRedis (and tables)

        #region HelperFunctions

        protected internal string ParseRedisKey(IEnumerable<Attribute> obj, string id)
        {
            //var classAttr = obj.GetType().GetCustomAttributes(true);
            string prefix = null, suffix = null, redisKey;

            foreach (var attr in obj)
            {
                if (attr is RedisPrefix)
                {
                    prefix = ((RedisPrefix)attr).Key;
                    //Debug.WriteLine("Key Found");
                }
                else if (attr is RedisSuffix)
                {
                    suffix = ((RedisSuffix)attr).Key;
                    //Debug.WriteLine("Suffix Found.");
                }
            }

            if (prefix != null)
            {
                redisKey = suffix != null ? $"{prefix}:{id}:{suffix}" : $"{prefix}:{id}";
            }
            else
            {
                redisKey = suffix != null ? $"{id}:{suffix}" : id;
            }

            return redisKey;
        } 
        
        #endregion
    }
   
}