using Castle.DynamicProxy;

namespace DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor
{
    public class DictionaryAddInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public DictionaryAddInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            throw new System.NotImplementedException();
        }
    }
}