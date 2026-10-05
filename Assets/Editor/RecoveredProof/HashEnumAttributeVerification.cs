using System;
using System.Reflection;
using Hardlight.Utils;
using UnityEngine;

namespace ProjectLucid
{
    public static class HashEnumAttributeVerification
    {
        public static int RunManaged()
        {
            int checks = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new InvalidOperationException(message);
                checks++;
            };
            Type type = typeof(HashEnumAttribute);
            check(type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original assembly identity");
            check(type.FullName == "Hardlight.Utils.HashEnumAttribute", "original type identity");
            check((int)type.Attributes == 0x00100001, "original public/nonsealed/beforefieldinit type flags");
            check(type.BaseType == typeof(PropertyAttribute), "real Unity PropertyAttribute base");
            check(type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "no invented Type storage field");
            check(type.GetCustomAttributesData().Count == 0, "original type has no own custom attributes");
            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            check(constructors.Length == 1 && constructors[0].IsPublic, "original single public constructor");
            ParameterInfo[] parameters = constructors[0].GetParameters();
            check(parameters.Length == 1 && parameters[0].ParameterType == typeof(Type), "exact Type constructor argument");
            check(parameters[0].Name == "aType" && parameters[0].IsOptional && parameters[0].HasDefaultValue && parameters[0].DefaultValue == null, "original optional null argument");
            var empty = new HashEnumAttribute();
            var explicitNull = new HashEnumAttribute(null);
            var primitive = new HashEnumAttribute(typeof(int));
            var generic = new HashEnumAttribute(typeof(System.Collections.Generic.Dictionary<string, object>));
            check(empty.order == 0 && explicitNull.order == 0 && primitive.order == 0 && generic.order == 0, "native constructor forwards unchanged to real PropertyAttribute for every argument");
            check(empty.GetType() == type && explicitNull.GetType() == type && primitive.GetType() == type && generic.GetType() == type, "arguments do not change original attribute identity");
            check(empty is Attribute && primitive is PropertyAttribute, "genuine CLR and Unity attribute contracts");
            return checks;
        }
    }
}
