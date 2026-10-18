using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned scene objects and member access shared by the gravity volume, hover fan,
    // look-at and leave-checker tests. Every object is destroyed on Dispose.
    internal sealed class OwnedWorldObjects : IDisposable
    {
        private readonly List<Object> m_owned = new List<Object>();
        private readonly string m_prefix;

        public OwnedWorldObjects(string prefix) { m_prefix = prefix; }

        public GameObject Create(string name, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var gameObject = new GameObject(m_prefix + "-" + name);
            m_owned.Add(gameObject);
            if (parent != null) gameObject.transform.SetParent(parent, false);
            gameObject.transform.SetPositionAndRotation(position, rotation);
            return gameObject;
        }

        public GameObject Create(string name) => Create(name, Vector3.zero, Quaternion.identity);

        public T Own<T>(T instance) where T : Object
        {
            m_owned.Add(instance);
            return instance;
        }

        public void Dispose()
        {
            for (int i = m_owned.Count - 1; i >= 0; i--)
                if (m_owned[i] != null) Object.DestroyImmediate(m_owned[i]);
            m_owned.Clear();
        }
    }

    internal static class WorldMemberAccess
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Declared = Instance | Static | BindingFlags.DeclaredOnly;

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Declared);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        public static object Get(object target, string field) => FindField(target.GetType(), field).GetValue(target);

        public static void Set(object target, string field, object value) =>
            FindField(target.GetType(), field).SetValue(target, value);

        public static object GetStatic(Type type, string field) => FindField(type, field).GetValue(null);

        public static void SetStatic(Type type, string field, object value) => FindField(type, field).SetValue(null, value);

        // Invokes the most derived declaration visible from the target's type; virtual calls dispatch normally.
        public static object Call(object target, string method, params object[] args)
        {
            for (Type current = target.GetType(); current != null; current = current.BaseType)
            {
                MethodInfo info = current.GetMethods(Declared).FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
                if (info != null) return Unwrap(() => info.Invoke(target, args));
            }
            throw new MissingMethodException(target.GetType().FullName, method);
        }

        public static object CallStatic(Type type, string method, object[] args)
        {
            MethodInfo info = type.GetMethod(method, Static);
            return Unwrap(() => info.Invoke(null, args));
        }

        private static object Unwrap(Func<object> invoke)
        {
            try { return invoke(); }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
        }

        // "name:FieldType:attributes" for each declared field, in metadata order.
        public static string[] Fields(Type type) => type.GetFields(Declared).OrderBy(f => f.MetadataToken)
            .Select(f => f.Name + ":" + f.FieldType.Name + ":" + ((int)f.Attributes).ToString("x4")).ToArray();

        // "name:attributes" for each declared method and constructor, in metadata order.
        public static string[] Methods(Type type) => type.GetMembers(Declared).OfType<MethodBase>()
            .OrderBy(m => m.MetadataToken).Select(m => m.Name + ":" + ((int)m.Attributes).ToString("x4")).ToArray();

        // Attribute type names and their string or float arguments, in metadata order.
        public static string AttributeRow(MemberInfo member) => string.Join(" ", member.GetCustomAttributesData()
            .Select(a => a.AttributeType.Name + "(" + string.Join(",", a.ConstructorArguments.Select(Argument)) + ")"));

        private static string Argument(CustomAttributeTypedArgument argument)
        {
            if (argument.Value is Type type) return type.Name;
            if (argument.Value is float number) return number.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return argument.Value == null ? "null" : argument.Value.ToString();
        }

        public static void AreClose(Vector3 expected, Vector3 actual, string label, float tolerance = 1e-4f)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThanOrEqualTo(tolerance),
                label + ": expected " + expected.ToString("F5") + " but was " + actual.ToString("F5"));
        }

        public static void IsNaN(Vector3 value, string label)
        {
            Assert.That(float.IsNaN(value.x) && float.IsNaN(value.y) && float.IsNaN(value.z), Is.True,
                label + ": " + value.ToString("F5"));
        }
    }
}
