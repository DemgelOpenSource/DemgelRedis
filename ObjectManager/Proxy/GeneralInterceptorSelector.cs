using System;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class GeneralInterceptorSelector : IInterceptorSelector
    {
        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
        {
            if (IsSetter(method) || IsAdd(method)) return interceptors.Where(i => i is ChangeTrackerInterceptor).ToArray();
            var generalInterceptor = interceptors.SingleOrDefault(i => i is GeneralInterceptor) as GeneralInterceptor;
            if (generalInterceptor != null)
            {
                return new IInterceptor[] {generalInterceptor};
            }
            return null;
        }

        private bool IsSetter(MethodInfo methodInfo)
        {
            return methodInfo.IsSpecialName && methodInfo.Name.StartsWith("set_", StringComparison.Ordinal);
        }

        private bool IsAdd(MethodInfo methodInfo)
        {
            return methodInfo.Name.StartsWith("Add", StringComparison.Ordinal);
        }
    }
}