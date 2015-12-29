using Castle.DynamicProxy;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class GeneralGetInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public GeneralGetInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            // Don't do anything if the proxy isn't created yet
            if (!_commonData.Created || _commonData.Processing)
            {
                invocation.Proceed();
                return;
            }

            // if the proxy is created, we need to process it if we get something from this proxy
            // only if the proxy has not yet been processed
            if (!_commonData.Processed)
            {
                _commonData.Processing = true;
                // Process the proxy (do a retrieveObject)
                
                _commonData.RedisObjectManager.RetrieveObject(invocation.Proxy, _commonData.Id, _commonData.RedisDatabase, null);
                _commonData.Processed = true;
                _commonData.Processing = false;
            }

            invocation.Proceed();
        }

        public string GetId()
        {
            return _commonData.Id;
        }

        public CommonData GetCommonData()
        {
            return _commonData;
        }
    }
}