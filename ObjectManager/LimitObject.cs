using System.Collections;
using System.Collections.Generic;
using System.Deployment.Internal;

namespace DemgelRedis.ObjectManager
{
    public class LimitObject<T> : ILimitObject
    {
        public object LimitedObject { get; set; }
        public int StartLimit { get; set; }
        public int TakeLimit { get; set; }
        public IList<object> KeyLimit { get; set; } 
    }

    public class LimitObject<TKey, TValue> : ILimitObject
    {
        public object LimitedObject { get; set; }
        public int StartLimit { get; set; }
        public int TakeLimit { get; set; }
        public IList<object> KeyLimit { get; set; }
    }

    public interface ILimitObject
    {
        object LimitedObject { get; set; }
        int StartLimit { get; set; }
        int TakeLimit { get; set; }
        IList<object> KeyLimit { get; set; }
    }
}