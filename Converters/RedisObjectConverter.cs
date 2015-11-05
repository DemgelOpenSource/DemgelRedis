using System;
using System.Linq;
using System.Reflection;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.Converters
{
    public class RedisObjectConverter : ITypeConverter
    {
        public RedisValue ToWrite(object prop)
        {
            return new RedisValue();
        }

        public object OnRead(RedisValue obj, PropertyInfo info)
        {
            //var newObj = Activator.CreateInstance(info.PropertyType);
            var key = ParseKey(obj);

            return key;
            //foreach (var prop in newObj.GetType().GetProperties())
            //{
            //    if (prop.GetCustomAttributes().Any(x => x is RedisIdKey))
            //    {
            //        if (prop.PropertyType == typeof (string))
            //        {
            //            prop.SetValue(newObj, key);
            //        } else if (prop.PropertyType == typeof (Guid))
            //        {
            //            prop.SetValue(newObj, Guid.Parse(key));
            //        }
            //    }
            //}

            //return newObj;
        }

        private static string ParseKey(RedisValue ret)
        {
            var keyindex1 = ((string)ret).IndexOf(":", StringComparison.Ordinal);
            var stringPart1 = ((string)ret).Substring(keyindex1 + 1);
            var keyindex2 = stringPart1.IndexOf(":", StringComparison.Ordinal);
            var key = keyindex2 > 0 ? stringPart1.Substring(keyindex2) : stringPart1;
            return key;
        }
    }
}