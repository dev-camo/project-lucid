using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000084 (06000327..0600032d), nested <>c
    // 02000085 (0600032e..06000331); bodies from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class TypeExtensions
    {
        // 0x06000327 (0x1ab61a0): Type slot 21 is IsSubclassOf, so Delegate itself
        // is not a delegate here.
        public static bool IsDelegate(this Type type)
        {
            return type != null && type.IsSubclassOf(typeof(Delegate));
        }

        // 0x06000328 (0x1ab6260): cut at the first '`'.
        public static string GetNameWithoutGenericArity(this Type type)
        {
            string name = type.Name;
            int arityIndex = name.IndexOf('`');
            return arityIndex != -1 ? name.Substring(0, arityIndex) : name;
        }

        // 0x06000329 (0x1ab62b0): Int32.Parse of everything after the first '`'.
        public static int GetGenericArgumentCount(this Type type)
        {
            string name = type.Name;
            int arityIndex = name.IndexOf('`');
            return arityIndex != -1 ? int.Parse(name.Substring(arityIndex + 1, name.Length - (arityIndex + 1))) : 0;
        }

        // 0x0600032a (0x1ab6310).
        public static bool IsNullable(this Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }

        // 0x0600032b (0x1ab6370): namespace prefix once, then each declaring type
        // outwards. For a constructed type the generic arguments are consumed from
        // the end of GenericTypeArguments by each level's own arity.
        public static string ConvertToCode(this Type type)
        {
            string namespacePrefix = type.GetTypeNamespacePrefix();
            if (type.IsConstructedGenericType)
            {
                string[] genericArguments = type.GenericTypeArguments.Convert<Type, string>((genericType, _) => genericType.ConvertToCode());
                int argumentIndex = genericArguments.Length;
                string typeName = string.Empty;
                for (Type currentType = type; currentType != null; currentType = currentType.DeclaringType)
                {
                    int argumentCount = currentType.GetGenericArgumentCount();
                    string name = currentType.GetNameWithoutGenericArity();
                    argumentIndex -= argumentCount;
                    if (argumentCount > 0)
                    {
                        // Kept as a local: the shipped body builds "<" + Join + ">" first
                        // (Concat(3) at 0x1ab675b) and then appends it to name (Concat(2)
                        // at 0x1ab6768).
                        string arguments = "<" + string.Join(", ", genericArguments, argumentIndex, argumentCount) + ">";
                        name += arguments;
                    }
                    typeName = string.IsNullOrEmpty(typeName) ? name : name + "." + typeName;
                }
                return namespacePrefix + typeName;
            }

            if (type.IsGenericType)
            {
                throw new InvalidOperationException(string.Format("Non-constructed generic types are not supported, please construct the Type: {0}", type));
            }

            string fullName = type.Name;
            for (Type declaringType = type.DeclaringType; declaringType != null; declaringType = declaringType.DeclaringType)
            {
                fullName = declaringType.Name + "." + fullName;
            }
            return namespacePrefix + fullName;
        }

        // 0x0600032c (0x1ab6820).
        public static string GetTypeNamespacePrefix(this Type type)
        {
            return string.IsNullOrEmpty(type.Namespace) ? string.Empty : type.Namespace + ".";
        }

        // 0x0600032d (0x1ab68c0): each generic argument is replaced by its base type,
        // or object when it has none; MakeGenericType is Type slot 127.
        public static Type ConstructWithBaseTypes(this Type type)
        {
            return type.MakeGenericType(type.GetGenericArguments().Convert<Type, Type>((oldType, _) => oldType.BaseType ?? typeof(object)));
        }
    }
}
