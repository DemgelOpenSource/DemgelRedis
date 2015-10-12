using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Handlers
{
    public class EnumerableHandler : RedisHandler
    {
        public override bool CanHandle(object obj)
        {
            return obj is IEnumerable && !(obj is IDictionary) && !(obj is string);
        }

        public override object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var listKey = _demgelRedis.ParseRedisKey(basePropertyInfo.GetCustomAttributes(), id);
            Type itemType = null;

            if (objType.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType &&
                      interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                try
                {
                    itemType = objType.GetGenericArguments()[0];
                }
                catch { }
            }

            var retList = redisDatabase.ListRange(listKey);

            //if (itemType != typeof(RedisValue))
            //{
            //    // Try to process each entry as a proxy, or fail
            //    throw new InvalidCastException($"Use RedisValue instead of {itemType?.Name}.");
            //}

            var method = objType.GetMethod("Add");

            foreach (var ret in retList)
            {
                method.Invoke(obj, new[] {(object)ret});
            }
            return obj;
        }

        public override bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var listKey = _demgelRedis.ParseRedisKey(basePropertyInfo.GetCustomAttributes(), id);

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
                trans.ListRemoveAsync(listKey, o);
                trans.ListLeftPushAsync(listKey, o);
            }
            var test = trans.Execute();
            //redisDatabase.ListRightPush(listKey, ((IEnumerable<RedisValue>)obj).ToArray());

            return true;
        }

        public EnumerableHandler(global::DemgelRedis.ObjectManager.DemgelRedis demgelRedis) : base(demgelRedis)
        {
        }
    }
}