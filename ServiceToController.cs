using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace ServiceToController
{
    public static class ServiceToController
    {
        public static void AddCastedService<T>(this IMvcBuilder mvcBuilder, Func<Type, object> createInstanceOfCastedType = null, bool addTestMethod = true)
        {
            var castedType = Cast<T>(addTestMethod); // create controller type
            createInstanceOfCastedType = createInstanceOfCastedType == null ? (type) => Activator.CreateInstance(type) : createInstanceOfCastedType; // set defualt instance creator
            var instance = createInstanceOfCastedType(castedType);  // create instance of this type
            mvcBuilder.Services.AddSingleton(castedType, instance); // add controller as a service
            mvcBuilder.AddApplicationPart(castedType.Assembly); // map controller
        }
        public static Type Cast<T>(bool addTestMethod = true)
        {
            var type = typeof(T);
            var dynamicNamespace = new AssemblyName(type.Namespace);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(dynamicNamespace, AssemblyBuilderAccess.Run).DefineDynamicModule(dynamicNamespace.Name);
            var classProxy = assemblyBuilder.DefineType(type.Name + "Controller", TypeAttributes.Public, type);
            classProxy.SetParent(typeof(ControllerBase)); // make this class a controller

            if (addTestMethod)
            {
                var method = classProxy.DefineMethod("test", MethodAttributes.Public, typeof(string), null);
                method.SetCustomAttribute(new CustomAttributeBuilder(typeof(HttpGetAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { $"/api/{classProxy.Name}/test" }));
                var ilgen = method.GetILGenerator();
                ilgen.Emit(OpCodes.Ldstr, "OK"); // push ok to stack
                ilgen.Emit(OpCodes.Ret);  // return
            }

            // get all parent methods tasks
            var methods = type.GetMethods((BindingFlags)(-1)).Where(x => x.IsPublic && (x.ReturnType == typeof(Task) || (x.ReturnType.IsGenericType && x.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))).ToList();
            foreach (var existingMethod in methods)
            {
                var existingParams = existingMethod.GetParameters().ToList();
                var methodBuilder = classProxy.DefineMethod(
                    existingMethod.Name + "Post",
                    MethodAttributes.Public,
                    existingMethod.ReturnType,
                    existingParams.Select(x => x.ParameterType).ToArray()
                );

                var ilgen = methodBuilder.GetILGenerator();
                ilgen.Emit(OpCodes.Ldarg_0); // load this ref to stack
                for (int i = 0; i < existingParams.Count; i++)
                {
                    var x = existingParams[i];
                    var p = methodBuilder.DefineParameter(i + 1, x.Attributes, x.Name);

                    // if only one parameter, get from body
                    if (existingParams.Count == 1)
                    {
                        p.SetCustomAttribute(new CustomAttributeBuilder(typeof(FromBodyAttribute).GetConstructor(new Type[] { }), new object[] { }));
                    }

                    ilgen.Emit(OpCodes.Ldarg_S, i + 1); // push paramater to stack
                }
                ilgen.Emit(OpCodes.Callvirt, existingMethod); // call parent method
                ilgen.Emit(OpCodes.Ret); // return

                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(HttpPostAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { $"/api/{classProxy.Name}/{existingMethod.Name}" }));
            }
            return classProxy.CreateType();
        }
    }
}
