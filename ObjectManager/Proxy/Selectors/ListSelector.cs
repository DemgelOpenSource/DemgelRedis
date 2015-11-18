using System;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Extensions;
using DemgelRedis.ObjectManager.Proxy.ListInterceptor;

namespace DemgelRedis.ObjectManager.Proxy.Selectors
{
    public class ListSelector : IInterceptorSelector
    {
        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
        {
            if (method.IsAddMethod()) return interceptors.Where(x => x is ListAddInterceptor).ToArray();
            if (method.IsSetMethod()) return interceptors.Where(x => x is ListSetInterceptor).ToArray();
            if (method.IsRemoveMethod()) return interceptors.Where(x => x is ListRemoveInterceptor).ToArray();
            return new IInterceptor[0];
        }
    }
}