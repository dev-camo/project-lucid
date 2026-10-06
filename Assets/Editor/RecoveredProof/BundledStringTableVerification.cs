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

        public static int Run()
        {
            Require(!Application.isPlaying, "Bundled synchronous loader verification requires EditMode.");
            Require(ProcessManager.IsSystemNull<Table>() && ReferenceEquals(MonoSingleton<Table>.Instance, null) &&
                ProcessManager.IsSystemNull<Language>() && ReferenceEquals(MonoSingleton<Language>.Instance, null),
                "An original language/table host is already registered.");
            string directory = Path.Combine(Application.streamingAssetsPath, "LanguageStrings");
            string[] paths = Directory.GetFiles(directory, "*.bytes").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Require(paths.Length == 17, "Expected all seventeen extracted language files.");
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
                    typeof(MonoSingleton<Language>).GetMethod("Awake", Own).Invoke(language, null);
                    languageRegistered = true;
                    tableOwner = new GameObject("Lucid bundled string table verification");
                    tableOwner.SetActive(false);
                    table = tableOwner.AddComponent<Table>();
                    typeof(MonoSingleton<Table>).GetMethod("Awake", Own).Invoke(table, null);
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
                                finally { if (languageOwner != null) UnityEngine.Object.DestroyImmediate(languageOwner); }
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
