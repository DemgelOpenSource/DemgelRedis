using System;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Extensions;
using DemgelRedis.ObjectManager.Proxy.RedisObjectInterceptor;

namespace DemgelRedis.ObjectManager.Proxy.Selectors
{
    public class RedisObjectSelector : IInterceptorSelector
    {
        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
        {
            return method.IsSetMethod() ? interceptors.Where(i => i is RedisObjectSetInterceptor).ToArray() 
                : interceptors.Where(i => i is GeneralGetInterceptor).ToArray();
        }
    }
}