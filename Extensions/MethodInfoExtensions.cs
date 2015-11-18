using System;
using System.Reflection;

namespace DemgelRedis.Extensions
{
    public static class MethodInfoExtensions
    {
        public static bool IsSetMethod(this MethodInfo methodInfo)
        {
            return methodInfo.IsSpecialName && methodInfo.Name.StartsWith("set_", StringComparison.Ordinal);
        }

        public static bool IsAddMethod(this MethodInfo methodInfo)
        {
            return methodInfo.Name.StartsWith("Add", StringComparison.Ordinal);
        }

        public static bool IsRemoveMethod(this MethodInfo methodInfo)
        {
            return methodInfo.Name.StartsWith("Remove", StringComparison.Ordinal);
        }
    }
}