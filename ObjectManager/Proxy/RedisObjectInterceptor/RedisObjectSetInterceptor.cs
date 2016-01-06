using System.Linq;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using StackExchange.Redis;
using Castle.Core.Internal;
using DemgelRedis.ObjectManager.Attributes;

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

                // Check to see if there is an ID set in the database (if not it has never been saved)

                if (!_commonData.Processed)
                {
                    if (_commonData.RedisDatabase.KeyExists(key.RedisKey))
                    {
                        invocation.Proceed();
                        return;
                    }
                }

                var property =
                    invocation.Method.ReflectedType?.GetProperties()
                        .SingleOrDefault(x => x.SetMethod != null && x.SetMethod.Name == invocation.Method.Name);

                if (property != null)
                {
                    bool deleteCascade = true;
                    // Need to check if the item is currently set (ie are we replacing a value)
                    if (property.HasAttribute<RedisDeleteCascade>())
                    {
                        deleteCascade = property.GetAttribute<RedisDeleteCascade>().Cascade;
                    }

                    if (deleteCascade)
                    {
                        object currentValue = property.GetValue(invocation.Proxy);
                        if (currentValue is IRedisObject)
                        {
                            ((IRedisObject)currentValue).DeleteRedisObject();
                        }
                    }
                    // Need to check is there is a RedisDeleteCascade on property
                    _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(new HashEntry(property.Name, key.RedisKey), objectKey);

                    _commonData.RedisDatabase.HashSet(objectKey.RedisKey, property.Name, key.RedisKey);
                    _commonData.RedisObjectManager.SaveObject(redisObject, key.Id, _commonData.RedisDatabase);
                }
            }
            else
            {
                if (!_commonData.Processed)
                {
                    if (!_commonData.Processing)
                    {
                        //invocation.Proceed();
                        //return;

                        _commonData.Processing = true;
                        // Process the proxy (do a retrieveObject)
                        _commonData.RedisObjectManager.RetrieveObject(invocation.Proxy, _commonData.Id, _commonData.RedisDatabase, null);
                        _commonData.Processed = true;
                        _commonData.Processing = false;
                    }
                }
                // Set the individual item
                var property =
                        invocation.Method.ReflectedType?.GetProperties()
                            .SingleOrDefault(x => x.SetMethod.Name == invocation.Method.Name);

                ITypeConverter converter;
                if (property != null && _commonData.RedisObjectManager.TypeConverters.TryGetValue(property.PropertyType, out converter))
                {
                    var ret = new HashEntry(property.Name, converter.ToWrite(invocation.Arguments[0]));

                    //Need to check if the value already stored is different

                    _commonData.RedisObjectManager.RedisBackup?.RestoreHash(_commonData.RedisDatabase, objectKey);

                    if (_commonData.RedisDatabase.HashGet(objectKey.RedisKey, ret.Name) != ret.Value)
                    {
                        _commonData.RedisObjectManager.RedisBackup?.UpdateHashValue(ret, objectKey);
                        _commonData.RedisDatabase.HashSet(objectKey.RedisKey, ret.Name, ret.Value);
                    }
                }
            }
            invocation.Proceed();
        }
    }
}