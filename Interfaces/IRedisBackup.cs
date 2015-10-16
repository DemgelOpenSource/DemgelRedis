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
        HashEntry GetHashEntry(string valueKey, RedisKeyObject hashKey);

        void UpdateString(IDatabase redisDatabase, RedisKeyObject key, string table = "string");
        void DeleteString(RedisKeyObject key, string table = "string");
        RedisValue GetString(RedisKeyObject key, string table = "string");
        string RestoreString(IDatabase redisDatabase, RedisKeyObject key, string table = "string");

        List<RedisValue> RestoreList(IDatabase redisDatabase, RedisKeyObject listKey, RedisKeyObject key);
        void DeleteList(IDatabase redisDatabase, RedisKeyObject key);
        void AddListItem(IDatabase redisDatabase, RedisKeyObject key, RedisValue value);
        void RemoveListItem(IDatabase redisDatabase, RedisKeyObject key, RedisValue value);
        void UpdateListItem(IDatabase redisDatabase, RedisKeyObject key, RedisValue oldValue, RedisValue newValue);

        void UpdateSet();
        void DeleteSet(string setKey);

    }
}