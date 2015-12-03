using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DemgelRedis.BackingManager;
using DemgelRedis.Common;
using DemgelRedis.ObjectManager;
using NUnit.Framework;
using StackExchange.Redis;
using DemgelRedis.Extensions;
using Microsoft.WindowsAzure.Storage;

namespace DemgelRedis.Tests
{
    [TestFixture]
    public class UnitTest1
    {
        private readonly RedisObjectManager _redis =
            new RedisObjectManager(/*new TableRedisBackup(CloudStorageAccount.DevelopmentStorageAccount)*/);
        private readonly IConnectionMultiplexer _connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS"));

        [Test]
        [Ignore("don't run")]
        public void TestConvertToRedisHash()
        {
            //var demgelRedis = new DemgelRedis();

            var test = new TestClass
            {
                TestGuid = Guid.NewGuid(),
                TestString = "Some String..."
            };

            var ret = _redis.ConvertToRedisHash(test).ToList();

            Assert.IsTrue(ret.Count == 7);
        }

        [Test]
        [Ignore("don't run")]
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
        [Ignore("Can't reliably test on remote CI server")]
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
        [Ignore("don't run")]
        public void AllNewTests()
        {
            var test = _redis.RetrieveObjectProxy<RedisUser>("3", _connection.GetDatabase());
            //var t = test.DisplayName;
            test.DisplayName = "New Name2";

            Debug.WriteLine(test.DisplayName);
        }

        [Test]
        [Ignore("Can't reliably test on CI remote server.")]
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
        [Ignore("dont run")]
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
        [Ignore("don't run")]
        public void TestDictionarySupport()
        {
            var testDictObject = _redis.RetrieveObjectProxy<TestDictionaryClass>("12345737", _connection.GetDatabase());

            //testDictObject.TestDictionary.Remove("hello");

            if (!testDictObject.TestingInterface.KeyExists("hello"))
            {
                testDictObject.TestingInterface.Add("hello", new TestInterface { test = "test" });
            }
            else
            {
                testDictObject.TestingInterface["hello"] = new TestInterface { test = "test2" };
            }
            var t = testDictObject.TestingInterface.FullDictionary();
            Debug.WriteLine(t["hello"].test);

            //testDictObject.TestConvertClasses.Remove("hello");
        }

        [Test]
        public void TestSetSupport()
        {
            var testSet = _redis.RetrieveObjectProxy<TestSetOpertions>("666", _connection.GetDatabase());

            var t = testSet.TestSet.FullSet();

            //var ttt = new TestSet {Id = "667", SomeDate = DateTime.Now + TimeSpan.FromDays(2), SomeString = "testString2"};
            //t.Add(new TestSet { Id = "667", SomeDate = DateTime.UtcNow, SomeString = "testString2" });
            //t.Add(new TestSet { Id = "668", SomeDate = DateTime.UtcNow, SomeString = "testString2" });
            //t.Add(new TestSet { Id = "669", SomeDate = DateTime.UtcNow, SomeString = "testString2" });
            //t.Add(new TestSet { Id = "670", SomeDate = DateTime.UtcNow, SomeString = "testString2" });
            //t.Add(new TestSet { Id = "671", SomeDate = DateTime.UtcNow, SomeString = "testString2" });
            //t.Add(new TestSet { Id = "672", SomeDate = DateTime.UtcNow, SomeString = "testString2" });
            //t.First().SomeDate = DateTime.Now;

            //t.Remove(ttt);
        }
    }
}

