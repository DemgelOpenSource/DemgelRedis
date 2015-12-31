using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DemgelRedis.Common;
using DemgelRedis.ObjectManager;
using NUnit.Framework;
using StackExchange.Redis;
using DemgelRedis.Extensions;

namespace DemgelRedis.Tests
{
    [TestFixture]
    public class UnitTest1
    {
        private readonly RedisObjectManager _redis =
            new RedisObjectManager(/*new TableRedisBackup(CloudStorageAccount.DevelopmentStorageAccount)*/);
        private readonly IConnectionMultiplexer _connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS"));
        private IDatabase _database
        {
            get
            {
                return _connection.GetDatabase(2);
            }
        }

        [Test]
        public void TestConvertToRedisHash()
        {
            var test = new TestClass
            {
                TestGuid = Guid.NewGuid(),
                TestString = "Some String..."
            };

            var ret = _redis.ConvertToRedisHash(test).ToList();

            Assert.IsTrue(ret.Count >= 5);
        }

        [Test]
        public void TestRedisHashToObject()
        {
            var hashList = new List<HashEntry>
            {
                new HashEntry("TestGuid", Guid.NewGuid().ToByteArray()),
                new HashEntry("TestString", "SomeTest"),
                new HashEntry("TestInt", "123234"),
                new HashEntry("TestFloat", "76234233234.323"),
                new HashEntry("TestDouble", "32342938283982.234232"),
                new HashEntry("TestDateTime", new DateTime(1980, 6, 2).ToString(CultureInfo.InvariantCulture))
            };

            var ret = _redis.ConvertToObject(new TestClass(), hashList.ToArray());

            Assert.IsInstanceOf<TestClass>(ret);
            Assert.IsInstanceOf<Guid>(((TestClass)ret).TestGuid);
            Assert.AreEqual(((TestClass) ret).TestString, "SomeTest");
        }

        [Test]
        public void TestRedisRetrieveObject()
        {
            var test1 = _redis.RetrieveObjectProxy<TestConvertClassSubSuffix2>("12345", _database);
            test1.subTest.TestInitite.test = "test string";

            var test2 = _redis.RetrieveObjectProxy<TestConvertClassSubSuffix2>("12345", _database);
            Debug.WriteLine($"Id {test2.subTest.Id} - Test Value: {test2.subTest.TestInitite.test}");

            Assert.IsTrue(test2.Id == "12345");
            Assert.IsTrue(test2.subTest.TestInitite.test == "test string");

            Assert.IsTrue(test1 != null);
        }

        [Test]
        public void TestRedisKey()
        {
            var type = typeof (TestConvertClassSubSuffix);
            var key = new RedisKeyObject(type, "123");

            var propertyType = type.GetProperties().SingleOrDefault(x => x.Name == "SomeStrings");
            var key2 = new RedisKeyObject(propertyType, "123");

            Assert.IsTrue(key2.RedisKey.Equals("TestConvertClassSubSuffix:123:SomeStrings"));
            Assert.IsTrue(key.RedisKey.Equals("TestConvertClassSubSuffix:123"));
        }

        [Test]
        public void TestDictionarySupport()
        {
            var testDictObject = _redis.RetrieveObjectProxy<TestDictionaryClass>("testDictionary", _database);

            testDictObject.TestDictionaryWithInt.Add(15, "test");

            Assert.IsTrue(testDictObject.TestDictionaryWithInt.KeyExists(15));

            var newTestDictObject = _redis.RetrieveObjectProxy<TestDictionaryClass>("testDictionary", _database);
            var t = newTestDictObject.TestDictionaryWithInt.FullDictionary();

            Assert.IsTrue(newTestDictObject.TestDictionaryWithInt.ContainsKey(15));

            newTestDictObject.TestDictionaryWithInt.Remove(15);
            Assert.IsFalse(newTestDictObject.TestDictionaryWithInt.KeyExists(15));
        }

        [Test]
        public void TestSetSupport()
        {
            // Populate with Data
            var testSet = _redis.RetrieveObjectProxy<TestSetOpertions>("666", _database);
            testSet.TestSet.Add(new TestSet { Id = "667", SomeDate = DateTime.UtcNow, SomeString = "testString2" });
            testSet.TestSet.Add(new TestSet { Id = "668", SomeDate = DateTime.UtcNow + TimeSpan.FromMinutes(10), SomeString = "testString3" });
            testSet.TestSet.Add(new TestSet { Id = "669", SomeDate = DateTime.UtcNow + TimeSpan.FromMinutes(15), SomeString = "testString4" });
            testSet.TestSet.Add(new TestSet { Id = "670", SomeDate = DateTime.UtcNow + TimeSpan.FromMinutes(20), SomeString = "testString5" });
            testSet.TestSet.Add(new TestSet { Id = "671", SomeDate = DateTime.UtcNow + TimeSpan.FromMinutes(25), SomeString = "testString6" });
            testSet.TestSet.Add(new TestSet { Id = "672", SomeDate = DateTime.UtcNow + TimeSpan.FromMinutes(30), SomeString = "testString7" });

            var testSet2 = _redis.RetrieveObjectProxy<TestSetOpertions>("666", _database);
            testSet2.TestSet.Limit(DateTime.MinValue, DateTime.MaxValue, 4, 2);

            Assert.IsTrue(testSet2.TestSet.Count == 4);

            testSet2.TestSet.Remove(testSet2.TestSet.First());
            testSet2.TestSet.Remove(testSet2.TestSet.First());

            Assert.IsTrue(testSet2.TestSet.Count == 2);

            var testSet3 = _redis.RetrieveObjectProxy<TestSetOpertions>("666", _database);
            testSet3.TestSet.Limit(DateTime.MinValue, DateTime.MaxValue, 6, 0);

            Assert.IsTrue(testSet3.TestSet.Count == 4);
        }
    }
}

