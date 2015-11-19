using System;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Extensions;
using DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor;

namespace DemgelRedis.ObjectManager.Proxy.Selectors
{
    public class DictionarySelector : IInterceptorSelector
    {
        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
        {
            if (method.IsAddMethod()) return interceptors.Where(x => x is DictionaryAddInterceptor).ToArray();
            if (method.IsSetMethod()) return interceptors.Where(x => x is DictionarySetInterceptor).ToArray();
            if (method.IsRemoveMethod()) return interceptors.Where(x => x is DictionaryRemoveInterceptor).ToArray();
            return new IInterceptor[0];
        }
    }
}