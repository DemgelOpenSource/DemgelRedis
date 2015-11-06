using System;
using StackExchange.Redis;

namespace DemgelRedis.Extensions
{
    public static class RedisValueExtensions
    {
        public static bool IsByteArray(this RedisValue value)
        {
            var byteArray = (byte[]) value;
            var stringArray = (string) value;
            return value.Equals(byteArray) && !value.Equals(stringArray);
        }

        public static string ParseKey(this RedisValue value)
        {
            var keyindex1 = ((string)value).IndexOf(":", StringComparison.Ordinal);
            var stringPart1 = ((string)value).Substring(keyindex1 + 1);
            var keyindex2 = stringPart1.IndexOf(":", StringComparison.Ordinal);
            var key = keyindex2 > 0 ? stringPart1.Substring(keyindex2) : stringPart1;
            return key;
        }
    }
}