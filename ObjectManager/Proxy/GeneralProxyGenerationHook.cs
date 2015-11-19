using System;
using System.Diagnostics;
using System.Reflection;
using Castle.DynamicProxy;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class GeneralProxyGenerationHook : IProxyGenerationHook
    {
        public void MethodsInspected()
        {
        }

        public void NonProxyableMemberNotification(Type type, MemberInfo memberInfo)
        {
        }

        public bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
        {
            if (methodInfo.Name.StartsWith("Add", StringComparison.Ordinal))
                return true;

            if (methodInfo.Name.StartsWith("Remove", StringComparison.Ordinal))
                return true;

            //if (methodInfo.Name.StartsWith("get_Count", StringComparison.Ordinal))
            //    return true;

            return methodInfo.IsSpecialName &&
                   (methodInfo.Name.StartsWith("get_", StringComparison.Ordinal) ||
                    methodInfo.Name.StartsWith("set_", StringComparison.Ordinal));
        }

        public override bool Equals(object obj)
        {
            return GetType() == obj.GetType();
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }
    }
}