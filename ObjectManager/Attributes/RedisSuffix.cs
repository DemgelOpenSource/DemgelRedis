using System;

namespace Demgel.Redis.ObjectManager.Attributes
{
    /// <summary>
    /// The suffix of you want to use one
    /// example prefix:id:suffix
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class RedisSuffix : Attribute
    {
         public string Key { get; set; }
    }
}