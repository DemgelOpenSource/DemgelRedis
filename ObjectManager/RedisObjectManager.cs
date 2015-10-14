using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using Demgel.Redis.Converters;
using DemgelRedis.Common;
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
        private readonly GeneralInterceptorSelector _generalInterceptorSelector;
        private readonly IRedisBackup _redisBackup;

        public RedisObjectManager()
        {
            TypeConverters = new Dictionary<Type, ITypeConverter>
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

        public RedisObjectManager(IRedisBackup redisBackup)
            : this()
        {
            _redisBackup = redisBackup;
        }

        public IEnumerable<HashEntry> ConvertToRedisHash(object o, bool ignoreFail = false)
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
        /// <returns></returns>
        public async Task<T> RetrieveObjectProxy<T>(string id, IDatabase redisDatabase)
            where T : class
        {
            var proxy = RetrieveObjectProxy(typeof(T), id, redisDatabase, null);            
            var result = (await RetrieveObject(proxy, id, redisDatabase)).Object as T;
            var changeTrackerInterceptor = (ChangeTrackerInterceptor) ((result as IProxyTargetAccessor)?.GetInterceptors())?.SingleOrDefault(x => x is ChangeTrackerInterceptor);
            if (changeTrackerInterceptor != null)
                changeTrackerInterceptor.Processed = true;
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
                    new ChangeTrackerInterceptor(redisDatabase, this, _redisBackup, id));
            else if (obj == null)
            {
                proxy = _generator.CreateInterfaceProxyWithoutTarget(type,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    new GeneralInterceptor(id, redisDatabase, this),
                    new ChangeTrackerInterceptor(redisDatabase, this, _redisBackup, id));
            }
            else
            {
                proxy = _generator.CreateInterfaceProxyWithTarget(type, obj,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _generalInterceptorSelector
                    },
                    new GeneralInterceptor(id, redisDatabase, this),
                    new ChangeTrackerInterceptor(redisDatabase, this, _redisBackup, id));
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
        protected internal async Task<DemgelRedisResult> RetrieveObject(object obj, string id, IDatabase redisDatabase, PropertyInfo basePropertyInfo = null)
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
            //var redisKey = ParseRedisKey(objType.GetCustomAttributes(), id);
            var redisKey = new RedisKeyObject(objType.GetCustomAttributes(), id);

            // TODO check to see if we need to do Restore from Backing
            var ret = redisDatabase.HashGetAll(redisKey.RedisKey);
            if (ret.Length == 0)
            {
                // Probably need to try to restore this item
                if (_redisBackup != null)
                {
                    ret = _redisBackup.RestoreHash(redisDatabase, redisKey);
                }
            }

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
                        continue;
                    }

                    // If value is not set, then recursion
                    // Not sure if this check is really needed, we are getting all new values
                    var value = prop.GetValue(result.Object, null);
                    if (value != null) continue;

                    try
                    {
                        var newObj = Activator.CreateInstance(prop.PropertyType);
                        var subresult = await RetrieveObject(newObj, id, redisDatabase, prop);
                        if (subresult.IsValid)
                        {
                            prop.SetValue(result.Object, subresult.Object);
                        }
                    }
                    catch
                    {
                        // TODO add something to log this better
                        Debug.WriteLine($"Exception handing '{prop.Name}'");
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
            //var redisKey = ParseRedisKey(objType.GetCustomAttributes(), id);
            var redisKey = new RedisKeyObject(objType.GetCustomAttributes(), id);

            var hashList = ConvertToRedisHash(obj);
            // TODO check if we need to do any Backing work
            redisDatabase.HashSet(redisKey.RedisKey, hashList.ToArray());
        }
        // DeleteObjectFromRedis (and tables)
    }   
}