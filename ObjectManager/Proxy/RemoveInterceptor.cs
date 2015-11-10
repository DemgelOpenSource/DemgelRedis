using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Proxy
{
    public class RemoveInterceptor : IInterceptor
    {
        private readonly string _id;
        private readonly CommonData _commonData;

        //protected internal bool Processed { private get; set; }
        //protected internal object ParentProxy { private get; set; }

        public RemoveInterceptor(string id, CommonData data)
        {
            _id = id;
            _commonData = data;
        }

        public void Intercept(IInvocation invocation)
        {
            var cAttr =
                   _commonData.ParentProxy?.GetType().BaseType?
                       .GetProperties()
                       .SingleOrDefault(x => x.GetValue(_commonData.ParentProxy, null) == invocation.Proxy)
                   ??
                   invocation.Proxy;

            var cPropertyInfo = cAttr as PropertyInfo;
            if (cPropertyInfo != null)
            {
                if (invocation.Method.Name.StartsWith("Remove", StringComparison.Ordinal))
                {
                    if (cPropertyInfo.PropertyType.Name.StartsWith("IList", StringComparison.Ordinal))
                    {
                        DoRemoveListItem(invocation, cPropertyInfo);
                    }
                    else if (cPropertyInfo.PropertyType.Name.StartsWith("IDictionary", StringComparison.Ordinal))
                    {
                        DoRemoveDictionaryItem(invocation, cPropertyInfo);
                    }
                }
                else if (invocation.Method.Name.StartsWith("Clear", StringComparison.Ordinal))
                {
                    if (cPropertyInfo.PropertyType.Name.StartsWith("IList"))
                    {
                        //DoSetListItem(invocation, cPropertyInfo);
                    }
                    else if (cPropertyInfo.PropertyType.Name.StartsWith("IDictionary"))
                    {
                        //DoSetDictionaryItem(invocation, cPropertyInfo);
                    }
                }
            }

            invocation.Proceed();
        }

        private void DoRemoveListItem(IInvocation invocation, PropertyInfo propertyInfo)
        {
            var listKey = new RedisKeyObject(propertyInfo, _id);

            var original = invocation.Arguments[0];
            // 1 Figure out if this is removing a IRedisObject
            if (original is IRedisObject)
            {
                var deleteCascade = propertyInfo.GetCustomAttribute<RedisDeleteCascade>();

                var objectKey = new RedisKeyObject(original.GetType(), string.Empty);
                //_commonData.RedisObjectManager.GenerateId(_commonData.RedisDatabase, objectKey, original);
                _commonData.RedisDatabase.GenerateId(objectKey, original, _commonData.RedisObjectManager.RedisBackup);

                if (!(deleteCascade != null && !deleteCascade.Cascade))
                {    
                    _commonData.RedisObjectManager.DeleteObject(original, objectKey.Id, _commonData.RedisDatabase);
                }

                _commonData.RedisObjectManager.RedisBackup?.RemoveListItem(listKey, objectKey.RedisKey);
                _commonData.RedisDatabase.ListRemove(listKey.RedisKey, objectKey.RedisKey, 1);
            }
            else
            {
                _commonData.RedisObjectManager.RedisBackup?.RemoveListItem(listKey, (RedisValue)original);
                _commonData.RedisDatabase.ListRemove(listKey.RedisKey, (RedisValue) original, 1);
            }
        }

        private void DoRemoveAtListItem(IInvocation invocation, PropertyInfo propertyInfo)
        {
            
        }

        private void DoRemoveDictionaryItem(IInvocation invocation, PropertyInfo propertyInfo)
        {
            var hashKey = new RedisKeyObject(propertyInfo, _id);

            var accessor = (IProxyTargetAccessor)invocation.Proxy;
            var original = (accessor.DynProxyGetTarget() as IDictionary)?[invocation.Arguments[0]];
            if (original == null) return;
            // 1 Figure out if this is removing a IRedisObject
            if (original is IRedisObject)
            {
                // Look for cascade, if cascade is false don't do anything for redisobject
                var deleteCascade = propertyInfo.GetCustomAttribute<RedisDeleteCascade>();

                if (!(deleteCascade != null && !deleteCascade.Cascade))
                {
                    var objectKey = new RedisKeyObject(original.GetType(), string.Empty);
                    _commonData.RedisObjectManager.RedisBackup?.DeleteHash(objectKey);
                    //_commonData.RedisObjectManager.GenerateId(_commonData.RedisDatabase, objectKey, original);
                    _commonData.RedisDatabase.GenerateId(objectKey, original, _commonData.RedisObjectManager.RedisBackup);
                    _commonData.RedisObjectManager.DeleteObject(original, objectKey.Id, _commonData.RedisDatabase);
                } 
            }

            // Delete the keys
            _commonData.RedisObjectManager.RedisBackup?.DeleteHashValue((string) invocation.Arguments[0], hashKey);
            _commonData.RedisDatabase.HashDelete(hashKey.RedisKey, (string) invocation.Arguments[0]);
        }
    }
}