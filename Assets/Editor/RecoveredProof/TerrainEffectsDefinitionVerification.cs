using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    public static class TerrainEffectsDefinitionVerification
    {
        private static int checks;
        private static readonly BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Own) ?? throw new InvalidOperationException(name);
        private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Own) ?? throw new InvalidOperationException(name);
        private static void Check(bool condition, string meaning)
        {
            if (!condition) throw new InvalidOperationException("Terrain definition verification: " + meaning);
            ++checks;
        }
        private static void Set(object value, string name, object data) => Field(value.GetType(), name).SetValue(value, data);
        private static object Call(object value, string name, params object[] args)
        {
            try { return Method(value.GetType(), name).Invoke(value, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
        private static T Uninitialised<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return true; }
            return false;
        }

        public static int RunManaged()
        {
            checks = 0;
            var value = Uninitialised<ActorTerrainEffectsDefinition>();
            var group = Uninitialised<ActorTerrainEffectsDefinitionGroup>();
            var actorAnimation = Uninitialised<ActorAnimationDefinition>();
            var impactAnimation = Uninitialised<ActorAnimationDefinition>();
            Check(value.Name == null && Bits(value.MinimumImpactSpeedSqr) == 0, "native base-only default state");
            Check(value.TerrainEffectType.Equals((TerrainEffectType)0), "zero enum field remains unnormalised");
            Check(ReferenceEquals(value.ActorAnimationDefinition, null) && ReferenceEquals(value.ImpactAnimationDefinition, null), "default animation fields remain null");
            Set(value, "m_terrainEffectType", TerrainEffectType.Grass);
            Set(value, "m_actorAnimationDefinition", actorAnimation);
            Set(value, "m_impactAnimationDefinition", impactAnimation);
            Check(value.TerrainEffectType == TerrainEffectType.Grass, "direct authored terrain key");
            Check(ReferenceEquals(value.ActorAnimationDefinition, actorAnimation), "direct actor animation reference");
            Check(ReferenceEquals(value.ImpactAnimationDefinition, impactAnimation), "direct distinct impact animation reference");
            Check((TerrainEffectType)Call(group, "GetElementKey", value) == TerrainEffectType.Grass, "group uses original authored key");
            Check(ReferenceEquals(Call(group, "GetKeyComparer"), HardlightEnumComparers.TerrainEffectTypeComparer), "group shares original comparer identity");
            Set(value, "m_terrainEffectType", TerrainEffectType.ImpactDream);
            Check((TerrainEffectType)Call(group, "GetElementKey", value) == TerrainEffectType.ImpactDream, "group reloads current key");
            Check(Throws<NullReferenceException>(() => Call(group, "GetElementKey", (object)null)), "managed null input fails without invented fallback");

            Set(value, "m_minimumImpactSpeed", -3.0f);
            Call(value, "UpdateCachedValues");
            Check(value.MinimumImpactSpeedSqr == 9.0f, "negative speed is squared rather than clamped");
            value.Name = "retained lifecycle name";
            Set(value, "m_minimumImpactSpeed", 7.0f);
            Call(value, "OnValidate");
            Check(value.MinimumImpactSpeedSqr == 49.0f && value.Name == "retained lifecycle name", "validation refreshes only squared cache");
            Set(value, "m_minimumImpactSpeed", 1.5f);
            Call(value, "Awake");
            Check(value.MinimumImpactSpeedSqr == 2.25f && value.Name == "retained lifecycle name", "awake refreshes only squared cache");
            foreach (var fixture in new[] {
                new float[] { -4.5f, 20.25f }, new float[] { -0.0f, 0.0f },
                new float[] { 1.0e-23f, 0.0f }, new float[] { 2.0e20f, float.PositiveInfinity },
                new float[] { float.NegativeInfinity, float.PositiveInfinity } })
            {
                Set(value, "m_minimumImpactSpeed", fixture[0]);
                Call(value, "UpdateCachedValues");
                Check(Bits(value.MinimumImpactSpeedSqr) == Bits(fixture[1]), "Single square input " + fixture[0]);
            }
            Set(value, "m_minimumImpactSpeed", float.NaN);
            Call(value, "UpdateCachedValues");
            Check(float.IsNaN(value.MinimumImpactSpeedSqr), "NaN is retained by multiplication");

            FieldInfo mapField = typeof(HardlightEnumExtensions).GetField("TerrainEffectTypeEnumToString", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (mapField == null) throw new InvalidOperationException("Original terrain string map missing");
            object oldMap = mapField.GetValue(null);
            var map = new Dictionary<TerrainEffectType, string>(HardlightEnumComparers.TerrainEffectTypeComparer);
            try
            {
                mapField.SetValue(null, map);
                map[TerrainEffectType.Grass] = "current terrain label";
                Set(value, "m_terrainEffectType", TerrainEffectType.Grass);
                Set(value, "m_minimumImpactSpeed", 200.0f);
                Set(value, "<MinimumImpactSpeedSqr>k__BackingField", -11.0f);
                value.Name = "old label";
                value.OnBeforeSerialize();
                Check(value.Name == "current terrain label", "before-serialize uses original mutable enum registry");
                Check(value.MinimumImpactSpeedSqr == -11.0f, "before-serialize does not refresh squared cache");
                map[TerrainEffectType.Grass] = "second label";
                value.OnAfterDeserialize();
                Check(value.Name == "second label", "after-deserialize reloads current map entry");
                Check(value.MinimumImpactSpeedSqr == -11.0f, "after-deserialize leaves cache as it stands");
                map[TerrainEffectType.Grass] = null;
                value.OnBeforeSerialize();
                Check(value.Name == null, "mapped null name is assigned without replacement");
                Set(value, "m_terrainEffectType", (TerrainEffectType)(-1));
                value.OnAfterDeserialize();
                Check(value.Name == "0xFFFFFFFF", "unknown signed enum uses original uppercase hex fallback");
                Check(map[(TerrainEffectType)(-1)] == value.Name, "unknown key is cached by genuine dependency");
                value.Name = "retained after dependency failure";
                mapField.SetValue(null, null);
                Check(Throws<NullReferenceException>(() => value.OnBeforeSerialize()), "managed missing map dependency fails before name assignment");
                Check(value.Name == "retained after dependency failure" && value.MinimumImpactSpeedSqr == -11.0f, "failed serialization dependency leaves own state unchanged");
                Check(Throws<NullReferenceException>(() => value.OnAfterDeserialize()), "managed deserialize dependency fails without swallowed callback");
                Check(value.Name == "retained after dependency failure", "failed deserialize dependency leaves name unchanged");
            }
            finally { mapField.SetValue(null, oldMap); }
            Check(ReferenceEquals(mapField.GetValue(null), oldMap), "original registry object restored exactly");
            return checks;
        }

        public static int RunEngine()
        {
            checks = 0;
            ActorTerrainEffectsDefinition value = null, clone = null;
            ActorTerrainEffectsDefinitionGroup group = null;
            ActorAnimationDefinition actor = null, impact = null;
            try
            {
                value = ScriptableObject.CreateInstance<ActorTerrainEffectsDefinition>();
                clone = ScriptableObject.CreateInstance<ActorTerrainEffectsDefinition>();
                group = ScriptableObject.CreateInstance<ActorTerrainEffectsDefinitionGroup>();
                actor = ScriptableObject.CreateInstance<ActorAnimationDefinition>();
                impact = ScriptableObject.CreateInstance<ActorAnimationDefinition>();
                Check(value != null && clone != null, "real concrete terrain ScriptableObjects");
                Check(group != null && actor != null && impact != null, "real group and maintained animation dependencies");
                Set(value, "m_terrainEffectType", TerrainEffectType.Grass);
                Set(value, "m_actorAnimationDefinition", actor);
                Set(value, "m_impactAnimationDefinition", impact);
                Set(value, "m_minimumImpactSpeed", -7.5f);
                ((ISerializationCallbackReceiver)value).OnBeforeSerialize();
                Check(value.Name == "Grass", "real before-serialize original label");
                Call(value, "Awake");
                Check(value.MinimumImpactSpeedSqr == 56.25f, "real object native-derived square cache");
                string json = JsonUtility.ToJson(value);
                Check(json.Contains("\"Name\":\"Grass\""), "public hidden Name is genuinely serialized");
                Check(json.Contains("\"m_terrainEffectType\":"), "private authored key is genuinely serialized");
                Check(json.Contains("\"m_actorAnimationDefinition\":"), "actor animation reference is genuinely serialized");
                Check(json.Contains("\"m_impactAnimationDefinition\":"), "impact animation reference is genuinely serialized");
                Check(json.Contains("\"m_minimumImpactSpeed\":-7.5"), "negative authored speed roundtrips without normalization");
                Check(!json.Contains("MinimumImpactSpeedSqr") && !json.Contains("k__BackingField"), "derived cache stays outside Unity JSON");
                JsonUtility.FromJsonOverwrite(json, clone);
                Check(clone.TerrainEffectType == TerrainEffectType.Grass, "real JSON authored key roundtrip");
                Check(ReferenceEquals(clone.ActorAnimationDefinition, actor), "real JSON actor object identity retained");
                Check(ReferenceEquals(clone.ImpactAnimationDefinition, impact), "real JSON impact object identity retained");
                Call(clone, "OnValidate");
                Check(clone.MinimumImpactSpeedSqr == 56.25f, "roundtrip validation refreshes squared cache");
                Set(clone, "m_minimumImpactSpeed", 10.0f);
                clone.Name = "manual";
                ((ISerializationCallbackReceiver)clone).OnAfterDeserialize();
                Check(clone.Name == "Grass" && clone.MinimumImpactSpeedSqr == 56.25f, "real deserialize callback updates only label");
                Check((TerrainEffectType)Call(group, "GetElementKey", clone) == TerrainEffectType.Grass, "real typed group key");
                Check(ReferenceEquals(Call(group, "GetKeyComparer"), HardlightEnumComparers.TerrainEffectTypeComparer), "real typed group shared comparer");
                return checks;
            }
            finally
            {
                try { if (value != null) UnityEngine.Object.DestroyImmediate(value); }
                finally { try { if (clone != null) UnityEngine.Object.DestroyImmediate(clone); }
                    finally { try { if (group != null) UnityEngine.Object.DestroyImmediate(group); }
                        finally { try { if (actor != null) UnityEngine.Object.DestroyImmediate(actor); }
                            finally { if (impact != null) UnityEngine.Object.DestroyImmediate(impact); } } } }
            }
        }
    }
}
