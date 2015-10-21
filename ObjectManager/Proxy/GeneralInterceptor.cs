using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
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
                invocation.Proceed();
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
                if (!(value is IProxyTargetAccessor)) return;

                string redisId;
                if (invocation.Arguments.Length > 0)
                {
                    var id = value.GetType().GetProperties()
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
                }
                else
                {
                    redisId = _id;
                }

                var result = _demgelRedis.RetrieveObject(value, redisId,
                    _database, cAttr);

                var changeTracker = ((IProxyTargetAccessor) value)
                    .GetInterceptors()
                    .SingleOrDefault(x => x is AddSetInterceptor) as AddSetInterceptor;

                if (changeTracker != null)
                {
                    changeTracker.Processed = true;
                    changeTracker.ParentProxy = invocation.Proxy;
                }

                var removeInterceptor = ((IProxyTargetAccessor)value)
                    .GetInterceptors()
                    .SingleOrDefault(x => x is RemoveInterceptor) as RemoveInterceptor;

                if (removeInterceptor != null)
                {
                    removeInterceptor.Processed = true;
                    removeInterceptor.ParentProxy = invocation.Proxy;
                }

                _retrieved.Add(invocation.Method.Name, true);

                invocation.ReturnValue = result.Object;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
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