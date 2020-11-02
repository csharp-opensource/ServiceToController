using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ServiceToController
{
    public class CastOptions<T> where T : class
    {
        public string? CustomClassName { get; set; }
        public Func<string, string> MethodNameRefactor = x => x;
        public bool AddTestMethod { get; set; } = true;
        public string? ApiPath { get; set; }
        public Func<Type, T> CreateInstanceFunc { get; set; } = (type) => (T)Activator.CreateInstance(type);
        public bool UseNewInstanceEveryMethod { get; set; } = false;
        public Action<T> BeforeMethod { get; set; } = _ => { };
        public Func<T, object, object> AfterMethod { get; set; } = (_, __) => __;
        public void ExecBeforeMethod(T instance) => BeforeMethod(instance);
        public object ExecAfterMethod(T instance, object res)
        {
            try
            {
                if (res is Task task)
                {
                    task.Wait();
                }
            }
            catch { }
            return AfterMethod(instance, res);
        }
        public Func<MethodInfo, bool>? MethodFilter { get; set; } = null;
    }
}
