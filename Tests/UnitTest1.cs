using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DemgelRedis.BackingManager;
using DemgelRedis.Common;
using DemgelRedis.ObjectManager;
using Microsoft.Data.Edm.Library;
using Microsoft.WindowsAzure.Storage;
using NUnit.Framework;
using StackExchange.Redis;

namespace DemgelRedis.Tests
{
    [TestFixture]
    public class UnitTest1
    {
        private readonly RedisObjectManager _redis =
            new RedisObjectManager(new TableRedisBackup(CloudStorageAccount.DevelopmentStorageAccount));
        private readonly IConnectionMultiplexer _connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS"));

        [Test]
        public void TestConvertToRedisHash()
        {
            //var demgelRedis = new DemgelRedis();

            var test = new TestClass
            {
                TestGuid = Guid.NewGuid(),
                TestString = "Some String..."
            };

            var ret = _redis.ConvertToRedisHash(test).ToList();

            Assert.IsTrue(ret.Count == 5);
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
                new HashEntry("TestDouble", "32342938283982.234232")
            };

            var ret = _redis.ConvertToObject(new TestClass(), hashList.ToArray());

            Assert.IsInstanceOf<TestClass>(ret);
            Assert.IsInstanceOf<Guid>(((TestClass)ret).TestGuid);
            Assert.AreEqual(((TestClass) ret).TestString, "SomeTest");
        }

        [Test]
        [Ignore]
        public void TestRedisRetrieveObject()
        {
            var connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS"));

            var test3 = _redis.RetrieveObjectProxy<TestConvertClassSubSuffix>("12345", connection.GetDatabase());

            Assert.IsTrue(test3 != null);
        }

        [Test]
        [Ignore]
        public void TestRedisListTests()
        {
            // TODO fix this test to actually be a test
            //var connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS"));

            var test3 = _redis.RetrieveObjectProxy<TestConvertClassSubSuffix>("12347", _connection.GetDatabase());

            //test3.SomeStrings.Add("test9");
            //test3.SomeStrings.Add("test1");
            //test3.SomeStrings.Add("test5");

            //test3.SomeStrings[0] = "something else";

            //test3.test = "Hello Redis... lets see if you saved";

            foreach (var t in test3.SomeIntegers)
            {
                Debug.WriteLine(t.TestValue);
            }

            test3.SomeIntegers.Add(new TestConvertClass());
            //var hello = test3.SomeIntegers[0];
            var testClass = new TestConvertClass {TestValue = "Blah Blah Blah"};
            test3.SomeIntegers.Add(testClass);

            //test3.test = "This should be changed to this new value...";
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
            var testDictObject = _redis.RetrieveObjectProxy<TestDictionaryClass>("12345737", _connection.GetDatabase());

            //if (!testDictObject.TestDictionary.ContainsKey("hello"))
            //{
            //    //testDictObject.TestDictionary.Add("hello", "dick");
            //    testDictObject.TestDictionary.Add(new KeyValuePair<string, RedisValue>("hello", "dick"));
            //}
            //else
            //{
            //    testDictObject.TestDictionary["hello"] = "Not A Dick";
            //}

            //testDictObject.TestDictionary.Remove("hello");

            if (!testDictObject.TestConvertClasses.ContainsKey("hello"))
            {
                testDictObject.TestConvertClasses.Add("hello", new TestConvertClass() {TestValue = "test"});
            }
            else
            {
                testDictObject.TestConvertClasses["hello"] = new TestConvertClass() {TestValue = "test2"};
            }

            testDictObject.TestConvertClasses.Remove("hello");
        }
    }
}

