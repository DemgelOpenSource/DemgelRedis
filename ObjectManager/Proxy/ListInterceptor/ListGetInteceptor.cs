using Castle.DynamicProxy;

namespace DemgelRedis.ObjectManager.Proxy.ListInterceptor
{
    public class ListGetInteceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public ListGetInteceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        /// <summary>
        /// Get by Index with Redis is very unreliable... Usage is not preferred
        /// </summary>
        /// <param name="invocation"></param>
        public void Intercept(IInvocation invocation)
        {
            // This interceptor needs to see if this is a Get at index (and handle it)
            // Index will be based on the current list index
            invocation.Proceed();
        }
    }
}