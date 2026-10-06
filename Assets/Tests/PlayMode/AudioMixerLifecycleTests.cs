#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class AudioMixerLifecycleTests
    {
        private const string SuppliedMixer = "Assets/Recovered/Data/Audio/Main.mixer";
        private static readonly string[] Keys = { "MasterVolume", "MusicVolume", "SfxVolume", "VO", "AmbientVolume" };

        [Serializable]
        private sealed class ExportReceipt
        {
            public string status;
            public string project_path;
        }

        private static byte[] HashFile(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path)) return hash.ComputeHash(stream);
        }

        private static float Read(AudioMixer mixer, string key)
        {
            Assert.That(mixer.GetFloat(key, out float value), Is.True,
                "The supplied mixer must resolve the original exposed parameter: " + key);
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False,
                "Finite mixer fixture unexpectedly returned a nonfinite parameter: " + key);
            return value;
        }

        private static float ExpectedDecibels(float value)
        {
            float clamped = value < 0.0001f ? 0.0001f : value > 1f ? 1f : value;
            float logarithm = (float)Math.Log10(clamped);
            return logarithm * 20f;
        }

        [UnityTest]
        public IEnumerator OriginalMixerKeysNormalizeAndResetTheSuppliedMixerAcrossFrames()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string work = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
            if (string.IsNullOrEmpty(work)) work = Path.Combine(root, ".cache", "project-lucid");
            var export = JsonUtility.FromJson<ExportReceipt>(File.ReadAllText(
                Path.Combine(work, "assets", "latest-assets.json")));
            Assert.That(export, Is.Not.Null);
            Assert.That(export.status, Is.EqualTo("exported"));
            Assert.That(export.project_path, Is.Not.Null.And.Not.Empty);
            string raw = Path.Combine(export.project_path, "Assets", "Data", "Audio", "Main.mixer");
            string prepared = Path.Combine(root, SuppliedMixer);
            var originals = new Dictionary<string, byte[]>();
            foreach (string path in new[] { raw, raw + ".meta", prepared, prepared + ".meta" })
                originals.Add(path, HashFile(path));
            CollectionAssert.AreEqual(originals[raw], originals[prepared],
                "The supplied engine-only mixer must retain its raw exported asset bytes.");

            string folderName = "LucidMixerProof-" + Guid.NewGuid().ToString("N");
            string folder = "Assets/Recovered/" + folderName;
            string copiedPath = folder + "/Main.mixer";
            bool folderCreated = false;
            HLAudioMixerDefinition definition = null;
            try
            {
                Assert.That(AssetDatabase.IsValidFolder("Assets/Recovered"), Is.True);
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.False,
                    "Mixer fixture destination must be fresh and owned by this test.");
                string createdGuid = AssetDatabase.CreateFolder("Assets/Recovered", folderName);
                folderCreated = AssetDatabase.IsValidFolder(folder);
                Assert.That(createdGuid, Is.Not.Null.And.Not.Empty);
                Assert.That(folderCreated, Is.True);
                Assert.That(AssetDatabase.CopyAsset(SuppliedMixer, copiedPath), Is.True,
                    "The fixture must copy the genuine supplied mixer without editing it.");
                Assert.That(AssetDatabase.AssetPathToGUID(copiedPath),
                    Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(SuppliedMixer)));
                AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(copiedPath);
                Assert.That(mixer, Is.Not.Null);
                definition = ScriptableObject.CreateInstance<HLAudioMixerDefinition>();
                FieldInfo field = typeof(HLAudioMixerDefinition).GetField("m_audioMixer",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Assert.That(field, Is.Not.Null);
                field.SetValue(definition, mixer);

                // Unity mixer startup must finish before setting exposed parameters.
                // The source mixer is never changed; all setters target this copy.
                yield return null;
                foreach (string key in Keys) Read(mixer, key);
                float pitch = Read(mixer, "MusicPitch");
                Assert.That(mixer.ClearFloat("AmbientVolume"), Is.True);
                float ambientSnapshot = Read(mixer, "AmbientVolume");
                Action<float>[] setters = { definition.SetMasterVolume, definition.SetMusicVolume,
                    definition.SetSfxVolume, definition.SetVoiceVolume, definition.SetAmbientVolume };
                foreach (float volume in new[] { -1f, 0f, 0.0001f, 0.001f, 0.1f, 0.5f, 1f, 2f })
                {
                    for (int i = 0; i < setters.Length; ++i)
                    {
                        setters[i](volume);
                        Assert.That(Read(mixer, Keys[i]), Is.EqualTo(ExpectedDecibels(volume)).Within(0.0001f),
                            "Original clamp and decibel conversion for " + Keys[i]);
                    }
                    float clamped = volume < 0.0001f ? 0.0001f : volume > 1f ? 1f : volume;
                    Assert.That(definition.GetMusicVolume(), Is.EqualTo(clamped).Within(0.000002f),
                        "Original music inverse conversion must use the actual engine value.");
                }
                definition.SetMute(true);
                Assert.That(Read(mixer, "MasterVolume"), Is.EqualTo(-80f));
                definition.SetMute(false);
                Assert.That(Read(mixer, "MasterVolume"), Is.EqualTo(0f));

                definition.SetAmbientVolume(0.123f);
                definition.ClearAmbientVolume();
                Assert.That(Read(mixer, "AmbientVolume"), Is.EqualTo(ambientSnapshot).Within(0.0001f),
                    "ClearAmbient must remove the override and return to the supplied snapshot.");
                definition.SetAmbientVolume(0.333f);
                var settings = new SaveDataSettings
                { MasterVolume = 0.25f, MusicVolume = 0.5f, SfxVolume = 0f, VoiceVolume = 2f };
                definition.ResetVolume(settings);
                foreach (var pair in new[] {
                    new KeyValuePair<string, float>("MasterVolume", settings.MasterVolume),
                    new KeyValuePair<string, float>("MusicVolume", settings.MusicVolume),
                    new KeyValuePair<string, float>("SfxVolume", settings.SfxVolume),
                    new KeyValuePair<string, float>("VO", settings.VoiceVolume) })
                    Assert.That(Read(mixer, pair.Key), Is.EqualTo(ExpectedDecibels(pair.Value)).Within(0.0001f));
                Assert.That(Read(mixer, "AmbientVolume"), Is.EqualTo(ambientSnapshot).Within(0.0001f));
                Assert.That(Read(mixer, "MusicPitch"), Is.EqualTo(pitch).Within(0.000001f),
                    "Original volume setters must leave the separately exposed pitch unchanged.");
                yield return null;
                Assert.That(Read(mixer, "MusicVolume"), Is.EqualTo(ExpectedDecibels(0.5f)).Within(0.0001f),
                    "The genuine exposed value must remain observable after another frame.");
            }
            finally
            {
                try { if (definition != null) UnityEngine.Object.DestroyImmediate(definition); }
                finally
                {
                    try
                    {
                        if (folderCreated)
                            Assert.That(AssetDatabase.DeleteAsset(folder), Is.True,
                                "All copied mixer assets and their temporary directory must be removed.");
                    }
                    finally
                    {
                        foreach (var pair in originals)
                            CollectionAssert.AreEqual(pair.Value, HashFile(pair.Key),
                                "The original raw/prepared mixer or meta changed: " + pair.Key);
                    }
                }
            }
        }
    }
}
#endif
