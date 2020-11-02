using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSwag.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace ServiceToController
{
    public static class ServiceToController
    {
        public static T AddCastedService<T>(this IMvcBuilder mvcBuilder, CastOptions<T>? castOptions = null) where T : class
        {
            castOptions ??= new CastOptions<T>();
            var castedType = Cast(castOptions); // create controller type
            var instance = castOptions.CreateInstanceFunc(castedType);  // create instance of this type
            mvcBuilder.Services.AddSingleton(castedType, instance); // add controller as a service
            mvcBuilder.AddApplicationPart(castedType.Assembly); // map controller
            return instance;
        }
        public static Type Cast<T>(CastOptions<T>? castOptions = null) where T : class
        {
            castOptions ??= new CastOptions<T>();
            var type = typeof(T);
            var dynamicNamespace = new AssemblyName(type.GetTypeInfo().Assembly.GetTypes().Select(x => x.Namespace).First(x => x != null));
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(dynamicNamespace, AssemblyBuilderAccess.Run).DefineDynamicModule(dynamicNamespace.Name);
            var classProxy = assemblyBuilder.DefineType(castOptions.CustomClassName ?? (type.Name + "Controller"), TypeAttributes.Public, type);
            classProxy.SetParent(type); // make this class a controller
            classProxy.CreatePassThroughConstructors(type);
            var apiPath = string.IsNullOrEmpty(castOptions.ApiPath) ? $"/api/{classProxy.Name}" : castOptions.ApiPath;

            if (castOptions.AddTestMethod)
            {
                var method = classProxy.DefineMethod("test", MethodAttributes.Public | MethodAttributes.NewSlot | MethodAttributes.CheckAccessOnOverride, typeof(string), null);
                method.SetCustomAttribute(new CustomAttributeBuilder(typeof(HttpGetAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { $"{apiPath}/test" }));
                var ilgen = method.GetILGenerator();
                ilgen.Emit(OpCodes.Ldstr, "OK"); // push ok to stack
                ilgen.Emit(OpCodes.Ret);  // return
            }

            var props = type.GetProperties((BindingFlags)(-1)).Select(x => x.Name).ToList();

            var allMethods = type.GetMethods((BindingFlags)(-1));

            var methods = allMethods
                .Where(castOptions.MethodFilter ?? (x => x != null))
                .Where(x => !props.Any(p => x.Name == $"get_{p}" || x.Name == $"set_{p}"))
                .ToList();

            var methodNames = new Dictionary<string, int>();

            foreach (var existingMethod in allMethods)
            {
                var exposeMethod = methods.Contains(existingMethod);
                if (!exposeMethod) { continue; }
                var methodName = castOptions.MethodNameRefactor(existingMethod.Name);
                var counter = methodNames.GetValueOrDefault(methodName, 0);
                methodNames[methodName] = counter + 1;
                methodName += counter == 0 ? "" : counter.ToString();
                classProxy.CopyMethod(existingMethod, castOptions, methodName, exposeMethod, $"{apiPath}/{methodName}");
            }
            return classProxy.CreateType();
        }

        private static MethodBuilder? CopyMethod<T>(this TypeBuilder classProxy, MethodInfo existingMethod, CastOptions<T> castOptions, string methodName, bool exposeMethod, string route) where T : class
        {
            if (!exposeMethod) { return null; }
            var type = typeof(T);
            var existingParams = existingMethod.GetParameters().ToList();
            var methodBuilder = classProxy.DefineMethod(
                methodName,
                MethodAttributes.Public,
                existingMethod.ReturnType,
                existingParams.Select(x => x.ParameterType).ToArray()
            );

            var ilgen = methodBuilder.GetILGenerator();

            // define locals
            ilgen.DeclareLocal(typeof(T)); // instance
            ilgen.DeclareLocal(typeof(object)); // res

            void loadNewInstanceOrThis()
            {
                if (castOptions.UseNewInstanceEveryMethod)
                {
                    ilgen.Emit(OpCodes.Ldloc_0);
                }
                else
                {
                    ilgen.Emit(OpCodes.Ldarg_0); // load this ref to stack
                }
            }

            ilgen.Emit_LdInst(castOptions);
            ilgen.Emit(OpCodes.Callvirt, typeof(CastOptions<T>).GetMethod("CreateInstance", new Type[] { }));
            ilgen.Emit(OpCodes.Stloc_0);

            // Before Method
            ilgen.Emit_LdInst(castOptions);
            loadNewInstanceOrThis();
            ilgen.Emit(OpCodes.Callvirt, typeof(CastOptions<T>).GetMethod("ExecBeforeMethod", new Type[] { typeof(T) }));
            loadNewInstanceOrThis();
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
            ilgen.Emit(OpCodes.Stloc_1);

            // After Method
            ilgen.DeclareLocal(typeof(object));
            ilgen.Emit_LdInst(castOptions);
            loadNewInstanceOrThis();
            ilgen.Emit(OpCodes.Ldloc_1); //load res from local
            ilgen.Emit(OpCodes.Callvirt, typeof(CastOptions<T>).GetMethod("ExecAfterMethod", new Type[] { typeof(T), typeof(object) }));
            ilgen.Emit(OpCodes.Ret); // return


            if (!exposeMethod)
            {
                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(OpenApiIgnoreAttribute).GetConstructor(new Type[] { }), new object[] { }));
            }
            else
            {
                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(HttpPostAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { route }));
            }

            return methodBuilder;
        }

        public static void Emit_LdInst<TInst>(this ILGenerator il, TInst inst) where TInst : class
        {
            var gch = GCHandle.Alloc(inst);
            var ptr = GCHandle.ToIntPtr(gch);
            if (IntPtr.Size == 4)
            {
                il.Emit(OpCodes.Ldc_I4, ptr.ToInt32());
            }
            else
            {
                il.Emit(OpCodes.Ldc_I8, ptr.ToInt64());
            }
            il.Emit(OpCodes.Ldobj, typeof(TInst));
        }
    }
}
