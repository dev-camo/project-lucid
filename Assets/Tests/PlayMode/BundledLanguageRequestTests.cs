using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClientDataAPI;
using Hardlight;
using Hardlight.Enums;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Table = Hardlight.Localisation.StringTable;

namespace ProjectLucid.Tests
{
    public sealed class BundledLanguageRequestTests
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [UnityTest]
        public IEnumerator PortableAsyncLoaderReadsEverySuppliedLanguageBeforeCallback()
        {
            string[] paths = Directory.GetFiles(Path.Combine(Application.streamingAssetsPath,
                "LanguageStrings"), "*.bytes").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.That(paths.Length, Is.EqualTo(17));
            Type[] models = { typeof(LocalisedString), typeof(ClientDataAPI.StringTable),
                typeof(SupportedLanguage), typeof(LocalisationDefinitions) };
            var saved = new List<KeyValuePair<FieldInfo, object>>();
            MethodInfo loader = typeof(Table).GetMethod("LoadLocalisationDefinitions", Own);
            Assert.That(loader, Is.Not.Null);
            IEnumerator active = null;
            try
            {
                // Fresh decode pools protect entries owned by other systems.
                // The async path never uses the cached encoding singleton.
                foreach (Type model in models)
                {
                    foreach (FieldInfo field in model.GetFields(Own).Where(f => f.IsStatic && !f.IsLiteral && !f.IsInitOnly))
                        saved.Add(new KeyValuePair<FieldInfo, object>(field, field.GetValue(null)));
                    model.GetField("s_poolSize", Own).SetValue(null, 20);
                    model.GetField("s_poolNumAcquired", Own).SetValue(null, 0);
                    model.GetField("s_poolInstances", Own).SetValue(null, Array.CreateInstance(model, 20));
                }
                int loaded = 0, entries = 0;
                foreach (string path in paths)
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Languages language = EnumUtilities.SafeParse<Languages>(Path.GetFileNameWithoutExtension(path),
                        (Languages)int.MinValue);
                    Assert.That(Enum.IsDefined(typeof(Languages), language), Is.True);
                    int callbacks = 0;
                    Action<LocalisationDefinitions> callback = definition =>
                    {
                        ++callbacks;
                        Assert.That(definition, Is.Not.Null);
                        Assert.That(definition.StringTable.Language, Is.EqualTo((int)language));
                        // The actual original iterator owns a real live request
                        // throughout decoding and delivery to its caller.
                        var request = (UnityWebRequest)active.GetType().GetFields(Own)
                            .Single(f => f.FieldType == typeof(UnityWebRequest)).GetValue(active);
                        Assert.That(request, Is.Not.Null);
                        Assert.That(request.uri.IsFile, Is.True, "Desktop assets must use the local file protocol.");
                        Assert.That(Path.GetFullPath(request.uri.LocalPath), Is.EqualTo(Path.GetFullPath(path)),
                            "The portable request must retain the supplied asset path.");
                        Assert.That(request.isDone, Is.True);
                        Assert.That(request.error, Is.Null.Or.Empty);
                        CollectionAssert.AreEqual(bytes, request.downloadHandler.data);
                        CollectionAssert.AreEqual(bytes, definition.encode());
                        entries += definition.StringTable.Strings.Length;
                    };
                    active = (IEnumerator)loader.Invoke(null, new object[] { language, "LanguageStrings", callback });
                    yield return active;
                    Assert.That(callbacks, Is.EqualTo(1), "The original async loader must complete its supplied-file callback once.");
                    (active as IDisposable)?.Dispose();
                    active = null;
                    CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
                    ++loaded;
                }
                Assert.That(loaded, Is.EqualTo(17));
                Assert.That(entries, Is.EqualTo(31363));
            }
            finally
            {
                try { (active as IDisposable)?.Dispose(); }
                finally
                {
                    foreach (var pair in saved) pair.Key.SetValue(null, pair.Value);
                }
            }
        }
    }
}
