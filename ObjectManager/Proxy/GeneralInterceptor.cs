using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class GeneralInterceptor : IInterceptor
    {
        private readonly string _id;
        private readonly IDatabase _database;
        private readonly DemgelRedis _demgelRedis;

        private readonly Dictionary<string, bool> _retrieved;

        protected internal bool SubGet { get; private set; }

        public GeneralInterceptor(string id, IDatabase database, DemgelRedis demgelRedis)
        {
            _id = id;
            _database = database;
            _demgelRedis = demgelRedis;

            _retrieved = new Dictionary<string, bool>();
        }

        public void Intercept(IInvocation invocation)
        {
            Debug.WriteLine("GeneralInterceptor " + invocation.Method.Name);
            if (!invocation.Method.Name.StartsWith("get_", StringComparison.Ordinal) ||
                SubGet)
            {

                //if (invocation.Method.Name.StartsWith("set_", StringComparison.Ordinal))
                //{
                //    Debug.WriteLine("set called on: " + invocation.Method.Name);
                //    invocation.ReturnValue = invocation.GetArgumentValue(0);
                //}
                invocation.Proceed();
                return;
            }

            // See if we need to process this
            bool retrieved;
            if (_retrieved.TryGetValue(invocation.Method.Name, out retrieved))
            {
                if (retrieved)
                {
                    invocation.Proceed();
                    return;
                }
            }

            SubGet = true;

            try
            {
                var cAttr = invocation.Method.ReflectedType?.GetMembers().Where(x =>
                {
                    var y = x as PropertyInfo;
                    return y != null && y.GetMethod.Name.Equals(invocation.Method.Name);
                }).SingleOrDefault() as PropertyInfo;

                var value = cAttr?.GetValue(invocation.Proxy);
                if (!(value is IProxyTargetAccessor)) return;

                var result = _demgelRedis.RetrieveObject(value, _id,
                    _database, cAttr);

                _retrieved.Add(invocation.Method.Name, true);

                invocation.ReturnValue = result.Object;                
            }
            finally
            {
                SubGet = false;
                invocation.Proceed();
            }
        }

        public bool HasRetrievedObject(MethodInfo methodInfo)
        {
            bool ret;
            return _retrieved.TryGetValue(methodInfo.Name, out ret) && ret;
        }
    }
}