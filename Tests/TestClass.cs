using System;
using System.Collections.Generic;
using Demgel.Redis.Interfaces;
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

    [RedisPrefix(Key = "testcase")]
    public class TestConvertClass : IRedisObject
    {
        [RedisIdKey]
        public string Id { get; set; }
    }

    [RedisPrefix(Key = "testcase")]
    [RedisSuffix(Key = "infosuffix")]
    public class TestConvertClassSubSuffix : IRedisObject
    {
        public TestConvertClassSubSuffix()
        {
        }

        [RedisIdKey]
        public string Id { get; set; }
        public string test { get; set; }
        public virtual TestConvertClassSub subTest { get; set; }
        [RedisPrefix(Key = "testlist")]
        public virtual IList<RedisValue> SomeStrings { get; set; } = new List<RedisValue>();
    }

    [RedisPrefix(Key = "testcasesub")]
    public class TestConvertClassSub : IRedisObject
    {
        [RedisIdKey]
        public string Id { get; set; }
        public string test { get; set; }

    }
}