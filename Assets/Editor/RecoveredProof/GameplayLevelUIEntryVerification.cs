using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class GameplayLevelUIEntryVerification
    {
        public static void Run() => Debug.Log("Project Lucid GameplayLevelUIEntry schema checks=" + RunManaged());
        public static int RunManaged()
        {
            int checks = 0;
            void Check(bool value) { if (!value) throw new InvalidOperationException("GameplayLevelUIEntry contract check " + checks); checks++; }
            Type type = typeof(GameplayLevelUIEntry);
            Check(type.FullName == "HardlightProject.GameplayLevelUIEntry");
            Check(type.IsPublic && type.IsAbstract);
            Check(type.BaseType == typeof(ScriptableObjectWithGuid));
            Check(typeof(ILevelDefinition).IsAssignableFrom(type));
            BindingFlags declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Check(type.GetFields(declared).Length == 0);
            Check(type.GetProperties(declared).Length == 0 && type.GetEvents(declared).Length == 0);
            Check(type.GetMethods(declared).Length == 2);
            Check(type.GetMethod("GetName").IsPublic && type.GetMethod("GetName").IsAbstract && type.GetMethod("GetName").ReturnType == typeof(string));
            MethodInfo set = type.GetMethod("SetData");
            Check(set.IsPublic && set.IsAbstract && set.ReturnType == typeof(void) && set.GetParameters().Length == 1 && set.GetParameters()[0].ParameterType == typeof(string) && set.GetParameters()[0].Name == "sceneName");
            Check(type.GetConstructors(declared).Length == 1 && type.GetConstructors(declared)[0].IsFamily && type.GetConstructors(declared)[0].GetParameters().Length == 0);
            var options = (Il2CppSetOptionAttribute[])type.GetCustomAttributes(typeof(Il2CppSetOptionAttribute), false);
            Check(options.Length == 2);
            Check(options[0].Option == Option.ArrayBoundsChecks && (bool)options[0].Value == false);
            Check(options[1].Option == Option.NullChecks && (bool)options[1].Value == false);
            Check(type.Assembly.GetName().Name == "Game.Runtime");
            Check(type.GetMethod("GetName").GetMethodBody() == null && set.GetMethodBody() == null);
            VerifyConstructor(type.GetConstructors(declared)[0], Check);
            return checks;
        }

        private static void VerifyConstructor(ConstructorInfo constructor, Action<bool> check)
        {
            // This original type is abstract. Inspect the real Editor-compiled
            // constructor instead of creating a substitute runtime subclass.
            var body = constructor.GetMethodBody();
            check(body != null);
            byte[] il = body.GetILAsByteArray();
            int position = 0;
            while (position < il.Length && il[position] == 0x00) position++;
            check(position < il.Length && il[position++] == 0x02); // ldarg.0
            check(position < il.Length && il[position++] == 0x28 && position + 4 <= il.Length); // call
            int token = BitConverter.ToInt32(il, position);
            position += 4;
            var target = (ConstructorInfo)constructor.Module.ResolveMethod(token);
            check(target.DeclaringType == typeof(ScriptableObjectWithGuid) && target.GetParameters().Length == 0);
            while (position < il.Length && il[position] == 0x00) position++;
            check(position < il.Length && il[position++] == 0x2a && position == il.Length); // ret
            check(body.LocalVariables.Count == 0);
        }
    }
}
