using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemgelRedis.ObjectManager.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    class IgnorePropertyAttribute : Attribute
    {
    }
}
