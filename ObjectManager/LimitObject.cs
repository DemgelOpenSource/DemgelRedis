using System.Collections.Generic;
using StackExchange.Redis;

namespace DemgelRedis.ObjectManager
{
    public class LimitObject<T> : ILimitObject
    {
        public object LimitedObject { get; set; }
        public long StartLimit { get; set; }
        public long TakeLimit { get; set; }
        public long EndLimit { get; set; }
        public long SkipLimit { get; set; }
        public IList<object> KeyLimit { get; set; }
        public bool RestoreOnly { get; set; } = false;
        public Order Order { get; set; }
    }

    public class LimitObject<TKey, TValue> : ILimitObject
    {
        public object LimitedObject { get; set; }
        public long StartLimit { get; set; }
        public long TakeLimit { get; set; }
        public long EndLimit { get; set; }
        public long SkipLimit { get; set; }
        public IList<object> KeyLimit { get; set; }
        public bool RestoreOnly { get; set; } = false;
        public Order Order { get; set; }
    }

    public interface ILimitObject
    {
        object LimitedObject { get; set; }
        long StartLimit { get; set; }
        long TakeLimit { get; set; }
        long EndLimit { get; set; }
        long SkipLimit { get; set; }
        IList<object> KeyLimit { get; set; }
        bool RestoreOnly { get; set; }
        Order Order { get; set; }
    }
}