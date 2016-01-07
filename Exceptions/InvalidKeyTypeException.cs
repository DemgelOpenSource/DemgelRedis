using System;

namespace DemgelRedis.Exceptions
{
    public class InvalidKeyTypeException : Exception
    {
        public InvalidKeyTypeException(string msg) : base(msg) { }
    }
}
