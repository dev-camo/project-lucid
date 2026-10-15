using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        // and FORMAT_X_OF_Y is "{0}/{1}" with 2 in every file.
        private static readonly Dictionary<string, Sample> RawSamples = new Dictionary<string, Sample>(StringComparer.Ordinal)
        {
            ["Arabic"] = new Sample("15003386dc3bbf90e3ad2cd121337ebbb2d28917a9bae7d75a777f72161edf9a", "إلغاء", "{0}ألف", ".", ","),
            ["ChineseSimplified"] = new Sample("d1ad379ac84e48bd30b1a29a9fdab38457d2cc487643092ece3149da80de2946", "取消", "{0}千", ".", ","),
            ["ChineseTraditional"] = new Sample("7a06a0efd0172adb5f2503c5003f8c22d27b97f59ab0de9df4d16fdebb5a4662", "取消", "{0}千", ".", ","),
            ["Dutch"] = new Sample("37377945354c95731afe16e5cc407fb5992f5eeb879969580ae31b30160c9140", "Annuleren", "{0}K", ",", "."),
            ["EnglishAus"] = new Sample("92578c7cd8f1a397e7ec1506d9340e097a62594fb23ba09cbc589d8dc45569f4", "Cancel", "{0}K", ".", ","),
            ["EnglishUK"] = new Sample("c1dbcd8a03c6c071ada67186f145ebc1ee97210e94f817f2a22877641e03b2db", "Cancel", "{0}K", ".", ","),
            ["EnglishUS"] = new Sample("4226c228b621abd03541d9dbc2ac6bc3648b2e96a80c5b68c18ff686f75f9bbb", "Cancel", "{0}K", ".", ","),
            ["French"] = new Sample("3c357fec617fb904f8b20da1c0ab8f8c12dd116d3e882fbae76738ee4f598163", "Annuler", "{0}K", ",", " "),
            ["FrenchCanadian"] = new Sample("f6e7bd7f0d9fb877cfef1c8be599dbaf23c5aff0890203b17e943016a89628a3", "Annuler", "{0}K", ",", " "),
            ["German"] = new Sample("2f7c1dcbe3e774ca68b31aa1e5f04fe447beadf57c77b1f32ccd9df7f99687c7", "Abbrechen", "{0}Tsd.", ",", "."),
            ["Italian"] = new Sample("14781d8e6abdc9c32984001dad9d6c483e3e4536cfac6b81fd1226fd53bfb51e", "Annulla", "{0}K", ",", "."),
            ["Japanese"] = new Sample("86024ae729b39415b251759a07f224011cb3b9e0258503121173effc83b48dc9", "キャンセル", "{0}千", ".", ","),
            ["Korean"] = new Sample("8d5535dcd176452eb676066b8cf6d8714c2901df7de7d4852aa5d2d252910fba", "취소", "{0}천", ".", ","),
            ["PortugueseBrazil"] = new Sample("8026ac655e2440e22db3b76f1914522a216603ca4312b78c5d4dce0b980782b7", "Cancelar", "{0}Mil", ",", "."),
            ["Russian"] = new Sample("f14552c760b8f89c5471a0c6eb89fbac3296e40af72f2e737243cd23e64ad733", "Отмена", "{0} тыс.", ",", " "),
            ["Spanish"] = new Sample("03f20390f53c374c7b590d2fa451493bed137df3cf19b762d62da7c3bab5c858", "Cancelar", "{0}000", ",", "."),
            ["Turkish"] = new Sample("03d2c741f9bcd1b7644e756519c49dc4d598c9437183fa8249519329252f5d41", "İptal", "{0} Bin", ",", "."),
        };

        private static void RequireRawSample(string language, string id, string content, int numArgs)
        {
            Require(Table.GetStringAndNumArgs(id, out string loaded, out int args) && loaded == content && args == numArgs,
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
                        RequireRawSample(name, "FORMAT_X_OF_Y", "{0}/{1}", 2);
                        Require(table.NumberFormat.NumberGroupSeparator == sample.Group && table.NumberFormat.NumberDecimalSeparator == sample.Decimal,
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
