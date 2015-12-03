using System;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Extensions;
using DemgelRedis.ObjectManager.Proxy.SetInterceptor;

namespace DemgelRedis.ObjectManager.Proxy.Selectors
{
    public class SetSelector : IInterceptorSelector
    {
         
        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
        {
            if (method.IsAddMethod()) return interceptors.Where(x => x is SetAddInterceptor).ToArray();
            if (method.IsRemoveMethod()) return interceptors.Where(x => x is SetRemoveInterceptor).ToArray();
            return new IInterceptor[0];
        }
    }
}