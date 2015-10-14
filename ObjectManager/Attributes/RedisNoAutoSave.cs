using System;

namespace DemgelRedis.ObjectManager.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class RedisNoAutoSave : Attribute
    {
         
    }
}