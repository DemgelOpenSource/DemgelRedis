using Castle.DynamicProxy;

namespace DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor
{
    public class DictionaryRemoveInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public DictionaryRemoveInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            throw new System.NotImplementedException();
        }
    }
}