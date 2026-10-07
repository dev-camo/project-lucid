using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HLCloud.Plugin.iOS;
using UnityEngine;
using HLCloud.Plugin;

namespace ProjectLucid.Offline
{
    /// <summary>
    /// Portable replacement at the original plugin boundary, not a recovered
    /// implementation of Apple's cloud store or the game's SaveManager.
    /// </summary>
    public class LocalCloud : Cloud
    {
        // Local state is guarded and key sets returned
        // to callers are copies, so a caller cannot mutate a pending save snapshot.
        private readonly Dictionary<string, string> m_cloudProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> m_allCloudKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly object m_dataGate = new object();
        private readonly object m_saveGate = new object();
        private readonly string m_savePath;
        private readonly bool m_useFindGameObject;
        private bool m_loaded;
        private Exception m_loadFailure;

        public static string DefaultSavePath => Path.Combine(Application.persistentDataPath, "LocalCloud", "properties.json");
        public string SavePath => m_savePath;
        public string LastError { get; private set; }
        public string RecoveryNotice { get; private set; }

        public LocalCloud() : this(DefaultSavePath) { }

        // Explicit injection keeps verification and future save-slot tooling away
        // from users' real data. No Apple/Game Center identifier selects this path.
        public LocalCloud(string savePath, ICloud cloudObject = null, bool useFindGameObject = false)
        {
            if (String.IsNullOrEmpty(savePath)) throw new ArgumentException("A save file path is required.", nameof(savePath));
            m_savePath = Path.GetFullPath(savePath);
            this.cloudObject = cloudObject;
            m_useFindGameObject = useFindGameObject;
        }

        // Original F15CFA21AE73DF8B42D4EB5A7FD726A6554DF63F is RET.
        // Replacement loads disk state; it emits no fabricated initial-sync event.
        public override void InitializeWithGameObjectName(string gameObjectName, bool useGamePlayerID)
        {
            lock (m_dataGate) EnsureLoaded();
        }

        public override bool Synchronize()
        {
            lock (m_saveGate)
            {
                string temporary = null;
                try
                {
                    Entry[] snapshot;
                    lock (m_dataGate)
                    {
                        EnsureLoaded();
                        snapshot = MakeSnapshot();
                    }
                    // Serialize an independent, immutable-by-caller snapshot. A
                    // concurrent Set* belongs to the next explicit Synchronize.
                    string payload = JsonUtility.ToJson(new Payload { entries = snapshot });
                    string json = JsonUtility.ToJson(new Envelope { formatVersion = 1, payload = payload, sha256 = Hash(payload) });
                    string directory = Path.GetDirectoryName(m_savePath);
                    Directory.CreateDirectory(directory);
                    temporary = Path.Combine(directory, "." + Path.GetFileName(m_savePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
                    byte[] bytes = new UTF8Encoding(false, true).GetBytes(json);
                    using (FileStream file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        file.Write(bytes, 0, bytes.Length);
                        file.Flush(true);
                    }

                    if (File.Exists(m_savePath))
                    {
                        Dictionary<string, string> oldValues;
                        Exception error;
                        bool previousIsGood = TryRead(m_savePath, out oldValues, out error);
                        // Keep a bad primary for diagnosis without replacing the
                        // last-good backup with bad bytes. Atomic replace only;
                        // deliberately no delete-then-move fallback.
                        string backup = previousIsGood ? m_savePath + ".bak" : m_savePath + ".corrupt-" + Guid.NewGuid().ToString("N");
                        File.Replace(temporary, m_savePath, backup, true);
                    }
                    else File.Move(temporary, m_savePath);
                    temporary = null;
                    LastError = null;
                    return true;
                }
                catch (Exception exception) when (IsStorageFailure(exception))
                {
                    LastError = exception.Message;
                    return false;
                }
                finally
                {
                    if (temporary != null)
                    {
                        try { File.Delete(temporary); }
                        catch (Exception exception) when (IsStorageFailure(exception)) { }
                    }
                }
            }
        }

        public override HashSet<string> RetrieveAllCloudKeys(string allCloudKeysSeparator)
        {
            // Recovered 7A877C9D92E218D7008DF563323159341FABD76B rebuilds
            // m_allCloudKeys from dictionary keys and ignores the separator.
            lock (m_dataGate)
            {
                EnsureLoaded();
                m_allCloudKeys.Clear();
                m_allCloudKeys.UnionWith(m_cloudProperties.Keys);
                return new HashSet<string>(m_allCloudKeys, StringComparer.Ordinal);
            }
        }

        public override string StringForKey(string key)
        {
            lock (m_dataGate)
            {
                EnsureLoaded();
                string value;
                return m_cloudProperties.TryGetValue(key, out value) ? value : String.Empty;
            }
        }

        public override void SetStringForKey(string value, string key)
        {
            // Original F9625D2C6865558BD14902D4C728F69C00C556E3 sets the
            // dictionary value. Preserve strings, including empty/null values.
            lock (m_dataGate) { EnsureLoaded(); m_cloudProperties[key] = value; }
        }

        public override float FloatForKey(string key)
        {
            string value;
            if (!TryValue(key, out value)) return 0f;
            return Single.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public override void SetFloatForKey(float value, string key)
        {
            SetStringForKey(value.ToString("R", CultureInfo.InvariantCulture), key);
        }

        public override int IntForKey(string key)
        {
            string value;
            if (!TryValue(key, out value)) return 0;
            return Int32.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        public override void SetIntForKey(int value, string key)
        {
            SetStringForKey(value.ToString(CultureInfo.InvariantCulture), key);
        }

        public override bool BoolForKey(string key)
        {
            string value;
            if (!TryValue(key, out value)) return false;
            if (value == "1") return true;
            if (value == "0") return false;
            return Boolean.Parse(value);
        }

        public override void SetBoolForKey(bool value, string key)
        {
            SetStringForKey(value ? "true" : "false", key);
        }

        public override void RemoveForKey(string key)
        {
            // Original BD0CFE5A2CBBB8234B7920FDC9AA058211B5EB87 is Dictionary.Remove.
            lock (m_dataGate) { EnsureLoaded(); m_cloudProperties.Remove(key); }
        }

        public override void CloudDidChange(string message)
        {
            // Recovered B0BE911DDA7D29A201E8E63E112652CF305C6C8C uses
            // FromJsonOverwrite(UserInfo), then ICloud.OnCloudChange(keys, reason).
            UserInfo info = new UserInfo();
            JsonUtility.FromJsonOverwrite(message, info);
            if (cloudObject == null) throw new InvalidOperationException("Cloud notification requires an ICloud receiver.");
            cloudObject.OnCloudChange(info.NSUbiquitousKeyValueStoreChangedKeysKey,
                (ChangeReason)info.NSUbiquitousKeyValueStoreChangeReasonKey);
        }

        /// <summary>
        /// Explicit notification input for a future transport. The original Mac
        /// callback sends raw commandData through m_onConnect, or directly to the
        /// named GameObject when useFindGameObject is true. This local replacement
        /// uses the supplied ICloud object for that direct route. Disk operations
        /// never call this method and never synthesize cloud-conflict notifications.
        /// </summary>
        public void ReceiveNativeNotification(string message)
        {
            if (m_useFindGameObject)
            {
                if (cloudObject == null) throw new InvalidOperationException("Cloud notification requires an ICloud receiver.");
                cloudObject.Native_CloudDidChange(message);
            }
            else m_onConnect?.Invoke(message);
        }

        private bool TryValue(string key, out string value)
        {
            lock (m_dataGate) { EnsureLoaded(); return m_cloudProperties.TryGetValue(key, out value); }
        }

        private void EnsureLoaded()
        {
            if (m_loadFailure != null) throw m_loadFailure;
            if (m_loaded) return;
            Dictionary<string, string> values;
            Exception primaryError;
            Exception backupError;
            bool hasPrimary = File.Exists(m_savePath);
            bool hasBackup = File.Exists(m_savePath + ".bak");
            if (hasPrimary && TryRead(m_savePath, out values, out primaryError)) LoadValues(values);
            else if (hasBackup && TryRead(m_savePath + ".bak", out values, out backupError))
            {
                LoadValues(values);
                RecoveryNotice = "Loaded last-good backup; primary save is missing or invalid. No files were changed while loading: " + m_savePath;
                Debug.LogWarning(RecoveryNotice);
            }
            else if (hasPrimary || hasBackup)
            {
                m_loadFailure = new InvalidDataException("Local save and last-good backup could not be read. Preserved both files; refusing to initialize empty data: " + m_savePath);
                LastError = m_loadFailure.Message;
                throw m_loadFailure;
            }
            m_loaded = true;
        }

        private void LoadValues(Dictionary<string, string> values)
        {
            foreach (KeyValuePair<string, string> entry in values) m_cloudProperties.Add(entry.Key, entry.Value);
        }

        private Entry[] MakeSnapshot()
        {
            List<string> keys = new List<string>(m_cloudProperties.Keys);
            keys.Sort(StringComparer.Ordinal);
            Entry[] entries = new Entry[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                string value = m_cloudProperties[keys[i]];
                entries[i] = new Entry { key = keys[i], value = value ?? String.Empty, valueIsNull = value == null };
            }
            return entries;
        }

        private static bool TryRead(string path, out Dictionary<string, string> values, out Exception error)
        {
            values = null;
            error = null;
            try
            {
                string json = File.ReadAllText(path, new UTF8Encoding(false, true));
                Envelope envelope = JsonUtility.FromJson<Envelope>(json);
                if (envelope == null || envelope.formatVersion != 1 || envelope.payload == null ||
                    !String.Equals(envelope.sha256, Hash(envelope.payload), StringComparison.Ordinal))
                    throw new InvalidDataException("Unsupported or corrupt local save envelope.");
                Payload payload = JsonUtility.FromJson<Payload>(envelope.payload);
                if (payload == null || payload.entries == null) throw new InvalidDataException("Missing local save entries.");
                Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Entry entry in payload.entries)
                {
                    if (entry == null || entry.key == null || result.ContainsKey(entry.key))
                        throw new InvalidDataException("Invalid or duplicate local save key.");
                    result.Add(entry.key, entry.valueIsNull ? null : entry.value);
                }
                values = result;
                return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                error = exception;
                return false;
            }
        }

        private static bool IsStorageFailure(Exception exception)
        {
            return exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException || exception is CryptographicException;
        }

        private static string Hash(string value)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(new UTF8Encoding(false, true).GetBytes(value));
                return BitConverter.ToString(bytes).Replace("-", String.Empty).ToLowerInvariant();
            }
        }

        [Serializable] private sealed class Envelope { public int formatVersion; public string payload; public string sha256; }
        [Serializable] private sealed class Payload { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string key; public string value; public bool valueIsNull; }
    }
}
