using System.Collections.Generic;
using System.Threading.Tasks;
using DemgelRedis.Common;
using StackExchange.Redis;

namespace DemgelRedis.Interfaces
{
    public interface IRedisBackup
    {
        void UpdateHash(IEnumerable<HashEntry> entries, RedisKeyObject hashKey);
        void DeleteHash(RedisKeyObject hashKey);
        void UpdateHashValue(HashEntry entry, RedisKeyObject hashKey);
        void DeleteHashValue(HashEntry entry, RedisKeyObject hashKey);
        void DeleteHashValue(string valueKey, RedisKeyObject hashKey);
        HashEntry[] GetHash(RedisKeyObject hashKey);
        HashEntry[] RestoreHash(IDatabase redisDatabase, RedisKeyObject hashKey);
        Task<HashEntry> GetHashEntry(string valueKey, RedisKeyObject hashKey);

        void UpdateString(IDatabase redisDatabase, RedisKeyObject key, string table = "string");
        void DeleteString(RedisKeyObject key, string table = "string");
        Task<RedisValue> GetString(RedisKeyObject key, string table = "string");
        Task<string> RestoreString(IDatabase redisDatabase, RedisKeyObject key, string table = "string");

        void UpdateSet();
        void DeleteSet(string setKey);

    }
}