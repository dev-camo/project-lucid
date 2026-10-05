using System;
using System.Collections.Generic;
using System.Reflection;

namespace Hardlight
{
    public static class JSONClassFactory
    {
        // HLUnityCore.Runtime.dll:Hardlight.JSONClassFactory:0x06000935;
        // arm64 shared reference body 0x93a8ac. Public static factories are
        // cached by their short type name; later discoveries overwrite a key.
        public static bool TryCacheFactoryType<TBaseType>(this IDictionary<string, JSONFactoryClass> lookup,
            Type assemblyType, ScopedStringBuilder scopedStringBuilder)
        {
            if (!typeof(TBaseType).IsAssignableFrom(assemblyType) || assemblyType.IsAbstract) return false;
            MethodInfo constructInstance = assemblyType.GetMethod("ConstructInstance");
            if (constructInstance == null || !constructInstance.IsStatic) return false;
            Type jsonCtorArgs = GetJSONCtorArgs(assemblyType);
            scopedStringBuilder.AppendLine("Found " + typeof(TBaseType).Name + " class: " + assemblyType.FullName);
            lookup[assemblyType.Name] = new JSONFactoryClass(assemblyType, constructInstance, jsonCtorArgs);
            return true;
        }

        // Original token 0x06000936; arm64 shared reference body 0x93a690.
        // The JSON is an unchanged argument to the factory method.
        public static TInstance ConstructInstance<TInstance>(this IDictionary<string, JSONFactoryClass> lookup,
            string className, object obj, string jsonCtorArgs, Type[] genericArguments = null) where TInstance : class
        {
            return ConstructInstance<TInstance>(lookup, className, new object[] { obj, jsonCtorArgs }, genericArguments);
        }

        // Original token 0x06000937; arm64 shared reference body 0x93a788.
        public static TInstance ConstructInstance<TInstance>(this IDictionary<string, JSONFactoryClass> lookup,
            string className, object arg1, object arg2, string jsonCtorArgs, Type[] genericArguments = null) where TInstance : class
        {
            return ConstructInstance<TInstance>(lookup, className, new object[] { arg1, arg2, jsonCtorArgs }, genericArguments);
        }

        // Original token 0x06000938; arm64 shared reference body 0x93a1e8.
        // Closed generic entries use Type.ToString(), and retain the original
        // open type's constructor-argument metadata. Invocation errors propagate.
        private static TInstance ConstructInstance<TInstance>(this IDictionary<string, JSONFactoryClass> lookup,
            string className, object[] parameters, Type[] genericArguments) where TInstance : class
        {
            if (!lookup.TryGetValue(className, out JSONFactoryClass factory))
                throw new Exception("Failed to construct with class name '" + className + "' as no class with that name is registered in the class-factory.");

            if (!factory.ClassType.IsGenericType)
                return factory.ConstructInstanceMethod.Invoke(null, parameters) as TInstance;

            if (genericArguments == null || genericArguments.Length == 0)
                throw new Exception("Failed to construct generic with class name '" + className + "' as no generic arguments have been provided.");
            foreach (Type genericArgument in genericArguments)
                if (genericArgument == null)
                    throw new Exception("Failed to construct generic with class name '" + className + "' as one or more generic arguments are null.");

            Type classType = factory.ClassType.MakeGenericType(genericArguments);
            string constructedName = classType.ToString();
            if (!lookup.TryGetValue(constructedName, out JSONFactoryClass constructedFactory))
            {
                constructedFactory = new JSONFactoryClass(classType, classType.GetMethod("ConstructInstance"), factory.JSONCtorArgsType);
                lookup[constructedName] = constructedFactory;
            }
            return constructedFactory.ConstructInstanceMethod.Invoke(null, parameters) as TInstance;
        }

        // Original token 0x06000939; arm64 0x1af5d58. Flags 48 select
        // public/nonpublic nested types before advancing to the base type.
        private static Type GetJSONCtorArgs(Type assemblyType)
        {
            while (assemblyType != null)
            {
                foreach (Type nestedType in assemblyType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                    if (nestedType.GetCustomAttribute<JSONCtorArgsAttribute>() != null || nestedType.Name == "JSONCtorArgs")
                        return nestedType;
                assemblyType = assemblyType.BaseType;
            }
            return null;
        }
    }
}
