namespace DemgelRedis.ObjectManager
{
    public class LimitObject<T>
    {
        public object LimitedObject { get; set; }
        public int StartLimit { get; set; }
        public int TakeLimit { get; set; } 
    }
}