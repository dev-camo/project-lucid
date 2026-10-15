#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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

        // Literal expectations from the native bodies cited in HLAudioMixerDefinition:
        // SetFloat 0x06001a0d (x86-64 0x53e310) clamps with minss 1f and a
        // cmpltss 0.0001f select, then log10f * 20f; GetMusicVolume 0x06001a0c
        // (x86-64 0x53e400) inverts with divss 20f and exp10f. Rows are the
        // volume, the decibels the setter stores and the linear value read back.
        private static readonly float[,] VolumeCases =
        {
            { -1f, -80f, 0.0001f }, { 0f, -80f, 0.0001f }, { 0.0001f, -80f, 0.0001f },
            { 0.001f, -60f, 0.001f }, { 0.1f, -20f, 0.1f }, { 0.5f, -6.0206f, 0.5f },
            { 1f, 0f, 1f }, { 2f, 0f, 1f },
        };
        private const float QuarterDecibels = -12.0412f;
        private const float HalfDecibels = -6.0206f;

        [Serializable]
        private sealed class ExportReceipt
        {
            public string status;
            public string project_path;
        }

        private sealed class MixerFixture
        {
            public readonly Dictionary<string, byte[]> Originals = new Dictionary<string, byte[]>();
            public string Folder;
            public bool FolderCreated;
            public HLAudioMixerDefinition Definition;
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

        // Steps a body iterator so its first failure stays the primary exception
        // and cleanup failures are only reported alongside it.
        private static IEnumerator Drive(IEnumerator body, Action<ExceptionDispatchInfo> fail)
        {
            while (true)
            {
                try { if (!body.MoveNext()) yield break; }
                catch (Exception failure) { fail(ExceptionDispatchInfo.Capture(failure)); yield break; }
                yield return body.Current;
            }
        }

        private static void Rethrow(ExceptionDispatchInfo primary, List<Exception> secondary)
        {
            if (primary != null)
            {
                if (secondary.Count != 0)
                    try { primary.SourceException.Data["MixerCleanupFailures"] = new AggregateException(secondary); } catch { }
                primary.Throw();
            }
            if (secondary.Count != 0) throw new AggregateException(secondary);
        }

        private static IEnumerator Run(Func<MixerFixture, IEnumerator> exercise)
        {
            var fixture = new MixerFixture();
            var secondary = new List<Exception>();
            ExceptionDispatchInfo primary = null;
            try
            {
                IEnumerator body = Drive(exercise(fixture), failure => primary = failure);
                while (body.MoveNext()) yield return body.Current;
            }
            finally { Cleanup(fixture, secondary); }
            Rethrow(primary, secondary);
        }

        private static void VerifyRawSupply(MixerFixture fixture)
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
            foreach (string path in new[] { raw, raw + ".meta", prepared, prepared + ".meta" })
                fixture.Originals.Add(path, HashFile(path));
            CollectionAssert.AreEqual(fixture.Originals[raw], fixture.Originals[prepared],
                "The supplied engine-only mixer must retain its raw exported asset bytes.");
        }

        private static string CopyOwnedMixer(MixerFixture fixture)
        {
            string folderName = "LucidMixerProof-" + Guid.NewGuid().ToString("N");
            fixture.Folder = "Assets/Recovered/" + folderName;
            string copiedPath = fixture.Folder + "/Main.mixer";
            Assert.That(AssetDatabase.IsValidFolder("Assets/Recovered"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(fixture.Folder), Is.False,
                "Mixer fixture destination must be fresh and owned by this test.");
            string createdGuid = AssetDatabase.CreateFolder("Assets/Recovered", folderName);
            fixture.FolderCreated = AssetDatabase.IsValidFolder(fixture.Folder);
            Assert.That(createdGuid, Is.Not.Null.And.Not.Empty);
            Assert.That(fixture.FolderCreated, Is.True);
            Assert.That(AssetDatabase.CopyAsset(SuppliedMixer, copiedPath), Is.True,
                "The fixture must copy the genuine supplied mixer without editing it.");
            Assert.That(AssetDatabase.AssetPathToGUID(copiedPath),
                Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(SuppliedMixer)));
            return copiedPath;
        }

        private static HLAudioMixerDefinition Bind(MixerFixture fixture, string copiedPath)
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(copiedPath);
            Assert.That(mixer, Is.Not.Null);
            fixture.Definition = ScriptableObject.CreateInstance<HLAudioMixerDefinition>();
            FieldInfo field = typeof(HLAudioMixerDefinition).GetField("m_audioMixer",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null);
            field.SetValue(fixture.Definition, mixer);
            return fixture.Definition;
        }

        private static void Cleanup(MixerFixture fixture, List<Exception> secondary)
        {
            try { if (fixture.Definition != null) UnityEngine.Object.DestroyImmediate(fixture.Definition); }
            catch (Exception failure) { secondary.Add(failure); }
            if (fixture.FolderCreated)
                try
                {
                    Assert.That(AssetDatabase.DeleteAsset(fixture.Folder), Is.True,
                        "All copied mixer assets and their temporary directory must be removed.");
                }
                catch (Exception failure) { secondary.Add(failure); }
            foreach (var pair in fixture.Originals)
                try
                {
                    CollectionAssert.AreEqual(pair.Value, HashFile(pair.Key),
                        "The original raw/prepared mixer or meta changed: " + pair.Key);
                }
                catch (Exception failure) { secondary.Add(failure); }
        }

        [UnityTest]
        public IEnumerator OriginalMixerKeysNormalizeAndResetTheSuppliedMixerAcrossFrames()
        {
            return Run(ExerciseSuppliedKeys);
        }

        private static IEnumerator ExerciseSuppliedKeys(MixerFixture fixture)
        {
            VerifyRawSupply(fixture);
            HLAudioMixerDefinition definition = Bind(fixture, CopyOwnedMixer(fixture));
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(fixture.Folder + "/Main.mixer");

            // Unity mixer startup must finish before setting exposed parameters.
            // The source mixer is never changed; all setters target this copy.
            yield return null;
            foreach (string key in Keys) Read(mixer, key);
            float pitch = Read(mixer, "MusicPitch");
            Assert.That(mixer.ClearFloat("AmbientVolume"), Is.True);
            float ambientSnapshot = Read(mixer, "AmbientVolume");
            Action<float>[] setters = { definition.SetMasterVolume, definition.SetMusicVolume,
                definition.SetSfxVolume, definition.SetVoiceVolume, definition.SetAmbientVolume };
            for (int row = 0; row < VolumeCases.GetLength(0); ++row)
            {
                float volume = VolumeCases[row, 0];
                for (int i = 0; i < setters.Length; ++i)
                {
                    setters[i](volume);
                    Assert.That(Read(mixer, Keys[i]), Is.EqualTo(VolumeCases[row, 1]).Within(0.0001f),
                        "Original clamp and decibel conversion for " + Keys[i] + " at " + volume);
                }
                Assert.That(definition.GetMusicVolume(), Is.EqualTo(VolumeCases[row, 2]).Within(0.000002f),
                    "Original music inverse conversion must use the actual engine value.");
            }
            // SetMute 0x06001a05 (x86-64 0x53e290): literal -80f when muted, xorps zero otherwise.
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
                new KeyValuePair<string, float>("MasterVolume", QuarterDecibels),
                new KeyValuePair<string, float>("MusicVolume", HalfDecibels),
                new KeyValuePair<string, float>("SfxVolume", -80f),
                new KeyValuePair<string, float>("VO", 0f) })
                Assert.That(Read(mixer, pair.Key), Is.EqualTo(pair.Value).Within(0.0001f));
            Assert.That(Read(mixer, "AmbientVolume"), Is.EqualTo(ambientSnapshot).Within(0.0001f));
            Assert.That(Read(mixer, "MusicPitch"), Is.EqualTo(pitch).Within(0.000001f),
                "Original volume setters must leave the separately exposed pitch unchanged.");
            yield return null;
            Assert.That(Read(mixer, "MusicVolume"), Is.EqualTo(HalfDecibels).Within(0.0001f),
                "The genuine exposed value must remain observable after another frame.");
        }

        [UnityTest]
        public IEnumerator OriginalGetMusicVolumeReturnsUnityGainWhenTheParameterIsMissing()
        {
            return Run(ExerciseMissingMusicParameter);
        }

        private static IEnumerator ExerciseMissingMusicParameter(MixerFixture fixture)
        {
            VerifyRawSupply(fixture);
            string copiedPath = CopyOwnedMixer(fixture);
            // Only the owned copy loses its exposed-parameter list; the supplied
            // mixer bytes stay pinned by the raw/prepared hashes during cleanup.
            string physical = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), copiedPath);
            string text = File.ReadAllText(physical);
            var exposed = new Regex(
                @"(?m)^  m_ExposedParameters:\r?\n(?:  - guid: [0-9a-f]{32}\r?\n    name: [^\r\n]*\r?\n)+");
            Assert.That(exposed.Matches(text).Count, Is.EqualTo(1),
                "The owned copy must contain exactly one exposed-parameter list to remove.");
            File.WriteAllText(physical, exposed.Replace(text, "  m_ExposedParameters: []\n"));
            AssetDatabase.ImportAsset(copiedPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            HLAudioMixerDefinition definition = Bind(fixture, copiedPath);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(copiedPath);
            yield return null;
            Assert.That(mixer.GetFloat("MusicVolume", out _), Is.False,
                "The owned copy must no longer expose MusicVolume.");
            // GetMusicVolume 0x06001a0c (x86-64 0x53e400) zeroes its local before
            // GetFloat and ignores the result, so exp10f(0f / 20f) returns one.
            Assert.That(definition.GetMusicVolume(), Is.EqualTo(1f));
        }
    }
}
#endif
