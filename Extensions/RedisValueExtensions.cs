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
    }
}