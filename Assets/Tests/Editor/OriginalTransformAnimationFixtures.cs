using System;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // Owned objects for the restored transform movers, rotators and scalers. The Editor may
    // send Reset and OnValidate while a TimeScaledComponent_SDT is added, and both read its
    // s_config lookup, which would otherwise register the SystemConfiguration row; an owned,
    // empty TimeCategoryLookup is installed there (no entry, so no category is assigned) and
    // the prior value is put back. UnityEngine.Random.state is restored as well, so a case may
    // seed the generator. No case calls the TimeScaledComponent OnEnable, OnDisable or time
    // getters, which read the shared TimeManager reference.
    internal sealed class OwnedTimeScaledObjects : IDisposable
    {
        public readonly OwnedWorldObjects Objects;
        private readonly object m_priorConfig;
        private readonly Random.State m_randomState;

        public OwnedTimeScaledObjects(string prefix)
        {
            m_randomState = Random.state;
            Objects = new OwnedWorldObjects(prefix);
            m_priorConfig = GetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal");
            try
            {
                SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", Objects.Own(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
            }
            catch
            {
                // The using statement never receives a fixture whose constructor throws.
                Dispose();
                throw;
            }
        }

        public T Add<T>(string name, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component =>
            Objects.Create(name, position, rotation, parent).AddComponent<T>();

        public T Add<T>(string name) where T : Component => Add<T>(name, Vector3.zero, Quaternion.identity);

        public void Dispose()
        {
            try { Objects.Dispose(); }
            finally
            {
                SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", m_priorConfig);
                Random.state = m_randomState;
            }
        }
    }

    internal static class TransformAnimationAccess
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // TimeScaledComponent_SDT.SynchroniseType: None 0, OnAwake 1, OnEnable 2, OnLevelActivation 3.
        public static void SetSynchronise(TimeScaledComponent_SDT component, int synchronise)
        {
            Type type = typeof(TimeScaledComponent_SDT).GetNestedType("SynchroniseType", BindingFlags.NonPublic);
            Set(component, "m_synchronise", Enum.ToObject(type, synchronise));
        }

        public static void AreClose(Quaternion expected, Quaternion actual, string label, float toleranceDegrees = 1e-2f)
        {
            Assert.That(Quaternion.Angle(expected, actual), Is.LessThanOrEqualTo(toleranceDegrees),
                label + ": expected " + expected.eulerAngles.ToString("F4") + " but was " + actual.eulerAngles.ToString("F4"));
        }

        // Reads a private struct field, changes one of its fields and stores it back.
        public static void SetInStruct(object target, string structField, string field, object value)
        {
            FieldInfo outer = FindField(target.GetType(), structField);
            object boxed = outer.GetValue(target);
            outer.FieldType.GetField(field, Instance).SetValue(boxed, value);
            outer.SetValue(target, boxed);
        }

        public static object GetInStruct(object target, string structField, string field)
        {
            FieldInfo outer = FindField(target.GetType(), structField);
            return outer.FieldType.GetField(field, Instance).GetValue(outer.GetValue(target));
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        // Invokes a method with ref parameters and returns the arguments as they were left.
        public static object[] CallWithRefs(object target, string method, out object result, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, Instance);
            try { result = info.Invoke(target, args); }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
            return args;
        }
    }
}
