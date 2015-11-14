using System;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.ObjectManager.Attributes;
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
        public string Id { get; set; }

        public void ProcessProxy(object parentProxy, PropertyInfo cAttr, object value)
        {
            var t = ((IProxyTargetAccessor)value)
                       .GetInterceptors()
                       .SingleOrDefault(x => x is GeneralInterceptor) as GeneralInterceptor;
            t.CommonData.Processing = true;
            string redisId;

            var id = value?.GetType().GetProperties()
                .SingleOrDefault(x => x.HasAttribute<RedisIdKey>());
            var redisvalue = id?.GetValue(value, null);

            if (id != null && id.PropertyType == typeof(string))
            {
                redisId = (string)redisvalue;
            }
            else if (id != null && id.PropertyType == typeof(Guid))
            {
                redisId = (redisvalue as Guid?)?.ToString();
            }
            else
            {
                redisId = Id;
            }

            // TODO clean this
            if (redisId == null)
            {
                redisId = Id;
            }

            if (value == null)
            {
                throw new Exception("Object is not valid.");
            }

            var generalInterceptorOfValue = ((IProxyTargetAccessor)value)
                .GetInterceptors()
                .SingleOrDefault(x => x is GeneralInterceptor) as GeneralInterceptor;

            generalInterceptorOfValue.CommonData.ParentProxy = parentProxy;

            generalInterceptorOfValue.CommonData.RedisObjectManager.RetrieveObject(value, redisId,
                generalInterceptorOfValue.CommonData.RedisDatabase, cAttr);

            generalInterceptorOfValue.CommonData.Processed = true;
            t.CommonData.Processing = false;
            //Processing = false;
        }
    }
}