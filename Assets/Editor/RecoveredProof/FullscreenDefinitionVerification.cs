using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using D = HardlightProject.FullscreenShaderParametersDefinition;
using G = HardlightProject.FullscreenShaderParametersDefinitionGroup;

namespace ProjectLucid
{
    // Bounded original parameter-family proof. Engine fixtures own their objects;
    // no supplied asset, renderer, shader, or application startup is exercised.
    public static class FullscreenDefinitionVerification
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Fullscreen definition verification: " + message);
            ++checks;
        }
        private static FieldInfo Field(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo found = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (found != null) return found;
            }
            throw new MissingFieldException(type.FullName, name);
        }
        private static object Get(object value, string name) => Field(value.GetType(), name).GetValue(value);
        private static void Set(object value, string name, object data) => Field(value.GetType(), name).SetValue(value, data);
        private static object Call(object value, string name, params object[] arguments)
        {
            for (Type current = value.GetType(); current != null; current = current.BaseType)
            {
                MethodInfo found = current.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (found != null) return found.Invoke(value, arguments);
            }
            throw new MissingMethodException(value.GetType().FullName, name);
        }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static bool SameColour(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        private static readonly string[] LimitFields = {
            "m_fadeAmount", "m_vignetteAmount", "m_vignetteUVScale", "m_vignetteOpacity",
            "m_vignetteColourBoost", "m_flowMapStrength", "m_flowMapSpeed", "m_edgeDistortionAmount",
            "m_speedLinesOpacity", "m_speedLinesAmount", "m_speedLinesScale", "m_speedLinesSpeed"
        };
        private static readonly float[] Minima = { 0, 0, 0, 0, 0, -1, -1, float.NegativeInfinity, 0, 0, 0, -2 };
        private static readonly float[] Maxima = { 1, 2, 2, 1, 2, 1, 1, float.PositiveInfinity, 1, float.PositiveInfinity, 0.2f, 2 };
        private static void Populate(D definition)
        {
            foreach (FieldInfo field in typeof(D).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (typeof(D.FullscreenShaderParameter).IsAssignableFrom(field.FieldType))
                    field.SetValue(definition, Activator.CreateInstance(field.FieldType));
            }
        }
        public static int RunManaged()
        {
            checks = 0;
            // Raw ScriptableObject fixtures below inspect managed fields only.
            // Their constructors/lifecycle/serialization are covered separately.
            D raw = Raw<D>();
            string[] properties = { "FullscreenShaderParametersType", "Priority", "FadeColour", "FadeAmount", "VignetteTexture", "VignetteUVScale", "VignetteAmount", "VignetteOpacity", "VignetteTint", "VignetteColourBoost", "FlowMapStrength", "FlowMapSpeed", "EdgeDistortionAmount", "SpeedLinesTexture", "SpeedLinesOpacity", "SpeedLinesAmount", "SpeedLinesScale", "SpeedLinesSpeed", "SpeedLinesColourTint" };
            string[] fields = { "m_fullscreenShaderType", "m_priority", "m_fadeColour", "m_fadeAmount", "m_vignetteTexture", "m_vignetteUVScale", "m_vignetteAmount", "m_vignetteOpacity", "m_vignetteTint", "m_vignetteColourBoost", "m_flowMapStrength", "m_flowMapSpeed", "m_edgeDistortionAmount", "m_speedLinesTexture", "m_speedLinesOpacity", "m_speedLinesAmount", "m_speedLinesScale", "m_speedLinesSpeed", "m_speedLinesColourTint" };
            Populate(raw);
            Set(raw, "m_priority", 317);
            Set(raw, "m_fullscreenShaderType", Enum.ToObject(typeof(HardlightProject.FullscreenShaderParametersType), 1));
            for (int i = 0; i < properties.Length; ++i)
                Check(Equals(typeof(D).GetProperty(properties[i]).GetValue(raw), Get(raw, fields[i])), "getter projects original field " + properties[i]);
            object[] parameters = { new D.FullscreenShaderParameterFloat(), new D.FullscreenShaderParameterColour(), new D.FullscreenShaderParameterTexture(), new D.FullscreenShaderParameterHDRColour() };
            foreach (object parameter in parameters)
            {
                Check(!(bool)Get(parameter, "m_overrideEnabled") && Get(parameter, "m_weightOverTime") == null, "native base-only constructor preserves disabled/null curve defaults");
                Check(Equals(Get(parameter, "m_minLimit"), Get(parameter, "m_maxLimit")), "native generic constructor default limits");
            }
            D.FullscreenShaderParameterFloat f = new D.FullscreenShaderParameterFloat();
            Check(f.Evaluate(7, 0, false) == 0, "disabled float still clamps incoming base to default limits before override gate");
            Set(f, "m_value", 9f); f.SetLimits(-2, 3);
            Check((float)Get(f, "m_value") == 3 && (float)Get(f, "m_minLimit") == -2 && (float)Get(f, "m_maxLimit") == 3, "SetLimits writes both limits then normalizes existing value");
            Check(f.Evaluate(-7, 0, true) == -2 && f.Evaluate(7, 0, false) == 3, "disabled override tolerates null curve and clamps both ends");
            Check(f.Evaluate(1.5f, float.NaN, true) == 1.5f, "disabled override ignores time entirely");
            Check(float.IsNaN(f.Evaluate(float.NaN, 0, false)), "native ordered float comparisons preserve NaN");
            f.SetLimits(2, -2);
            Check(f.Evaluate(0, 0, false) == 2 && f.Evaluate(3, 0, false) == -2, "reversed limits preserve original lower-test-first branch");
            Check((float)Call(f, "Lerp", -2f, 6f, -1f) == -2 && (float)Call(f, "Lerp", -2f, 6f, 2f) == 6, "float Lerp clamps weight");
            Check(float.IsNaN((float)Call(f, "Lerp", -2f, 6f, float.NaN)), "float Lerp retains NaN weight");
            D.FullscreenShaderParameterColour colour = new D.FullscreenShaderParameterColour();
            Color stored = new Color(-2, 5, 9, 3), baseValue = new Color(8, -4, 2, -1);
            Set(colour, "m_value", stored); colour.SetLimits(Color.white, Color.black);
            Check(SameColour((Color)Get(colour, "m_value"), stored), "default virtual colour limit enforcement is identity");
            Check(SameColour(colour.Evaluate(baseValue, float.NaN, true), baseValue), "disabled colour projects incoming value with no channel clamping");
            Check(SameColour((Color)Call(colour, "Lerp", Color.red, Color.blue, -1f), Color.red), "colour Lerp clamps negative weight");
            Check(SameColour((Color)Call(colour, "Lerp", Color.red, Color.blue, 2f), Color.blue), "colour Lerp clamps high weight");
            D.FullscreenShaderParameterHDRColour hdr = new D.FullscreenShaderParameterHDRColour();
            Color hdrResult = (Color)Call(hdr, "Lerp", new Color(-2, 4, 10, 2), new Color(2, 8, -4, -2), 0.5f);
            Check(SameColour(hdrResult, new Color(0, 6, 3, 0)), "HDR uses original Color.Lerp and preserves out-of-unit channels");
            D.FullscreenShaderParameterTexture texture = new D.FullscreenShaderParameterTexture();
            Check(texture.Evaluate(null, float.NaN, true) == null, "disabled texture does not touch absent curve");
            Check(Call(texture, "Lerp", null, null, float.NaN) == null, "texture Lerp accepts NaN weight without scalar work");
            foreach (string field in LimitFields) Set(Get(raw, field), "m_value", float.PositiveInfinity);
            Call(raw, "OnValidate");
            for (int i = 0; i < LimitFields.Length; ++i)
            {
                object value = Get(raw, LimitFields[i]);
                Check((float)Get(value, "m_minLimit") == Minima[i] && (float)Get(value, "m_maxLimit") == Maxima[i], "native validation exact limits " + LimitFields[i]);
                Check((float)Get(value, "m_value") == Maxima[i], "validation immediately enforces limits " + LimitFields[i]);
            }
            D interrupted = Raw<D>(); Populate(interrupted);
            Set(Get(interrupted, "m_fadeAmount"), "m_value", 5f);
            Set(Get(interrupted, "m_vignetteUVScale"), "m_value", 5f);
            Set(interrupted, "m_vignetteAmount", null);
            bool threw = false;
            try { Call(interrupted, "OnValidate"); }
            catch (TargetInvocationException e) when (e.InnerException is NullReferenceException) { threw = true; }
            Check(threw && (float)Get(Get(interrupted, "m_fadeAmount"), "m_value") == 1, "validation preserves completed fade mutation before failing subsequent missing parameter");
            Check((float)Get(Get(interrupted, "m_vignetteUVScale"), "m_value") == 5, "native vignette amount validation precedes UV scale");
            G group = Raw<G>();
            Check(Equals(Call(group, "GetElementKey", raw), raw.FullscreenShaderParametersType), "group uses genuine definition type key");
            Check(ReferenceEquals(Call(group, "GetKeyComparer"), HardlightProject.HardlightEnumComparers.FullscreenShaderParametersTypeComparer), "group uses maintained original comparer registry");
            return checks;
        }
        private static void DestroyOwned(IReadOnlyList<UnityEngine.Object> values, int index)
        {
            if (index < 0) return;
            try { if (values[index] != null) UnityEngine.Object.DestroyImmediate(values[index]); }
            finally { DestroyOwned(values, index - 1); }
        }
        public static int RunEngine()
        {
            checks = 0;
            List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            try
            {
                D definition = ScriptableObject.CreateInstance<D>(); owned.Add(definition);
                G group = ScriptableObject.CreateInstance<G>(); owned.Add(group);
                Populate(definition);
                Texture2D from = new Texture2D(1, 1); owned.Add(from);
                Texture2D to = new Texture2D(1, 1); owned.Add(to);
                AnimationCurve curve = AnimationCurve.Linear(0, 0, 2, 1);
                D.FullscreenShaderParameterFloat f = definition.FadeAmount;
                Set(f, "m_value", 10f); Set(f, "m_overrideEnabled", true); Set(f, "m_weightOverTime", curve); f.SetLimits(-20, 20);
                Check(f.Evaluate(2, 0.25f, true) == 4, "actual curve normalizes time by final key duration");
                Check(f.Evaluate(2, 0.25f, false) == 3, "actual curve uses supplied time without normalization");
                Check(f.Evaluate(50, 0, false) == 20, "base enforcement occurs before interpolation at zero weight");
                Set(f, "m_weightOverTime", AnimationCurve.Constant(0, 2, -3));
                Check(f.Evaluate(2, 0.5f, false) == 2, "negative curve weight clamps to zero");
                Set(f, "m_weightOverTime", AnimationCurve.Constant(0, 2, 4));
                Check(f.Evaluate(2, 0.5f, false) == 10, "high curve weight clamps to one");
                Set(f, "m_weightOverTime", new AnimationCurve());
                Check(f.Evaluate(2, 1, true) == 2, "empty curve skips last-key indexing with normalization enabled");
                Set(f, "m_overrideEnabled", false); Set(f, "m_weightOverTime", null);
                Check(f.Evaluate(8, 1, true) == 8, "disabled original curve gate tolerates missing curve on actual parameter");
                Set(f, "m_overrideEnabled", true);
                bool nullCurve = false;
                try { f.Evaluate(2, 1, false); } catch (NullReferenceException) { nullCurve = true; }
                Check(nullCurve, "enabled original path reads curve length even without normalization");
                D.FullscreenShaderParameterColour colour = definition.FadeColour;
                Set(colour, "m_overrideEnabled", true); Set(colour, "m_weightOverTime", curve); Set(colour, "m_value", new Color(4, 8, -4, 2));
                colour.SetLimits(Color.white, Color.black);
                Check(SameColour(colour.Evaluate(new Color(0, 0, 4, -2), 0.5f, true), new Color(2, 4, 0, 0)), "actual curve colour interpolation preserves unclamped channels");
                D.FullscreenShaderParameterHDRColour hdr = definition.SpeedLinesColourTint;
                Set(hdr, "m_overrideEnabled", true); Set(hdr, "m_weightOverTime", curve); Set(hdr, "m_value", new Color(6, 10, -6, 4));
                Check(SameColour(hdr.Evaluate(new Color(2, 2, 2, 0), 1, false), new Color(4, 6, -2, 2)), "actual HDR curve interpolation");
                D.FullscreenShaderParameterTexture texture = definition.VignetteTexture;
                Set(texture, "m_value", to); Set(texture, "m_weightOverTime", AnimationCurve.Constant(0, 2, 0)); Set(texture, "m_overrideEnabled", true);
                Check(ReferenceEquals(texture.Evaluate(from, 1, false), to), "enabled texture override selects destination even with zero curve weight");
                Set(texture, "m_overrideEnabled", false); Set(texture, "m_weightOverTime", null);
                Check(ReferenceEquals(texture.Evaluate(from, 1, true), from), "disabled texture selects actual input reference without reading curve");
                Check(ReferenceEquals(Call(texture, "Lerp", from, to, float.NaN), from), "disabled texture Lerp branch ignores NaN weight");
                Set(texture, "m_overrideEnabled", true);
                Check(ReferenceEquals(Call(texture, "Lerp", from, to, float.NaN), to), "enabled texture Lerp branch ignores NaN weight");
                Set(definition, "m_priority", -317);
                string json = JsonUtility.ToJson(definition);
                Check(json.Contains("m_priority") && json.Contains("m_fadeAmount") && json.Contains("m_overrideEnabled") && json.Contains("m_weightOverTime"), "actual Unity serializer retains original outer/nested fields");
                Check(!json.Contains("FullscreenShaderParametersType") && !json.Contains("OverrideEnabled"), "original getter APIs do not create serialized fields");
                JsonUtility.FromJsonOverwrite("{\"m_priority\":29,\"m_fullscreenShaderType\":1}", definition);
                Check(definition.Priority == 29 && Convert.ToInt32(definition.FullscreenShaderParametersType) == 1, "actual Unity overwrite uses original fields");
                Call(definition, "OnValidate");
                Check((float)Get(definition.FlowMapStrength, "m_minLimit") == -1 && (float)Get(definition.FlowMapSpeed, "m_maxLimit") == 1, "native flow ranges remain distinct from tooltip wording");
                Check((float)Get(definition.VignetteColourBoost, "m_minLimit") == 0 && (float)Get(definition.SpeedLinesScale, "m_maxLimit") == 0.2f, "native boost/scale ranges on actual ScriptableObject");
                Check(ReferenceEquals(Call(group, "GetKeyComparer"), HardlightProject.HardlightEnumComparers.FullscreenShaderParametersTypeComparer), "actual group resolves maintained original registry");
                return checks;
            }
            finally { DestroyOwned(owned, owned.Count - 1); }
        }
        public static void Run()
        {
            Debug.Log("Project Lucid bounded fullscreen parameter checks: managed=" + RunManaged() + "; engine=" + RunEngine());
        }
    }
}
