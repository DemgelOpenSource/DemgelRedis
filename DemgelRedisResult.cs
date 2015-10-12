namespace Demgel.Redis
{
    public class DemgelRedisResult
    {
        public object Object { get; set; }
        public DemgelResult Result { get; set; }

        public bool IsValid => Result == DemgelResult.Success;
    }

    public class DemgelRedisResult<T>
        where T : class
    {
        public T Object { get; set; }
        public DemgelResult Result { get; set; }

        public bool IsValid => Result == DemgelResult.Success;
    }

    public enum DemgelResult
    {
        Success,
        NotFound
    }
}