using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demgel.Redis.Interfaces;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using StackExchange.Redis;

namespace Demgel.Redis
{
    /// <summary>
    /// Does an ongoing (real time) back up to Azure Tables
    /// 
    /// Format is Table:PartitionKey:RowKey (as in: user:1201212:info)
    /// Format could be PartitionKey:RowKey (as in: user:1201212) Table is provided as param
    /// 
    /// TODO: Ability to override PartitionKey and Rowkey values by offering a
    /// TODO: RemapKeys object.
    /// 
    /// TODO: RemapKeys object needs still needs String value but will be able to
    /// TODO: set Keys based on Lamda Functions to different keys. Will be used to read
    /// TODO: both from and to the backup database.
    /// 
    /// Autofac example registration:
    ///     
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
        /// This function is not async, but once the tables are
        /// found and referenced, this should be O(1)
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private async Task<CloudTable> GetCloudTable(string tableName)
        {
            CloudTable table;
            lock (_lock)
            {
                
                if (_tablesDictionary.TryGetValue(tableName, out table)) return table;
            }

            table = Client.GetTableReference(tableName);
            await table.CreateIfNotExistsAsync();

            lock (_lock) {
                if (!_tablesDictionary.ContainsKey(tableName))
                {
                    _tablesDictionary.Add(tableName, table);
                }
                return table;
            }
        }

        private static void ParseTableEntities(string key, out string keyOne, out string keyTwo)
        {
            var sepIndex = key.IndexOf(":", StringComparison.Ordinal);
            keyOne = key.Substring(0, sepIndex);
            keyTwo = key.Substring(sepIndex + 1);
        }

        /// <summary>
        /// Will process all hash entries (need to come from same hash)
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="hashKey"></param>
        public async void UpdateHash(HashEntry[] entries, string hashKey)
        {
            string table, partitionkey;
            ParseTableEntities(hashKey, out table, out partitionkey);

            var operation = new TableBatchOperation();
            var cloudTable = await GetCloudTable(table);

            foreach (var entry in entries)
            {
                dynamic entity = new DynamicTableEntity();
                entity.PartitionKey = partitionkey;
                entity.RowKey = entry.Name;
                
                entity.Properties.Add("value", new EntityProperty((string)entry.Value));

                operation.InsertOrReplace(entity);
            }
           
#pragma warning disable 4014
            cloudTable.ExecuteBatchAsync(operation);
#pragma warning restore 4014
        }

        /// <summary>
        /// Not a very effecient way to delete a hash, better to use
        /// DeleteHashValues if you have the whole hash from the cache.
        /// </summary>
        /// <param name="hashKey"></param>
        public async void DeleteHash(string hashKey)
        {
            string table, partitionKey;
            ParseTableEntities(hashKey, out table, out partitionKey);
            var cloudTable = await GetCloudTable(table);

            var query = new TableQuery<DynamicTableEntity>
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey)
            };

            var dynamicTableEntities = await cloudTable.ExecuteQuerySegmentedAsync(query, null);

            while (dynamicTableEntities.ContinuationToken != null)
            {
                TableBatchOperation batch = new TableBatchOperation();
                foreach (var row in dynamicTableEntities)
                {
                    batch.Delete(row);
                }

                await cloudTable.ExecuteBatchAsync(batch);

                dynamicTableEntities = await cloudTable.ExecuteQuerySegmentedAsync(query, dynamicTableEntities.ContinuationToken);
            }
        }

        public async void UpdateHashValue(HashEntry entry, string hashKey)
        {
            string table, partitionkey;
            ParseTableEntities(hashKey, out table, out partitionkey);

            var cloudTable = await GetCloudTable(table);

            dynamic entity = new DynamicTableEntity();
            entity.PartitionKey = partitionkey;
            entity.RowKey = entry.Name;

            entity.Properties.Add("value", new EntityProperty((string)entry.Value));

            var operation = TableOperation.InsertOrReplace(entity);

            cloudTable.ExecuteAsync(operation);
        }

        public void DeleteHashValue(HashEntry entry, string hashKey)
        {
            DeleteHashValue(entry.Name, hashKey);
        }

        public async void DeleteHashValue(string valueKey, string hashKey)
        {
            string varOne, varTwo;
            ParseTableEntities(hashKey, out varOne, out varTwo);
            var cloudTable = await GetCloudTable(varOne);
            var operation = TableOperation.Delete(new DynamicTableEntity(varTwo, valueKey));
#pragma warning disable 4014
            cloudTable.ExecuteAsync(operation);
#pragma warning restore 4014
        }

        public async Task<HashEntry[]> GetHash(string hashKey)
        {
            string table, partitionKey;
            ParseTableEntities(hashKey, out table, out partitionKey);
            var cloudTable = await GetCloudTable(table);

            var query = new TableQuery<DynamicTableEntity>
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey)
            };

            var result = new List<HashEntry>();

            var dynamicTableEntities = await cloudTable.ExecuteQuerySegmentedAsync(query, null);

            while (dynamicTableEntities.ContinuationToken != null)
            {
                foreach (var row in dynamicTableEntities)
                {
                    EntityProperty value;
                    if (row.Properties.TryGetValue("value", out value))
                    {
                        result.Add(new HashEntry(row.RowKey, value.StringValue));
                    }
                }

                dynamicTableEntities = await cloudTable.ExecuteQuerySegmentedAsync(query, dynamicTableEntities.ContinuationToken);
            }

            return result.ToArray();
        }

        public async Task<HashEntry> GetHashEntry(string valueKey, string hashKey)
        {
            string varOne, varTwo;
            ParseTableEntities(hashKey, out varOne, out varTwo);
            var cloudTable = await GetCloudTable(varOne);
            var operation = TableOperation.Retrieve<DynamicTableEntity>(varTwo, valueKey);
            var result = await cloudTable.ExecuteAsync(operation);
            var dynamicTableEntity = result.Result as DynamicTableEntity;
            if (dynamicTableEntity == null) return new HashEntry("null", "null");
            EntityProperty resultString;
            return dynamicTableEntity.Properties.TryGetValue(valueKey, out resultString) ? new HashEntry(valueKey, resultString.StringValue) : new HashEntry("null", "null");
        }

        public async void UpdateString(string value, string key, string table = "string")
        {
            string partitionKey, rowKey;
            ParseTableEntities(key, out partitionKey, out rowKey);
            var cloudTable = await GetCloudTable(table);
            var entity = new DynamicTableEntity(partitionKey, rowKey);
            entity.Properties.Add("value", new EntityProperty(value));
            var operation = TableOperation.InsertOrReplace(entity);
            
#pragma warning disable 4014
            cloudTable.ExecuteAsync(operation);
#pragma warning restore 4014
        }

        public async void DeleteString(string key, string table = "string")
        {
            string partitionKey, rowKey;
            ParseTableEntities(key, out partitionKey, out rowKey);
            var cloudTable = await GetCloudTable(table);
            var operation = TableOperation.Delete(new DynamicTableEntity(partitionKey, rowKey));
#pragma warning disable 4014
            cloudTable.ExecuteAsync(operation);
#pragma warning restore 4014
        }

        public async Task<RedisValue> GetString(string key, string table = "string")
        {
            string partitionKey, rowKey;
            ParseTableEntities(key, out partitionKey, out rowKey);
            var cloudTable = await GetCloudTable(table);
            var operation = TableOperation.Retrieve<DynamicTableEntity>(partitionKey, rowKey);
            var result = await cloudTable.ExecuteAsync(operation);
            var dynamicResult = result.Result as DynamicTableEntity;
            if (dynamicResult == null) return "";
            EntityProperty resultProperty;
            return dynamicResult.Properties.TryGetValue("value", out resultProperty) ? resultProperty.StringValue : "";
        }

        public void UpdateSet()
        {
            throw new NotImplementedException();
        }

        public void DeleteSet(string setKey)
        {
            throw new NotImplementedException();
        }
    }
}