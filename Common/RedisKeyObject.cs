using System;
using System.Reflection;
using DemgelRedis.ObjectManager.Attributes;

namespace DemgelRedis.Common
{
    public class RedisKeyObject
    {
        public string Prefix { get; set; }
        public string Id { get; set; }
        public string Suffix { get; set; }

        public string RedisKey
        {
            get
            {
                if (Prefix != null)
                {
                    return Suffix != null ? $"{Prefix}:{Id}:{Suffix}" : $"{Prefix}:{Id}";
                }
                return Suffix != null ? $"{Id}:{Suffix}" : Id;
            }
        }

        public string CounterKey
        {
            get
            {
                if (Prefix != null)
                {
                    return Suffix != null ? $"{Prefix}:{Suffix}" : $"{Prefix}";
                }
                return Suffix;
            }
        }

        public RedisKeyObject()
        {
        }

        public RedisKeyObject(PropertyInfo propertyInfo, string id)
        {
            var prefix = propertyInfo.DeclaringType?.GetCustomAttribute<RedisPrefix>();
            if (propertyInfo.DeclaringType?.BaseType == typeof(object))
            {
                Prefix = prefix != null ? prefix.Key : propertyInfo.DeclaringType.Name;
            }
            else
            {
                Prefix = prefix != null ? prefix.Key : propertyInfo.DeclaringType?.BaseType?.Name;
            }

            var suffix = propertyInfo.GetCustomAttribute<RedisSuffix>();
            Suffix = suffix != null ? suffix.Key : propertyInfo.Name;

            Id = id;
        }

        public RedisKeyObject(Type classType, string id)
        {
            var prefix = classType.GetCustomAttribute<RedisPrefix>();
            if (classType.BaseType != null && classType.BaseType == typeof(object))
            {
                Prefix = prefix != null ? prefix.Key : classType.Name;
            }
            else
            {
                Prefix = prefix != null ? prefix.Key : classType.BaseType?.Name;
            }
            Id = id;
        }

        public RedisKeyObject(Type classType) : this(classType, string.Empty)
        {
        }
    }
}