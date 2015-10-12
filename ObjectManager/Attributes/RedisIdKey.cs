using System;

namespace DemgelRedis.ObjectManager.Attributes
{
    /// <summary>
    /// The Id key for the redis cache
    /// example: key:id (this is the id)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class RedisIdKey : Attribute
    {
    }
}