using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
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

        public override object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var listKey = new RedisKeyObject(basePropertyInfo, id);
            var targetType = GetTarget(obj).GetType();
            Type itemType = null;

            if (targetType.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType &&
                      interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                if (targetType.GetGenericArguments().Any())
                {
                    itemType = targetType.GetGenericArguments()[0];
                }
            }

            var method = objType.GetMethod("Add");

            if (itemType != null && itemType.GetInterfaces().Contains(typeof(IRedisObject)))
            {
                RedisObjectManager.RedisBackup?.RestoreList(redisDatabase, listKey);
                var retlist = redisDatabase.ListRange(listKey.RedisKey);
                foreach (var ret in retlist)
                {
                    var hashKey = new RedisKeyObject(itemType, ret.ParseKey());
                    RedisObjectManager.RedisBackup?.RestoreHash(redisDatabase, hashKey);
                    // Detect if the base object exists in Redis
                    if (!redisDatabase.KeyExists((string) ret))
                    {
                        RedisObjectManager.RedisBackup?.RemoveListItem(listKey, ret);
                        redisDatabase.ListRemove(listKey.RedisKey, ret, 1);
                        continue;
                    }

                    var newObj = Activator.CreateInstance(itemType);
                    var newProxy = RedisObjectManager.RetrieveObjectProxy(itemType, hashKey.Id, redisDatabase, newObj);
                    var redisKeyProp = itemType.GetProperties().SingleOrDefault(x => x.GetCustomAttributes().Any(y => y is RedisIdKey));

                    if (redisKeyProp != null)
                    {
                        // Parse the key...
                        var key = ret.ParseKey();

                        if (redisKeyProp.PropertyType == typeof(string))
                        {
                            redisKeyProp.SetValue(newProxy, key);
                        }
                        else
                        {
                            redisKeyProp.SetValue(newProxy, Guid.Parse(key));
                        }
                    }
                    method.Invoke(obj, new[] { newProxy });
                }
                return obj;
            }

            if (itemType != typeof (RedisValue))
            {
                // Try to process each entry as a proxy, or fail
                throw new InvalidCastException($"Use RedisValue instead of {itemType?.Name}.");
            }

            RedisObjectManager.RedisBackup?.RestoreList(redisDatabase, listKey);
            var retList = redisDatabase.ListRange(listKey.RedisKey);
            foreach (var ret in retList)
            {
                method.Invoke(obj, new[] {(object)ret});
            }
            return obj;
        }

        public override bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var listKey = new RedisKeyObject(basePropertyInfo, id);

            var trans = redisDatabase.CreateTransaction();
            foreach (var o in ((IEnumerable<RedisValue>) obj).ToArray())
            {
                trans.ListRemoveAsync(listKey.RedisKey, o);
                trans.ListLeftPushAsync(listKey.RedisKey, o);
            }
            trans.Execute();

            return true;
        }

        public override bool Delete(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var listKey = new RedisKeyObject(basePropertyInfo, id);

            redisDatabase.KeyDelete(listKey.RedisKey);

            return true;
        }

        public ListHandler(RedisObjectManager demgelRedis) : base(demgelRedis)
        {
        }
    }
}