using System;
using System.Reflection;
using Hardlight;

namespace ProjectLucid
{
    public static class SceneAuthoringAttributeVerification
    {
        public static int RunManaged()
        {
            int checks = 0;
            Action<bool, string> check = (value, label) =>
            {
                if (!value) throw new InvalidOperationException(label);
                checks++;
            };
            check(new GraphUnityObjectAttribute(null).UnityObjectType == null, "object accepts null");
            check(new GraphUnityObjectAttribute(typeof(string)).UnityObjectType == typeof(string), "object accepts CLR type");
            check(new GraphUnityObjectPopupAttribute(null).UnityObjectType == null, "popup accepts null");
            check(new GraphUnityObjectPopupAttribute(typeof(string)).UnityObjectType == typeof(string), "popup accepts CLR type");
            check(new GraphUnityObjectPopupAttribute(null).IncludeChildren, "popup default true");
            check(!new GraphUnityObjectPopupAttribute(null, false).IncludeChildren, "popup false retained");
            check(new GraphUnityObjectPopupAttribute(null, true).IncludeChildren, "popup true retained");
            check(new GraphUnitySceneAttribute() is GraphAttributeBase, "scene real base construction");
            foreach (Type type in new[] { typeof(GraphUnityObjectAttribute), typeof(GraphUnityObjectPopupAttribute), typeof(GraphUnitySceneAttribute) })
            {
                check(type.BaseType == typeof(GraphAttributeBase), type.Name + " exact base");
                check(type.IsSealed && type.IsPublic, type.Name + " original public sealed");
                check(type.GetCustomAttributes(typeof(AttributeUsageAttribute), false).Length == 0, type.Name + " no own usage override");
                var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(type, typeof(AttributeUsageAttribute), true);
                check(usage.ValidOn == AttributeTargets.Field && usage.AllowMultiple && usage.Inherited, type.Name + " inherited field multiple usage");
            }
            check(typeof(GraphUnityObjectAttribute).GetField("UnityObjectType").IsInitOnly, "object readonly field");
            check(typeof(GraphUnityObjectPopupAttribute).GetField("UnityObjectType").IsInitOnly, "popup readonly type");
            check(typeof(GraphUnityObjectPopupAttribute).GetField("IncludeChildren").IsInitOnly, "popup readonly bool");
            check(typeof(GraphUnitySceneAttribute).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Length == 0, "scene no own fields");
            ParameterInfo flag = typeof(GraphUnityObjectPopupAttribute).GetConstructor(new[] { typeof(Type), typeof(bool) }).GetParameters()[1];
            check(flag.IsOptional && Equals(flag.DefaultValue, true), "popup original optional default metadata");
            return checks;
        }
        public static void Run()
        {
            int checks = RunManaged();
            Console.WriteLine("PASS scene authoring attributes " + checks + " checks; source/native contracts only");
        }
    }
}
