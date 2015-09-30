using StackExchange.Redis;

namespace Demgel.Redis.Interfaces
{
    public interface IRedisBackup
    {
        void UpdateHash(HashEntry[] entries, string hashKey);
        void DeleteHash(string hashKey);
        void UpdateHashValue(HashEntry entry, string hashKey);
        void DeleteHashValue(HashEntry entry, string hashKey);
        void DeleteHashValue(string valueKey, string hashKey);

        void UpdateString(string value, string key, string table = "string");
        void DeleteString(string key, string table = "string");

        void UpdateSet();
        void DeleteSet(string setKey);

    }
}