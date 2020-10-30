﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ServiceToController
{
    public static class ServiceToController
    {
        public static object AddCastedService<T>(this IMvcBuilder mvcBuilder, Func<Type, object> createInstanceOfCastedType = null, CastOptions castOptions = null)
        {
            var castedType = Cast<T>(castOptions); // create controller type
            if (createInstanceOfCastedType == null) { createInstanceOfCastedType = (type) => Activator.CreateInstance(type); } // set defualt instance creator
            var instance = createInstanceOfCastedType(castedType);  // create instance of this type
            mvcBuilder.Services.AddSingleton(castedType, instance); // add controller as a service
            mvcBuilder.AddApplicationPart(castedType.Assembly); // map controller
            return instance;
        }
        public static Type Cast<T>(CastOptions castOptions = null)
        {
            if (castOptions == null) { castOptions = new CastOptions(); }
            var type = typeof(T);
            var dynamicNamespace = new AssemblyName(type.GetTypeInfo().Assembly.GetTypes().Select(x => x.Namespace).First(x => x != null));
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(dynamicNamespace, AssemblyBuilderAccess.Run).DefineDynamicModule(dynamicNamespace.Name);
            var classProxy = assemblyBuilder.DefineType(type.Name + "Controller", TypeAttributes.Public, type);
            classProxy.SetParent(typeof(ControllerBase)); // make this class a controller
            var apiPath = string.IsNullOrEmpty(castOptions.ApiPath) ? $"/api/{classProxy.Name}" : castOptions.ApiPath;

            if (castOptions.AddTestMethod)
            {
                var method = classProxy.DefineMethod("test", MethodAttributes.Public, typeof(string), null);
                method.SetCustomAttribute(new CustomAttributeBuilder(typeof(HttpGetAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { $"{apiPath}/test" }));
                var ilgen = method.GetILGenerator();
                ilgen.Emit(OpCodes.Ldstr, "OK"); // push ok to stack
                ilgen.Emit(OpCodes.Ret);  // return
            }

            // get all parent methods tasks
            var methods = type
                .GetMethods((BindingFlags)(-1))
                .Where(x => x.IsPublic && (x.ReturnType == typeof(Task) || (x.ReturnType.IsGenericType && x.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))))
                .ToList();

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
                // Before Method
                ilgen.Emit_LdInst(castOptions);
                ilgen.Emit(OpCodes.Ldarg_0); // load this
                ilgen.Emit(OpCodes.Callvirt, typeof(CastOptions).GetMethod("ExecBeforeMethod", new Type[] { typeof(object) }));

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
                ilgen.Emit(OpCodes.Stloc_0);

                // After Method
                ilgen.DeclareLocal(typeof(object));
                ilgen.Emit_LdInst(castOptions);
                ilgen.Emit(OpCodes.Ldarg_0); // load this
                ilgen.Emit(OpCodes.Ldloc_0); //load res from local
                ilgen.Emit(OpCodes.Callvirt, typeof(CastOptions).GetMethod("ExecAfterMethod", new Type[] { typeof(object), typeof(object) }));
                ilgen.Emit(OpCodes.Ret); // return

                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(HttpPostAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { $"{apiPath}/{existingMethod.Name}" }));
            }
            return classProxy.CreateType();
        }

        /// <summary>
        /// Burn an reference to the specified runtime object instance into the DynamicMethod
        /// </summary>
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
            /// Do this only if you can otherwise ensure that 'inst' outlives the DynamicMethod
            // gch.Free();
        }
    }
}