using System;
using System.Reflection;
using Castle.DynamicProxy;

namespace DemgelRedis.ObjectManager.Proxy.Selectors
{
    public class DictionarySelector : IInterceptorSelector
    {
        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
        {
            throw new NotImplementedException();
        }
    }
}