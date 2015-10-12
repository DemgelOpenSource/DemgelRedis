using System.Diagnostics;
using Castle.DynamicProxy;
using StackExchange.Redis;

namespace Demgel.Redis.ObjectManager.Proxy
{
    public class ChangeTrackerInterceptor : IInterceptor
    {
        public ChangeTrackerInterceptor(IDatabase redisDatabase)
        {
            
        }

        public void Intercept(IInvocation invocation)
        {
            Debug.WriteLine("ChangeTrackerInterceptor " + invocation.Method.Name);
            invocation.Proceed();
        }
    }
}