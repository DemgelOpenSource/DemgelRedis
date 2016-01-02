using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DemgelRedis.Common;
using DemgelRedis.Extensions;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using DemgelRedis.ObjectManager.Proxy;
using DemgelRedis.ObjectManager.Proxy.Selectors;
using DemgelRedis.ObjectManager.Proxy.SetInterceptor;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager.Handlers
{
    public class SetHandler : RedisHandler
    {
        private readonly SetSelector _setSelector;

        public SetHandler(RedisObjectManager demgelRedis) : base(demgelRedis)
        {
            _setSelector = new SetSelector();
        }

        public override bool CanHandle(object obj)
        {
            var targetObject = GetTarget(obj);
            var t = targetObject.GetType();
            return t.Name.StartsWith("RedisSortedSet", StringComparison.Ordinal);
        }

        public override object Read(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo,
            LimitObject limits = null)
        {
            var setKey = new RedisKeyObject(basePropertyInfo, id);

            // TODO Workon RedisBackup

            if (limits != null && limits.RestoreOnly)
            {
                return obj;
            }

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

            //var method = objType.GetMethod("Add", BindingFlags.DeclaredOnly, null, new[] { itemType }, null);
            var method = objType.GetMethods()
                .SingleOrDefault(x => x.Name.Equals("Add") && x.ReturnType == typeof (bool));

            if (itemType != null && (itemType.GetInterfaces().Contains(typeof (IRedisObject)) || typeof (IRedisObject) == itemType))
            {
                List<RedisValue> retList;

                // TODO add limits to sets (for now we just grab everything)
                if (limits != null)
                {
                    retList = redisDatabase.SortedSetRangeByScore(setKey.RedisKey, limits.StartLimit, limits.EndLimit, Exclude.None, limits.Order, limits.SkipLimit, limits.TakeLimit).ToList();
                }
                else
                {
                    retList = redisDatabase.SortedSetRangeByRank(setKey.RedisKey).ToList();
                }

                foreach (var ret in retList)
                {
                    if (!redisDatabase.KeyExists((string) ret))
                    {
                        redisDatabase.SortedSetRemove(setKey.RedisKey, (string)ret);
                        continue;
                    }

                    var key = ret.ParseKey();

                    var newProxy = RedisObjectManager.GetRedisObjectWithType(redisDatabase, (string)ret, key);

                    var t = method.Invoke(obj, new[] { newProxy as IRedisObject });
                }
            }
            return obj;
        }

        public override bool Save(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            Debug.WriteLine("Save was called on this object...");
            return true;
        }

        public override bool Delete(object obj, Type objType, IDatabase redisDatabase, string id, PropertyInfo basePropertyInfo = null)
        {
            var setKey = new RedisKeyObject(basePropertyInfo, id);

            redisDatabase.KeyDelete(setKey.RedisKey);

            return true;
        }

        public override object BuildProxy(ProxyGenerator generator, Type objType, CommonData data, object baseObj)
        {
            if (!objType.IsInterface)
            {
                throw new Exception("Set can only be created from ISet Interface");
            }

            object proxy;

            if (baseObj == null)
            {
                proxy = generator.CreateInterfaceProxyWithoutTarget(objType,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _setSelector
                    },
                    new GeneralGetInterceptor(data), new SetAddInterceptor(data), new SetRemoveInterceptor(data));
            }
            else
            {
                proxy = generator.CreateInterfaceProxyWithTarget(objType, baseObj,
                    new ProxyGenerationOptions(new GeneralProxyGenerationHook())
                    {
                        Selector = _setSelector
                    },
                    new GeneralGetInterceptor(data), new SetAddInterceptor(data), new SetRemoveInterceptor(data));
            }
            return proxy;
        }
    }
}