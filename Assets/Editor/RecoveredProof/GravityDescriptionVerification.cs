using System;
using System.Reflection;
using System.Runtime.Serialization;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class GravityDescriptionVerification
    {
        private static void Require(bool value, string label, ref int count)
        { if (!value) throw new InvalidOperationException("GravityDescription: " + label); ++count; }
        private static FieldInfo Field(string name) => typeof(GravityDescription).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        private static float Read(GravityDescription value, string name) => (float)Field(name).GetValue(value);
        private static void Write(GravityDescription value, string name, float input) => Field(name).SetValue(value, input);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static void Same(float actual, float expected, string label, ref int count)
        { Require(float.IsNaN(expected) ? float.IsNaN(actual) : Bits(actual) == Bits(expected), label, ref count); }

        // These checks do not request static fields or execute the Physics cctor.
        public static int RunManaged()
        {
            int n = 0;
            Type t = typeof(GravityDescription);
            Require(t.IsSerializable && t.BaseType == typeof(object), "original serializable class", ref n);
            Require(t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 16, "sixteen own fields", ref n);
            Require(t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 6, "six original properties", ref n);
            foreach (string name in new[] { "InnerFalloffDistanceDefault", "InnerDistanceDefault", "OuterDistanceDefault", "OuterFalloffDistanceDefault", "DistanceTolerance" })
                Require(Field(name).IsLiteral && Field(name).FieldType == typeof(float), "original Single literal " + name, ref n);
            Same((float)Field("InnerFalloffDistanceDefault").GetRawConstantValue(), 0f, "inner falloff default", ref n);
            Same((float)Field("InnerDistanceDefault").GetRawConstantValue(), 0f, "inner default", ref n);
            Same((float)Field("OuterDistanceDefault").GetRawConstantValue(), 10f, "outer default", ref n);
            Same((float)Field("OuterFalloffDistanceDefault").GetRawConstantValue(), 15f, "outer falloff default", ref n);
            Same((float)Field("DistanceTolerance").GetRawConstantValue(), 0.01f, "original tolerance bits", ref n);
            foreach (string name in new[] { "GravityDefault", "GizmoColourFalloff", "GizmoColourFullStrength" })
                Require(Field(name).IsPublic && Field(name).IsStatic && Field(name).IsInitOnly, "original static readonly " + name, ref n);
            foreach (string name in new[] { "m_gravity", "m_innerFalloffDistance", "m_innerDistance", "m_outerDistance", "m_outerFalloffDistance" })
                Require(Field(name).IsPrivate && Field(name).IsDefined(typeof(SerializeField), false), "serialized private " + name, ref n);
            foreach (string name in new[] { "m_innerFalloffFactor", "m_outerFalloffFactor", "<Distance>k__BackingField" })
                Require(Field(name).IsPrivate && !Field(name).IsDefined(typeof(SerializeField), false), "original transient " + name, ref n);
            PropertyInfo distance = t.GetProperty("Distance");
            Require(distance.GetGetMethod().IsPublic && distance.GetSetMethod(true).IsPrivate, "public query/private distance setter", ref n);
            Require(Field("<Distance>k__BackingField").IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false), "natural backing field", ref n);
            return n;
        }

        // Actual instance methods; the raw host path bypasses construction only.
        private static int RunScalar(GravityDescription d)
        {
            int n = 0;
            d.Set(-8f, 0f, 2f, 10f, 15f);
            Same(d.MaxDistance, 15f, "maximum follows outer falloff", ref n);
            Same(Read(d, "m_innerFalloffFactor"), 0.5f, "inner positive reciprocal", ref n);
            Same(Read(d, "m_outerFalloffFactor"), 0f, "original reversed outer guard", ref n);
            float[] queries = { -1f, 0f, 1f, 2f, 10f, 12f, 15f, 16f, float.NaN, float.NegativeInfinity, float.PositiveInfinity };
            float[] results = { 0f, -0f, -4f, -8f, -8f, -8f, -8f, 0f, -8f, 0f, 0f };
            for (int i = 0; i < queries.Length; ++i)
            {
                Same(d.CalculateGravity(queries[i]), results[i], "genuine query " + i, ref n);
                Same(d.Distance, queries[i], "query cache even on exit " + i, ref n);
            }
            d.Set(-8f, 4f, 2f, 1f, 3f);
            Same(d.InnerFalloffDistance, 4f, "retain authored inner falloff", ref n);
            Same(d.InnerDistance, 4f, "clamp inner", ref n); Same(d.OuterDistance, 4f, "clamp outer", ref n);
            Same(d.OuterFalloffDistance, 4f, "clamp outer falloff", ref n);
            Same(Read(d, "m_innerFalloffFactor"), 0f, "factor uses preclamp inputs", ref n);
            d.Set(-8f, float.NaN, 2f, 10f, 15f);
            Require(float.IsNaN(d.InnerDistance) && float.IsNaN(d.OuterDistance) && float.IsNaN(d.OuterFalloffDistance), "ordered Max second argument NaN", ref n);
            Same(Read(d, "m_innerFalloffFactor"), 0f, "unordered inner factor zero", ref n);
            Same(Read(d, "m_outerFalloffFactor"), 0f, "unordered outer factor zero", ref n);
            Same(d.CalculateGravity(99f), -8f, "unordered bounds return authored gravity", ref n);
            d.Set(-8f, 0f, float.NaN, 10f, 15f);
            Same(d.InnerDistance, 0f, "ordered Max first argument NaN selects second", ref n);
            Same(Read(d, "m_innerFalloffFactor"), 0f, "unordered original factor input", ref n);
            d.Set(-8f, 0f, 2f, 10f, float.PositiveInfinity);
            Require(float.IsNaN(d.CalculateGravity(float.PositiveInfinity)), "infinity times zero retained", ref n);
            Same(d.Distance, float.PositiveInfinity, "infinite query cache", ref n);
            Write(d, "m_innerFalloffDistance", 4f); Write(d, "m_innerDistance", 2f);
            Write(d, "m_outerDistance", 1f); Write(d, "m_outerFalloffDistance", 3f);
            d.Validate();
            Same(d.InnerDistance, 4f, "Validate clamps serialized inner", ref n);
            Same(d.OuterDistance, 4f, "Validate clamps serialized outer", ref n);
            Same(d.OuterFalloffDistance, 4f, "Validate clamps serialized outer falloff", ref n);
            Same(Read(d, "m_innerFalloffFactor"), 0f, "Validate preclamp factor", ref n);
            Same(Read(d, "m_outerFalloffFactor"), 0f, "Validate original outer guard", ref n);
            d.Set(-8f, -0f, +0f, -0f, +0f);
            Same(d.InnerDistance, -0f, "equal signed zeros retain second Max operand inner", ref n);
            Same(d.OuterDistance, -0f, "equal signed zeros retain second Max operand outer", ref n);
            Same(d.OuterFalloffDistance, -0f, "equal signed zeros retain second Max operand falloff", ref n);
            Same(d.CalculateGravity(-0f), -8f, "signed zero query retains full gravity", ref n);
            Same(d.Distance, -0f, "negative zero query cache", ref n);
            return n;
        }

        // Public API only: standalone reflection field access itself requests the
        // real Physics cctor on .NET, so it is excluded from this bounded path.
        private static int RunRawPublicScalar(GravityDescription d)
        {
            int n = 0;
            d.Set(-8f, 0f, 2f, 10f, 15f);
            Same(d.MaxDistance, 15f, "maximum follows outer falloff", ref n);
            float[] queries = { -1f, 0f, 1f, 2f, 10f, 12f, 15f, 16f, float.NaN, float.NegativeInfinity, float.PositiveInfinity };
            float[] results = { 0f, -0f, -4f, -8f, -8f, -8f, -8f, 0f, -8f, 0f, 0f };
            for (int i = 0; i < queries.Length; ++i)
            {
                Same(d.CalculateGravity(queries[i]), results[i], "genuine query " + i, ref n);
                Same(d.Distance, queries[i], "query cache even on exit " + i, ref n);
            }
            d.Set(-8f, 4f, 2f, 1f, 3f);
            Same(d.InnerFalloffDistance, 4f, "retain authored inner falloff", ref n);
            Same(d.InnerDistance, 4f, "clamp inner", ref n); Same(d.OuterDistance, 4f, "clamp outer", ref n);
            Same(d.OuterFalloffDistance, 4f, "clamp outer falloff", ref n);
            d.Set(-8f, float.NaN, 2f, 10f, 15f);
            Require(float.IsNaN(d.InnerDistance) && float.IsNaN(d.OuterDistance) && float.IsNaN(d.OuterFalloffDistance), "ordered Max second argument NaN", ref n);
            Same(d.CalculateGravity(99f), -8f, "unordered bounds return authored gravity", ref n);
            d.Set(-8f, 0f, float.NaN, 10f, 15f);
            Same(d.InnerDistance, 0f, "ordered Max first argument NaN selects second", ref n);
            d.Set(-8f, 0f, 2f, 10f, float.PositiveInfinity);
            Require(float.IsNaN(d.CalculateGravity(float.PositiveInfinity)), "infinity times zero retained", ref n);
            Same(d.Distance, float.PositiveInfinity, "infinite query cache", ref n);
            d.Set(-8f, 4f, 2f, 1f, 3f);
            d.Validate();
            Same(d.InnerDistance, 4f, "Validate retains already clamped inner", ref n);
            Same(d.OuterDistance, 4f, "Validate retains already clamped outer", ref n);
            Same(d.OuterFalloffDistance, 4f, "Validate retains already clamped outer falloff", ref n);
            d.Set(-8f, -0f, +0f, -0f, +0f);
            Same(d.InnerDistance, -0f, "equal signed zeros retain second Max operand inner", ref n);
            Same(d.OuterDistance, -0f, "equal signed zeros retain second Max operand outer", ref n);
            Same(d.OuterFalloffDistance, -0f, "equal signed zeros retain second Max operand falloff", ref n);
            Same(d.CalculateGravity(-0f), -8f, "signed zero query retains full gravity", ref n);
            Same(d.Distance, -0f, "negative zero query cache", ref n);
            return n;
        }

        // No substituted Physics API: this isolated probe records host limitations.
        public static void ProbeRawInstance()
        {
            try
            {
                var d = (GravityDescription)FormatterServices.GetUninitializedObject(typeof(GravityDescription));
                int n = RunRawPublicScalar(d);
                Console.WriteLine("RAW_HOST_PASS checks=" + n);
            }
            catch (Exception ex)
            {
                Exception inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                if (!(ex is TypeInitializationException) || !(inner is MissingMethodException)) throw;
                Console.WriteLine("RAW_HOST_BLOCKED " + ex.GetType().FullName + " / " + inner.GetType().FullName + " / " + inner.Message);
            }
        }

        public static int RunEngine()
        {
            int n = 0;
            Require(!float.IsNaN(GravityDescription.GravityDefault), "real Physics-derived static gravity", ref n);
            Color falloff = GravityDescription.GizmoColourFalloff, full = GravityDescription.GizmoColourFullStrength;
            Same(falloff.r, 0f, "cyan red", ref n); Same(falloff.g, 1f, "cyan green", ref n);
            Same(falloff.b, 1f, "cyan blue", ref n); Same(falloff.a, 1f, "cyan alpha", ref n);
            Same(full.r, 1f, "yellow red", ref n); Same(full.g, 0.9215686321258545f, "yellow green", ref n);
            Same(full.b, 0.01568627543747425f, "yellow blue", ref n); Same(full.a, 1f, "yellow alpha", ref n);
            var d = new GravityDescription();
            Same(Read(d, "m_gravity"), GravityDescription.GravityDefault, "constructor static gravity", ref n);
            Same(d.InnerFalloffDistance, 0f, "constructor inner falloff", ref n);
            Same(d.InnerDistance, 0f, "constructor inner", ref n);
            Same(d.OuterDistance, 10f, "constructor outer", ref n);
            Same(d.OuterFalloffDistance, 15f, "constructor outer falloff", ref n);
            Same(d.MaxDistance, 15f, "maximum is outer falloff", ref n);
            Same(Read(d, "m_innerFalloffFactor"), 0f, "constructor inner factor not validated", ref n);
            Same(Read(d, "m_outerFalloffFactor"), 0f, "constructor outer factor not validated", ref n);
            Same(d.Distance, 0f, "constructor query cache zero", ref n);
            n += RunScalar(d);
            d.Set(-12f, 1f, 3f, 7f, 9f); d.CalculateGravity(2f);
            string json = JsonUtility.ToJson(d);
            var roundtrip = JsonUtility.FromJson<GravityDescription>(json);
            Same(Read(roundtrip, "m_gravity"), -12f, "JSON authored gravity", ref n);
            Same(roundtrip.InnerFalloffDistance, 1f, "JSON inner falloff", ref n);
            Same(roundtrip.InnerDistance, 3f, "JSON inner", ref n);
            Same(roundtrip.OuterDistance, 7f, "JSON outer", ref n);
            Same(roundtrip.OuterFalloffDistance, 9f, "JSON outer falloff", ref n);
            Require(!json.Contains("FalloffFactor") && !json.Contains("Distance>"), "transient factors/query omitted", ref n);
            roundtrip.Validate(); Same(roundtrip.CalculateGravity(2f), -6f, "JSON Validate restores inner behavior", ref n);
            Require(JsonUtility.ToJson(roundtrip) == json, "serialized shape stable after Validate/query", ref n);
            return n;
        }
    }
}
