using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;

namespace DemgelRedis.Common
{
    public class RedisSortedSet<T> : SortedSet<T>
        where T : IRedisObject
    {
        public RedisSortedSet() : base(new RedisSortedSetComparer<T>())
        {
        } 
    }

    public class RedisSortedSetComparer<T> : IComparer<T>
    {
        private readonly PropertyInfo _setOrderKey;

        public RedisSortedSetComparer()
        {
            var prop = typeof (T).GetProperties().SingleOrDefault(p => p.HasAttribute<RedisSetOrderKey>());

            if (prop == null)
            {
                throw new Exception("");
            }

            if (prop.PropertyType != typeof (int)
                && prop.PropertyType != typeof (double)
                && prop.PropertyType != typeof (long)
                && prop.PropertyType != typeof (DateTime))
            {
                throw new Exception("OrderKey is not of an acceptable type.");
            }

            _setOrderKey = prop;
        }

        public int Compare(T x, T y)
        {
            var x1 = _setOrderKey.GetValue(x, null);
            var y1 = _setOrderKey.GetValue(y, null);

            // Numeric is as is
            // Datetime needs to be converted to UnixTimestamp (milliseconds)

            if (_setOrderKey.PropertyType == typeof (DateTime))
            {
                int test = ((DateTime) x1).CompareTo((DateTime) y1);
                return test;// == 0 ? 1 : test;
            }

            // TODO fix this for performance
            if (_setOrderKey.PropertyType == typeof (int))
            {
                var x2 = (int) x1;
                var y2 = (int) y1;
                return x2.CompareTo(y2);
            }

            if (_setOrderKey.PropertyType == typeof (double))
            {
                var x2 = (double)x1;
                var y2 = (double)y1;
                return x2.CompareTo(y2);
            }

            if (_setOrderKey.PropertyType == typeof(long))
            {
                var x2 = (long)x1;
                var y2 = (long)y1;
                return x2.CompareTo(y2);
            }

            throw new Exception("Uncomparable values.");
        }
    }
}