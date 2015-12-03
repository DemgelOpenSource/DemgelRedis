using System;
using System.Globalization;
using System.Reflection;
using DemgelRedis.Interfaces;
using StackExchange.Redis;

namespace DemgelRedis.Converters
{
    public class DateTimeConverter : ITypeConverter
    {
        public RedisValue ToWrite(object prop)
        {
            return ((DateTime) prop).ToString("o", CultureInfo.InvariantCulture);
        }

        public object OnRead(RedisValue obj, PropertyInfo info)
        {
            DateTime dateTime;
            if(obj.IsNullOrEmpty) return new DateTime();
            if (DateTime.TryParse(obj, out dateTime))
            {
                return dateTime;
            }

            throw new Exception("Not a valid DateTime");
        }
    }
}