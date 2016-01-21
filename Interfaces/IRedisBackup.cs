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
        Task<HashEntry> GetHashEntryAsync(string valueKey, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));

        void UpdateString(IDatabase redisDatabase, RedisKeyObject key, string table = "string");
        Task UpdateStringAsync(IDatabase redisDatabase, RedisKeyObject key, string table = "string", CancellationToken token = default(CancellationToken));
        void DeleteString(RedisKeyObject key, string table = "string");
        Task DeleteStringAsync(RedisKeyObject key, string table = "string", CancellationToken token = default(CancellationToken));
        RedisValue GetString(RedisKeyObject key, string table = "string");
        Task<RedisValue> GetStringAsync(RedisKeyObject key, string table = "string", CancellationToken token = default(CancellationToken));
        string RestoreString(IDatabase redisDatabase, RedisKeyObject key, string table = "string");
        Task<string> RestoreStringAsync(IDatabase redisDatabase, RedisKeyObject key, string table = "string", CancellationToken token = default(CancellationToken));

        void RestoreCounter(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter");
        Task RestoreCounterAsync(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter", CancellationToken token = default(CancellationToken));
        void UpdateCounter(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter");
        Task UpdateCounterAsync(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter", CancellationToken token = default(CancellationToken));

        List<RedisValue> RestoreList(IDatabase redisDatabase, RedisKeyObject listKey);
        Task<List<RedisValue>> RestoreListAsync(IDatabase redisDatabase, RedisKeyObject listKey, CancellationToken token = default(CancellationToken));
        void DeleteList(RedisKeyObject key);
        Task DeleteListAsync(RedisKeyObject key, CancellationToken token = default(CancellationToken));
        void AddListItem(RedisKeyObject key, RedisValue value);
        Task AddListItemAsync(RedisKeyObject key, RedisValue value, CancellationToken token = default(CancellationToken));
        void RemoveListItem(RedisKeyObject key, RedisValue value);
        Task RemoveListItemAsync(RedisKeyObject key, RedisValue value, CancellationToken token = default(CancellationToken));
        void UpdateListItem(RedisKeyObject key, RedisValue oldValue, RedisValue newValue);
        Task UpdateListItemAsync(RedisKeyObject key, RedisValue oldValue, RedisValue newValue, CancellationToken token = default(CancellationToken));

        void RestoreSet(IDatabase redisDatabase, RedisKeyObject key);
        Task RestoreSetAsync(IDatabase redisDatabase, RedisKeyObject key, CancellationToken token = default(CancellationToken));
        void UpdateSetItem(RedisKeyObject key, RedisValue value, double score, RedisValue oldValue, double oldScore);
        void UpdateSetItem(RedisKeyObject key, SortedSetEntry entry, SortedSetEntry oldEntry);
        Task UpdateSetItemAsync(RedisKeyObject key, SortedSetEntry entry, SortedSetEntry oldEntry, CancellationToken token = default(CancellationToken));
        void AddSetItem(RedisKeyObject key, RedisValue value, double score);
        void AddSetItem(RedisKeyObject key, SortedSetEntry entry);
        Task AddSetItemAsync(RedisKeyObject key, SortedSetEntry entry, CancellationToken token = default(CancellationToken));
        void DeleteSet(RedisKeyObject setKey);
        Task DeleteSetAsync(RedisKeyObject setkey, CancellationToken token = default(CancellationToken));
    }
}