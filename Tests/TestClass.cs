using System;
using System.Collections.Generic;
using System.Diagnostics;
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace DemgelRedis.Tests
{

    public class TestClass : IRedisObject
    {
        public virtual Guid TestGuid { get; set; }
        public virtual string TestString { get; set; }
        public virtual int TestInt { get; set; }
        public virtual float TestFloat { get; set; }
        public virtual double TestDouble { get; set; }
        public virtual DateTime TestDateTime { get; set; }
    }

    //[RedisPrefix(Key = "testcase")]
    public class TestConvertClass : IRedisObject
    {
        [RedisIdKey]
        public Guid Id { get; set; }
        public virtual string TestValue { get; set; }
    }

    public class TestConvertClass2 : IRedisObject
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
        public virtual string TestValue { get; set; }
    }

    //[RedisPrefix(Key = "testcase")]
    //[RedisSuffix(Key = "infosuffix")]
    public class TestConvertClassSubSuffix : IRedisObject
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
        public virtual string test { get; set; }
        public virtual TestConvertClassSub subTest { get; set; }
        //[RedisSuffix(Key = "testlist")]
        public virtual IList<RedisValue> SomeStrings { get; set; } = new List<RedisValue>();
        //[RedisPrefix(Key = "guidtest")]
        public virtual IList<TestConvertClass2> SomeIntegers { get; set; } = new List<TestConvertClass2>(); 
    }

    [RedisPrefix(Key = "TestConvertClassSubSuffix")]
    public class TestConvertClassSubSuffix2 : IRedisObject
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
        //public virtual string test { get; set; }
        public virtual TestConvertClassSub subTest { get; set; }
        //[RedisSuffix(Key = "testlist")]
        //public virtual IList<RedisValue> SomeStrings { get; set; } = new List<RedisValue>();
        //[RedisPrefix(Key = "guidtest")]
        //public virtual IList<TestConvertClass> SomeIntegers { get; set; } = new List<TestConvertClass>();
    }

    [RedisPrefix(Key = "testcasesub")]
    public class TestConvertClassSub : IRedisObject
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
        public virtual string test { get; set; }
        public virtual TestConvertClassSubSuffix TestInitite { get; set; }
    }

    public class TestDictionaryClass : IRedisObject
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
        public virtual IDictionary<string, RedisValue> TestDictionary { get; set; } = new Dictionary<string, RedisValue>();
        [RedisDeleteCascade(Cascade = true)]
        public virtual IDictionary<string, TestConvertClass2> TestConvertClasses { get; set; } = new Dictionary<string, TestConvertClass2>();
        public virtual IDictionary<RedisValue, ITestInterface> TestingInterface { get; set; } = new Dictionary<RedisValue, ITestInterface>();
    }

    public interface ITestInterface : IRedisObject
    {
        string Id { get; set; }
        string test { get; set; }
    }
    public class TestInterface : ITestInterface
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
        public virtual string test { get; set; }
        //public virtual TestConvertClassSubSuffix TestInitite { get; set; }
    }

    public class RedisUser : IRedisObject
    {
        [JsonProperty("id")]
        [RedisIdKey]
        public virtual string Id { get; set; }
        [JsonProperty("displayname")]
        public virtual string DisplayName { get; set; }
        //[RedisDeleteCascade(Cascade = false)]
        public virtual IList<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public virtual IList<RedisValue> SomeStrings { get; set; } = new List<RedisValue>(); 
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Subscription : IRedisObject
    {
        [JsonProperty("id")]
        [RedisIdKey]
        public virtual string Id { get; set; }
        [JsonProperty("name")]
        public virtual string Name { get; set; }
        [JsonProperty("slug")]
        public virtual string Slug { get; set; }
        [JsonProperty("founder")]
        [RedisDeleteCascade(Cascade = false)]
        public virtual RedisUser Founder { get; set; }
        public virtual IDictionary<string, RedisUser> Members { get; set; } = new Dictionary<string, RedisUser>();
    }

    public class TestSetOpertions : IRedisObject
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
        public virtual ISet<TestSet> TestSet { get; set; } = new RedisSortedSet<TestSet>(); 
    }

    public class TestSet : IRedisObject
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
        public virtual string SomeString { get; set; }
        [RedisSetOrderKey]
        public virtual DateTime SomeDate { get; set; }
    }
}