using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Castle.Core.Internal;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using StackExchange.Redis;

namespace DemgelRedis.BackingManager
{
    /// <summary>
    /// Does an ongoing (real time) back up to Azure Tables
    /// 
    /// Format is Table:PartitionKey:RowKey (as in: user:1201212:info)
    /// Format could be PartitionKey:RowKey (as in: user:1201212) Table is provided as param. 
    /// </summary>
    public class TableRedisBackup : IRedisBackup
    {
        // Call from Autofac
        public delegate TableRedisBackup Factory(string storageName, string accessKey, bool useHttps = true);

        /// <summary>
        /// _tablesDictionary contains all tables that have been created/referenced
        /// </summary>
        private readonly Dictionary<string, CloudTable> _tablesDictionary = new Dictionary<string, CloudTable>();

        private readonly CloudStorageAccount _storageAccount;
        private CloudTableClient Client => _tableClient ?? (_tableClient = _storageAccount.CreateCloudTableClient());
        private CloudTableClient _tableClient;

        private readonly object _lock = new object();

        /// <summary>
        /// It is recommended to use the Factory Method
        /// and pass in your credientials that way
        /// </summary>
        /// <param name="storageName"></param>
        /// <param name="accessKey"></param>
        /// <param name="useHttps"></param>
        public TableRedisBackup(string storageName, string accessKey, bool useHttps)
        {
            var creds = new StorageCredentials(storageName, accessKey);
            _storageAccount = new CloudStorageAccount(creds, useHttps);
        }

        /// <summary>
        /// Initialize with a specific storage account.
        /// 
        /// Can be used with Autofac, but do not register both CloudStorageAccount and StorageCredentials
        /// with Autofac
        /// </summary>
        /// <param name="storageAccount"></param>
        public TableRedisBackup(CloudStorageAccount storageAccount)
        {
            _storageAccount = storageAccount;
        }

        /// <summary>
        /// Intialize with a specific storageCredentials
        /// 
        /// Can be used with Autofac (or IoC), but do not register both CloudStorageAccount and StorageCredientials
        /// unless you have full control of instanciation
        /// </summary>
        /// <param name="storageCredentials"></param>
        public TableRedisBackup(StorageCredentials storageCredentials)
        {
            _storageAccount = new CloudStorageAccount(storageCredentials, true);
        }

        /// <summary>
        /// Retrieve, Create, and register CloudTables in use, Caching for quick lookup.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private CloudTable GetCloudTable(string tableName)
        {
            CloudTable table;
            lock (_lock)
            {
                if (_tablesDictionary.TryGetValue(tableName, out table)) return table;
            }

            table = Client.GetTableReference(tableName);
            table.CreateIfNotExistsAsync().Wait();

            lock (_lock) {
                if (!_tablesDictionary.ContainsKey(tableName))
                {
                    _tablesDictionary.Add(tableName, table);
                }
                return table;
            }
        }

        /// <summary>
        /// Will process all hash entries (need to come from same hash)
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="hashKey"></param>
        public void UpdateHash(IEnumerable<HashEntry> entries, RedisKeyObject hashKey)
        {
            var operation = new TableBatchOperation();
            var cloudTable = GetCloudTable(hashKey.Prefix);

            foreach (var entry in entries)
            {
                var entity = new DynamicTableEntity
                {
                    PartitionKey = GetPartitionKey(hashKey),
                    RowKey = entry.Name
                };

                entity.Properties.Add("value",
                    entry.Value.IsByteArray()
                        ? new EntityProperty((byte[]) entry.Value)
                        : new EntityProperty((string) entry.Value));
                
                operation.InsertOrReplace(entity);
            }

            cloudTable.ExecuteBatchAsync(operation);
        }

        /// <summary>
        /// Not a very effecient way to delete a hash, better to use
        /// DeleteHashValues if you have the whole hash from the cache.
        /// </summary>
        /// <param name="hashKey"></param>
        public void DeleteHash(RedisKeyObject hashKey)
        {
            var cloudTable = GetCloudTable(hashKey.Prefix);

            var query = new TableQuery<DynamicTableEntity>
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, GetPartitionKey(hashKey))
            };

            var dynamicTableEntities = cloudTable.ExecuteQuerySegmented(query, null);

            do
            {
                var batch = new TableBatchOperation();
                foreach (var row in dynamicTableEntities)
                {
                    batch.Delete(row);
                }

                if (!batch.IsNullOrEmpty())
                    cloudTable.ExecuteBatchAsync(batch).Wait();

                dynamicTableEntities = cloudTable.ExecuteQuerySegmented(query, dynamicTableEntities.ContinuationToken);
            } while (dynamicTableEntities.ContinuationToken != null);
        }

        public void UpdateHashValue(HashEntry entry, RedisKeyObject hashKey)
        {
            //string table, partitionkey;
            //ParseTableEntities(hashKey, out table, out partitionkey);

            var cloudTable = GetCloudTable(hashKey.Prefix);

            var partKey = GetPartitionKey(hashKey);

            dynamic entity = new DynamicTableEntity();
            entity.PartitionKey = partKey;
            entity.RowKey = entry.Name;

            entity.Properties.Add("value", new EntityProperty((string)entry.Value));

            var operation = TableOperation.InsertOrReplace(entity);

            cloudTable.ExecuteAsync(operation).Wait();
        }

        public void DeleteHashValue(HashEntry entry, RedisKeyObject hashKey)
        {
            DeleteHashValue(entry.Name, hashKey);
        }

        public void DeleteHashValue(string valueKey, RedisKeyObject hashKey)
        {
            var cloudTable = GetCloudTable(hashKey.Prefix);

            var partKey = GetPartitionKey(hashKey);

            var operation = TableOperation.Delete(new DynamicTableEntity(partKey, valueKey) {ETag = "*"});

            try
            {
                cloudTable.ExecuteAsync(operation).Wait();
            }
            catch
            {
                Debug.WriteLine("Object to Delete not found.");
            }
        }

        public HashEntry[] GetHash(RedisKeyObject key)
        {
            var cloudTable = GetCloudTable(key.Prefix);

            var query = new TableQuery<DynamicTableEntity>
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, GetPartitionKey(key))
            };

            var result = new List<HashEntry>();

            var dynamicTableEntities = cloudTable.ExecuteQuerySegmentedAsync(query, null).Result;

            do
            {
                foreach (var row in dynamicTableEntities)
                {
                    EntityProperty value;
                    if (row.Properties.TryGetValue("value", out value))
                    {
                        result.Add(new HashEntry(row.RowKey, value.StringValue));
                    }
                }

                dynamicTableEntities = cloudTable.ExecuteQuerySegmentedAsync(query, dynamicTableEntities.ContinuationToken).Result;
            } while (dynamicTableEntities.ContinuationToken != null);
           

            return result.ToArray();
        }

        public HashEntry[] RestoreHash(IDatabase redisDatabase, RedisKeyObject hashKey)
        {
            var hashes = GetHash(hashKey);
            if (hashes.Length != 0)
            {
                redisDatabase.HashSet(hashKey.RedisKey, hashes);
            }
            return hashes;
        }

        public async Task<HashEntry> GetHashEntry(string valueKey, RedisKeyObject hashKey)
        {
            var cloudTable = GetCloudTable(hashKey.Prefix);
            var operation = TableOperation.Retrieve<DynamicTableEntity>(GetPartitionKey(hashKey), valueKey);
            var result = await cloudTable.ExecuteAsync(operation);
            var dynamicTableEntity = result.Result as DynamicTableEntity;
            if (dynamicTableEntity == null) return new HashEntry("null", "null");
            EntityProperty resultString;
            return dynamicTableEntity.Properties.TryGetValue(valueKey, out resultString) ? new HashEntry(valueKey, resultString.StringValue) : new HashEntry("null", "null");
        }

        /// <summary>
        /// Will update the table database to the current value of the redisDatabase given and key.
        /// </summary>
        /// <param name="redisDatabase"></param>
        /// <param name="key"></param>
        /// <param name="table"></param>
        public async void UpdateString(IDatabase redisDatabase, RedisKeyObject key, string table = "string")
        {
            var value = await redisDatabase.StringGetAsync(key.RedisKey);
            if (value.IsNullOrEmpty) return;

            var cloudTable = GetCloudTable(table);
            var entity = new DynamicTableEntity(key.Prefix, GetPartitionKey(key));
            entity.Properties.Add("value", new EntityProperty((string) value));
            var operation = TableOperation.InsertOrReplace(entity);

            await cloudTable.ExecuteAsync(operation);
        }

        public async void DeleteString(RedisKeyObject key, string table = "string")
        {
            var cloudTable = GetCloudTable(table);
            var operation = TableOperation.Delete(new DynamicTableEntity(key.Prefix, GetPartitionKey(key)));

            await cloudTable.ExecuteAsync(operation);
        }

        /// <summary>
        /// TODO this will be renamed to RestoreString
        /// Gets and restores the string to the redisDatabase given from table storage
        /// </summary>
        /// <param name="key"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public async Task<RedisValue> GetString(RedisKeyObject key, string table = "string")
        {
            var cloudTable = GetCloudTable(table);
            var operation = TableOperation.Retrieve<DynamicTableEntity>(key.Prefix, GetPartitionKey(key));
            var result = await cloudTable.ExecuteAsync(operation);
            var dynamicResult = result.Result as DynamicTableEntity;
            if (dynamicResult == null) return "";
            EntityProperty resultProperty;
            return dynamicResult.Properties.TryGetValue("value", out resultProperty) ? resultProperty.StringValue : "";
        }

        /// <summary>
        /// Gets and restores the string to the redisDatabase given from table storage
        /// </summary>
        /// <param name="redisDatabase"></param>
        /// <param name="key"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public async Task<string> RestoreString(IDatabase redisDatabase, RedisKeyObject key, string table = "string")
        {
            var cloudTable = GetCloudTable(table);
            var operation = TableOperation.Retrieve<DynamicTableEntity>(key.Prefix, GetPartitionKey(key));
            var result = await cloudTable.ExecuteAsync(operation);
            var dynamicResult = result.Result as DynamicTableEntity;

            EntityProperty resultProperty;
            string value = dynamicResult != null && dynamicResult.Properties.TryGetValue("value", out resultProperty) ? resultProperty.StringValue : null;

            if (string.IsNullOrEmpty(value)) return value;
            // Assume redis database is most upto date?
            if (redisDatabase.StringSet(key.RedisKey, value, null, When.NotExists)) return value;
            // value already exists, so update the new value
            UpdateString(redisDatabase, key, table);
            value = await redisDatabase.StringGetAsync(key.RedisKey);

            return value;
        }

        public void UpdateSet()
        {
            throw new NotImplementedException();
        }

        public void DeleteSet(string setKey)
        {
            throw new NotImplementedException();
        }

        private string GetPartitionKey(RedisKeyObject key)
        {
            return key.Suffix != null ? $"{key.Id}:{key.Suffix}" : key.Id;
        }
    }
}