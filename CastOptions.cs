using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ServiceToController
{
    public class CastOptions
    {
        public bool AddTestMethod { get; set; } = true;
        public string ApiPath { get; set; }
        public Func<Type, object> CreateInstanceFunc { get; set; } = (type) => Activator.CreateInstance(type);
        public bool UseNewInstanceEveryMethod { get; set; } = false;
        public Action<object> BeforeMethod { get; set; } = _ => { };
        public Func<object, object, object> AfterMethod { get; set; } = (_, __) => __;
        public void ExecBeforeMethod(object instance) => BeforeMethod(instance);
        public object ExecAfterMethod(object instance, object res) => AfterMethod(instance, res);
        public Func<MethodInfo, bool> MethodFilter { get; set; } = null;
    }
}
