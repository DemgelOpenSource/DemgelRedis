using System;
using System.Linq;
using Castle.Core.Internal;
using DemgelRedis.Common;
using DemgelRedis.Interfaces;
using DemgelRedis.ObjectManager.Attributes;
using StackExchange.Redis;

namespace DemgelRedis.Extensions
{
    public static class RedisDatabaseExtensions
    {
        public static void GenerateId(this IDatabase database, RedisKeyObject key, object argument, IRedisBackup redisBackup)
        {
            var redisIdAttr =
                argument.GetType().GetProperties().SingleOrDefault(
                    x => x.HasAttribute<RedisIdKey>());

            if (redisIdAttr == null) return; // Throw error

            var value = redisIdAttr.GetValue(argument, null);

            if (redisIdAttr.PropertyType == typeof(string))
            {
                var currentValue = (string)value;
                if (string.IsNullOrEmpty(currentValue))
                {
                    redisBackup?.RestoreCounter(database, key);
                    var newId = database.StringIncrement($"demgelcounter:{key.CounterKey}");
                    redisBackup?.UpdateCounter(database, key);
                    key.Id = newId.ToString();
                    redisIdAttr.SetValue(argument, key.Id);
                }
                else
                {
                    key.Id = currentValue;
                }
            }
            else if (redisIdAttr.PropertyType == typeof(Guid))
            {
                var guid = (Guid)value;
                if (guid == Guid.Empty)
                {
                    guid = Guid.NewGuid();
                    key.Id = guid.ToString();
                    redisIdAttr.SetValue(argument, guid);
                }
                else
                {
                    key.Id = guid.ToString();
                }
            }
            else
            {
                throw new ArgumentException("RedisIdKey needs to be either Guid or String");
            }
        }
    }
}