using System;

namespace Demgel.Redis.ObjectManager.Attributes
{
    /// <summary>
    /// The base key for this class in the redis database
    /// example: key:id (this represents key)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class RedisPrefix : Attribute
    {
        public string Key { get; set; }
    }
}