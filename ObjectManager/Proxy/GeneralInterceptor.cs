using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Interfaces;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class GeneralInterceptor : IInterceptor
    {
        public readonly CommonData CommonData;

        private readonly Dictionary<string, bool> _retrieved;

        //public object ParentProxy { get; set; }

        //private bool _processing;

        public GeneralInterceptor(CommonData data)
        {
            CommonData = data;

            _retrieved = new Dictionary<string, bool>();
        }

        public void Intercept(IInvocation invocation)
        {
            invocation.Proceed();
            if (!invocation.Method.Name.StartsWith("get_", StringComparison.Ordinal) ||
                CommonData.Processing)
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

            
            CommonData.Processing = true;

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

                        if (t != null && !t.CommonData.Processed)
                        {
                            //CommonData.ProcessProxy(invocation.Proxy, cAttr, invocation.Proxy);
                        }

                        value = cAttr?.GetValue(invocation.Proxy, invocation.Arguments);
                    }
                }

                if (!(value is IProxyTargetAccessor)) return;

                var a = ((IProxyTargetAccessor) value)
                    .GetInterceptors()
                    .SingleOrDefault(x => x is GeneralInterceptor) as GeneralInterceptor;

                if (a != null && a.CommonData.Processed) return;

                //CommonData.ProcessProxy(invocation.Proxy, cAttr, value);
                _retrieved.Add(invocation.Method.Name, true);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error in GeneralInterceptor: " + e.Message + " --- " + e.StackTrace);
            }
            finally
            {
                CommonData.Processing = false;
                invocation.Proceed();
            }
        }

        public bool ResetObject(MethodInfo methodInfo)
        {
            return true;
        }
    }
}