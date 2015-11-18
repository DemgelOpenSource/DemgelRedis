using Castle.DynamicProxy;

namespace DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor
{
    public class DictionarySetInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public DictionarySetInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            throw new System.NotImplementedException();
        }
    }
}