using System.Collections.Generic;
using DemgelRedis.Common;
using StackExchange.Redis;
using System.Threading.Tasks;
using System.Threading;

namespace DemgelRedis.Interfaces
{
    public interface IRedisBackup
    {
        void UpdateHash(IEnumerable<HashEntry> entries, RedisKeyObject hashKey);
        Task UpdateHashAsync(IEnumerable<HashEntry> entries, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        void DeleteHash(RedisKeyObject hashKey);
        Task DeleteHashAsync(RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        void UpdateHashValue(HashEntry entry, RedisKeyObject hashKey);
        Task UpdateHashValueAsync(HashEntry entry, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        void DeleteHashValue(HashEntry entry, RedisKeyObject hashKey);
        Task DeleteHashValueAsync(HashEntry entry, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        void DeleteHashValue(string valueKey, RedisKeyObject hashKey);
        Task DeleteHashValueAsync(string valueKey, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        HashEntry[] GetHash(RedisKeyObject hashKey);
        Task<HashEntry[]> GetHashAsync(RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        HashEntry[] RestoreHash(IDatabase redisDatabase, RedisKeyObject hashKey);
        Task<HashEntry[]> RestoreHashAsync(IDatabase redisDatabase, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        HashEntry GetHashEntry(string valueKey, RedisKeyObject hashKey);

        void UpdateString(IDatabase redisDatabase, RedisKeyObject key, string table = "string");
        void DeleteString(RedisKeyObject key, string table = "string");
        RedisValue GetString(RedisKeyObject key, string table = "string");
        string RestoreString(IDatabase redisDatabase, RedisKeyObject key, string table = "string");

        void RestoreCounter(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter");
        void UpdateCounter(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter");

        List<RedisValue> RestoreList(IDatabase redisDatabase, RedisKeyObject listKey);
        void DeleteList(RedisKeyObject key);
        void AddListItem(RedisKeyObject key, RedisValue value);
        void RemoveListItem(RedisKeyObject key, RedisValue value);
        void UpdateListItem(RedisKeyObject key, RedisValue oldValue, RedisValue newValue);

        void RestoreSet(IDatabase redisDatabase, RedisKeyObject key);
        void UpdateSetItem(RedisKeyObject key);
        void AddSetItem(RedisKeyObject key, SortedSetEntry[] entries);
        void AddSetItem(RedisKeyObject key, RedisValue value, double score);
        void AddSetItem(RedisKeyObject key, SortedSetEntry entry);
        void DeleteSet(RedisKeyObject setKey);

    }
}