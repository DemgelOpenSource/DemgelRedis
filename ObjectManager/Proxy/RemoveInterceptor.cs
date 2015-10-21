using System.Diagnostics;
using Castle.DynamicProxy;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class RemoveInterceptor : IInterceptor
    {
        private readonly string _id;
        private readonly IDatabase _database;
        private readonly RedisObjectManager _redisObjectManager;

        protected internal bool Processed { private get; set; }
        protected internal object ParentProxy { private get; set; }

        public RemoveInterceptor(string id, IDatabase database, RedisObjectManager redisObjectManager)
        {
            _id = id;
            _database = database;
            _redisObjectManager = redisObjectManager;
        }

        public void Intercept(IInvocation invocation)
        {
            Debug.WriteLine("Remove called");
            invocation.Proceed();
        }
    }
}