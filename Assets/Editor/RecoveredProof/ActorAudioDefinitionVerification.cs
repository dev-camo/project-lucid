using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace ProjectLucid
{
    // Genuine dependency checks; external hosts do not run Unity constructors.
    public static class ActorAudioDefinitionVerification
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static void Require(bool value, ref int checks, string message)
        { if (!value) throw new InvalidOperationException(message); ++checks; }
        private static T Raw<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Own);
        private static float Convert(string name, float value) => (float)typeof(HLAudioMixerDefinition).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { value });
        private static bool Near(float actual, float expected, float tolerance = 0.00001f) => Math.Abs(actual - expected) <= tolerance;
        private static bool BaseOnlyConstructor(Type type, Type baseType)
        {
            ConstructorInfo ctor = type.GetConstructor(Own, null, Type.EmptyTypes, null);
            byte[] body = ctor.GetMethodBody().GetILAsByteArray();
            if (body.Length != 7 || body[0] != 0x02 || body[1] != 0x28 || body[6] != 0x2a) return false;
            MethodBase target = ctor.Module.ResolveMethod(BitConverter.ToInt32(body, 2));
            return target.IsConstructor && target.DeclaringType == baseType && target.GetParameters().Length == 0;
        }
        private static bool FullMixerConstructor()
        {
            ConstructorInfo ctor = typeof(HLAudioMixerDefinition).GetConstructor(Type.EmptyTypes);
            byte[] body = ctor.GetMethodBody().GetILAsByteArray();
            if (body.Length != 23 || body[0] != 0x02 || body[1] != 0x7e || body[6] != 0x73 || body[11] != 0x7d || body[16] != 0x02 || body[17] != 0x28 || body[22] != 0x2a) return false;
            FieldInfo comparer = ctor.Module.ResolveField(BitConverter.ToInt32(body, 2));
            MethodBase dictionary = ctor.Module.ResolveMethod(BitConverter.ToInt32(body, 7));
            FieldInfo stored = ctor.Module.ResolveField(BitConverter.ToInt32(body, 12));
            MethodBase baseCtor = ctor.Module.ResolveMethod(BitConverter.ToInt32(body, 18));
            return comparer.DeclaringType == typeof(HardlightProject.HardlightEnumComparers) && comparer.Name == "HLAudioMixerGroupIdentifierComparer"
                && dictionary.IsConstructor && dictionary.DeclaringType == typeof(SerializableDictionary<HLAudioMixerGroupIdentifier, AudioMixerGroup>)
                && dictionary.GetParameters().Length == 1 && dictionary.GetParameters()[0].ParameterType == typeof(IEqualityComparer<HLAudioMixerGroupIdentifier>)
                && stored.DeclaringType == typeof(HLAudioMixerDefinition) && stored.Name == "m_mixerGroups"
                && baseCtor.IsConstructor && baseCtor.DeclaringType == typeof(ScriptableObject) && baseCtor.GetParameters().Length == 0;
        }
        public static int RunManaged()
        {
            int checks = 0;
            Require(BaseOnlyConstructor(typeof(HLAudioClipDefinition), typeof(ScriptableObject)), ref checks, "exact original clip constructor has no field initializer");
            Require(BaseOnlyConstructor(typeof(HLMusicClipDefinition), typeof(HLAudioClipDefinition)), ref checks, "exact original music constructor has no invented timing list");
            Require(BaseOnlyConstructor(typeof(HLSfxClipDefinition), typeof(HLAudioClipDefinition)), ref checks, "exact original sfx constructor has no invented attenuation list");
            Require(FullMixerConstructor(), ref checks, "exact original mixer comparer/dictionary/store-before-base constructor");
            var music = Raw<HLMusicClipDefinition>();
            var sfx = Raw<HLSfxClipDefinition>();
            Require(music.StartTimes == null && sfx.Dampening == null && !sfx.DampenRepeatedOneShotVolume, ref checks, "raw genuine serialized defaults");
            foreach (int identifier in new[] { 0, 17, -19, int.MinValue, int.MaxValue })
            {
                Field(typeof(HLAudioClipDefinition), "m_identifier").SetValue(music, (HLAudioClipIdentifier)identifier);
                Field(typeof(HLAudioClipDefinition), "m_identifier").SetValue(sfx, (HLAudioClipIdentifier)identifier);
                Require((int)music.Identifier == identifier && (int)sfx.Identifier == identifier, ref checks, "base identity preserves every signed enum bit");
            }
            Require(music.AudioClipReference == null && sfx.AudioClipReference == null, ref checks, "missing asset reference retained");
            var times = new List<float> { 0f, -1f, float.NaN, 19f };
            Field(typeof(HLMusicClipDefinition), "m_alternativeStartTimesInSeconds").SetValue(music, times);
            Require(ReferenceEquals(music.StartTimes, times), ref checks, "authored alternative times aliased");
            times[0] = 23f;
            Require(music.StartTimes[0] == 23f && float.IsNaN(music.StartTimes[2]), ref checks, "mutation and nonfinite timing values remain visible");
            var replacement = new List<float> { float.PositiveInfinity };
            Field(typeof(HLMusicClipDefinition), "m_alternativeStartTimesInSeconds").SetValue(music, replacement);
            Require(ReferenceEquals(music.StartTimes, replacement), ref checks, "replacement timing list published");
            Field(typeof(HLSfxClipDefinition), "m_volumeOfRepeats").SetValue(sfx, times);
            Require(ReferenceEquals(sfx.Dampening, times), ref checks, "authored attenuation aliased");
            Field(typeof(HLSfxClipDefinition), "m_dampenRepeatedOneShotVolume").SetValue(sfx, true);
            Require(sfx.DampenRepeatedOneShotVolume && ReferenceEquals(sfx.Dampening, times), ref checks, "enabled attenuation list unchanged");
            Field(typeof(HLSfxClipDefinition), "m_dampenRepeatedOneShotVolume").SetValue(sfx, false);
            Require(!sfx.DampenRepeatedOneShotVolume && ReferenceEquals(sfx.Dampening, times), ref checks, "disabled flag does not discard authored attenuation");
            Field(typeof(HLSfxClipDefinition), "m_volumeOfRepeats").SetValue(sfx, null);
            Require(sfx.Dampening == null, ref checks, "null attenuation retained");
            var mixer = Raw<HLAudioMixerDefinition>();
            foreach (int identifier in new[] { 0, -1, 31, int.MinValue, int.MaxValue })
            {
                Field(typeof(HLAudioMixerDefinition), "m_identifier").SetValue(mixer, (HLAudioMixerIdentifier)identifier);
                Require((int)mixer.Identifier == identifier, ref checks, "mixer enum identity");
            }
            Require(mixer.MixerGroups == null, ref checks, "raw fixture bypasses constructor explicitly");
            var groups = new SerializableDictionary<HLAudioMixerGroupIdentifier, AudioMixerGroup>(HardlightProject.HardlightEnumComparers.HLAudioMixerGroupIdentifierComparer);
            Field(typeof(HLAudioMixerDefinition), "m_mixerGroups").SetValue(mixer, groups);
            Require(ReferenceEquals(groups, mixer.MixerGroups), ref checks, "genuine dictionary getter aliases exact instance");
            groups.Add((HLAudioMixerGroupIdentifier)19, null);
            Require(mixer.MixerGroups.ContainsKey((HLAudioMixerGroupIdentifier)19) && mixer.MixerGroups.Count == 1, ref checks, "dictionary mutation through getter");
            foreach (float linear in new[] { 0.0001f, 0.001f, 0.01f, 0.1f, 0.5f, 1f, 2f, 10f })
            {
                float decibels = Convert("ConvertToDecibels", linear);
                Require(Near(decibels, (float)Math.Log10(linear) * 20f), ref checks, "genuine decibel converter finite domain");
                Require(Near(Convert("ConvertDecibelsToLinear", decibels), linear, Math.Max(0.000001f, Math.Abs(linear) * 0.000002f)), ref checks, "finite amplitude round trip");
            }
            Require(float.IsNegativeInfinity(Convert("ConvertToDecibels", 0f)), ref checks, "standalone converter does not clamp zero");
            Require(float.IsNegativeInfinity(Convert("ConvertToDecibels", -0f)), ref checks, "standalone negative zero remains logarithmic zero");
            Require(float.IsNaN(Convert("ConvertToDecibels", -1f)), ref checks, "standalone negative input remains NaN");
            Require(float.IsNaN(Convert("ConvertToDecibels", float.NaN)), ref checks, "standalone NaN remains NaN");
            Require(float.IsPositiveInfinity(Convert("ConvertToDecibels", float.PositiveInfinity)), ref checks, "standalone positive infinity remains infinite");
            Require(Convert("ConvertDecibelsToLinear", float.NegativeInfinity) == 0f, ref checks, "negative infinite decibels produce zero");
            Require(float.IsPositiveInfinity(Convert("ConvertDecibelsToLinear", float.PositiveInfinity)), ref checks, "positive infinite decibels produce infinity");
            Require(float.IsNaN(Convert("ConvertDecibelsToLinear", float.NaN)), ref checks, "NaN decibels remain NaN");
            Require(Convert("ConvertDecibelsToLinear", 0f) == 1f && Convert("ConvertDecibelsToLinear", -0f) == 1f, ref checks, "zero decibels produce unit amplitude");
            Require(Convert("ConvertDecibelsToLinear", -80f) == 0.0001f, ref checks, "mute endpoint inverse");
            return checks;
        }
        public static int RunEngine()
        {
            int checks = 0;
            HLMusicClipDefinition music = null;
            HLSfxClipDefinition sfx = null;
            HLAudioMixerDefinition mixer = null;
            HLAudioMixerDefinition secondMixer = null;
            try
            {
                music = ScriptableObject.CreateInstance<HLMusicClipDefinition>();
                sfx = ScriptableObject.CreateInstance<HLSfxClipDefinition>();
                mixer = ScriptableObject.CreateInstance<HLAudioMixerDefinition>();
                secondMixer = ScriptableObject.CreateInstance<HLAudioMixerDefinition>();
                Require((int)music.Identifier == 0 && music.AudioClipReference == null && music.StartTimes == null, ref checks, "real music constructor retains null serialized list/reference");
                Require((int)sfx.Identifier == 0 && sfx.AudioClipReference == null && !sfx.DampenRepeatedOneShotVolume && sfx.Dampening == null, ref checks, "real sfx constructor defaults");
                Require(mixer.MixerGroups != null && mixer.MixerGroups.Count == 0, ref checks, "real mixer constructor makes complete original dictionary");
                Require(ReferenceEquals(((Dictionary<HLAudioMixerGroupIdentifier, AudioMixerGroup>)Field(mixer.MixerGroups.GetType().BaseType, "m_dictionary").GetValue(mixer.MixerGroups)).Comparer, HardlightProject.HardlightEnumComparers.HLAudioMixerGroupIdentifierComparer), ref checks, "real mixer original comparer");
                Require(!ReferenceEquals(mixer.MixerGroups, secondMixer.MixerGroups), ref checks, "mixer dictionaries are per-instance");
                JsonUtility.FromJsonOverwrite("{\"m_identifier\":17,\"m_alternativeStartTimesInSeconds\":[0,2.5,-1]}", music);
                Require((int)music.Identifier == 17 && music.StartTimes.Count == 3 && music.StartTimes[1] == 2.5f && music.StartTimes[2] == -1f, ref checks, "music authored JSON field identities");
                string musicJson = JsonUtility.ToJson(music);
                Require(musicJson.Contains("m_identifier") && musicJson.Contains("m_alternativeStartTimesInSeconds") && musicJson.Contains("m_audioClipReference"), ref checks, "music serialized fields retained");
                JsonUtility.FromJsonOverwrite("{\"m_identifier\":23,\"m_dampenRepeatedOneShotVolume\":true,\"m_volumeOfRepeats\":[1,0.5,0]}", sfx);
                Require((int)sfx.Identifier == 23 && sfx.DampenRepeatedOneShotVolume && sfx.Dampening.Count == 3 && sfx.Dampening[2] == 0f, ref checks, "sfx authored JSON field identities");
                string sfxJson = JsonUtility.ToJson(sfx);
                Require(sfxJson.Contains("m_dampenRepeatedOneShotVolume") && sfxJson.Contains("m_volumeOfRepeats") && sfxJson.Contains("m_audioClipReference"), ref checks, "sfx serialized fields retained");
                JsonUtility.FromJsonOverwrite("{\"m_identifier\":31}", mixer);
                Require((int)mixer.Identifier == 31 && mixer.MixerGroups != null, ref checks, "mixer authored identifier and initialized dictionary");
                string mixerJson = JsonUtility.ToJson(mixer);
                Require(mixerJson.Contains("m_identifier") && mixerJson.Contains("m_audioMixer") && mixerJson.Contains("m_mixerGroups"), ref checks, "mixer serialized fields retained");
                var reference = new AssetReferenceT<AudioClip>("0123456789abcdef0123456789abcdef");
                Field(typeof(HLAudioClipDefinition), "m_audioClipReference").SetValue(music, reference);
                Require(ReferenceEquals(music.AudioClipReference, reference), ref checks, "genuine typed clip reference getter identity");
                Field(typeof(HLAudioClipDefinition), "m_audioClipReference").SetValue(music, null);
                Require(music.AudioClipReference == null, ref checks, "typed clip reference clearing");
                return checks;
            }
            finally
            {
                try { if (music != null) UnityEngine.Object.DestroyImmediate(music); }
                finally
                {
                    try { if (sfx != null) UnityEngine.Object.DestroyImmediate(sfx); }
                    finally
                    {
                        try { if (mixer != null) UnityEngine.Object.DestroyImmediate(mixer); }
                        finally { if (secondMixer != null) UnityEngine.Object.DestroyImmediate(secondMixer); }
                    }
                }
            }
        }
    }
}
