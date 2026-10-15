using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using UnityEngine;
using Table = Hardlight.Localisation.StringTable;

namespace ProjectLucid
{
    // Exercise extracted language data through the original loader, table and
    // callbacks. The fixture changes no streaming files or persistent saves.
    public static class BundledStringTableVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Sample
        {
            // Content values are SHA-256 digests of the UTF-8 entry text, so no supplied text is copied here.
            public readonly string Sha256, Cancel, Thousands, Decimal, Group;
            public Sample(string sha256, string cancel, string thousands, string decimalSeparator, string groupSeparator)
            {
                Sha256 = sha256; Cancel = cancel; Thousands = thousands; Decimal = decimalSeparator; Group = groupSeparator;
            }
        }

        // Independent oracle, read from the raw supplied bytes rather than the
        // original loader: each file's SHA-256 equals its input-manifest row
        // (Contents/Resources/Data/StreamingAssets/LanguageStrings/*.bytes of
        // input 1.10.1), and each entry is length-prefixed with tag 1 Id,
        // tag 2 Content and tag 3 NumArgs (absent means 0). NumArgs are
        // COMMON_MENU_CANCEL 0, CULTURE_NUMBER_THOUSANDS 1, both separators 0,
        // and FORMAT_X_OF_Y is the same two-argument entry in every file.
        private static readonly Dictionary<string, Sample> RawSamples = new Dictionary<string, Sample>(StringComparer.Ordinal)
        {
            ["Arabic"] = new Sample("15003386dc3bbf90e3ad2cd121337ebbb2d28917a9bae7d75a777f72161edf9a", "9a30dc2a96b897abdf7412f2bc0e09c6979d7a932c0bfc7567dcff37b73e50bb", "3f181e450db25fe4d7108c13b7a2c624ff1e44e369ad7ad4093cbfe326ff81df", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7"),
            ["ChineseSimplified"] = new Sample("d1ad379ac84e48bd30b1a29a9fdab38457d2cc487643092ece3149da80de2946", "2cd0f3be8738a86cede1a1452f874f31d569864bbb00cacd4130990f4610cb5c", "181e2f62cc60291011b0ed17ab63c9ea27ae43e2d1681046846ca73c6f754888", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7"),
            ["ChineseTraditional"] = new Sample("7a06a0efd0172adb5f2503c5003f8c22d27b97f59ab0de9df4d16fdebb5a4662", "2cd0f3be8738a86cede1a1452f874f31d569864bbb00cacd4130990f4610cb5c", "181e2f62cc60291011b0ed17ab63c9ea27ae43e2d1681046846ca73c6f754888", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7"),
            ["Dutch"] = new Sample("37377945354c95731afe16e5cc407fb5992f5eeb879969580ae31b30160c9140", "c6a8d25fe37a7cc5cedaf962804161565fed233819bf4a646b1e02d3685f3aad", "dc8d441f18686ac0763756394de4c0ee209c0b38d27a5d63ef2d3a9a44a59034", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8"),
            ["EnglishAus"] = new Sample("92578c7cd8f1a397e7ec1506d9340e097a62594fb23ba09cbc589d8dc45569f4", "19766ed6ccb2f4a32778eed80d1928d2c87a18d7c275ccb163ec6709d3eb2e27", "dc8d441f18686ac0763756394de4c0ee209c0b38d27a5d63ef2d3a9a44a59034", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7"),
            ["EnglishUK"] = new Sample("c1dbcd8a03c6c071ada67186f145ebc1ee97210e94f817f2a22877641e03b2db", "19766ed6ccb2f4a32778eed80d1928d2c87a18d7c275ccb163ec6709d3eb2e27", "dc8d441f18686ac0763756394de4c0ee209c0b38d27a5d63ef2d3a9a44a59034", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7"),
            ["EnglishUS"] = new Sample("4226c228b621abd03541d9dbc2ac6bc3648b2e96a80c5b68c18ff686f75f9bbb", "19766ed6ccb2f4a32778eed80d1928d2c87a18d7c275ccb163ec6709d3eb2e27", "dc8d441f18686ac0763756394de4c0ee209c0b38d27a5d63ef2d3a9a44a59034", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7"),
            ["French"] = new Sample("3c357fec617fb904f8b20da1c0ab8f8c12dd116d3e882fbae76738ee4f598163", "46ad3916f6a0979841df9c4ba6f5310d34434d95024912df97b2eba644d0a60d", "dc8d441f18686ac0763756394de4c0ee209c0b38d27a5d63ef2d3a9a44a59034", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "abfbd10daf8965c8860b3582af942d7a7cac972b31d1c50f382b67d9b6c07365"),
            ["FrenchCanadian"] = new Sample("f6e7bd7f0d9fb877cfef1c8be599dbaf23c5aff0890203b17e943016a89628a3", "46ad3916f6a0979841df9c4ba6f5310d34434d95024912df97b2eba644d0a60d", "dc8d441f18686ac0763756394de4c0ee209c0b38d27a5d63ef2d3a9a44a59034", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "abfbd10daf8965c8860b3582af942d7a7cac972b31d1c50f382b67d9b6c07365"),
            ["German"] = new Sample("2f7c1dcbe3e774ca68b31aa1e5f04fe447beadf57c77b1f32ccd9df7f99687c7", "f7ff1178af202f6ffb67b9572c3fcf88059d030657e6e07447123f4e48c78e50", "eda292df6ce236d674cf018448e895d828abac355a496cf3cb50cfa141a6b859", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8"),
            ["Italian"] = new Sample("14781d8e6abdc9c32984001dad9d6c483e3e4536cfac6b81fd1226fd53bfb51e", "18c9d912a21072ddff188e816624629685101130d7b2abd498114a8a45236727", "dc8d441f18686ac0763756394de4c0ee209c0b38d27a5d63ef2d3a9a44a59034", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8"),
            ["Japanese"] = new Sample("86024ae729b39415b251759a07f224011cb3b9e0258503121173effc83b48dc9", "bca84ea5c65fee0eccd1f23a972b95cce908f83977c65b4e8b32ca72a772a9a5", "181e2f62cc60291011b0ed17ab63c9ea27ae43e2d1681046846ca73c6f754888", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7"),
            ["Korean"] = new Sample("8d5535dcd176452eb676066b8cf6d8714c2901df7de7d4852aa5d2d252910fba", "be876433993ab7ba221512b5703b32060626f390aa50d7ed464a847a34539a6c", "c17c8625a289552f9f75277a6e42a0acfe5f1e25e78dfd6fa5609c106260812b", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7"),
            ["PortugueseBrazil"] = new Sample("8026ac655e2440e22db3b76f1914522a216603ca4312b78c5d4dce0b980782b7", "bb9dbb406dcbd5248babc609bb3d021d384398f83dbdb8d53c9b0e32c2aad915", "a4789d48c391a089aaa600683059e8e9c814af7089caeb6e23fdbb7aaf485e78", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8"),
            ["Russian"] = new Sample("f14552c760b8f89c5471a0c6eb89fbac3296e40af72f2e737243cd23e64ad733", "8fbe9b75cbdfbfa6f57f060f286e00dee7e021068ebe378cfb495f3380d1e96b", "9b84dd834cac35011ddb2b5c93ccad7e985485bc1b66e5d2aa6344bac629f1d5", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "abfbd10daf8965c8860b3582af942d7a7cac972b31d1c50f382b67d9b6c07365"),
            ["Spanish"] = new Sample("03f20390f53c374c7b590d2fa451493bed137df3cf19b762d62da7c3bab5c858", "bb9dbb406dcbd5248babc609bb3d021d384398f83dbdb8d53c9b0e32c2aad915", "f08cf833e8cac8b9f5b2d20be7cc3d56ebcfb78ae6e08569ff5f91bf650cb31e", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8"),
            ["Turkish"] = new Sample("03d2c741f9bcd1b7644e756519c49dc4d598c9437183fa8249519329252f5d41", "72278748133ac996ce08434c37b3e51221d7cc6b1417944494484be57b9eb6f7", "d7beb3ae6e8143ba9a77f5b16afddce3deb1533c9ee7e9732128679c2b9eac1b", "d03502c43d74a30b936740a9517dc4ea2b2ad7168caa0a774cefe793ce0b33e7", "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8"),
        };

        private static void RequireRawSample(string language, string id, string content, int numArgs)
        {
            Require(Table.GetStringAndNumArgs(id, out string loaded, out int args) && Sha256(Encoding.UTF8.GetBytes(loaded)) == content && args == numArgs,
                "Original lookup differs from the raw supplied entry: " + language + "/" + id);
        }

        private static string Sha256(byte[] bytes)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        public static int Run()
        {
            Require(!Application.isPlaying, "Bundled synchronous loader verification requires EditMode.");
            Require(ProcessManager.IsSystemNull<Table>() && ReferenceEquals(MonoSingleton<Table>.Instance, null) &&
                ProcessManager.IsSystemNull<Language>() && ReferenceEquals(MonoSingleton<Language>.Instance, null),
                "An original language/table host is already registered.");
            string directory = Path.Combine(Application.streamingAssetsPath, "LanguageStrings");
            // Supplied inputs are ignored extracted files; their absence is a
            // precondition failure, not a pass.
            Require(Directory.Exists(directory), "Bundled language verification requires the extracted supplied LanguageStrings inputs.");
            string[] paths = Directory.GetFiles(directory, "*.bytes").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Require(paths.Length == 17, "Expected all seventeen extracted language files.");
            Require(paths.Select(Path.GetFileNameWithoutExtension).SequenceEqual(RawSamples.Keys.OrderBy(k => k, StringComparer.Ordinal)),
                "Extracted language files differ from the pinned supplied inventory.");
            // Own both default-name rows: absent before, removed after. The
            // original OnDestroy only revokes them (rows survive unregistration).
            var languageRegistration = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<Language>());
            var tableRegistration = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<Table>());
            FieldInfo[] statics = typeof(Table).GetFields(Own).Where(f => f.IsStatic && !f.IsLiteral).ToArray();
            object[] prior = statics.Select(f => f.GetValue(null)).ToArray();
            FieldInfo languageEvent = typeof(Language).GetField("OnLanguageChanged", Own);
            object priorEvent = languageEvent.GetValue(null);
            GameObject languageOwner = null, tableOwner = null;
            bool languageRegistered = false, tableRegistered = false;
            Language language = null;
            Table table = null;
            using (ClientDataAPIVerification.IsolatePools())
            {
                try
                {
                    languageEvent.SetValue(null, null);
                    languageOwner = new GameObject("Lucid bundled language loader verification");
                    languageOwner.SetActive(false);
                    language = languageOwner.AddComponent<Language>();
                    languageRegistration.RegisterUsing(language, () => typeof(MonoSingleton<Language>).GetMethod("Awake", Own).Invoke(language, null));
                    languageRegistered = true;
                    tableOwner = new GameObject("Lucid bundled string table verification");
                    tableOwner.SetActive(false);
                    table = tableOwner.AddComponent<Table>();
                    tableRegistration.RegisterUsing(table, () => typeof(MonoSingleton<Table>).GetMethod("Awake", Own).Invoke(table, null));
                    tableRegistered = true;
                    Table.StringsTableHandlers = new FastAction();
                    var trace = new List<string>();
                    Table.StringsTableHandlers += () => trace.Add("strings");
                    Table.OnRequestStringTable += () => trace.Add("request");
                    typeof(Table).GetField("ShouldInvokeStringHandlers", Own).SetValue(null, (Func<bool>)(() => true));
                    MethodInfo apply = typeof(Table).GetMethod("OnLanguageDefinitionLoaded", Own);
                    int totalEntries = 0;
                    foreach (string path in paths)
                    {
                        byte[] before = File.ReadAllBytes(path);
                        string name = Path.GetFileNameWithoutExtension(path);
                        Sample sample = RawSamples[name];
                        Require(Sha256(before) == sample.Sha256, "Supplied language bytes differ from the input manifest: " + name);
                        Languages selected = EnumUtilities.SafeParse<Languages>(name, (Languages)int.MinValue);
                        Require(Enum.IsDefined(typeof(Languages), selected), "Unrecognized language file: " + name);
                        Language.OverrideLanguage(selected);
                        int callbackCount = 0;
                        var definition = Table.LoadLanguageDefinition(selected, "LanguageStrings", _ => callbackCount++);
                        Require(definition != null && definition.StringTable != null && definition.StringTable.Strings != null,
                            "Original loader returned an incomplete supplied language: " + name);
                        Require(callbackCount == 0, "Synchronous loader unexpectedly invoked its async callback.");
                        Require(definition.StringTable.Language == (int)selected,
                            "Supplied table language differs from its original loading key: " + name);
                        trace.Clear();
                        apply.Invoke(table, new object[] { definition });
                        Require(trace.SequenceEqual(new[] { "strings", "request" }),
                            "String handler/request callback order changed for " + name);
                        Require(Table.AreStringsLoaded() && Table.StringsLanguage == selected && Table.Hash == definition.StringTable.Hash,
                            "Original readiness, language and hash did not update for " + name);
                        Require(ReferenceEquals(Table.GetSupportedLanguages(), definition.SupportedLanguages),
                            "Supported language data no longer retains original ownership.");
                        var expected = new Dictionary<int, ClientDataAPI.LocalisedString>();
                        foreach (var item in definition.StringTable.Strings)
                            expected[HLCRC32.GenerateInt(item.Id)] = item;
                        foreach (var pair in expected)
                        {
                            var id = (Strings)pair.Key;
                            var item = pair.Value;
                            Require(Table.StringExists(id), "Original named string is absent: " + item.Id);
                            Require(Table.GetString(id) == (id == Strings.NONE ? string.Empty : item.Content),
                                "Original string content differs: " + name + "/" + item.Id);
                            Require(Table.GetStringAndNumArgs(item.Id, out string content, out int args) &&
                                content == item.Content && args == item.NumArgs,
                                "Original content/argument lookup differs: " + name + "/" + item.Id);
                        }
                        RequireRawSample(name, "COMMON_MENU_CANCEL", sample.Cancel, 0);
                        RequireRawSample(name, "CULTURE_NUMBER_THOUSANDS", sample.Thousands, 1);
                        RequireRawSample(name, "DECIMAL_SEPARATOR", sample.Decimal, 0);
                        RequireRawSample(name, "NUMBER_SEPARATOR", sample.Group, 0);
                        RequireRawSample(name, "FORMAT_X_OF_Y", "cca770ce6397400c6aafac0afd746ccc490210390d1c998a48d4d1148978eb6d", 2);
                        Require(Sha256(Encoding.UTF8.GetBytes(table.NumberFormat.NumberGroupSeparator)) == sample.Group &&
                            Sha256(Encoding.UTF8.GetBytes(table.NumberFormat.NumberDecimalSeparator)) == sample.Decimal,
                            "Original number formatting differs from the raw supplied separators: " + name);
                        Require(table.NumberFormat.NumberGroupSeparator == Table.GetString(Strings.NUMBER_SEPARATOR) &&
                            table.NumberFormat.NumberDecimalSeparator == Table.GetString(Strings.DECIMAL_SEPARATOR),
                            "Original number formatting differs from supplied locale strings.");
                        Require(before.SequenceEqual(File.ReadAllBytes(path)), "Supplied language bytes changed during loading.");
                        totalEntries += definition.StringTable.Strings.Length;
                    }
                    Require(totalEntries == 31363, "Supplied language content inventory changed.");
                    Debug.Log("Project Lucid loaded " + paths.Length + " bundled languages / " + totalEntries +
                        " localized entries through the original loader, string lookup and callback order.");
                    return paths.Length;
                }
                finally
                {
                    try
                    {
                        try { if (tableRegistered) typeof(Table).GetMethod("OnDestroy", Own).Invoke(table, null); }
                        finally
                        {
                            try { if (languageRegistered) typeof(Language).GetMethod("OnDestroy", Own).Invoke(language, null); }
                            finally
                            {
                                try { if (tableOwner != null) UnityEngine.Object.DestroyImmediate(tableOwner); }
                                finally
                                {
                                    try { if (languageOwner != null) UnityEngine.Object.DestroyImmediate(languageOwner); }
                                    finally
                                    {
                                        try { tableRegistration.Dispose(); }
                                        finally { languageRegistration.Dispose(); }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        for (int i = 0; i < statics.Length; ++i) statics[i].SetValue(null, prior[i]);
                        languageEvent.SetValue(null, priorEvent);
                    }
                }
            }
        }
    }
}
