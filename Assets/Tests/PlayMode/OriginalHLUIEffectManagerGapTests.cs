using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Genuine HLUIEffectManager Awake/OnDestroy over an isolated ProcessManager
    // registry. The singleton Instance slot and the registry are restored by
    // reference. A genuine inactive HLUnityCore component is registered as the
    // logging system so CustomKeyString runs; its own Awake is never entered.
    public sealed class OriginalHLUIEffectManagerGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Awake 0x02000049 (x86-64 0x1b499d0): GetSystem<HLUnityCore> then
        // CustomKeyString("HLUIEffectManager", "Awake") (0x1b49aaa) before the
        // MonoSingleton base Awake (0x1b49abc) registers the system and publishes
        // Instance; then each cue is added only when TryGetValue misses
        // (0x1b49b24/0x1b49b7b), so the first entry per key wins even when its
        // clip is null. Last-wins insertion (indexer assignment) keeps the later
        // clip; logging after base.Awake sees the manager already registered.
        [Test]
        public void AwakeLogsBeforeRegistrationAndKeepsFirstCuePerKeyIncludingNullClip()
        {
            FieldInfo registryField = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRegistry = registryField.GetValue(null);
            PropertyInfo instance = typeof(MonoSingleton<HLUIEffectManager>).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            MethodInfo setInstance = instance.GetSetMethod(true);
            HLUIEffectManager previousInstance = HLUIEffectManager.Instance;
            var allocations = new List<UObject>();
            try
            {
                registryField.SetValue(null, Activator.CreateInstance(registryField.FieldType));
                AudioClip firstLater = Clip(allocations, "Lucid owned later cue for first key");
                AudioClip second = Clip(allocations, "Lucid owned first cue for second key");
                var firstKey = (HLAudioTypes)17;
                var secondKey = (HLAudioTypes)(-3);

                var coreOwner = new GameObject("Lucid owned inactive logging core");
                allocations.Add(coreOwner);
                coreOwner.SetActive(false);
                HLUnityCore core = coreOwner.AddComponent<HLUnityCore>();
                ProcessManager.RegisterSystem(core);

                var managerOwner = new GameObject("Lucid owned UI effect manager");
                allocations.Add(managerOwner);
                managerOwner.SetActive(false);
                HLUIEffectManager manager = managerOwner.AddComponent<HLUIEffectManager>();
                IList cues = (IList)Field(typeof(HLUIEffectManager), "m_audioCueList").GetValue(manager);
                cues.Add(Cue(firstKey, null));
                cues.Add(Cue(firstKey, firstLater));
                cues.Add(Cue(secondKey, second));
                cues.Add(Cue(secondKey, null));
                var audioCues = (Dictionary<HLAudioTypes, AudioClip>)Field(typeof(HLUIEffectManager), "m_audioCues").GetValue(manager);

                var keys = new List<string>();
                bool registeredAtAwakeLog = true;
                bool instanceUnchangedAtAwakeLog = false;
                int cuesAtAwakeLog = -1;
                core.AddCustomKeyStringHandler((key, value) =>
                {
                    keys.Add(key + "=" + value);
                    if (value != "Awake") return;
                    registeredAtAwakeLog = !ProcessManager.IsSystemNull<HLUIEffectManager>();
                    instanceUnchangedAtAwakeLog = ReferenceEquals(HLUIEffectManager.Instance, previousInstance);
                    cuesAtAwakeLog = audioCues.Count;
                });

                managerOwner.SetActive(true);
                Assert.That(keys, Is.EqualTo(new[] { "HLUIEffectManager=Awake" }));
                Assert.That(registeredAtAwakeLog, Is.False, "logged before base registration");
                Assert.That(instanceUnchangedAtAwakeLog, Is.True, "logged before Instance publication");
                Assert.That(cuesAtAwakeLog, Is.Zero, "logged before cue insertion");
                Assert.That(ReferenceEquals(ProcessManager.GetSystem<HLUIEffectManager>(), manager), Is.True);
                Assert.That(ReferenceEquals(HLUIEffectManager.Instance, manager), Is.True);

                Assert.That(audioCues.Count, Is.EqualTo(2));
                Assert.That(audioCues.ContainsKey(firstKey), Is.True);
                Assert.That(audioCues[firstKey] == null, Is.True, "first (null) cue kept over later clip");
                Assert.That(ReferenceEquals(audioCues[secondKey], second), Is.True, "first clip kept over later null");

                UObject.DestroyImmediate(managerOwner);
                Assert.That(keys, Is.EqualTo(new[] { "HLUIEffectManager=Awake", "HLUIEffectManager=OnDestroy" }));
                Assert.That(ProcessManager.IsSystemNull<HLUIEffectManager>(), Is.True);
                Assert.That(HLUIEffectManager.Instance == null, Is.True);
            }
            finally
            {
                try { DestroyAll(allocations, allocations.Count - 1); }
                finally
                {
                    try { setInstance.Invoke(null, new object[] { previousInstance }); }
                    finally { registryField.SetValue(null, previousRegistry); }
                }
            }
            Assert.That(ReferenceEquals(HLUIEffectManager.Instance, previousInstance), Is.True);
        }

        private static FieldInfo Field(Type type, string name) =>
            type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly) ?? throw new MissingFieldException(name);

        // Genuine private serializable cue row built with its own constructor; the
        // field writes stand in for authored serialized values.
        private static object Cue(HLAudioTypes key, AudioClip clip)
        {
            Type type = typeof(HLUIEffectManager).GetNestedType("HLAudioCueKVP", BindingFlags.NonPublic);
            object cue = Activator.CreateInstance(type, true);
            Field(type, "m_audioType").SetValue(cue, key);
            Field(type, "m_audioClip").SetValue(cue, clip);
            return cue;
        }

        private static AudioClip Clip(List<UObject> allocations, string name)
        {
            AudioClip clip = AudioClip.Create(name, 1, 1, 44100, false);
            allocations.Add(clip);
            return clip;
        }

        private static void DestroyAll(List<UObject> objects, int index)
        {
            if (index < 0) return;
            try { if (objects[index] != null) UObject.DestroyImmediate(objects[index]); }
            finally { DestroyAll(objects, index - 1); }
        }
    }
}
