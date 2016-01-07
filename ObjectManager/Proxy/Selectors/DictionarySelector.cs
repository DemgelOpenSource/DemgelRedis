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
            if (method.IsGetMethod() && !method.Name.StartsWith("get_Count", StringComparison.Ordinal)) return interceptors.Where(x => x is DictionaryGetInterceptor).ToArray();
            if (method.IsTryGetValueMethod()) return interceptors.Where(x => x is DictionaryGetInterceptor).ToArray();
            return new IInterceptor[0];
        }
    }
}