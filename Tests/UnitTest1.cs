using System;
using System.Collections.Generic;
using System.Linq;
using DemgelRedis.BackingManager;
using DemgelRedis.Common;
using DemgelRedis.ObjectManager;
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
            var connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS"));

            var test3 = _redis.RetrieveObjectProxy<TestConvertClassSubSuffix>("12347", connection.GetDatabase());

            test3.SomeStrings.Add("test9");
            test3.SomeStrings.Add("test1");
            test3.SomeStrings.Add("test5");

            test3.SomeStrings[0] = "something else";

            test3.test = "Hello Redis... lets see if you saved";

            test3.SomeIntegers.Add(new TestConvertClass());
            //var hello = test3.SomeIntegers[0];
            var testClass = new TestConvertClass {TestValue = "Blah Blah Blah"};
            test3.SomeIntegers.Add(testClass);

            test3.test = "This should be changed to this new value...";
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
    }
}

