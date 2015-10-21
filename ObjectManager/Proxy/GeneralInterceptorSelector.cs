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
            if (IsRemove(method)) return interceptors.Where(i => i is RemoveInterceptor).ToArray();
            if (IsSetter(method) || IsAdd(method)) return interceptors.Where(i => i is AddSetInterceptor).ToArray();
            var generalInterceptor = interceptors.SingleOrDefault(i => i is GeneralInterceptor) as GeneralInterceptor;
            return generalInterceptor != null ? new IInterceptor[] {generalInterceptor} : null;
        }

        private bool IsSetter(MethodInfo methodInfo)
        {
            return methodInfo.IsSpecialName && methodInfo.Name.StartsWith("set_", StringComparison.Ordinal);
        }

        private bool IsAdd(MethodInfo methodInfo)
        {
            return methodInfo.Name.StartsWith("Add", StringComparison.Ordinal);
        }

        private bool IsRemove(MethodInfo methodInfo)
        {
            return methodInfo.Name.StartsWith("Remove", StringComparison.Ordinal);
        }
    }
}