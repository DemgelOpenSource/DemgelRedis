using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            //var connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS"));

            var test3 = _redis.RetrieveObjectProxy<TestConvertClassSubSuffix2>("12345", _connection.GetDatabase());
            //var t = test3.subTest;

            Debug.WriteLine("Something " + test3.subTest.Id + " - " + test3.subTest.test);

            //test3.subTest = new TestConvertClassSub() {Id = "someid5", test = "something68"};
            test3.subTest.TestInitite.test = "do this again - and again";
            //test3.subTest.TestInitite = new TestConvertClassSubSuffix {Id = "Testing", test = "Test"};
            //test3.subTest.TestInitite.subTest = test3.subTest;

            Assert.IsTrue(test3 != null);
        }

        [Test]
        public void AllNewTests()
        {
            var test = _redis.RetrieveObjectProxy<RedisUser>("3", _connection.GetDatabase());

            Debug.WriteLine(test.DisplayName);
        }

        [Test]
        [Ignore]
        public void TestRedisListTests()
        {
            var test4 = _redis.RetrieveObjectProxy<RedisUser>("3", _connection.GetDatabase());
            var watch = Stopwatch.StartNew();
            
            Debug.WriteLine($"There are {test4.Subscriptions.FullCount()} in this list.");
            foreach (var t in test4.Subscriptions.Limit(3, 50))
            {
                if (t.Founder == null)
                {
                    t.Founder = test4;
                    
                }
                Debug.WriteLine(t.Id + " --- " + t.Name + " --- " + t.Founder?.Id);

                foreach (var dict in t.Members.Limit(0, 10))
                {
                    Debug.WriteLine("    THERE WAS A USER: " + dict.Value.DisplayName);
                }
            }
            Debug.Write("New Time is: " + watch.ElapsedMilliseconds);
            //test4.Subscriptions[4].Name = "Some new Name";
            //var watch2 = Stopwatch.StartNew();
            //var test5 = _redis.RetrieveObjectProxy<RedisUser>("3", _connection.GetDatabase());
            //foreach (var t in test5.Subscriptions)
            //{
            //    if (t.Founder == null)
            //    {
            //        t.Founder = test4;
            //    }
            //    Debug.WriteLine(t.Name + " --- " + t.Founder?.Id);
            //}
            //Debug.Write("New Time is: " + watch2.ElapsedMilliseconds);

            //var i = test4.Subscriptions[2];
            //test4.Subscriptions.Remove(i);
            //test3.SomeIntegers.Add(new TestConvertClass2());
            ////var hello = test3.SomeIntegers[0];
            //var testClass = new TestConvertClass2 {TestValue = "Blah Blah Blah"};
            //test3.SomeIntegers.Add(testClass);
            //var newsub = new Subscription() {Name = "test Name"};
            //var newsub = _redis.RetrieveObjectProxy<Subscription>(_connection.GetDatabase());
            ////newsub.Id = "105";
            //newsub.Name = "hello";
            //newsub.Founder = test4;
            //test4.Subscriptions.Add(newsub);
            //newsub.Members.Add(test4.Id, test4);
            

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

