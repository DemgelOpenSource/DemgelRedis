using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using NUnit.Framework.Constraints;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Handlers
{
    public class ListHandler : RedisHandler
    {
        /// <summary>
        /// Need to pass in the Proxy object of the list/enumerable
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool CanHandle(object obj)
        {
            var targetObject = GetTarget(obj);
            return targetObject is IList;
        }

        private static object GetTarget(object obj)
        {
            var accessor = obj as IProxyTargetAccessor;
            return accessor?.DynProxyGetTarget();    
        }

        public override object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var listKey = new RedisKeyObject(basePropertyInfo.GetCustomAttributes(), id);
            var targetType = GetTarget(obj).GetType();
            Type itemType = null;

            if (targetType.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType &&
                      interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                try
                {
                    itemType = targetType.GetGenericArguments()[0];
                }
                catch { }
            }

            if (itemType != null && itemType.GetInterfaces().Contains(typeof(IRedisObject)))
            {
                // TODO make proxies for each List Item
                Debug.WriteLine("This would be complex, as these are RedisObjects");
                return obj;
            }

            if (itemType != typeof (RedisValue))
            {
                // Try to process each entry as a proxy, or fail
                throw new InvalidCastException($"Use RedisValue instead of {itemType?.Name}.");
            }

            var method = objType.GetMethod("Add");

            var retList = redisDatabase.ListRange(listKey.RedisKey);
            foreach (var ret in retList)
            {
                method.Invoke(obj, new[] {(object)ret});
            }
            return obj;
        }

        public override bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            //var listKey = DemgelRedis.ParseRedisKey(basePropertyInfo.GetCustomAttributes(), id);
            var listKey = new RedisKeyObject(basePropertyInfo.GetCustomAttributes(), id);

            // Only handles lists if they are not currently set, lists need to be handled
            // on a per item basis otherwise
            //if (redisDatabase.KeyExists(listKey)) return true;

            //Type itemType = null;

            //if (objType.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType &&
            //          interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            //{
            //    itemType = objType.GetGenericArguments()[0];
            //}

            //if (itemType != typeof(RedisValue))
            //{
            //    // Try to process each entry as a proxy, or fail
            //    throw new InvalidCastException($"Use RedisValue instead of {itemType?.Name}.");
            //}

            var trans = redisDatabase.CreateTransaction();
            foreach (var o in ((IEnumerable<RedisValue>) obj).ToArray())
            {
                trans.ListRemoveAsync(listKey.RedisKey, o);
                trans.ListLeftPushAsync(listKey.RedisKey, o);
            }
            trans.Execute();
            //redisDatabase.ListRightPush(listKey, ((IEnumerable<RedisValue>)obj).ToArray());

            return true;
        }

        public ListHandler(RedisObjectManager demgelRedis) : base(demgelRedis)
        {
        }
    }
}