using System;
using System.Reflection;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid
{
    public static class FastActionClassOptionsVerification
    {
        public static void Run()
        {
            Console.WriteLine("PASS FastAction class options checks=" + RunManaged());
        }

        public static int RunManaged()
        {
            int checks = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new InvalidOperationException("FastAction original options: " + message);
                checks++;
            };
            Type[] types = { typeof(Hardlight.FastActionBase<,>), typeof(Hardlight.FastAction<>) };
            string[] names = { "Hardlight.FastActionBase`2", "Hardlight.FastAction`1" };
            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                Type type = types[typeIndex];
                check(type.FullName == names[typeIndex], "original type name " + typeIndex);
                check(type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original assembly " + type.Name);
                check(type.IsGenericTypeDefinition && type.IsPublic && type.IsClass, "original public generic class " + type.Name);
                check(type.IsAbstract == (typeIndex == 0), "original abstractness " + type.Name);
                var attributes = CustomAttributeData.GetCustomAttributes(type);
                check(attributes.Count == 2, "exact two directly declared original attributes " + type.Name);
                Option[] options = { Option.ArrayBoundsChecks, Option.NullChecks };
                for (int index = 0; index < attributes.Count; index++)
                {
                    CustomAttributeData attribute = attributes[index];
                    check(attribute.AttributeType == typeof(Il2CppSetOptionAttribute), "original attribute type " + type.Name + "/" + index);
                    check(attribute.AttributeType.Assembly == type.Assembly, "original local attribute assembly " + type.Name + "/" + index);
                    ParameterInfo[] parameters = attribute.Constructor.GetParameters();
                    check(parameters.Length == 2 && parameters[0].ParameterType == typeof(Option) && parameters[1].ParameterType == typeof(object), "exact original Option/object constructor " + type.Name + "/" + index);
                    check(attribute.ConstructorArguments.Count == 2, "two exact original constructor arguments " + type.Name + "/" + index);
                    check(attribute.ConstructorArguments[0].ArgumentType == typeof(Option) && (int)attribute.ConstructorArguments[0].Value == (int)options[index], "original option value and declaration order " + type.Name + "/" + index);
                    check(attribute.ConstructorArguments[1].ArgumentType == typeof(bool) && attribute.ConstructorArguments[1].Value is bool value && !value, "original boxed Boolean false " + type.Name + "/" + index);
                    check(attribute.NamedArguments.Count == 0, "no fabricated named attribute assignments " + type.Name + "/" + index);
                }
            }
            return checks;
        }
    }
}
