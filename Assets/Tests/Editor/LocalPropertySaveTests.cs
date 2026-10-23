using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Offline;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class LocalPropertySaveTests
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        [TestCase("version")]
        [TestCase("checksum")]
        [TestCase("missing-payload")]
        public void InvalidPrimaryRecoversBackupAndExplicitSavePreservesCorruption(string fault)
        {
            WithOwnedDirectory(directory =>
            {
                const string first = "first valid payload\n";
                const string later = "later explicit payload\n";
                var adapter = new LocalPropertySave("unused", "save-", directory);
                Assert.That(adapter.SaveData(HLPropertyStore.FileType.Primary, new StringBuilder(first), "slot"), Is.True);
                Assert.That(adapter.BackupData("slot"), Is.True);
                string primary = Path.Combine(directory, "save-slot");
                string backup = primary + "-backup";
                byte[] validBackup = File.ReadAllBytes(backup);
                byte[] invalid = InvalidEnvelope(fault);
                File.WriteAllBytes(primary, invalid);

                var restarted = new LocalPropertySave("unused", "save-", directory);
                Assert.That(restarted.LoadData(HLPropertyStore.FileType.Primary, "slot"), Is.Null);
                Assert.That(restarted.LastError, Does.Contain("Invalid local property-save envelope"));
                Assert.That(restarted.LoadData(HLPropertyStore.FileType.Backup, "slot"), Is.EqualTo(first));
                Assert.That(restarted.LastError, Is.Null);
                Assert.That(File.ReadAllBytes(primary), Is.EqualTo(invalid), "Recovery must not repair or erase the primary on read.");
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(validBackup));

                Assert.That(restarted.BackupData("slot"), Is.False, "An invalid primary cannot replace the last-good backup.");
                Assert.That(File.ReadAllBytes(primary), Is.EqualTo(invalid));
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(validBackup));
                Assert.That(restarted.SaveData(HLPropertyStore.FileType.Primary, new StringBuilder(later), "slot"), Is.True);
                Assert.That(restarted.LastError, Is.Null);
                Assert.That(restarted.LoadData(HLPropertyStore.FileType.Primary, "slot"), Is.EqualTo(later));
                Assert.That(restarted.LoadData(HLPropertyStore.FileType.Backup, "slot"), Is.EqualTo(first));
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(validBackup), "Explicit replacement preserves the last-good backup.");
                string[] quarantined = Directory.GetFiles(directory, ".save-slot.corrupt-*");
                Assert.That(quarantined.Length, Is.EqualTo(1));
                Assert.That(File.ReadAllBytes(quarantined[0]), Is.EqualTo(invalid), "The replaced corrupt envelope remains available for recovery.");
            });
        }

        // HLSaveMethodEncrypted.SaveData makes up to five write attempts, logging each failure
        // before the next (#462). A directory where the save file belongs blocks every write;
        // removing it when the first failure is logged lets the next attempt succeed.
        [TestCase(1, true)]
        [TestCase(5, false)]
        public void FailedWritesAreRetriedUpToFiveTimesLikeTheOriginal(int failures, bool saved)
        {
            WithOwnedDirectory(directory =>
            {
                var adapter = new LocalPropertySave("unused", "save-", directory);
                string primary = Path.Combine(directory, "save-slot");
                Directory.CreateDirectory(primary);
                bool result = false;
                List<string> errors;
                try
                {
                    errors = HLOutputDiagnosticCapture.CaptureErrors(
                        () => result = adapter.SaveData(HLPropertyStore.FileType.Primary, new StringBuilder("payload\n"), "slot"),
                        message => { if (failures == 1) Directory.Delete(primary); });
                }
                finally
                {
                    // The fixture cleans flat files only. Non-recursive: a failed write must leave nothing inside.
                    if (!saved && Directory.Exists(primary)) Directory.Delete(primary);
                }
                Assert.That(result, Is.EqualTo(saved));
                Assert.That(errors.Count, Is.EqualTo(failures));
                Assert.That(errors, Has.All.StartsWith("Exception occurred during saving "));
                if (saved)
                {
                    Assert.That(adapter.LastError, Is.Null);
                    Assert.That(adapter.LoadData(HLPropertyStore.FileType.Primary, "slot"), Is.EqualTo("payload\n"));
                }
                else
                {
                    Assert.That(adapter.LastError, Is.Not.Null);
                    Assert.That(File.Exists(primary), Is.False, "A failed save writes nothing at the path.");
                }
            });
        }

        [TestCase("version")]
        [TestCase("checksum")]
        [TestCase("missing-payload")]
        public void TwoInvalidEnvelopesReportFinalErrorWithoutErasingEitherCopy(string fault)
        {
            WithOwnedDirectory(directory =>
            {
                string primary = Path.Combine(directory, "save-slot");
                string backup = primary + "-backup";
                byte[] invalidPrimary = InvalidEnvelope(fault);
                byte[] invalidBackup = Utf8.GetBytes(Utf8.GetString(invalidPrimary) + "\n");
                File.WriteAllBytes(primary, invalidPrimary);
                File.WriteAllBytes(backup, invalidBackup);
                var adapter = new LocalPropertySave("unused", "save-", directory);

                Assert.That(adapter.LoadData(HLPropertyStore.FileType.Primary, "slot"), Is.Null);
                InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                    adapter.LoadData(HLPropertyStore.FileType.Backup, "slot"));
                Assert.That(error.Message, Is.EqualTo("Local property save and backup are unreadable; both files were preserved."));
                Assert.That(File.ReadAllBytes(primary), Is.EqualTo(invalidPrimary));
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(invalidBackup));
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(2), "Read failure must not create a replacement or quarantine file.");
            });
        }

        private static byte[] InvalidEnvelope(string fault)
        {
            switch (fault)
            {
                // Correct checksum isolates the format-version rejection.
                case "version": return Utf8.GetBytes("{\"formatVersion\":2,\"payload\":\"untrusted\",\"sha256\":\"6353de988bb15f611bd2eb9ca3a62eb2aeda604e2fdd570802f5c15404c073e3\"}");
                case "checksum": return Utf8.GetBytes("{\"formatVersion\":1,\"payload\":\"untrusted\",\"sha256\":\"wrong\"}");
                case "missing-payload": return Utf8.GetBytes("{\"formatVersion\":1,\"sha256\":\"wrong\"}");
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
        }

        private static void WithOwnedDirectory(Action<string> verify)
        {
            // Inject a fresh private directory; never use the store singleton,
            // registry, callbacks or the user's persistent-data directory.
            string directory = Path.Combine(Path.GetTempPath(), "ProjectLucidLocalSave-" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(directory) || File.Exists(directory), Is.False);
            Directory.CreateDirectory(directory);
            try { verify(directory); }
            finally
            {
                // The adapter writes flat files only. Do not recurse into an
                // unexpected directory while cleaning this disposable fixture.
                foreach (string path in Directory.GetFiles(directory)) File.Delete(path);
                Directory.Delete(directory);
            }
        }
    }
}
