using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;
namespace ProjectLucid
{
    // Bounded genuine definition/selection checks; test-only reflection creates
    // managed fixtures without running Unity constructors in an external host.
    public static class ActorDefinitionVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static void Require(bool value, ref int checks, string message)
        { if (!value) throw new InvalidOperationException(message); ++checks; }
        private static T Raw<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Own);
        private static int Last(ActorAudioReference value) => (int)Field(typeof(ActorAudioReference), "m_lastUsedClipIndex").GetValue(value);
        private static int Index(ActorAudioReference value, float weight) => (int)typeof(ActorAudioReference).GetMethod("GetClipIndex", Own).Invoke(value, new object[] { weight });
        private static bool ThrowsNull(Action action)
        {
            try { action(); }
            catch (NullReferenceException) { return true; }
            catch (TargetInvocationException exception) { return exception.InnerException is NullReferenceException; }
            return false;
        }
        private static ActorAudioReference.ClipWithWeight Clip(int id, float weight = 1f) => new ActorAudioReference.ClipWithWeight { ClipID = (HLAudioClipIdentifier)id, ProbabilityWeighting = weight };
        private static object Element(ActorParticleTriggerType trigger, ParticleEffectType effect)
        {
            Type type = typeof(ActorParticleEffectLookup).GetNestedType("ActorParticleEffectLookupElement", BindingFlags.NonPublic);
            object element = Activator.CreateInstance(type, true);
            Field(type, "m_trigger").SetValue(element, trigger); Field(type, "m_effect").SetValue(element, effect);
            return element;
        }
        private static ActorParticleEffectLookup ParticleLookup(params object[] elements)
        {
            var lookup = Raw<ActorParticleEffectLookup>();
            Type elementType = typeof(ActorParticleEffectLookup).GetNestedType("ActorParticleEffectLookupElement", BindingFlags.NonPublic);
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            foreach (object element in elements) list.Add(element);
            Field(typeof(ActorParticleEffectLookup), "m_elements").SetValue(lookup, list);
            Field(typeof(ActorParticleEffectLookup), "<Lookup>k__BackingField").SetValue(lookup,
                new Dictionary<ActorParticleTriggerType, ParticleEffectType>(HardlightProject.HardlightEnumComparers.ActorParticleTriggerTypeComparer));
            return lookup;
        }
        public static int RunManaged()
        {
            int checks = 0;
            var audio = new ActorAudioReference();
            Require(audio.Name == null && (int)audio.AudioType == 0 && audio.SelectionMode == 0, ref checks, "audio default names/enums");
            Require(ReferenceEquals(audio.Clips, Array.Empty<HLAudioClipIdentifier>()) && ReferenceEquals(audio.WeightedClips, Array.Empty<ActorAudioReference.ClipWithWeight>()), ref checks, "original shared empty arrays");
            Require(Last(audio) == -1 && !audio.Get((LevelSetupTypes)17).HasValue, ref checks, "empty selection retains initial index");
            audio.WeightedClips = null;
            Require(!audio.Get((LevelSetupTypes)17).HasValue && Last(audio) == -1, ref checks, "null weighted selection");
            var single = Clip(0);
            Require(single.Name == null && single.ProbabilityWeighting == 1f && !single.HasLevelSpecificOverride && single.LevelSpecificOverrides == null, ref checks, "weighted clip defaults");
            audio.WeightedClips = new[] { single };
            foreach (ActorAudioReference.SelectionModeType mode in new[] { (ActorAudioReference.SelectionModeType)0, (ActorAudioReference.SelectionModeType)1, (ActorAudioReference.SelectionModeType)2, (ActorAudioReference.SelectionModeType)47 })
            {
                audio.SelectionMode = mode; Field(typeof(ActorAudioReference), "m_lastUsedClipIndex").SetValue(audio, 19);
                HLAudioClipIdentifier? value = audio.Get((LevelSetupTypes)17);
                Require(value.HasValue && (int)value.Value == 0 && Last(audio) == 0, ref checks, "single zero clip bypasses mode/repeat reset");
            }
            audio.WeightedClips = new[] { Clip(17), Clip(23), Clip(41) }; audio.SelectionMode = ActorAudioReference.SelectionModeType.InOrder;
            Field(typeof(ActorAudioReference), "m_lastUsedClipIndex").SetValue(audio, -1);
            for (int i = 0; i < 8; ++i)
            {
                int expected = new[] { 17, 23, 41 }[i % 3];
                Require((int)audio.Get((LevelSetupTypes)9).Value == expected && Last(audio) == i % 3, ref checks, "ordered selection cycles");
            }
            audio.SelectionMode = (ActorAudioReference.SelectionModeType)47;
            Require((int)audio.Get((LevelSetupTypes)9).Value == 17 && Last(audio) == 0, ref checks, "unknown mode falls back to first");
            single.HasLevelSpecificOverride = true;
            single.LevelSpecificOverrides = new[] {
                new ActorAudioReference.LevelSpecificOverride { LevelType = (LevelSetupTypes)19, ClipID = (HLAudioClipIdentifier)0 },
                new ActorAudioReference.LevelSpecificOverride { LevelType = (LevelSetupTypes)19, ClipID = (HLAudioClipIdentifier)77 },
                new ActorAudioReference.LevelSpecificOverride { LevelType = (LevelSetupTypes)21, ClipID = (HLAudioClipIdentifier)91 }
            }; single.ClipID = (HLAudioClipIdentifier)55;
            Require((int)single.GetClipID((LevelSetupTypes)19) == 0, ref checks, "first matching zero override wins");
            Require((int)single.GetClipID((LevelSetupTypes)21) == 91 && (int)single.GetClipID((LevelSetupTypes)20) == 55, ref checks, "override match/fallback");
            single.HasLevelSpecificOverride = false; single.LevelSpecificOverrides = null;
            Require((int)single.GetClipID((LevelSetupTypes)19) == 55, ref checks, "disabled override ignores null array");
            single.HasLevelSpecificOverride = true; audio.WeightedClips = new[] { single }; audio.SelectionMode = ActorAudioReference.SelectionModeType.RandomNoRepeat;
            Field(typeof(ActorAudioReference), "m_lastUsedClipIndex").SetValue(audio, -1);
            Require(ThrowsNull(() => audio.Get((LevelSetupTypes)19)) && Last(audio) == 0, ref checks, "single index published before override failure");
            audio.SelectionMode = ActorAudioReference.SelectionModeType.InOrder; audio.WeightedClips = new[] { Clip(1), single };
            Field(typeof(ActorAudioReference), "m_lastUsedClipIndex").SetValue(audio, 0);
            Require(ThrowsNull(() => audio.Get((LevelSetupTypes)19)) && Last(audio) == 1, ref checks, "multi index published before override failure");
            audio.WeightedClips = new[] { Clip(1, 1), Clip(2, 2), Clip(3, 4) };
            float[] weights = { float.NegativeInfinity, -1f, 0f, 0.5f, 1f, 2f, 3f, 5f, 7f, 8f, float.PositiveInfinity, float.NaN };
            int[] indices = { 0, 0, 0, 0, 0, 1, 0, 2, 0, 0, 0, 0 };
            for (int i = 0; i < weights.Length; ++i) Require(Index(audio, weights[i]) == indices[i], ref checks, "strict weighted interval " + i);
            audio.WeightedClips = new[] { Clip(1, -1), Clip(2, 4), Clip(3, 1) };
            weights = new[] { -1f, -0.5f, 0f, 2f, 3f, 3.5f, 4f }; indices = new[] { 0, 1, 1, 1, 0, 2, 0 };
            for (int i = 0; i < weights.Length; ++i) Require(Index(audio, weights[i]) == indices[i], ref checks, "negative weighted intervals " + i);
            audio.WeightedClips = new[] { Clip(1, 0), Clip(2, 1), Clip(3, 0), Clip(4, 2) };
            weights = new[] { 0f, 0.5f, 1f, 2f, 3f }; indices = new[] { 0, 1, 0, 3, 0 };
            for (int i = 0; i < weights.Length; ++i) Require(Index(audio, weights[i]) == indices[i], ref checks, "zero weighted intervals " + i);
            audio.WeightedClips = new[] { Clip(1, float.NaN), Clip(2, 1) };
            Require(Index(audio, 0.5f) == 0, ref checks, "NaN accumulated weight has no interval");
            audio.WeightedClips = Array.Empty<ActorAudioReference.ClipWithWeight>(); Require(Index(audio, 2f) == 0, ref checks, "empty weight fallback");
            audio.WeightedClips = new ActorAudioReference.ClipWithWeight[] { null };
            Require(ThrowsNull(() => Index(audio, float.NegativeInfinity)), ref checks, "weight read precedes lower-bound short circuit");
            var lookup = new ActorAudioLookup(); var a = new ActorAudioReference { AudioType = (ActorAudioTypes)17, WeightedClips = new[] { Clip(3) } };
            var b = new ActorAudioReference { AudioType = (ActorAudioTypes)17, WeightedClips = new[] { Clip(9) } };
            var c = new ActorAudioReference { AudioType = (ActorAudioTypes)23, WeightedClips = new[] { Clip(0) } };
            Require(ReferenceEquals(lookup.AudioDictionary.Comparer, HardlightProject.HardlightEnumComparers.ActorAudioTypesComparer), ref checks, "audio original comparer");
            lookup.CompileLookup(new[] { a, b }); Require(lookup.AudioDictionary.Count == 1 && ReferenceEquals(lookup.AudioDictionary[(ActorAudioTypes)17], b), ref checks, "last audio reference wins");
            lookup.CompileLookup(new[] { c }); Require(lookup.AudioDictionary.Count == 2 && ReferenceEquals(lookup.AudioDictionary[(ActorAudioTypes)17], b), ref checks, "compile lookup adds without clearing");
            Require(lookup.TryGet((ActorAudioTypes)17, out HLAudioClipIdentifier? result, (LevelSetupTypes)19) && (int)result.Value == 9, ref checks, "audio successful selection");
            Require(lookup.TryGet((ActorAudioTypes)23, out result, (LevelSetupTypes)19) && result.HasValue && (int)result.Value == 0, ref checks, "lookup preserves present zero");
            Require(!lookup.TryGet((ActorAudioTypes)29, out result, (LevelSetupTypes)19) && !result.HasValue, ref checks, "missing audio assigns null");
            lookup.AudioDictionary[(ActorAudioTypes)29] = null;
            Require(!lookup.TryGet((ActorAudioTypes)29, out result, (LevelSetupTypes)19) && !result.HasValue, ref checks, "found null reference assigns null");
            lookup.AudioDictionary[(ActorAudioTypes)31] = new ActorAudioReference();
            Require(!lookup.TryGet((ActorAudioTypes)31, out result, (LevelSetupTypes)19) && !result.HasValue, ref checks, "found empty reference assigns null");
            lookup.AudioDictionary[(ActorAudioTypes)37] = audio; audio.WeightedClips = new[] { single }; result = (HLAudioClipIdentifier)83;
            Require(ThrowsNull(() => lookup.TryGet((ActorAudioTypes)37, out result, (LevelSetupTypes)19)) && result.HasValue && (int)result.Value == 83, ref checks, "selection failure preserves caller out value");
            Require(ThrowsNull(() => lookup.CompileLookup(new ActorAudioReference[] { a, null })) && ReferenceEquals(lookup.AudioDictionary[(ActorAudioTypes)17], a), ref checks, "lookup compilation partial failure preserves prefix");
            var level = new ActorAudioReference.LevelSpecificOverride();
            Require(level.Name == null && (int)level.ClipID == 0 && (int)level.LevelType == 0, ref checks, "override zero defaults");
            audio = new ActorAudioReference { AudioType = (ActorAudioTypes)17, Clips = new[] { (HLAudioClipIdentifier)67 }, WeightedClips = new[] { Clip(23) } };
            audio.WeightedClips[0].LevelSpecificOverrides = new[] { new ActorAudioReference.LevelSpecificOverride { LevelType = (LevelSetupTypes)29, ClipID = (HLAudioClipIdentifier)31 } };
            foreach (bool before in new[] { true, false })
            {
                audio.Name = "old"; audio.WeightedClips[0].Name = "old"; audio.WeightedClips[0].LevelSpecificOverrides[0].Name = "old";
                if (before) audio.OnBeforeSerialize(); else audio.OnAfterDeserialize();
                Require(audio.Name == ((ActorAudioTypes)17).ToString(), ref checks, "outer audio callback enum string");
                Require(audio.WeightedClips[0].Name == ((HLAudioClipIdentifier)23).ToString() && audio.WeightedClips[0].LevelSpecificOverrides[0].Name == ((LevelSetupTypes)29).ToString(), ref checks, "outer callback traverses nested enum strings");
                Require(audio.Clips.Length == 1 && (int)audio.Clips[0] == 67 && audio.WeightedClips.Length == 1 && (int)audio.WeightedClips[0].ClipID == 23, ref checks, "callbacks do not migrate legacy clips");
            }
            audio.WeightedClips[0].LevelSpecificOverrides = null; audio.Name = "old"; audio.WeightedClips[0].Name = "old";
            Require(ThrowsNull(audio.OnBeforeSerialize) && audio.Name == ((ActorAudioTypes)17).ToString() && audio.WeightedClips[0].Name == ((HLAudioClipIdentifier)23).ToString(), ref checks, "callback publishes names before null override array failure");
            var registryClip = Clip(0); registryClip.OnBeforeSerialize(); Require(registryClip.Name == ((HLAudioClipIdentifier)0).GetString(), ref checks, "standalone weighted callback registry");
            registryClip.Name = "old"; registryClip.OnAfterDeserialize(); Require(registryClip.Name == ((HLAudioClipIdentifier)0).GetString(), ref checks, "standalone weighted deserialize registry");
            level.OnBeforeSerialize(); Require(level.Name == ((LevelSetupTypes)0).GetString(), ref checks, "standalone override callback registry");
            level.Name = "old"; level.OnAfterDeserialize(); Require(level.Name == ((LevelSetupTypes)0).GetString(), ref checks, "standalone override deserialize registry");
            var settings = new CharacterSettings(); Require(settings.Collider == null && settings.Surface == null && settings.AnimationSettings == null, ref checks, "character setting reference defaults");
            var collider = new CharacterSettings.ColliderSettings();
            Require(collider.TrackMask.value == -1 && collider.CollisionMask.value == -1 && collider.StickToColliderMask.value == 0, ref checks, "collider default masks");
            Require(collider.StickToColliderAngleMax == 45f && collider.StickToColliderForce == 1f && collider.LookAheadSlopeHeight == 0.1f, ref checks, "collider surface defaults");
            Require(collider.GroundCheckMinDistance == 0.1f && collider.GroundCheckMaxDistance == 1f && collider.TrackingStep == 5f && collider.TrackingDistanceUp == 1f && collider.TrackingDistanceDown == 2f && collider.MaxGravityDeviationAngle == 135f, ref checks, "collider tracking defaults");
            Require(collider.MaxGravityDeviationAngleCosine == 0f && collider.GroundCheckMaxDistanceSqr == 0f, ref checks, "cached collider values remain zero until refresh");
            var surface = new CharacterSettings.SurfaceSettings();
            Require(!surface.ClampToSurfaceBoundsOnGround && !surface.ClampToSurfaceBoundsInAir, ref checks, "surface defaults");
            var animation = Raw<ActorAnimationSettingsDefinition>();
            string[] animationFields = { "m_rawXLocalVelocity", "m_rawYLocalVelocity", "m_rawZLocalVelocity", "m_rawXZLocalVelocity", "m_rawXControlInput", "m_rawYControlInput", "m_dampenedXZLocalVelocity" };
            string[] animationProperties = { "RawXLocalVelocity", "RawYLocalVelocity", "RawZLocalVelocity", "RawXZLocalVelocity", "RawXControlInput", "RawYControlInput", "DampenedXZLocalVelocity" };
            for (int i = 0; i < animationFields.Length; ++i)
            {
                var wrapper = new AnimationParameterWrapper(); Field(typeof(ActorAnimationSettingsDefinition), animationFields[i]).SetValue(animation, wrapper);
                Require(ReferenceEquals(typeof(ActorAnimationSettingsDefinition).GetProperty(animationProperties[i], Own).GetValue(animation), wrapper), ref checks, "animation wrapper identity " + i);
            }
            Require(animation.DampenedXZLocalVelocitySampleSize == 0, ref checks, "Min attribute does not imply initialized sample size");
            Field(typeof(ActorAnimationSettingsDefinition), "m_dampenedXZLocalVelocitySampleSize").SetValue(animation, -7);
            Require(animation.DampenedXZLocalVelocitySampleSize == -7, ref checks, "sample size getter does not clamp");
            var definition = Raw<ActorAudioDefinition>(); var refs = new[] { a, b };
            Field(typeof(ActorAudioDefinition), "m_audioReferences").SetValue(definition, refs);
            Require(ReferenceEquals(definition.ActorAudioReferences, refs), ref checks, "audio definition exact array identity");
            object e0 = Element((ActorParticleTriggerType)0, (ParticleEffectType)0);
            ((ISerializationCallbackReceiver)e0).OnBeforeSerialize();
            Require((string)Field(e0.GetType(), "Name").GetValue(e0) == "NONE -> NONE", ref checks, "zero particle display literal");
            Field(e0.GetType(), "Name").SetValue(e0, "retained"); ((ISerializationCallbackReceiver)e0).OnAfterDeserialize();
            Require((string)Field(e0.GetType(), "Name").GetValue(e0) == "retained", ref checks, "particle after-deserialize genuine RET");
            object e1 = Element((ActorParticleTriggerType)17, (ParticleEffectType)23), e2 = Element((ActorParticleTriggerType)17, (ParticleEffectType)29);
            ((ISerializationCallbackReceiver)e1).OnBeforeSerialize();
            Require((string)Field(e1.GetType(), "Name").GetValue(e1) == ((ActorParticleTriggerType)17).GetString() + " -> " + ((ParticleEffectType)23).GetString(), ref checks, "particle nonzero registry display");
            var particles = ParticleLookup(e0, e1, e2); particles.Lookup[(ActorParticleTriggerType)37] = (ParticleEffectType)41;
            particles.UpdateCachedValues(); Require(particles.Lookup.Count == 2 && (int)particles.Lookup[(ActorParticleTriggerType)17] == 29 && !particles.Lookup.ContainsKey((ActorParticleTriggerType)37), ref checks, "particle refresh clears and last entry wins");
            Require(ReferenceEquals(particles.Lookup.Comparer, HardlightProject.HardlightEnumComparers.ActorParticleTriggerTypeComparer), ref checks, "particle original comparer");
            particles = ParticleLookup(e1, null);
            Require(ThrowsNull(particles.UpdateCachedValues) && particles.Lookup.Count == 1 && (int)particles.Lookup[(ActorParticleTriggerType)17] == 23, ref checks, "particle refresh partial failure preserves prefix");
            var actor = Raw<ActorDefinition>(); var pfx = new Dictionary<ActorParticleTriggerType, ParticleEffectType>(); pfx[(ActorParticleTriggerType)17] = (ParticleEffectType)23;
            Field(typeof(ActorDefinition), "<PFXDefinitionsDictionary>k__BackingField").SetValue(actor, pfx);
            var actorAudio = new ActorAudioLookup(); actorAudio.CompileLookup(new[] { a }); Field(typeof(ActorDefinition), "ActorAudioLookup").SetValue(actor, actorAudio);
            actor.Settings = new CharacterSettings(); actor.PFXLookups = new List<ActorParticleEffectLookup>(); actor.AudioDefinitions = new List<ActorAudioDefinition>();
            Require(ThrowsNull(() => typeof(ActorDefinition).GetMethod("UpdateCachedValues", Own).Invoke(actor, null)) && pfx.Count == 1 && actorAudio.AudioDictionary.Count == 1, ref checks, "collider failure precedes both cache clears");
            var dispatch = Raw<DispatchFixture>(); dispatch.Events = new List<string>(); dispatch.CallAwake(); dispatch.CallValidate();
            Require(dispatch.Events.Count == 2 && dispatch.Events[0] == "refresh" && dispatch.Events[1] == "refresh", ref checks, "original Awake/OnValidate dispatch to virtual refresh");
            byte[] constructor = typeof(ActorAudioDefinition).GetConstructor(Type.EmptyTypes).GetMethodBody().GetILAsByteArray();
            Require(constructor.Length == 18 && constructor[0] == 0x02 && constructor[1] == 0x28 && constructor[6] == 0x7d && constructor[11] == 0x02 && constructor[12] == 0x28 && constructor[17] == 0x2a, ref checks, "original audio definition constructor call/store-before-base shape");
            Module module = typeof(ActorAudioDefinition).Module;
            MethodInfo empty = (MethodInfo)module.ResolveMethod(BitConverter.ToInt32(constructor, 2));
            Require(empty.DeclaringType == typeof(Array) && empty.Name == "Empty" && empty.IsGenericMethod && empty.GetGenericArguments().Length == 1 && empty.GetGenericArguments()[0] == typeof(ActorAudioReference) && empty.ReturnType == typeof(ActorAudioReference[]), ref checks, "original audio definition shared empty array generic target");
            FieldInfo initialized = module.ResolveField(BitConverter.ToInt32(constructor, 7));
            Require(initialized.DeclaringType == typeof(ActorAudioDefinition) && initialized.Name == "m_audioReferences" && initialized.FieldType == typeof(ActorAudioReference[]), ref checks, "original audio definition exact initialized field");
            MethodBase baseConstructor = module.ResolveMethod(BitConverter.ToInt32(constructor, 13));
            Require(baseConstructor.IsConstructor && baseConstructor.DeclaringType == typeof(ScriptableObject), ref checks, "original audio definition base call follows array publication");
            return checks;
        }
        private static float Single(float value) => BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
        public static int RunEngine()
        {
            int checks = 0;
            var settings = new CharacterSettings { Collider = new CharacterSettings.ColliderSettings() };
            float[] angles = { 0f, 45f, 135f, 10000000f, float.NaN };
            float[] distances = { 0f, 0.25f, -3f, 10000.25f, float.NaN };
            for (int i = 0; i < angles.Length; ++i)
            {
                settings.Collider.MaxGravityDeviationAngle = angles[i]; settings.Collider.GroundCheckMaxDistance = distances[i];
                settings.UpdateCachedValues();
                float cosine = (float)Math.Cos((double)Single(angles[i] * 0.01745329238474369f));
                float square = Single(distances[i] * distances[i]);
                Require(float.IsNaN(cosine) ? float.IsNaN(settings.Collider.MaxGravityDeviationAngleCosine) : Math.Abs(settings.Collider.MaxGravityDeviationAngleCosine - cosine) <= 0.000001f, ref checks, "real engine collider angle cache " + i);
                Require(float.IsNaN(square) ? float.IsNaN(settings.Collider.GroundCheckMaxDistanceSqr) : settings.Collider.GroundCheckMaxDistanceSqr == square, ref checks, "real engine squared-distance cache " + i);
            }
            UnityEngine.Random.State state = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(17843);
                var audio = new ActorAudioReference { SelectionMode = ActorAudioReference.SelectionModeType.Random, WeightedClips = new[] { Clip(17, 1f), Clip(23, 2f), Clip(41, 4f) } };
                for (int i = 0; i < 6; ++i)
                {
                    float selectedWeight = (float)typeof(ActorAudioReference).GetMethod("GetSelectedWeight", Own).Invoke(audio, null);
                    Require(selectedWeight >= 0f && selectedWeight <= 7f, ref checks, "original engine weighted random range " + i);
                    int clip = (int)audio.Get((LevelSetupTypes)19).Value;
                    Require(clip == 17 || clip == 23 || clip == 41, ref checks, "original engine random selection membership " + i);
                }
                audio.SelectionMode = ActorAudioReference.SelectionModeType.RandomNoRepeat;
                int last = Last(audio);
                for (int i = 0; i < 6; ++i)
                {
                    int clip = (int)audio.Get((LevelSetupTypes)19).Value;
                    Require(Last(audio) != last && (clip == 17 || clip == 23 || clip == 41), ref checks, "original engine nonrepeating weighted selection " + i);
                    last = Last(audio);
                }
                audio.SelectionMode = ActorAudioReference.SelectionModeType.Random; audio.WeightedClips = new[] { Clip(17, 0f), Clip(23, 0f) };
                Require((int)audio.Get((LevelSetupTypes)19).Value == 17, ref checks, "all-zero random weights fall back to first");
            }
            finally { UnityEngine.Random.state = state; }
            var json = JsonUtility.FromJson<ActorAudioReference>("{\"AudioType\":17,\"Clips\":[67],\"WeightedClips\":[{\"ClipID\":23,\"ProbabilityWeighting\":2,\"LevelSpecificOverrides\":[{\"ClipID\":31,\"LevelType\":29}]}]}");
            Require(json.Clips.Length == 1 && (int)json.Clips[0] == 67 && json.WeightedClips.Length == 1 && (int)json.WeightedClips[0].ClipID == 23, ref checks, "real Unity JSON preserves legacy/weighted clip arrays");
            Require(json.Name == ((ActorAudioTypes)17).ToString() && json.WeightedClips[0].Name == ((HLAudioClipIdentifier)23).ToString() && json.WeightedClips[0].LevelSpecificOverrides[0].Name == ((LevelSetupTypes)29).ToString(), ref checks, "real Unity JSON outer callback name refresh");
            CacheFixture actor = null; ActorAudioDefinition audio1 = null, audio2 = null; ActorParticleEffectLookup pfx1 = null, pfx2 = null;
            try
            {
                actor = ScriptableObject.CreateInstance<CacheFixture>();
                audio1 = ScriptableObject.CreateInstance<ActorAudioDefinition>(); audio2 = ScriptableObject.CreateInstance<ActorAudioDefinition>();
                pfx1 = ScriptableObject.CreateInstance<ActorParticleEffectLookup>(); pfx2 = ScriptableObject.CreateInstance<ActorParticleEffectLookup>();
                Require(actor.FSMMovement == null && actor.Settings == null && actor.AudioDefinitions == null && actor.PFXLookups == null && (int)actor.Name == 0, ref checks, "real actor definition constructor reference defaults");
                Require(actor.PFXDefinitionsDictionary.Count == 0 && ReferenceEquals(actor.PFXDefinitionsDictionary.Comparer, HardlightProject.HardlightEnumComparers.ActorParticleTriggerTypeComparer) && actor.ActorAudioLookup.AudioDictionary.Count == 0, ref checks, "real actor definition constructor dictionaries");
                Require(ReferenceEquals(audio1.ActorAudioReferences, Array.Empty<ActorAudioReference>()) && ReferenceEquals(audio2.ActorAudioReferences, audio1.ActorAudioReferences) && pfx1.Lookup.Count == 0, ref checks, "real child definition constructor defaults");
                Field(typeof(ActorParticleEffectLookup), "m_elements").SetValue(pfx1, Field(typeof(ActorParticleEffectLookup), "m_elements").GetValue(ParticleLookup(Element((ActorParticleTriggerType)17, (ParticleEffectType)23))));
                Field(typeof(ActorParticleEffectLookup), "m_elements").SetValue(pfx2, Field(typeof(ActorParticleEffectLookup), "m_elements").GetValue(ParticleLookup(Element((ActorParticleTriggerType)17, (ParticleEffectType)29))));
                var a = new ActorAudioReference { AudioType = (ActorAudioTypes)17, WeightedClips = new[] { Clip(23) } };
                var b = new ActorAudioReference { AudioType = (ActorAudioTypes)17, WeightedClips = new[] { Clip(41) } };
                Field(typeof(ActorAudioDefinition), "m_audioReferences").SetValue(audio1, new[] { a }); Field(typeof(ActorAudioDefinition), "m_audioReferences").SetValue(audio2, new[] { b });
                actor.Settings = new CharacterSettings { Collider = new CharacterSettings.ColliderSettings() };
                actor.PFXLookups = new List<ActorParticleEffectLookup> { pfx1, pfx2 }; actor.AudioDefinitions = new List<ActorAudioDefinition> { audio1, audio2 };
                actor.PFXDefinitionsDictionary[(ActorParticleTriggerType)31] = (ParticleEffectType)37; actor.ActorAudioLookup.AudioDictionary[(ActorAudioTypes)31] = a;
                actor.CallAwake();
                Require(actor.PFXDefinitionsDictionary.Count == 1 && (int)actor.PFXDefinitionsDictionary[(ActorParticleTriggerType)17] == 29, ref checks, "real actor PFX cache ordering");
                Require(actor.ActorAudioLookup.AudioDictionary.Count == 1 && ReferenceEquals(actor.ActorAudioLookup.AudioDictionary[(ActorAudioTypes)17], b), ref checks, "real actor audio cache ordering");
                Require(actor.Settings.Collider.GroundCheckMaxDistanceSqr == 1f && Math.Abs(actor.Settings.Collider.MaxGravityDeviationAngleCosine - (float)Math.Cos(135f * Math.PI / 180.0)) < 0.000001f, ref checks, "real actor refreshes collider first");
                actor.AudioDefinitions = new List<ActorAudioDefinition> { audio1, null };
                Require(ThrowsNull(actor.CallValidate) && actor.ActorAudioLookup.AudioDictionary.Count == 1 && ReferenceEquals(actor.ActorAudioLookup.AudioDictionary[(ActorAudioTypes)17], a) && actor.PFXDefinitionsDictionary.Count == 1, ref checks, "real Actor validation partial failure preserves refresh prefix");
            }
            finally
            {
                try { if (actor != null) UnityEngine.Object.DestroyImmediate(actor); }
                finally { try { if (audio1 != null) UnityEngine.Object.DestroyImmediate(audio1); }
                finally { try { if (audio2 != null) UnityEngine.Object.DestroyImmediate(audio2); }
                finally { try { if (pfx1 != null) UnityEngine.Object.DestroyImmediate(pfx1); }
                finally { if (pfx2 != null) UnityEngine.Object.DestroyImmediate(pfx2); } } } }
            }
            return checks;
        }
        private sealed class CacheFixture : ActorDefinition
        {
            // Fixture construction alone suppresses Unity's preconfiguration calls.
            // Explicit calls below exercise the unchanged original base methods.
            protected override void Awake() { }
            protected override void OnValidate() { }
            public void CallAwake() { base.Awake(); }
            public void CallValidate() { base.OnValidate(); }
        }
        private sealed class DispatchFixture : ActorDefinition
        {
            public List<string> Events;
            protected override void UpdateCachedValues() { Events.Add("refresh"); }
            public void CallAwake() { base.Awake(); }
            public void CallValidate() { base.OnValidate(); }
        }
    }
}
