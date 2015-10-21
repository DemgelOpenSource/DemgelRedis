using System;
using System.Collections.Generic;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.Tests
{

    public class TestClass : IRedisObject
    {
        public Guid TestGuid { get; set; }
        public string TestString { get; set; }
        public int TestInt { get; set; }
        public float TestFloat { get; set; }
        public double TestDouble { get; set; }
    }

    //[RedisPrefix(Key = "testcase")]
    public class TestConvertClass : IRedisObject
    {
        [RedisIdKey]
        public Guid Id { get; set; }
        public virtual string TestValue { get; set; }
    }

    //[RedisPrefix(Key = "testcase")]
    //[RedisSuffix(Key = "infosuffix")]
    public class TestConvertClassSubSuffix : IRedisObject
    {
        [RedisIdKey]
        public string Id { get; set; }
        public virtual string test { get; set; }
        public virtual TestConvertClassSub subTest { get; set; }
        //[RedisSuffix(Key = "testlist")]
        public virtual IList<RedisValue> SomeStrings { get; set; } = new List<RedisValue>();
        //[RedisPrefix(Key = "guidtest")]
        public virtual IList<TestConvertClass> SomeIntegers { get; set; } = new List<TestConvertClass>(); 
    }

    [RedisPrefix(Key = "testcasesub")]
    public class TestConvertClassSub : IRedisObject
    {
        [RedisIdKey]
        public string Id { get; set; }
        public virtual string test { get; set; }
        public virtual TestConvertClassSubSuffix TestInitite { get; set; }
    }

    public class TestDictionaryClass : IRedisObject
    {
        [RedisIdKey]
        public string Id { get; set; }
        public virtual IDictionary<string, RedisValue> TestDictionary { get; set; } = new Dictionary<string, RedisValue>();
    }
}