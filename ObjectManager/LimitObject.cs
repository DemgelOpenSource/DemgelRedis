using System.Collections.Generic;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager
{
    public class LimitObject<T> : LimitObject
    {
    }

    public class LimitObject<TKey, TValue> : LimitObject
    {
    }

    public abstract class LimitObject
    {
        protected internal object LimitedObject { get; set; }
        protected internal long StartLimit { get; set; }
        protected internal long TakeLimit { get; set; }
        protected internal long EndLimit { get; set; }
        protected internal long SkipLimit { get; set; }
        protected internal IList<object> KeyLimit { get; set; }
        protected internal bool RestoreOnly { get; set; }
        protected internal Order Order { get; set; }
    }
}