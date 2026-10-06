using System;
using System.Reflection;
using System.Runtime.Serialization;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    public static class HalfPipeTrajectoryVerification
    {
        private static void Require(bool condition, string label, ref int checks)
        { if (!condition) throw new InvalidOperationException(label); ++checks; }
        private static FieldInfo SpeedField => typeof(HalfPipeTrajectoryDefinition).GetField("m_trajectorySpeedMin", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        private static FieldInfo CurveField => typeof(HalfPipeTrajectoryDefinition).GetField("m_trajectoryFromAngleCurve", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        public static int RunManaged()
        {
            int checks = 0;
            Type type = typeof(HalfPipeTrajectoryDefinition);
            Require(type.IsPublic && type.BaseType == typeof(ScriptableObject), "full original public SO type", ref checks);
            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Require(fields.Length == 2 && fields[0].Name == "m_trajectorySpeedMin" && fields[1].Name == "m_trajectoryFromAngleCurve", "full ordered two original fields", ref checks);
            Require(SpeedField.IsPrivate && SpeedField.FieldType == typeof(float) && CurveField.IsPrivate && CurveField.FieldType == typeof(AnimationCurve), "exact original mutable field types", ref checks);
            Require(!SpeedField.IsInitOnly && !CurveField.IsInitOnly && !SpeedField.IsStatic && !CurveField.IsStatic, "original mutable instance flags", ref checks);
            Require(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 2, "original getter and evaluation methods only", ref checks);
            Require(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length == 1, "sole original public constructor", ref checks);
            Require(type.GetProperty("SpeedMin").CanRead && !type.GetProperty("SpeedMin").CanWrite, "original read-only property", ref checks);
            var instance = (HalfPipeTrajectoryDefinition)FormatterServices.GetUninitializedObject(type);
            Require(BitConverter.SingleToInt32Bits(instance.SpeedMin) == 0, "constructor-free zero scalar state; engine constructor separate", ref checks);
            Require(CurveField.GetValue(instance) == null, "constructor-free curve zero state only", ref checks);
            foreach (int bits in new[] { 0, unchecked((int)0x80000000), unchecked((int)0x7f800000), unchecked((int)0xff800000), unchecked((int)0x7fc00123), unchecked((int)0x3f800000), unchecked((int)0xbf800000), unchecked((int)0x43b40000) })
            {
                float value = BitConverter.Int32BitsToSingle(bits);
                SpeedField.SetValue(instance, value);
                Require(BitConverter.SingleToInt32Bits(instance.SpeedMin) == bits, "raw original scalar bit-preserving getter", ref checks);
            }
            SpeedField.SetValue(instance, -19f);
            Require(instance.SpeedMin == -19f, "negative scalar is not clamped", ref checks);
            SpeedField.SetValue(instance, 37f);
            Require(instance.SpeedMin == 37f, "changed scalar reloads original field", ref checks);
            return checks;
        }
        // Actual Unity only: real constructor curve and serializable engine data.
        public static int RunEngine()
        {
            int checks = 0;
            HalfPipeTrajectoryDefinition instance = null;
            try
            {
                instance = ScriptableObject.CreateInstance<HalfPipeTrajectoryDefinition>();
                Require(instance != null, "genuine original SO constructor", ref checks);
                Require(instance.SpeedMin == 0f, "original constructor leaves minimum speed zero", ref checks);
                var original = (AnimationCurve)CurveField.GetValue(instance);
                Require(original != null && original.length == 2, "original constructor creates real constant curve", ref checks);
                var keys = original.keys;
                Require(keys[0].time == 0f && keys[1].time == 360f && keys[0].value == 360f && keys[1].value == 360f, "original constant curve exact key times and values", ref checks);
                foreach (float angle in new[] { -1f, 0f, 40f, 95f, 180f, 360f, 721f })
                    Require(instance.TrajectoryFromAngle(angle) == 360f, "default original curve constant through real Evaluate", ref checks);
                var custom = AnimationCurve.Linear(-5f, -19f, 15f, 37f);
                custom.preWrapMode = WrapMode.Loop;
                custom.postWrapMode = WrapMode.PingPong;
                CurveField.SetValue(instance, custom);
                foreach (float angle in new[] { -45f, -5f, 0f, 5f, 15f, 35f })
                    Require(instance.TrajectoryFromAngle(angle).Equals(custom.Evaluate(angle)), "angle forwards unchanged including original wrap behavior", ref checks);
                SpeedField.SetValue(instance, -13.25f);
                Require(instance.SpeedMin == -13.25f, "authored negative minimum speed retained", ref checks);
                string json = JsonUtility.ToJson(instance);
                Require(json.Contains("\"m_trajectorySpeedMin\"") && json.Contains("\"m_trajectoryFromAngleCurve\""), "actual two private original serialized fields", ref checks);
                SpeedField.SetValue(instance, 77f);
                CurveField.SetValue(instance, AnimationCurve.Constant(0f, 1f, 7f));
                JsonUtility.FromJsonOverwrite(json, instance);
                Require(instance.SpeedMin == -13.25f, "actual scalar roundtrip", ref checks);
                Require(instance.TrajectoryFromAngle(5f).Equals(custom.Evaluate(5f)), "actual curve data roundtrip", ref checks);
                Require(JsonUtility.ToJson(instance) == json, "actual original two-field serialization stable", ref checks);
            }
            finally { if (instance != null) UnityEngine.Object.DestroyImmediate(instance); }
            return checks;
        }
    }
}
