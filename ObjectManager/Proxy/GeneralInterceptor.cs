using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class GeneralInterceptor : IInterceptor
    {
        private readonly string _id;
        public readonly CommonData CommonData;

        private readonly Dictionary<string, bool> _retrieved;

        //public object ParentProxy { get; set; }

        private bool _processing;

        public GeneralInterceptor(string id, CommonData data)
        {
            _id = id;
            CommonData = data;

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

                // TODO we really need this?
                // Checks to see if the underlying proxy is processed, if not process it, if it
                // is ready (id is set)
                if (!(value is IProxyTargetAccessor))
                {
                    if (invocation.Proxy is IRedisObject)
                    {
                        var t = ((IProxyTargetAccessor)invocation.Proxy)
                        .GetInterceptors()
                        .SingleOrDefault(x => x is GeneralInterceptor) as GeneralInterceptor;

                        if (t.CommonData.Processed)
                        {
                            return;
                        }

                        CommonData.ProcessProxy(invocation.Proxy, cAttr, invocation.Proxy);

                        value = cAttr?.GetValue(invocation.Proxy, invocation.Arguments);

                        if (value is IProxyTargetAccessor)
                        {
                            CommonData.ProcessProxy(invocation.Proxy, cAttr, value);
                            _retrieved.Add(invocation.Method.Name, true);
                        }
                    }
                } else
                {
                    CommonData.ProcessProxy(invocation.Proxy, cAttr, value);

                    _retrieved.Add(invocation.Method.Name, true);
                }
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

        //private void ProcessProxy(object parentProxy, PropertyInfo cAttr, object value)
        //{
        //    string redisId;

        //    var id = value?.GetType().GetProperties()
        //        .SingleOrDefault(x => x.HasAttribute<RedisIdKey>());
        //    var redisvalue = id?.GetValue(value, null);

        //    if (id != null && id.PropertyType == typeof (string))
        //    {
        //        redisId = (string) redisvalue;
        //    }
        //    else if (id != null && id.PropertyType == typeof (Guid))
        //    {
        //        redisId = (redisvalue as Guid?)?.ToString();
        //    }
        //    else
        //    {
        //        redisId = _id;
        //    }

        //    // TODO clean this
        //    if (redisId == null)
        //    {
        //        redisId = _id;
        //    }

        //    if (value == null)
        //    {
        //        throw new Exception("Object is not valid.");
        //    }

        //    var generalInterceptorOfValue = ((IProxyTargetAccessor)value)
        //        .GetInterceptors()
        //        .SingleOrDefault(x => x is GeneralInterceptor) as GeneralInterceptor;

        //    generalInterceptorOfValue.CommonData.ParentProxy = parentProxy;

        //    generalInterceptorOfValue.CommonData.RedisObjectManager.RetrieveObject(value, redisId,
        //        generalInterceptorOfValue.CommonData.RedisDatabase, cAttr);

        //    generalInterceptorOfValue.CommonData.Processed = true;
        //}

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