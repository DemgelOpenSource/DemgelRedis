using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DemgelRedis.Common;
using StackExchange.Redis;

namespace DemgelRedis.Interfaces
{
    public abstract class AbstractRedisBackup : IRedisBackup
    {
        public void AddListItem(RedisKeyObject key, RedisValue value)
        {
            Task.Run(async () => await AddListItemAsync(key, value)).Wait();
        }
        public abstract Task AddListItemAsync(RedisKeyObject key, RedisValue value, CancellationToken token = default(CancellationToken));
        public void AddSetItem(RedisKeyObject key, SortedSetEntry entry)
        {
            Task.Run(async () => await AddSetItemAsync(key, entry)).Wait();
        }
        public void AddSetItem(RedisKeyObject key, RedisValue value, double score)
        {
            Task.Run(async () => await AddSetItemAsync(key, new SortedSetEntry(value, score))).Wait();
        }
        public abstract Task AddSetItemAsync(RedisKeyObject key, SortedSetEntry entry, CancellationToken token = default(CancellationToken));
        public void DeleteHash(RedisKeyObject hashKey)
        {
            Task.Run(async () => await DeleteHashAsync(hashKey)).Wait();
        }
        public abstract Task DeleteHashAsync(RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        public void DeleteHashValue(string valueKey, RedisKeyObject hashKey)
        {
            Task.Run(async () => await DeleteHashValueAsync(valueKey, hashKey)).Wait();
        }
        public void DeleteHashValue(HashEntry entry, RedisKeyObject hashKey)
        {
            Task.Run(async () => await DeleteHashValueAsync(entry, hashKey)).Wait();
        }
        public abstract Task DeleteHashValueAsync(string valueKey, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        public abstract Task DeleteHashValueAsync(HashEntry entry, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        public void DeleteList(RedisKeyObject key)
        {
            Task.Run(async () => await DeleteListAsync(key)).Wait();
        }
        public abstract Task DeleteListAsync(RedisKeyObject key, CancellationToken token = default(CancellationToken));
        public void DeleteSet(RedisKeyObject setKey)
        {
            Task.Run(async () => await DeleteSetAsync(setKey)).Wait();
        }
        public abstract Task DeleteSetAsync(RedisKeyObject setkey, CancellationToken token = default(CancellationToken));
        public void DeleteString(RedisKeyObject key, string table = "string")
        {
            Task.Run(async () => await DeleteStringAsync(key, table)).Wait();
        }
        public abstract Task DeleteStringAsync(RedisKeyObject key, string table = "string", CancellationToken token = default(CancellationToken));
        public HashEntry[] GetHash(RedisKeyObject hashKey)
        {
            return Task.Run(async () => await GetHashAsync(hashKey)).Result;
        }
        public abstract Task<HashEntry[]> GetHashAsync(RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        public HashEntry GetHashEntry(string valueKey, RedisKeyObject hashKey)
        {
            return Task.Run(async () => await GetHashEntryAsync(valueKey, hashKey)).Result;
        }
        public abstract Task<HashEntry> GetHashEntryAsync(string valueKey, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        public RedisValue GetString(RedisKeyObject key, string table = "string")
        {
            return Task.Run(async () => await GetStringAsync(key, table)).Result;
        }
        public abstract Task<RedisValue> GetStringAsync(RedisKeyObject key, string table = "string", CancellationToken token = default(CancellationToken));
        public void RemoveListItem(RedisKeyObject key, RedisValue value)
        {
            Task.Run(async () => await RemoveListItemAsync(key, value)).Wait();
        }
        public abstract Task RemoveListItemAsync(RedisKeyObject key, RedisValue value, CancellationToken token = default(CancellationToken));
        public void RestoreCounter(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter")
        {
            Task.Run(async () => await RestoreCounterAsync(redisDatabase, key, table)).Wait();
        }
        public abstract Task RestoreCounterAsync(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter", CancellationToken token = default(CancellationToken));
        public HashEntry[] RestoreHash(IDatabase redisDatabase, RedisKeyObject hashKey)
        {
            return Task.Run(async () => await RestoreHashAsync(redisDatabase, hashKey)).Result;
        }
        public abstract Task<HashEntry[]> RestoreHashAsync(IDatabase redisDatabase, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        public List<RedisValue> RestoreList(IDatabase redisDatabase, RedisKeyObject listKey)
        {
            return Task.Run(async () => await RestoreListAsync(redisDatabase, listKey)).Result;
        }
        public abstract Task<List<RedisValue>> RestoreListAsync(IDatabase redisDatabase, RedisKeyObject listKey, CancellationToken token = default(CancellationToken));
        public void RestoreSet(IDatabase redisDatabase, RedisKeyObject key)
        {
            Task.Run(async () => await RestoreSetAsync(redisDatabase, key)).Wait();
        }
        public abstract Task RestoreSetAsync(IDatabase redisDatabase, RedisKeyObject key, CancellationToken token = default(CancellationToken));
        public string RestoreString(IDatabase redisDatabase, RedisKeyObject key, string table = "string")
        {
            return Task.Run(async () => await RestoreStringAsync(redisDatabase, key, table)).Result;
        }
        public abstract Task<string> RestoreStringAsync(IDatabase redisDatabase, RedisKeyObject key, string table = "string", CancellationToken token = default(CancellationToken));
        public void UpdateCounter(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter")
        {
            Task.Run(async () => await UpdateCounterAsync(redisDatabase, key, table)).Wait();
        }
        public abstract Task UpdateCounterAsync(IDatabase redisDatabase, RedisKeyObject key, string table = "demgelcounter", CancellationToken token = default(CancellationToken));
        public void UpdateHash(IEnumerable<HashEntry> entries, RedisKeyObject hashKey)
        {
            Task.Run(async () => await UpdateHashAsync(entries, hashKey)).Wait();
        }
        public abstract Task UpdateHashAsync(IEnumerable<HashEntry> entries, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        public void UpdateHashValue(HashEntry entry, RedisKeyObject hashKey)
        {
            Task.Run(async () => await UpdateHashValueAsync(entry, hashKey)).Wait();
        }
        public abstract Task UpdateHashValueAsync(HashEntry entry, RedisKeyObject hashKey, CancellationToken token = default(CancellationToken));
        public void UpdateListItem(RedisKeyObject key, RedisValue oldValue, RedisValue newValue)
        {
            Task.Run(async () => await UpdateListItemAsync(key, oldValue, newValue)).Wait();
        }
        public abstract Task UpdateListItemAsync(RedisKeyObject key, RedisValue oldValue, RedisValue newValue, CancellationToken token = default(CancellationToken));
        public void UpdateSetItem(RedisKeyObject key, SortedSetEntry entry, SortedSetEntry oldEntry)
        {
            Task.Run(async () => await UpdateSetItemAsync(key, entry, oldEntry)).Wait();
        }
        public void UpdateSetItem(RedisKeyObject key, RedisValue value, double score, RedisValue oldValue, double oldScore)
        {
            Task.Run(async () => await UpdateSetItemAsync(key, new SortedSetEntry(value, score), new SortedSetEntry(oldValue, oldScore))).Wait();
        }
        public abstract Task UpdateSetItemAsync(RedisKeyObject key, SortedSetEntry entry, SortedSetEntry oldEntry, CancellationToken token = default(CancellationToken));
        public void UpdateString(IDatabase redisDatabase, RedisKeyObject key, string table = "string")
        {
            Task.Run(async () => await UpdateStringAsync(redisDatabase, key, table)).Wait();
        }
        public abstract Task UpdateStringAsync(IDatabase redisDatabase, RedisKeyObject key, string table = "string", CancellationToken token = default(CancellationToken));
    }
}
