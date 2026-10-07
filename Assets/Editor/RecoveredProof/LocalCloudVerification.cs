using System;
using System.Globalization;
using System.IO;
using HLCloud.Plugin;
using UnityEngine;

namespace ProjectLucid
{
    /// <summary>Editor-only behavior checks for the intentional local storage adapter.</summary>
    public static class LocalCloudVerification
    {
        private static int checks;

        public static void Run()
        {
            checks = 0;
            string directory = Path.Combine(Path.GetTempPath(), "lucid-local-cloud-verification-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            CultureInfo oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                string path = Path.Combine(directory, "properties.json");
                Receiver receiver = new Receiver();
                ProjectLucid.Offline.LocalCloud cloud = new ProjectLucid.Offline.LocalCloud(path, receiver);
                receiver.plugin = cloud;
                int delivered = 0;
                Action<string> callback = message => { delivered++; receiver.Native_CloudDidChange(message); };
                Cloud.SubscribeOnConnect(callback);
                try
                {
                    cloud.InitializeWithGameObjectName("Verification", true);
                    Require(delivered == 0 && receiver.changes == 0, "initialization fabricated a cloud event");
                    Require(cloud.StringForKey("missing") == "" && cloud.IntForKey("missing") == 0 &&
                        cloud.FloatForKey("missing") == 0f && !cloud.BoolForKey("missing"), "missing-value defaults");
                    WriteFixture(cloud);
                    Require(cloud.StringForKey("float") == "1234.125", "float serialization depends on locale");
                    Require(cloud.Synchronize(), "first save failed: " + cloud.LastError);
                    Require(File.Exists(path) && !File.Exists(path + ".bak"), "first-save file state");
                    Require(delivered == 0 && receiver.changes == 0, "local writes fabricated a conflict notification");

                    ProjectLucid.Offline.LocalCloud reopened = new ProjectLucid.Offline.LocalCloud(path);
                    VerifyFixture(reopened);
                    var keys = reopened.RetrieveAllCloudKeys("|");
                    keys.Clear();
                    Require(reopened.RetrieveAllCloudKeys("|").Count == 8, "caller mutated internal keys");
                    cloud.SetStringForKey("unflushed", "string");
                    Require(new ProjectLucid.Offline.LocalCloud(path).StringForKey("string") == "Sonic \"dream\"\n\u2605", "saved snapshot changed after a setter");
                    cloud.RemoveForKey("nullable");
                    cloud.RemoveForKey("missing");
                    Require(cloud.Synchronize(), "second save failed");
                    Require(!new ProjectLucid.Offline.LocalCloud(path).RetrieveAllCloudKeys("").Contains("nullable"), "removal did not persist");
                    Require(File.Exists(path + ".bak"), "last-good backup missing");
                    VerifyFixture(new ProjectLucid.Offline.LocalCloud(path + ".bak"));

                    string message = "{\"NSUbiquitousKeyValueStoreChangeReasonKey\":3,\"NSUbiquitousKeyValueStoreChangedKeysKey\":[\"string\",\"int\"]}";
                    cloud.ReceiveNativeNotification(message);
                    Require(delivered == 1 && receiver.changes == 1 && receiver.reason == ChangeReason.AccountChange &&
                        receiver.keys.Length == 2 && receiver.keys[1] == "int", "raw native notification callback/decoder");
                    Cloud.UnsubscribeOnConnect(callback);
                    cloud.ReceiveNativeNotification(message);
                    Require(delivered == 1 && receiver.changes == 1, "unsubscribe did not remove callback");
                    ProjectLucid.Offline.LocalCloud direct = new ProjectLucid.Offline.LocalCloud(Path.Combine(directory, "direct.json"), receiver, true);
                    receiver.plugin = direct;
                    direct.ReceiveNativeNotification(message);
                    Require(receiver.changes == 2 && delivered == 1, "direct ICloud notification route");
                }
                finally { Cloud.UnsubscribeOnConnect(callback); }

                string checksumPath = Path.Combine(directory, "checksum.json");
                File.Copy(path + ".bak", checksumPath);
                File.Copy(path + ".bak", checksumPath + ".bak");
                File.WriteAllText(checksumPath, File.ReadAllText(checksumPath).Replace("1234.125", "1234.126"));
                ProjectLucid.Offline.LocalCloud checksumRecovery = new ProjectLucid.Offline.LocalCloud(checksumPath);
                VerifyFixture(checksumRecovery);
                Require(!String.IsNullOrEmpty(checksumRecovery.RecoveryNotice), "valid JSON with a bad checksum was accepted");
                File.Delete(checksumPath);
                ProjectLucid.Offline.LocalCloud missingPrimary = new ProjectLucid.Offline.LocalCloud(checksumPath);
                VerifyFixture(missingPrimary);
                Require(!String.IsNullOrEmpty(missingPrimary.RecoveryNotice) && !File.Exists(checksumPath), "missing-primary load changed disk");

                byte[] bad = new byte[] { 0x7b, 0x62, 0x61, 0x64 };
                File.WriteAllBytes(path, bad);
                ProjectLucid.Offline.LocalCloud recovered = new ProjectLucid.Offline.LocalCloud(path);
                VerifyFixture(recovered);
                Require(!String.IsNullOrEmpty(recovered.RecoveryNotice), "corrupt primary recovery was silent");
                Require(Equal(File.ReadAllBytes(path), bad), "load modified corrupt primary");
                recovered.SetIntForKey(9, "generation");
                Require(recovered.Synchronize(), "explicit backup recovery save failed: " + recovered.LastError);
                Require(new ProjectLucid.Offline.LocalCloud(path).IntForKey("generation") == 9, "recovery save not readable");
                VerifyFixture(new ProjectLucid.Offline.LocalCloud(path + ".bak"));
                string[] corruptFiles = Directory.GetFiles(directory, "properties.json.corrupt-*");
                Require(corruptFiles.Length == 1 && Equal(File.ReadAllBytes(corruptFiles[0]), bad), "corrupt evidence was not preserved");

                File.WriteAllBytes(path, bad);
                File.WriteAllBytes(path + ".bak", bad);
                ProjectLucid.Offline.LocalCloud invalid = new ProjectLucid.Offline.LocalCloud(path);
                bool rejected = false;
                try { invalid.InitializeWithGameObjectName("Verification", false); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected && !invalid.Synchronize() && !String.IsNullOrEmpty(invalid.LastError), "double corruption was accepted");
                Require(Equal(File.ReadAllBytes(path), bad) && Equal(File.ReadAllBytes(path + ".bak"), bad), "double corruption overwritten");

                string blocked = Path.Combine(directory, "blocked");
                File.WriteAllText(blocked, "retain");
                ProjectLucid.Offline.LocalCloud failedSave = new ProjectLucid.Offline.LocalCloud(Path.Combine(blocked, "properties.json"));
                failedSave.SetIntForKey(1, "value");
                Require(!failedSave.Synchronize() && !String.IsNullOrEmpty(failedSave.LastError) && File.ReadAllText(blocked) == "retain",
                    "I/O failure was hidden or damaged existing data");
                Cloud factory = Cloud.NativePluginInstance(new Receiver(), false);
                Require(factory is ProjectLucid.Offline.LocalCloud && ((ProjectLucid.Offline.LocalCloud)factory).SavePath == Path.GetFullPath(ProjectLucid.Offline.LocalCloud.DefaultSavePath), "factory did not choose local store");
                Debug.Log("Project Lucid local storage verification passed: " + checks + " checks. SaveManager integration remains unverified.");
            }
            finally
            {
                CultureInfo.CurrentCulture = oldCulture;
                Directory.Delete(directory, true);
            }
        }

        // Use separate Editor processes for these methods, in this order, with
        // LUCID_LOCAL_CLOUD_TEST_PATH set to an isolated absolute fixture path.
        public static void WriteRestartFixture()
        {
            ProjectLucid.Offline.LocalCloud cloud = new ProjectLucid.Offline.LocalCloud(RestartPath());
            WriteFixture(cloud);
            Require(cloud.Synchronize(), "restart fixture save failed: " + cloud.LastError);
            Debug.Log("Project Lucid local storage restart fixture written.");
        }

        public static void ReadRestartFixture()
        {
            VerifyFixture(new ProjectLucid.Offline.LocalCloud(RestartPath()));
            Debug.Log("Project Lucid local storage separate-process restart verification passed.");
        }

        private static string RestartPath()
        {
            string path = Environment.GetEnvironmentVariable("LUCID_LOCAL_CLOUD_TEST_PATH");
            if (String.IsNullOrEmpty(path) || !Path.IsPathRooted(path))
                throw new InvalidOperationException("Set LUCID_LOCAL_CLOUD_TEST_PATH to an isolated absolute test file.");
            return path;
        }

        private static void WriteFixture(ProjectLucid.Offline.LocalCloud cloud)
        {
            cloud.SetStringForKey("Sonic \"dream\"\n\u2605", "string");
            cloud.SetStringForKey("", "empty");
            cloud.SetStringForKey(null, "nullable");
            cloud.SetIntForKey(Int32.MinValue, "int");
            cloud.SetFloatForKey(1234.125f, "float");
            cloud.SetBoolForKey(true, "true");
            cloud.SetBoolForKey(false, "false");
            cloud.SetIntForKey(1, "generation");
        }

        private static void VerifyFixture(ProjectLucid.Offline.LocalCloud cloud)
        {
            Require(cloud.StringForKey("string") == "Sonic \"dream\"\n\u2605" && cloud.StringForKey("empty") == "" &&
                cloud.StringForKey("nullable") == null, "string/empty/null values did not survive restart");
            Require(cloud.IntForKey("int") == Int32.MinValue && cloud.FloatForKey("float") == 1234.125f &&
                cloud.BoolForKey("true") && !cloud.BoolForKey("false") && cloud.IntForKey("generation") == 1, "typed values did not survive restart");
        }

        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void Require(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Local storage verification failed: " + message);
        }

        private sealed class Receiver : ICloud
        {
            public ProjectLucid.Offline.LocalCloud plugin;
            public int changes;
            public ChangeReason reason;
            public string[] keys;
            public void Native_CloudDidChange(string cloudMessage) { plugin.CloudDidChange(cloudMessage); }
            public void OnCloudChange(string[] changedKeys, ChangeReason changeReason)
            { changes++; keys = changedKeys; reason = changeReason; }
        }
    }
}
