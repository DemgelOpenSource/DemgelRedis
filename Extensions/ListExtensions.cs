using System.Collections.Generic;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.ObjectManager;

namespace DemgelRedis.Extensions
{
    public static class ListExtensions
    {
        public static int FullCount<T>(this IList<T> list)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var accessor = list as IProxyTargetAccessor;
            if (accessor == null) return 0;

            // Get the Common data
            var commonData = accessor.GetCommonData();
            var key = new RedisKeyObject(accessor.GetTargetPropertyInfo(), commonData.Id);
            return (int)commonData.RedisDatabase.ListLength(key.RedisKey);
        } 

        public static IList<T> FullList<T>(this IList<T> list)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var accessor = list as IProxyTargetAccessor;
            if (accessor == null) return list;
            // Get the Common data
            var commonData = accessor.GetCommonData();
            commonData.Processing = true;
            commonData.RedisObjectManager.RetrieveObject<T>(list, commonData.Id, commonData.RedisDatabase, accessor.GetTargetPropertyInfo());
            commonData.Processing = false;
            return list;
        }

        public static LimitObject<T> Limit<T>(this IList<T> list)
        {
            return new LimitObject<T> {LimitedObject = list};
        }

        public static LimitObject<T> Start<T>(this LimitObject<T> limits, int startCount)
        {
            limits.StartLimit = startCount;
            return limits;
        }

        public static LimitObject<T> TakeLimit<T>(this LimitObject<T> limits, int takeCount)
        {
            limits.TakeLimit = takeCount;
            return limits;
        }

        public static IList<T> ExecuteLimit<T>(this LimitObject<T> limits)
        {
            var accessor = limits.LimitedObject as IProxyTargetAccessor;
            if (accessor == null) return limits.LimitedObject as IList<T>;
            // Get the Common data
            var commonData = accessor.GetCommonData();
            commonData.Processing = true;
            commonData.RedisObjectManager.RetrieveObject(limits.LimitedObject, commonData.Id, commonData.RedisDatabase, accessor.GetTargetPropertyInfo(), limits);
            commonData.Processing = false;
            return limits.LimitedObject as IList<T>;
        }
    }
}