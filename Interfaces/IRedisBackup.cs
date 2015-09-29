using StackExchange.Redis;

namespace Demgel.Redis.Interfaces
{
    public interface IRedisBackup
    {
        void UpdateHash(HashEntry[] entries, string hashKey);

        void UpdateHashValue(HashEntry entry, string hashKey);
        void DeleteHashValue(HashEntry entry, string hashKey);

        void UpdateString(RedisKey key, RedisValue value);

        void UpdateSet();

        void DeleteKey();
    }
}