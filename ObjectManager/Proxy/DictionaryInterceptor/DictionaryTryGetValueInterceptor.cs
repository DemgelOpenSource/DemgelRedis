using Castle.DynamicProxy;
using System;

namespace DemgelRedis.ObjectManager.Proxy.DictionaryInterceptor
{
    public class DictionaryTryGetValueInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public DictionaryTryGetValueInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            invocation.Proceed();
        }
    }
}
