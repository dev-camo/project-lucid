using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Hardlight;
using ProjectLucid.Offline;

namespace ProjectLucid.Editor
{
    // Pure format checks call the preserved original Encrypt/Decrypt methods.
    // Expected bytes were generated independently with OpenSSL AES-128-CBC,
    // -nopad, and explicitly zero-padded UTF-8 input. Empty input stays empty;
    // exact blocks receive no extra block. No storage operation runs here.
    public static class OriginalEncryptedSaveVerification
    {
        private const string SyntheticKey = "project-lucid synthetic encryption key";
        private const string SyntheticFileName = "original-format-fixture";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private sealed class Vector
        {
            public readonly string Name;
            public readonly string PlaintextBase64;
            public readonly string CiphertextHex;
            public readonly string PaddedPlaintextBase64;

            public Vector(string name, string plaintextBase64, string ciphertextHex, string paddedPlaintextBase64)
            {
                Name = name;
                PlaintextBase64 = plaintextBase64;
                CiphertextHex = ciphertextHex;
                PaddedPlaintextBase64 = paddedPlaintextBase64;
            }
        }

        // Synthetic key UTF-8 -> Base64 -> first sixteen UTF-8 ASCII bytes:
        // 63484a76616d566a6443317364574e70. Original IV:
        // 37256f31386d27627e296d5144256f31.
        private static readonly Vector[] Vectors =
        {
            new Vector("empty", "", "", ""),
            new Vector("one UTF-8 byte", "YQ==", "39024a5ba5a09ec485a7e8bd6ed5d08f", "YQAAAAAAAAAAAAAAAAAAAA=="),
            new Vector("exact sixteen UTF-8 bytes", "MDEyMzQ1Njc4OWFiY2RlZg==", "8cceb30a6b2636f8d89ae2b325c46886", "MDEyMzQ1Njc4OWFiY2RlZg=="),
            new Vector("Unicode", "U29uaWMg8J+mlCDigJQg5rWL6K+V", "f9aac0bb59fa9031352035643b24df71b8a2dc5b5613a94bfd19348302b877b2", "U29uaWMg8J+mlCDigJQg5rWL6K+VAAAAAAAAAAAAAAA="),
            new Vector("one authored NUL byte", "AA==", "7a4c9fdd29fea983f80290c2535ffb8b", "AAAAAAAAAAAAAAAAAAAAAA=="),
            new Vector("authored trailing NUL plus padding", "dHJhaWxpbmcA", "074d09826b24039eecdcce0a6a7e056f", "dHJhaWxpbmcAAAAAAAAAAA==")
        };

        public static int RunFixedVectors()
        {
            int checks = 0;
            var original = CreateOriginal();
            foreach (Vector vector in Vectors)
            {
                string plaintext = Encoding.UTF8.GetString(Convert.FromBase64String(vector.PlaintextBase64));
                byte[] expectedCiphertext = Hex(vector.CiphertextHex);
                byte[] actualCiphertext = (byte[])Invoke(original, "Encrypt", plaintext);
                Require(actualCiphertext.SequenceEqual(expectedCiphertext), vector.Name + " encrypts to the independent fixed ciphertext", ref checks);

                // Decrypt the independent bytes, not the output of Encrypt.
                string actualPlaintext = (string)Invoke(original, "Decrypt", expectedCiphertext);
                string expectedPlaintext = Encoding.UTF8.GetString(Convert.FromBase64String(vector.PaddedPlaintextBase64));
                Require(actualPlaintext == expectedPlaintext, vector.Name + " decrypts with every zero-padding byte retained", ref checks);
            }
            return checks;
        }

        public static int RunConstructorBoundaries()
        {
            int checks = 0;
            RequireThrows<ArgumentNullException>(() => new HLSaveMethodEncrypted(null, SyntheticFileName), "a null original key propagates", ref checks);
            RequireThrows<ArgumentOutOfRangeException>(() => new HLSaveMethodEncrypted("", SyntheticFileName), "an empty original key cannot supply sixteen Base64 characters", ref checks);
            RequireThrows<ArgumentOutOfRangeException>(() => new HLSaveMethodEncrypted("012345678", SyntheticFileName), "nine UTF-8 key bytes remain too short", ref checks);

            var minimumAscii = new HLSaveMethodEncrypted("0123456789", SyntheticFileName);
            Require(((byte[])Field("EncryptionKey").GetValue(minimumAscii)).SequenceEqual(Hex("4d4445794d7a51314e6a63344f513d3d")), "ten UTF-8 bytes retain the Base64 equals characters in the AES key", ref checks);
            var minimumUnicode = new HLSaveMethodEncrypted("\u00e9\u00e9\u00e9\u00e9\u00e9", SyntheticFileName);
            Require(((byte[])Field("EncryptionKey").GetValue(minimumUnicode)).SequenceEqual(Hex("77366e4471634f7077366e4471513d3d")), "the key boundary counts UTF-8 bytes rather than string characters", ref checks);

            var original = CreateOriginal();
            Require(((byte[])Field("EncryptionKey").GetValue(original)).SequenceEqual(Hex("63484a76616d566a6443317364574e70")), "the original synthetic-key derivation retains its fixed bytes", ref checks);
            Require(((byte[])Field("InitialisationVector").GetValue(original)).SequenceEqual(Hex("37256f31386d27627e296d5144256f31")), "the original IV retains its fixed bytes", ref checks);
            var noFileName = new HLSaveMethodEncrypted(SyntheticKey, null);
            Require(Field("OutputFileName").GetValue(noFileName) == null, "the original constructor accepts and retains a null file name", ref checks);
            return checks;
        }

        public static int RunDecryptCatchBoundaries()
        {
            int checks = 0;
            var original = CreateOriginal();
            RequireThrows<ArgumentNullException>(() => Invoke(original, "Encrypt", new object[] { null }), "null plaintext propagates from the original Encrypt", ref checks);
            Require((string)Invoke(original, "Decrypt", new object[] { null }) == string.Empty, "null ciphertext is handled inside the original Decrypt catch", ref checks);
            Require((string)Invoke(original, "Decrypt", new byte[1]) == string.Empty, "a one-byte partial block returns empty", ref checks);
            Require((string)Invoke(original, "Decrypt", new byte[15]) == string.Empty, "a fifteen-byte partial block returns empty", ref checks);
            Require((string)Invoke(original, "Decrypt", new byte[17]) == string.Empty, "a partial second block returns empty", ref checks);

            // Corrupt only fresh fixture instances to reach the real AES setup
            // faults. These setters execute before the original inner catch.
            var invalidKey = CreateOriginal();
            Field("EncryptionKey").SetValue(invalidKey, new byte[1]);
            RequireThrows<CryptographicException>(() => Invoke(invalidKey, "Decrypt", new byte[16]), "an invalid AES key propagates outside the Decrypt catch", ref checks);
            var invalidIv = CreateOriginal();
            Field("InitialisationVector").SetValue(invalidIv, new byte[15]);
            RequireThrows<CryptographicException>(() => Invoke(invalidIv, "Decrypt", new byte[16]), "an invalid AES IV propagates outside the Decrypt catch", ref checks);
            return checks;
        }

        public static int RunDefaultOfflineRoute()
        {
            int checks = 0;
            FieldInfo singleton = typeof(HLPropertyStore).GetField("s_internalInstance", BindingFlags.Static | BindingFlags.NonPublic);
            if (singleton == null) throw new InvalidOperationException("The original property-store singleton field is absent.");
            object previous = singleton.GetValue(null);
            try
            {
                // The default offline provider accepts a null key, while the
                // preserved original constructor above rejects one. Construction
                // selects storage only; no Load, Save, enumeration or wipe runs.
                var store = new HLPropertyStore(null, 4, SyntheticFileName);
                FieldInfo storageField = typeof(HLPropertyStore).GetField("m_propertyFileStorage", PrivateInstance);
                if (storageField == null) throw new InvalidOperationException("The original property-store storage field is absent.");
                object storage = storageField.GetValue(store);
                Require(storage != null && storage.GetType() == typeof(LocalPropertySave), "default construction selects ProjectLucid.Offline.LocalPropertySave", ref checks);
                Require(ReferenceEquals(singleton.GetValue(null), store), "construction publishes the original property-store singleton", ref checks);
                Require(!store.IsLoaded, "construction leaves the property store unloaded", ref checks);
                Require(store.CanSave, "construction leaves the original property-store state idle", ref checks);
                Require(store.GetSaveIdentifier() == HLPropertyStore.DefaultSaveIdentifier, "construction retains the original default identifier", ref checks);
            }
            finally
            {
                singleton.SetValue(null, previous);
            }
            return checks;
        }

        private static HLSaveMethodEncrypted CreateOriginal()
        {
            return new HLSaveMethodEncrypted(SyntheticKey, SyntheticFileName);
        }

        private static FieldInfo Field(string name)
        {
            FieldInfo field = typeof(HLSaveMethodEncrypted).GetField(name, PrivateInstance);
            if (field == null) throw new InvalidOperationException("The original encrypted-save field is absent: " + name);
            return field;
        }

        private static object Invoke(HLSaveMethodEncrypted original, string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(HLSaveMethodEncrypted).GetMethod(methodName, PrivateInstance);
            if (method == null) throw new InvalidOperationException("The original encrypted-save method is absent: " + methodName);
            try
            {
                return method.Invoke(original, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static byte[] Hex(string value)
        {
            byte[] bytes = new byte[value.Length / 2];
            for (int index = 0; index < bytes.Length; ++index)
                bytes[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
            return bytes;
        }

        private static void Require(bool condition, string message, ref int checks)
        {
            if (!condition) throw new InvalidOperationException(message);
            ++checks;
        }

        private static void RequireThrows<T>(Action action, string message, ref int checks) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                ++checks;
                return;
            }
            throw new InvalidOperationException(message + ": expected " + typeof(T).Name);
        }
    }
}
