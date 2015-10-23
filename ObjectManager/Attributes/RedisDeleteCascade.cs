using System;

namespace DemgelRedis.ObjectManager.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class RedisDeleteCascade : Attribute
    {
        public bool Cascade { get; set; } = true;
    }
}