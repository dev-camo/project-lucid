using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Hardlight
{
    // Genuine original HLUnityCore.Runtime 0x02000204, 0x06000d4c..0x06000d58.
    // Reconstructed from both native architectures. The offline provider selects
    // ProjectLucid.Offline.LocalPropertySave instead. Crypto vectors validate the
    // format; native retry/disposal fault equivalence remains unverified.
    public class HLSaveMethodEncrypted : IHLSaveMethod
    {
        private const string BackupSuffix = "-backup";
        private readonly string OutputFileName;
        private readonly string Key;
        private readonly byte[] EncryptionKey;
        private readonly byte[] InitialisationVector = Encoding.UTF8.GetBytes("7%o18m'b~)mQD%o1");
        private readonly List<string> SaveIdentifiers = new List<string>();

        public HLSaveMethodEncrypted(string key, string fileName)
        {
            Key = key;
            OutputFileName = fileName;
            byte[] keyBytes = Encoding.UTF8.GetBytes(Key);
            char[] encodedKey = Convert.ToBase64String(keyBytes).ToCharArray();
            EncryptionKey = Encoding.UTF8.GetBytes(encodedKey, 0, 16);
        }

        public IReadOnlyList<string> GetAllSaveIdentifiers()
        {
            string directory = Path.GetDirectoryName(GetFilePath(HLPropertyStore.FileType.Primary,
                HLPropertyStore.DefaultSaveIdentifier));
            if (string.IsNullOrEmpty(directory)) return null;
            SaveIdentifiers.Clear();
            foreach (string file in Directory.GetFiles(directory))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (!fileName.Contains(OutputFileName)) continue;
                string saveIdentifier = GetSaveIdentifierFromFileName(fileName);
                if (!SaveIdentifiers.Contains(saveIdentifier)) SaveIdentifiers.Add(saveIdentifier);
            }
            return SaveIdentifiers;
        }

        public bool TryGetSaveVersion(string saveIdentifier, out long version)
        {
            version = 0;
            // Native LSDA excludes path construction from the catch.
            string filePath = GetFilePath(HLPropertyStore.FileType.Primary, saveIdentifier);
            try
            {
                version = File.GetLastWriteTime(filePath).Ticks;
                return true;
            }
            catch (Exception exception)
            {
                HLOutput.LogError(string.Format(
                    "Save version for save identifier '{0}' at path '{1}' threw the exception: {2}",
                    saveIdentifier, filePath, exception));
                return false;
            }
        }

        private byte[] Encrypt(string abtest)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.Zeros;
                aes.Key = EncryptionKey;
                aes.IV = InitialisationVector;
                ICryptoTransform encryptor = aes.CreateEncryptor();
                byte[] plaintext = Encoding.UTF8.GetBytes(abtest);
                // The native original disposes the Aes instance only.
                return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
            }
        }

        private string Decrypt(byte[] ciphertext)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.Zeros;
                aes.Key = EncryptionKey;
                aes.IV = InitialisationVector;
                ICryptoTransform decryptor = aes.CreateDecryptor();
                Encoding encoding = Encoding.UTF8;
                // Configuration/CreateDecryptor/UTF8 retrieval are outside this catch.
                try
                {
                    return encoding.GetString(decryptor.TransformFinalBlock(ciphertext, 0,
                        ciphertext.Length));
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
        }

        public bool SaveData(HLPropertyStore.FileType fileType, StringBuilder contentBuilder,
            string saveIdentifier)
        {
            string filePath = GetFilePath(fileType, saveIdentifier);
            byte[] encrypted = Encrypt(contentBuilder.ToString());
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    // The original owns the stream through BinaryWriter alone. A failed
                    // writer constructor does not acquire an additional stream finally.
                    using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
                    {
                        writer.Write(encrypted.Length);
                        writer.Write(encrypted, 0, encrypted.Length);
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    HLOutput.LogError(string.Format("Exception occurred during saving {0}", exception)
                        + exception.ToString());
                    Resources.UnloadUnusedAssets();
                    PlatformUtils.ClearCache();
                    GC.Collect();
                    Thread.Sleep(10);
                }
            }
            return false;
        }

        public bool BackupData(string saveIdentifier)
        {
            try
            {
                string primary = GetFilePath(HLPropertyStore.FileType.Primary, saveIdentifier);
                string backup = GetFilePath(HLPropertyStore.FileType.Backup, saveIdentifier);
                File.Copy(primary, backup, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void WipeSaveFile(string saveIdentifier)
        {
            File.Delete(GetFilePath(HLPropertyStore.FileType.Primary, saveIdentifier));
            File.Delete(GetFilePath(HLPropertyStore.FileType.Backup, saveIdentifier));
        }

        public void WipeAllSaveFiles()
        {
            string directory = Path.GetDirectoryName(GetFilePath(HLPropertyStore.FileType.Primary,
                HLPropertyStore.DefaultSaveIdentifier));
            if (string.IsNullOrEmpty(directory)) return;
            SaveIdentifiers.Clear();
            foreach (string file in Directory.GetFiles(directory))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (!fileName.Contains(OutputFileName)) continue;
                // Original LSDA covers File.Delete only, not enumeration/filtering.
                try
                {
                    File.Delete(file);
                }
                catch (Exception exception)
                {
                    HLOutput.LogError("HLSaveMethodEncrypted failed to delete a save file with the following exception"
                        + exception.Message);
                }
            }
        }

        public string LoadData(HLPropertyStore.FileType fileType, string saveIdentifier)
        {
            string content = null;
            string filePath = GetFilePath(fileType, saveIdentifier);
            if (File.Exists(filePath))
            {
                try
                {
                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            int length = reader.ReadInt32();
                            content = Decrypt(reader.ReadBytes(length));
                        }
                    }
                }
                catch (Exception)
                {
                    // Preserve already assigned content when reader/stream disposal fails.
                }
            }
            return content;
        }

        private string GetFilePath(HLPropertyStore.FileType fileType, string saveIdentifier)
        {
            string fileName = GetFileName(fileType, saveIdentifier);
            return Application.persistentDataPath + "/" + fileName;
        }

        private string GetFileName(HLPropertyStore.FileType fileType, string saveIdentifier)
        {
            return fileType == HLPropertyStore.FileType.Primary
                ? OutputFileName + saveIdentifier
                : OutputFileName + saveIdentifier + BackupSuffix;
        }

        private string GetSaveIdentifierFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            return fileName.Replace(OutputFileName, string.Empty).Replace(BackupSuffix, string.Empty);
        }
    }
}
