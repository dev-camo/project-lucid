using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Hardlight;

namespace ProjectLucid.Offline
{
    // Intentional offline port backend at Hardlight.IHLSaveMethod. The original
    // Hardlight.HLSaveMethodEncrypted remains preserved beside this backend.
    // New local slots use a checksummed envelope and last-good backup; the
    // original encrypted-save format is not migrated.
    public class LocalPropertySave : IHLSaveMethod
    {
        private const string BackupSuffix = "-backup";
        private readonly string OutputFileName;
        private readonly List<string> SaveIdentifiers = new List<string>();
        private readonly string directory;
        private readonly object gate = new object();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public string LastError { get; private set; }

        public LocalPropertySave(string key, string fileName)
            : this(key, fileName, Application.persistentDataPath) { }

        // Explicit directory injection permits isolated persistence verification.
        // The key remains an argument at the shipped boundary; local saves do
        // not need the original Apple application's encryption secrets.
        public LocalPropertySave(string key, string fileName, string directory)
        {
            ValidateFilePart(fileName, false);
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A local save directory is required.", nameof(directory));
            OutputFileName = fileName;
            this.directory = Path.GetFullPath(directory);
        }

        public IReadOnlyList<string> GetAllSaveIdentifiers()
        {
            lock (gate)
            {
                SaveIdentifiers.Clear();
                if (!Directory.Exists(directory)) return SaveIdentifiers.ToArray();
                var identifiers = new SortedSet<string>(StringComparer.Ordinal);
                foreach (string path in Directory.EnumerateFiles(directory))
                {
                    string fileName = Path.GetFileName(path);
                    if (!fileName.StartsWith(OutputFileName, StringComparison.Ordinal)) continue;
                    string id = GetSaveIdentifierFromFileName(fileName);
                    // Include a backup-only slot so a lost primary can recover.
                    // Temp/corrupt files begin with '.' and never match the prefix.
                    identifiers.Add(id);
                }
                SaveIdentifiers.AddRange(identifiers);
                return SaveIdentifiers.ToArray();
            }
        }

        public bool TryGetSaveVersion(string saveIdentifier, out long version)
        {
            lock (gate)
            {
                version = 0L;
                try
                {
                    string path = GetFilePath(HLPropertyStore.FileType.Primary, saveIdentifier);
                    // Preserve the original adapter's timestamp-version boundary.
                    version = File.GetLastWriteTime(path).Ticks;
                    LastError = null;
                    return true;
                }
                catch (Exception error) when (IsStorageFailure(error)) { LastError = error.Message; return false; }
            }
        }

        public bool SaveData(HLPropertyStore.FileType fileType, StringBuilder contentBuilder, string saveIdentifier)
        {
            lock (gate)
            {
                try
                {
                    string payload = contentBuilder.ToString();
                    string json = JsonUtility.ToJson(new Envelope { formatVersion = 1, payload = payload, sha256 = Hash(payload) });
                    string path = GetFilePath(fileType, saveIdentifier);
                    // Only a valid old primary may replace the last-good backup.
                    string backup = null;
                    if (fileType == HLPropertyStore.FileType.Primary && File.Exists(path))
                    {
                        string previous;
                        backup = TryRead(path, out previous) ? GetFilePath(HLPropertyStore.FileType.Backup, saveIdentifier)
                            : Path.Combine(directory, "." + Path.GetFileName(path) + ".corrupt-" + Guid.NewGuid().ToString("N"));
                    }
                    AtomicWrite(path, Utf8.GetBytes(json), backup);
                    LastError = null;
                    return true;
                }
                catch (Exception error) when (IsStorageFailure(error)) { LastError = error.Message; return false; }
            }
        }

        public bool BackupData(string saveIdentifier)
        {
            lock (gate)
            {
                try
                {
                    string path = GetFilePath(HLPropertyStore.FileType.Primary, saveIdentifier);
                    string payload;
                    if (!TryRead(path, out payload)) return false;
                    AtomicWrite(GetFilePath(HLPropertyStore.FileType.Backup, saveIdentifier), File.ReadAllBytes(path), null);
                    LastError = null;
                    return true;
                }
                catch (Exception error) when (IsStorageFailure(error)) { LastError = error.Message; return false; }
            }
        }

        public void WipeSaveFile(string saveIdentifier)
        {
            lock (gate)
            {
                // An explicit game slot deletion removes both known copies.
                // Stale temporary/diagnostic files are never mistaken for slots.
                File.Delete(GetFilePath(HLPropertyStore.FileType.Primary, saveIdentifier));
                File.Delete(GetFilePath(HLPropertyStore.FileType.Backup, saveIdentifier));
            }
        }

        public void WipeAllSaveFiles()
        {
            lock (gate)
                foreach (string id in GetAllSaveIdentifiers()) WipeSaveFile(id);
        }

        public string LoadData(HLPropertyStore.FileType fileType, string saveIdentifier)
        {
            lock (gate)
            {
                string payload;
                if (TryRead(GetFilePath(fileType, saveIdentifier), out payload)) return payload;
                if (fileType == HLPropertyStore.FileType.Backup)
                {
                    string primary = GetFilePath(HLPropertyStore.FileType.Primary, saveIdentifier);
                    string backup = GetFilePath(HLPropertyStore.FileType.Backup, saveIdentifier);
                    // Both absent means a new slot. Both unreadable is a usable
                    // error, preserving the files instead of initializing empty
                    // progress which the next save could overwrite.
                    if ((File.Exists(primary) || File.Exists(backup)) && !TryRead(primary, out payload))
                        throw new InvalidDataException("Local property save and backup are unreadable; both files were preserved.");
                }
                return null;
            }
        }

        private bool TryRead(string path, out string payload)
        {
            payload = null;
            if (!File.Exists(path)) return false;
            try
            {
                Envelope envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Utf8));
                if (envelope == null || envelope.formatVersion != 1 || envelope.payload == null ||
                    !string.Equals(envelope.sha256, Hash(envelope.payload), StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid local property-save envelope: " + path);
                payload = envelope.payload;
                LastError = null;
                return true;
            }
            catch (Exception error) when (IsStorageFailure(error)) { LastError = error.Message; return false; }
        }

        private void AtomicWrite(string path, byte[] bytes, string backup)
        {
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, backup, true);
                else File.Move(temporary, path);
            }
            finally
            {
                // No delete-then-move fallback: failed writes retain the old file.
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private string GetFilePath(HLPropertyStore.FileType fileType, string saveIdentifier)
        {
            return Path.Combine(directory, GetFileName(fileType, saveIdentifier));
        }

        private string GetFileName(HLPropertyStore.FileType fileType, string saveIdentifier)
        {
            saveIdentifier = saveIdentifier ?? string.Empty;
            ValidateFilePart(saveIdentifier, true);
            if (fileType != HLPropertyStore.FileType.Primary && fileType != HLPropertyStore.FileType.Backup)
                throw new ArgumentOutOfRangeException(nameof(fileType));
            return OutputFileName + saveIdentifier + (fileType == HLPropertyStore.FileType.Backup ? BackupSuffix : string.Empty);
        }

        private string GetSaveIdentifierFromFileName(string fileName)
        {
            string id = fileName.Substring(OutputFileName.Length);
            return id.EndsWith(BackupSuffix, StringComparison.Ordinal) ? id.Substring(0, id.Length - BackupSuffix.Length) : id;
        }

        private static void ValidateFilePart(string value, bool allowEmpty)
        {
            if (value == null || (!allowEmpty && value.Length == 0) || value.StartsWith(".", StringComparison.Ordinal) ||
                value.EndsWith(BackupSuffix, StringComparison.Ordinal))
                throw new ArgumentException("Invalid local save filename or identifier.");
            foreach (char character in value)
                if (character < 32 || "<>:\"/\\|?*".IndexOf(character) >= 0)
                    throw new ArgumentException("Local save filenames and identifiers must be portable file components.");
        }

        private static bool IsStorageFailure(Exception error)
        {
            return error is IOException || error is UnauthorizedAccessException || error is ArgumentException ||
                error is NotSupportedException || error is CryptographicException;
        }

        private static string Hash(string payload)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(Utf8.GetBytes(payload))).Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable] private sealed class Envelope { public int formatVersion; public string payload; public string sha256; }
    }
}
