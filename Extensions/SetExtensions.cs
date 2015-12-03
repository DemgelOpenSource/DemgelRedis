using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.Extensions
{
    public static class SetExtensions
    {
        public static int FullCount<T>(this ISet<T> set)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var accessor = set as IProxyTargetAccessor;
            if (accessor == null) return 0;

            // Get the Common data
            var commonData = accessor.GetCommonData();
            var key = new RedisKeyObject(accessor.GetTargetPropertyInfo(), commonData.Id);
            return (int)commonData.RedisDatabase.SortedSetLength(key.RedisKey);
        }

        public static ISet<T> FullSet<T>(this ISet<T> set)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var accessor = set as IProxyTargetAccessor;
            if (accessor == null) return set;
            set.Clear();
            // Get the Common data
            var commonData = accessor.GetCommonData();
            commonData.Processing = true;
            commonData.RedisObjectManager.RetrieveObject(set, commonData.Id, commonData.RedisDatabase, accessor.GetTargetPropertyInfo());
            commonData.Processing = false;
            return set;
        }

        public static ISet<T> Limit<T>(this ISet<T> set, int start, int end, long take = -1, Order order = Order.Ascending)
        {
            var limits = new LimitObject<T>
            {
                LimitedObject = set,
                StartLimit = start,
                EndLimit = end,
                TakeLimit = take,
                Order = order
            };
            set.Clear();
            return limits.ExecuteLimitSet();
        }

        public static ISet<T> Limit<T>(this ISet<T> set, long start, long end, long take = -1, Order order = Order.Ascending)
        {
            var limits = new LimitObject<T>
            {
                LimitedObject = set,
                StartLimit = start,
                EndLimit = end,
                TakeLimit = take,
                Order = order
            };
            set.Clear();
            return limits.ExecuteLimitSet();
        }

        public static ISet<T> Limit<T>(this ISet<T> set, DateTime start, TimeSpan end, long take = -1, Order order = Order.Ascending)
        {
            var limits = new LimitObject<T>
            {
                LimitedObject = set,
                StartLimit = start.GetMillisecondsSinceEpoch(),
                EndLimit = (start + end).GetMillisecondsSinceEpoch(),
                TakeLimit = take,
                Order = order
            };
            set.Clear();
            return limits.ExecuteLimitSet();
        }

        public static ISet<T> Limit<T>(this ISet<T> set, DateTime start, DateTime end, long take = -1, Order order = Order.Ascending)
        {
            var limits = new LimitObject<T>
            {
                LimitedObject = set,
                StartLimit = start.GetMillisecondsSinceEpoch(),
                EndLimit = end.GetMillisecondsSinceEpoch(),
                TakeLimit = take,
                Order = order
            };
            set.Clear();
            return limits.ExecuteLimitSet();
        }

        internal static ISet<T> ExecuteLimitSet<T>(this LimitObject<T> limits)
        {
            var accessor = limits.LimitedObject as IProxyTargetAccessor;
            if (accessor == null) return limits.LimitedObject as ISet<T>;
            // Get the Common data
            var commonData = accessor.GetCommonData();
            commonData.Processing = true;
            commonData.RedisObjectManager.RetrieveObject(limits.LimitedObject, commonData.Id, commonData.RedisDatabase, accessor.GetTargetPropertyInfo(), limits);
            commonData.Processing = false;
            return limits.LimitedObject as ISet<T>;
        }

        internal static long GetMillisecondsSinceEpoch(this DateTime dateTime)
        {
            return (long)dateTime.ToUniversalTime().Subtract(
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                ).TotalMilliseconds;
        }

        internal static long GetSetScore(this IRedisObject setValue)
        {
            PropertyInfo prop;
            if (setValue is IProxyTargetAccessor)
            {
                prop =
                    ((IProxyTargetAccessor) setValue).DynProxyGetTarget()
                        .GetType()
                        .GetProperties()
                        .SingleOrDefault(p => p.HasAttribute<RedisSetOrderKey>());
            }
            else
            {
                prop = setValue.GetType().GetProperties().SingleOrDefault(p => p.HasAttribute<RedisSetOrderKey>());
            }

            if (prop == null)
            {
                throw new Exception("RedisOrderSetKey not found.");
            }

            var value = prop.GetValue(setValue, null);

            if (value is DateTime)
            {
                return ((DateTime)value).GetMillisecondsSinceEpoch();
            }

            return (long) value;
        }
    }
}