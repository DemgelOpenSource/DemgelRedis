using System;
using System.Reflection;
using DemgelRedis.ObjectManager.Attributes;
using System.Text.RegularExpressions;

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

        public RedisKeyObject(string key)
        {
            Regex re = new Regex(@"(\w*):?(\w*):?(\w*)$");
            MatchCollection mc = re.Matches(key);

            string f = "", s = "", t = "";
            var emc = mc.GetEnumerator();
            emc.MoveNext();
            var match = emc.Current as Match;

            f = match.Groups[1].Value;
            s = match.Groups[2].Value;
            t = match.Groups[3].Value;
                        
            if (string.IsNullOrEmpty(s) && string.IsNullOrEmpty(t))
            {
                Id = f;
            }
            else if (string.IsNullOrEmpty(t))
            {
                Prefix = f;
                Id = s;
            }
            else
            {
                Prefix = f;
                Id = s;
                Suffix = t;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is RedisKeyObject)) return false;
            var robj = (RedisKeyObject)obj;

            if (robj.RedisKey == RedisKey) return true;
            return false;
        }

        public override int GetHashCode()
        {
            return RedisKey.GetHashCode();
        }

        public static bool operator ==(RedisKeyObject left, RedisKeyObject right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RedisKeyObject left, RedisKeyObject right)
        {
            return !left.Equals(right);
        }
    }
}