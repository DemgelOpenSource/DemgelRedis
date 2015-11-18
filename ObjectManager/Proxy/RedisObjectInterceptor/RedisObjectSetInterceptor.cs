using System.Linq;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy.RedisObjectInterceptor
{
    public class RedisObjectSetInterceptor : IInterceptor
    {
        private readonly CommonData _commonData;

        public RedisObjectSetInterceptor(CommonData commonData)
        {
            _commonData = commonData;
        }

        public void Intercept(IInvocation invocation)
        {
            var objectKey = new RedisKeyObject(invocation.InvocationTarget.GetType(), _commonData.Id);

            if (invocation.Arguments[0] is IRedisObject)
            {
                var redisObject = (IRedisObject) invocation.Arguments[0];

                // Get or create the new key for the new RedisObject
                var key = new RedisKeyObject(redisObject.GetType(), string.Empty);
                _commonData.RedisDatabase.GenerateId(key, redisObject, _commonData.RedisObjectManager.RedisBackup);

                if (!(invocation.Arguments[0] is IProxyTargetAccessor))
                {
                    // Need to make it into a proxy, so it can be saved
                    var proxy = _commonData.RedisObjectManager.RetrieveObjectProxy(redisObject.GetType(), key.Id,
                        _commonData.RedisDatabase, redisObject);
                    invocation.SetArgumentValue(0, proxy);
                    redisObject = proxy as IRedisObject;
                }

                if (!_commonData.Processed)
                {
                    invocation.Proceed();
                    return;
                }
                //var objectKey = new RedisKeyObject(invocation.InvocationTarget.GetType(), _commonData.Id);
                // FIX THIS...
                var property =
                    invocation.Method.ReflectedType?.GetProperties()
                        .SingleOrDefault(x => x.SetMethod != null && x.SetMethod.Name == invocation.Method.Name);

                if (property != null)
                {
                    _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(
                        new HashEntry(property.Name, key.RedisKey), objectKey);

                    _commonData.RedisDatabase.HashSet(objectKey.RedisKey, property.Name, key.RedisKey);
                    _commonData.RedisObjectManager.SaveObject(redisObject, key.Id, _commonData.RedisDatabase);
                }
            }
            else
            {
                if (!_commonData.Processed)
                {
                    invocation.Proceed();
                    return;
                }
                // Set the individual item
                var property =
                        invocation.Method.ReflectedType?.GetProperties()
                            .SingleOrDefault(x => x.SetMethod.Name == invocation.Method.Name);

                ITypeConverter converter;
                if (property != null && _commonData.RedisObjectManager.TypeConverters.TryGetValue(property.PropertyType, out converter))
                {
                    var ret = new HashEntry(property.Name, converter.ToWrite(invocation.Arguments[0]));

                    _commonData.RedisObjectManager.RedisBackup?.RestoreHash(_commonData.RedisDatabase, objectKey);
                    _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(ret, objectKey);

                    _commonData.RedisDatabase.HashSet(objectKey.RedisKey, ret.Name, ret.Value);
                }
            }
            invocation.Proceed();
        }
    }
}