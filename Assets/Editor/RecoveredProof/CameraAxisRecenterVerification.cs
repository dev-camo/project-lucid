using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Cinemachine;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class CameraAxisRecenterVerification
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly string[] Replaced = { "m_SpeedMode", "m_MaxSpeed", "m_AccelTime", "m_DecelTime" };
        private static void Require(bool value, string text, ref int checks)
        { if (!value) throw new InvalidOperationException("Original camera axis/recenter: " + text); checks++; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Own) ?? throw new MissingFieldException(type.FullName, name);
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static T Read<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static float Float(int value) => BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
        private static T WithoutConstructor<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static AxisState Seed()
        {
            object box = default(AxisState);
            foreach (FieldInfo field in typeof(AxisState).GetFields(Own))
            {
                if (field.IsLiteral) continue;
                if (field.FieldType == typeof(float)) field.SetValue(box, Float(unchecked((int)0x80000000)));
                else if (field.FieldType == typeof(int)) field.SetValue(box, -37);
                else if (field.FieldType == typeof(bool)) field.SetValue(box, true);
                else if (field.FieldType == typeof(string)) field.SetValue(box, "camera-axis-preserved");
                else if (field.FieldType.IsEnum) field.SetValue(box, Enum.ToObject(field.FieldType, 17));
            }
            return (AxisState)box;
        }
        private static void Preserve(AxisState before, AxisState after, bool all, ref int checks)
        {
            object a = before, b = after;
            foreach (FieldInfo field in typeof(AxisState).GetFields(Own))
            {
                if (field.IsLiteral || (!all && Array.IndexOf(Replaced, field.Name) >= 0)) continue;
                object x = field.GetValue(a), y = field.GetValue(b);
                Require(field.FieldType == typeof(float) ? Bits((float)x) == Bits((float)y) : Equals(x, y), "axis preserves " + field.Name, ref checks);
            }
        }
        private static void Parameters(AxisState actual, int mode, float speed, float accel, float decel, ref int checks)
        {
            Require((int)actual.m_SpeedMode == mode, "exact raw speed mode", ref checks);
            Require(Bits(actual.m_MaxSpeed) == Bits(speed), "exact max speed bits", ref checks);
            Require(Bits(actual.m_AccelTime) == Bits(accel), "exact acceleration bits", ref checks);
            Require(Bits(actual.m_DecelTime) == Bits(decel), "exact deceleration bits", ref checks);
        }
        private static void Configure(CameraAxisStateDefinition definition, int mode, float speed, float accel, float decel)
        { Set(definition, "m_speedMode", (AxisState.SpeedMode)mode); Set(definition, "m_maxSpeed", speed); Set(definition, "m_accelTime", accel); Set(definition, "m_decelTime", decel); }
        private static void NullFault(Action action, string text, ref int checks)
        { bool fault = false; try { action(); } catch (NullReferenceException) { fault = true; } Require(fault, text, ref checks); }

        public static int RunManaged()
        {
            // These genuine ScriptableObjects bypass native construction only in this
            // host-safe portion. Real initialization, AnimationCurve and engine lifecycle
            // are checked separately by RunEngine; no host simulates those APIs.
            int checks = 0;
            var definition = WithoutConstructor<CameraAxisStateDefinition>();
            float nzero = Float(unchecked((int)0x80000000));
            float payloadNaN = Float(unchecked((int)0x7fc01234));
            int[] modes = { -4, int.MinValue, 0, 1, int.MaxValue, -1 };
            float[] speeds = { nzero, float.NegativeInfinity, 0f, 150f, float.Epsilon, payloadNaN };
            float[] accels = { float.PositiveInfinity, 9f, 1f, 0.5f, float.MaxValue, nzero };
            float[] decels = { payloadNaN, -3f, 2f, 0.2f, float.MinValue, float.PositiveInfinity };
            for (int i = 0; i < modes.Length; i++)
            {
                Configure(definition, modes[i], speeds[i], accels[i], decels[i]);
                AxisState axis = Seed(), before = axis; definition.Set(ref axis);
                Parameters(axis, modes[i], speeds[i], accels[i], decels[i], ref checks);
                Preserve(before, axis, false, ref checks);
            }
            var overrides = WithoutConstructor<CameraAxisStateOverrides>();
            var lookup = new SerializableDictionary<CameraSensitivityType, CameraAxisStateDefinition>(HardlightProject.HardlightEnumComparers.CameraSensitivityTypeComparer);
            Set(overrides, "m_overrides", lookup);
            AxisState target = Seed(), saved = target;
            Require(!overrides.SetOverride((CameraSensitivityType)int.MinValue, ref target), "missing raw key returns lookup false", ref checks);
            Preserve(saved, target, true, ref checks);
            lookup[(CameraSensitivityType)int.MinValue] = definition;
            Require(overrides.SetOverride((CameraSensitivityType)int.MinValue, ref target), "found raw key returns lookup true", ref checks);
            Parameters(target, -1, payloadNaN, nzero, float.PositiveInfinity, ref checks);
            Preserve(saved, target, false, ref checks);
            lookup[(CameraSensitivityType)17] = null;
            target = Seed(); saved = target;
            NullFault(() => overrides.SetOverride((CameraSensitivityType)17, ref target), "found CLR-null definition is not guarded", ref checks);
            Preserve(saved, target, true, ref checks);
            Set(overrides, "m_overrides", null);
            NullFault(() => overrides.SetOverride((CameraSensitivityType)18, ref target), "CLR-null dictionary is not silently repaired", ref checks);
            Preserve(saved, target, true, ref checks);
            var basic = WithoutConstructor<CameraRecenterHeadingDefinition_Basic>();
            Require(ReferenceEquals(basic.AngleToSpeedMultiplierCurve, null), "curve getter returns actual uninitialized field", ref checks);
            foreach (float value in new[] { nzero, payloadNaN, float.PositiveInfinity, -37f })
            { Set(basic, "m_rotationSpeed", value); Require(Bits(basic.RotationSpeed) == Bits(value), "speed getter preserves authored bits", ref checks); }
            return checks;
        }

        public static int RunEngine()
        {
            int checks = 0;
            CameraAxisStateDefinition definition = null;
            CameraAxisStateOverrides overrides = null;
            CameraRecenterHeadingDefinition_Basic basic = null;
            try
            {
                definition = ScriptableObject.CreateInstance<CameraAxisStateDefinition>();
                Parameters(SeedFromDefinition(definition), 0, 0f, 0f, 0f, ref checks);
                Configure(definition, 1, -9f, 0.25f, 0.75f);
                AxisState axis = Seed(), before = axis; definition.Set(ref axis);
                Parameters(axis, 1, -9f, 0.25f, 0.75f, ref checks); Preserve(before, axis, false, ref checks);
                overrides = ScriptableObject.CreateInstance<CameraAxisStateOverrides>();
                var lookup = Read<SerializableDictionary<CameraSensitivityType, CameraAxisStateDefinition>>(overrides, "m_overrides");
                Require(lookup != null, "real constructor creates dictionary", ref checks);
                Require(lookup.Count == 0, "real constructor dictionary starts empty", ref checks);
                Require(ReferenceEquals(((Dictionary<CameraSensitivityType, CameraAxisStateDefinition>)Field(lookup.GetType().BaseType, "m_dictionary").GetValue(lookup)).Comparer, HardlightProject.HardlightEnumComparers.CameraSensitivityTypeComparer), "real constructor exact concrete comparer", ref checks);
                axis = Seed(); before = axis;
                Require(!overrides.SetOverride((CameraSensitivityType)17, ref axis), "real empty lookup returns false", ref checks); Preserve(before, axis, true, ref checks);
                lookup[(CameraSensitivityType)17] = definition;
                Require(overrides.SetOverride((CameraSensitivityType)17, ref axis), "real found lookup returns true", ref checks);
                Parameters(axis, 1, -9f, 0.25f, 0.75f, ref checks); Preserve(before, axis, false, ref checks);
                lookup[(CameraSensitivityType)18] = null; axis = Seed(); before = axis;
                NullFault(() => overrides.SetOverride((CameraSensitivityType)18, ref axis), "real dictionary found-null remains source fault", ref checks); Preserve(before, axis, true, ref checks);
                basic = ScriptableObject.CreateInstance<CameraRecenterHeadingDefinition_Basic>();
                Require(basic.AngleToSpeedMultiplierCurve != null, "real basic curve initialized", ref checks);
                Keyframe[] keys = basic.AngleToSpeedMultiplierCurve.keys;
                Require(keys.Length == 2, "real EaseInOut has exactly two keys", ref checks);
                Require(keys[0].time == 0f && keys[0].value == 1f, "real first key (0,1)", ref checks);
                Require(keys[1].time == 1f && keys[1].value == 2f, "real second key (1,2)", ref checks);
                Require(keys[0].inTangent == 0f && keys[0].outTangent == 0f && keys[1].inTangent == 0f && keys[1].outTangent == 0f, "real keys use EaseInOut zero tangents", ref checks);
                Require(basic.AngleToSpeedMultiplierCurve.Evaluate(0f) == 1f && basic.AngleToSpeedMultiplierCurve.Evaluate(1f) == 2f, "real curve endpoints", ref checks);
                Require(Mathf.Abs(basic.AngleToSpeedMultiplierCurve.Evaluate(0.5f) - 1.5f) < 0.00001f, "real curve midpoint", ref checks);
                Require(basic.RotationSpeed == 150f, "real rotation default150", ref checks);
                Require(basic.TimeoutSeconds == 3f, "real inherited timeout3", ref checks);
            }
            finally
            {
                // Attempt each owned cleanup even if an earlier destruction fails.
                try { if (!ReferenceEquals(basic, null)) UnityEngine.Object.DestroyImmediate(basic); }
                finally { try { if (!ReferenceEquals(overrides, null)) UnityEngine.Object.DestroyImmediate(overrides); }
                    finally { if (!ReferenceEquals(definition, null)) UnityEngine.Object.DestroyImmediate(definition); } }
            }
            return checks;
        }
        private static AxisState SeedFromDefinition(CameraAxisStateDefinition definition)
        { AxisState axis = Seed(); definition.Set(ref axis); return axis; }
    }
}
