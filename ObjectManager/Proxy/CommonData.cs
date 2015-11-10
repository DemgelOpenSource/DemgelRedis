using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class CommonData
    {
        public object ParentProxy { get; set; }
        public bool Processed { get; set; }
        public bool Processing { get; set; }
        public IDatabase RedisDatabase { get; set; }
        public RedisObjectManager RedisObjectManager { get; set; }
    }
}