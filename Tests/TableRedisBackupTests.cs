using System;
using System.Collections.Generic;
using DemgelRedis.BackingManager;
using DemgelRedis.Common;
using Microsoft.WindowsAzure.Storage;
using NUnit.Framework;
using StackExchange.Redis;

namespace DemgelRedis.Tests
{
    [TestFixture]
    public class TableRedisBackupTests
    {
        private readonly TableRedisBackup _tableRedisBackup =  new TableRedisBackup(CloudStorageAccount.DevelopmentStorageAccount);
        [Test]
        [Ignore("Can't reliably test on remote server")]
        public void UpdateHashTest()
        {
            var hashes = new List<HashEntry>
            {
                new HashEntry("testbyte", Guid.NewGuid().ToByteArray()),
                new HashEntry("teststring", "some string")
            };

            var firstKey = new RedisKeyObject()
            {
                Prefix = "testhash7",
                Id = "13"
            };

            var secondKey = new RedisKeyObject()
            {
                Prefix = "testhash7",
                Id = "22",
                Suffix = "testsuffix"
            };

            _tableRedisBackup.UpdateHash(hashes, firstKey);

            _tableRedisBackup.DeleteHash(secondKey);

            hashes[1] = new HashEntry("teststring", "testing update");
            _tableRedisBackup.UpdateHashValue(hashes[1], firstKey);
            _tableRedisBackup.DeleteHashValue(hashes[0], secondKey);
            _tableRedisBackup.DeleteHashValue(hashes[0], firstKey);
        }
    }
}