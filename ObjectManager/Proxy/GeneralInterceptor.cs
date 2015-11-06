using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class GeneralInterceptor : IInterceptor
    {
        private readonly string _id;
        private readonly IDatabase _database;
        private readonly RedisObjectManager _demgelRedis;

        private readonly Dictionary<string, bool> _retrieved;

        private bool _processing;

        public GeneralInterceptor(string id, IDatabase database, RedisObjectManager demgelRedis)
        {
            _id = id;
            _database = database;
            _demgelRedis = demgelRedis;

            _retrieved = new Dictionary<string, bool>();
        }

        public void Intercept(IInvocation invocation)
        {
            if (!invocation.Method.Name.StartsWith("get_", StringComparison.Ordinal) ||
                _processing)
            {
                try
                {
                    invocation.Proceed();
                }
                catch
                {
                    invocation.ReturnValue = null;
                }
                return;
            }

            // See if we need to process this
            bool retrieved;
            if (_retrieved.TryGetValue(invocation.Method.Name, out retrieved))
            {
                if (retrieved)
                {
                    invocation.Proceed();
                    return;
                }
            }

            _processing = true;

            try
            {
                var cAttr = invocation.Method.ReflectedType?.GetMembers().Where(x =>
                {
                    var y = x as PropertyInfo;
                    return y != null && y.GetMethod.Name.Equals(invocation.Method.Name);
                }).SingleOrDefault() as PropertyInfo;

                var value = cAttr?.GetValue(invocation.Proxy, invocation.Arguments);

                // Checks to see if the underlying proxy is processed, if not process it, if it
                // is ready (id is set)
                if (!(value is IProxyTargetAccessor))
                {
                    if (value is IRedisObject)
                    {
                        //cAttr.SetValue(invocation.Proxy, value);
                        return;
                    } 
                    else
                    {
                        var t = ((IProxyTargetAccessor)invocation.Proxy)
                        .GetInterceptors()
                        .SingleOrDefault(x => x is AddSetInterceptor) as AddSetInterceptor;

                        if (t != null && t.Processed)
                        {
                            return;
                        }
                        value = invocation.Proxy;
                    }
                }

                string redisId;

                var id = value?.GetType().GetProperties()
                        .SingleOrDefault(x => x.GetCustomAttributes().Any(y => y is RedisIdKey));
                var redisvalue = id?.GetValue(value, null);

                if (id != null && id.PropertyType == typeof (string))
                {
                    redisId = (string) redisvalue;
                }
                else if (id != null && id.PropertyType == typeof (Guid))
                {
                    redisId = (redisvalue as Guid?)?.ToString();
                }
                else
                {
                    redisId = _id;
                }

                // TODO clean this
                if (redisId == null)
                {
                    redisId = _id;
                }

                if (value == null)
                {
                    throw new Exception("Object is not valid.");
                }

                var changeTracker = ((IProxyTargetAccessor) value)
                    .GetInterceptors()
                    .SingleOrDefault(x => x is AddSetInterceptor) as AddSetInterceptor;

                if (changeTracker != null) changeTracker.ParentProxy = invocation.Proxy;

                _demgelRedis.RetrieveObject(value, redisId,
                    _database, cAttr);

                if (changeTracker != null) changeTracker.Processed = true;

                var removeInterceptor = ((IProxyTargetAccessor)value)
                    .GetInterceptors()
                    .SingleOrDefault(x => x is RemoveInterceptor) as RemoveInterceptor;

                if (removeInterceptor != null)
                {
                    removeInterceptor.Processed = true;
                    removeInterceptor.ParentProxy = invocation.Proxy;
                }

                _retrieved.Add(invocation.Method.Name, true);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error in GeneralInterceptor: " + e.Message + " --- " + e.StackTrace);
            }
            finally
            {
                _processing = false;
                invocation.Proceed();
            }
        }

        public bool HasRetrievedObject(MethodInfo methodInfo)
        {
            bool ret;
            return _retrieved.TryGetValue(methodInfo.Name, out ret) && ret;
        }

        public bool ResetObject(MethodInfo methodInfo)
        {
            return true;
        }
    }
}