using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.ObjectManager.Proxy;

namespace DemgelRedis.Extensions
{
    public static class ProxyTargetAccessorExtensions
    {
        public static CommonData GetCommonData(this IProxyTargetAccessor accessor)
        {
            var interceptor = accessor.GetInterceptors().SingleOrDefault(x => x is GeneralGetInterceptor) as GeneralGetInterceptor;
            return interceptor?.GetCommonData();
        }

        public static PropertyInfo GetTargetPropertyInfo(this IProxyTargetAccessor accessor)
        {
                return accessor.GetCommonData().ParentProxy?.GetType().BaseType?
                    .GetProperties()
                    .SingleOrDefault(x => Equals(x.GetValue(accessor.GetCommonData().ParentProxy, null), accessor));
        }
    }
}