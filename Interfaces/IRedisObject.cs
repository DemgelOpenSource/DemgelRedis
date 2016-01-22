using DemgelRedis.ObjectManager.Attributes;

namespace DemgelRedis.Interfaces
{
    public interface IRedisObject
    {
         
    }

    public class RedisObjectString : IRedisObject
    {
        [RedisIdKey]
        public virtual string Id { get; set; }
    }
}