using System;
using System.Threading.Tasks;

namespace ServiceToController
{
    public class CastOptions
    {
        public bool AddTestMethod { get; set; } = true;
        public string ApiPath { get; set; }
        public Action<object> BeforeMethod { get; set; } = _ => { };
        public Func<object, object, object> AfterMethod { get; set; } = (_, __) => __;

        public void ExecBeforeMethod(object instance) => BeforeMethod(instance);
        public object ExecAfterMethod(object instance, object res) => AfterMethod(instance, res);
    }
}
