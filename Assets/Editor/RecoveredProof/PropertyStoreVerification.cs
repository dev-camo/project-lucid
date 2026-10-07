using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded store lifecycle proof against HLUnityCore.Runtime method group
    // 0x06000cee..0x06000d2d, with native file-adapter behavior deliberately
    // replaced. These checks do not establish SaveManager progression parity.
    public static class PropertyStoreVerification
    {
        private static int checks;
        private static readonly string[] StaticFields = { "s_internalInstance", "SaveHandlers", "ModifySaveHandlers",
            "ResolveConflictHandlers", "SaveSuccessHandlers", "AfterConflictsResolvedHandlers", "LoadHandlers" };

        public static void Run()
        {
            checks = 0;
            object[] previous = StaticFields.Select(name => StaticField(name).GetValue(null)).ToArray();
            try
            {
                VerifyLifecycle();
                VerifyFraming();
                VerifyIdentifiers();
                VerifyDeferredWrite();
                VerifyLocalDisk();
                Debug.Log("[Project Lucid] Original property-store/local-adapter checks passed: " + checks);
            }
            finally
            {
                for (int i = 0; i < StaticFields.Length; ++i) StaticField(StaticFields[i]).SetValue(null, previous[i]);
            }
        }

        private static HLPropertyStore CreateStore(MemoryStorage storage)
        {
            foreach (string name in StaticFields) StaticField(name).SetValue(null, null);
            var store = new HLPropertyStore("fixture-only", 4, "fixture-save");
            typeof(HLPropertyStore).GetField("m_propertyFileStorage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(store, storage);
            return store;
        }

        private static void VerifyLifecycle()
        {
            var memory = new MemoryStorage();
            HLPropertyStore store = CreateStore(memory);
            Require(!store.IsLoaded && store.CanSave && !HLPropertyStore.Save() && !HLPropertyStore.SaveDelayed(), "save requires first load");
            var order = new List<string>();
            bool loadedInside = true, canSaveInside = true, newFile = false;
            HLPropertyStore.LoadHandler load = (properties, isNew) =>
            {
                order.Add("load"); loadedInside = store.IsLoaded; canSaveInside = store.CanSave; newFile = isNew;
                properties.AddProperty("rings", 9);
            };
            HLPropertyStore.AddLoadHandler(load);
            Require(order.Count == 0 && HLPropertyStore.IsThereAnyLoadHandler, "subscription before load defers callback");
            HLPropertyStore.Load();
            Require(order.SequenceEqual(new[] { "load" }) && !loadedInside && !canSaveInside && newFile && store.IsLoaded && store.CanSave,
                "new load callback runs before state and loaded completion");
            Require(memory.Calls.SequenceEqual(new[] { "load:Primary:", "load:Backup:" }), "primary then backup loading");
            bool immediateNew = true;
            HLPropertyStore.LoadHandler late = (properties, isNew) => immediateNew = isNew;
            HLPropertyStore.AddLoadHandler(late);
            Require(!immediateNew, "late subscriber gets existing data immediately");
            HLPropertyStore.RemoveLoadHandler(late);
            bool reentrantSave = true;
            HLPropertyStore.SaveHandler save = properties => { order.Add("save"); reentrantSave = HLPropertyStore.Save(); };
            HLPropertyStore.ModifySaveHandler modify = (HLPropertyList properties, ref bool resolved) => { order.Add("modify"); return true; };
            HLPropertyStore.ResolveConflictHandler resolve = (HLPropertyList properties, bool overrideData, ref bool applied) =>
                { order.Add("resolve:" + overrideData); applied = false; };
            HLPropertyStore.AfterConflictsResolvedHandler after = properties => { order.Add("after"); Require(!store.CanSave, "after callback remains Saving"); };
            HLPropertyStore.SaveSuccessHandler success = properties => { order.Add("success"); Require(store.CanSave, "success callback runs after state reset"); };
            HLPropertyStore.AddSaveHandler(save); HLPropertyStore.AddModifySaveHandler(modify);
            HLPropertyStore.AddResolveConflictHandler(resolve); HLPropertyStore.AddAfterConflictsResolvedHandler(after);
            HLPropertyStore.AddSaveSuccessHandler(success);
            order.Clear(); memory.Calls.Clear();
            Require(!HLPropertyStore.Save(true) && !reentrantSave && store.CanSave, "resolved-without-changes suppresses success after writing");
            Require(order.SequenceEqual(new[] { "save", "modify", "resolve:False", "after" }) && memory.Calls.SequenceEqual(new[] { "save:", "backup:" }),
                "exact conflict callback and write order");
            order.Clear();
            Require(HLPropertyStore.Save(false) && order.SequenceEqual(new[] { "save", "after", "success" }), "offline save skips conflict callbacks, keeps after stage");
            HLPropertyStore.RemoveModifySaveHandler(modify);
            HLPropertyStore.ModifySaveHandler resolvedModify = (HLPropertyList properties, ref bool resolved) => { resolved = true; return true; };
            HLPropertyStore.AddModifySaveHandler(resolvedModify);
            order.Clear();
            Require(HLPropertyStore.Save(true) && order.SequenceEqual(new[] { "save", "resolve:True", "after", "success" }), "conflicts-resolved flag permits success");
            memory.SaveResult = false; memory.Calls.Clear(); order.Clear();
            Require(!HLPropertyStore.Save() && store.CanSave && memory.Calls.SequenceEqual(new[] { "save:" }) && !order.Contains("success"), "primary failure skips backup and success");
            memory.SaveResult = true; memory.BackupResult = false; memory.Calls.Clear(); order.Clear();
            Require(!HLPropertyStore.Save() && store.CanSave && memory.Calls.SequenceEqual(new[] { "save:", "backup:" }) && !order.Contains("success"), "backup failure follows primary and suppresses success");
            memory.BackupResult = true;
            memory.Calls.Clear();
            store.Reset();
            Require(!store.IsLoaded && store.CanSave && memory.Calls.Count == 0 && newFile && !loadedInside && !canSaveInside,
                "reset only clears memory and sends new-file callback while Reset");
            HLPropertyStore.Load();
            memory.Calls.Clear(); order.Clear();
            store.WipeSaveFile("other");
            Require(memory.Calls.SequenceEqual(new[] { "wipe:other" }) && store.IsLoaded && order.Count == 0, "wipe is a storage-only operation");
            store.Shutdown();
            Require(!HLPropertyStore.IsThereAnyLoadHandler && !HLPropertyStore.IsThereAnySaveHandler &&
                !HLPropertyStore.IsThereAnyModifySaveHandler && !HLPropertyStore.IsThereAnyResolveConflictHandler &&
                !HLPropertyStore.IsThereAnySaveSuccessHandler && HLPropertyStore.IsThereAnyAfterConflictsResolvedHandler && !HLPropertyStore.Save(),
                "shutdown retains original after-conflicts handler only");

            store = CreateStore(new MemoryStorage());
            HLPropertyStore.AddLoadHandler((properties, isNew) => { throw new IOException("fixture load"); });
            RequireThrows<IOException>(() => HLPropertyStore.Load(), "load callback exception propagates");
            Require(!store.IsLoaded && !store.CanSave, "failed load remains Loading");
            store = CreateStore(new MemoryStorage()); HLPropertyStore.Load();
            HLPropertyStore.AddSaveHandler(properties => { throw new IOException("fixture save"); });
            RequireThrows<IOException>(() => HLPropertyStore.Save(), "save callback exception propagates");
            Require(!store.CanSave && !HLPropertyStore.Save(), "failed callback remains Saving and prevents recursion");
        }

        private static void VerifyFraming()
        {
            var memory = new MemoryStorage(); HLPropertyStore store = CreateStore(memory); HLPropertyStore.Load();
            store.AddProperty("number", 12.25); store.AddProperty("empty", "");
            Require(HLPropertyStore.Save(), "native property text saves");
            string expected = "4\n2\n" + HLPropertyStore.GetCRC("number") + Environment.NewLine + "12.25" + Environment.NewLine +
                HLPropertyStore.GetCRC("empty") + Environment.NewLine + Environment.NewLine;
            Require(memory.Content[(HLPropertyStore.FileType.Primary, "")] == expected, "client-version header/list order/CRC values without names");
            var list = new HLPropertyList(); list.AddProperty("old", "retained");
            memory.Content[(HLPropertyStore.FileType.Primary, "fixture")] = "3\n0\n";
            Require(!Parse(store, list, memory, "fixture") && list.AsString("old") == "retained", "wrong client version leaves list intact");
            memory.Content[(HLPropertyStore.FileType.Primary, "fixture")] = "4\ninvalid\n";
            Require(!Parse(store, list, memory, "fixture") && list.AsString("old") == "retained", "invalid count leaves list intact");
            uint crc = HLPropertyStore.GetCRC("value");
            memory.Content[(HLPropertyStore.FileType.Primary, "fixture")] = "4\r\n3\r\n0\r\nignored\r\n" + crc + "\r\nfirst\r\n" + crc + "\r\nlast\r\ntrailing";
            Require(Parse(store, list, memory, "fixture") && list.Properties.Count == 1 && list.Properties[0].m_name == null && list.AsString("value") == "last",
                "CRLF/CRC-zero/duplicate CRC and ignored trailing content");
            memory.Content[(HLPropertyStore.FileType.Primary, "fixture")] = "4\n2\n" + crc + "\npartial\ninvalid\nvalue\n";
            Require(!Parse(store, list, memory, "fixture") && list.AsString("value") == "partial", "body parse failure retains earlier parsed entry");
            memory.Content[(HLPropertyStore.FileType.Primary, "fixture")] = "4\n-1\n";
            RequireThrows<ArgumentOutOfRangeException>(() => Parse(store, list, memory, "fixture"), "negative capacity propagates");
            Require(list.Properties.Count == 0, "negative count clears before capacity error");
            memory.Content[(HLPropertyStore.FileType.Primary, "fixture")] = "4\n2\n" + crc + "\nshort";
            RequireThrows<IndexOutOfRangeException>(() => Parse(store, list, memory, "fixture"), "truncated pairs preserve managed indexing error");

            memory = new MemoryStorage(); store = CreateStore(memory);
            memory.Content[(HLPropertyStore.FileType.Primary, "slot")] = "bad";
            memory.Content[(HLPropertyStore.FileType.Backup, "slot")] = Text("restored", "backup");
            bool isNew = true; HLPropertyStore.AddLoadHandler((properties, newFile) => isNew = newFile);
            store.LoadFromIdentifier("slot");
            Require(store.GetPropertyValue("restored") == "backup" && !isNew && memory.Calls.All(call => !call.StartsWith("save:")), "backup load does not repair primary or report new file");
        }

        private static void VerifyIdentifiers()
        {
            var memory = new MemoryStorage(); HLPropertyStore store = CreateStore(memory);
            memory.Content[(HLPropertyStore.FileType.Primary, "other")] = Text("name", "other data");
            Require(store.GetPropertyValueFromSave("other", "name") == "other data" && store.IsLoaded && store.GetSaveIdentifier() == "", "off-slot read sets loaded without changing active ID");
            Require(ReferenceEquals(store.GetPropertyListFromSave(""), store.GetPropertyListFromSave("")), "active slot returns list identity");
            // InvariantCulture considers embedded NUL equivalent; Ordinal does not.
            Require(ReferenceEquals(store.GetPropertyListFromSave(""), store.GetPropertyListFromSave("\0")), "original invariant-culture identifier comparison");
            memory.Calls.Clear();
            Require(store.TryGetSaveVersion("other", out long version) && version == 77 && memory.Calls.Last() == "version:", "missing version property queries active identifier timestamp");
            memory.Content[(HLPropertyStore.FileType.Primary, "other")] = Text("other", "not numeric"); memory.Calls.Clear();
            Require(!store.TryGetSaveVersion("other", out version) && version == 0 && !memory.Calls.Any(call => call.StartsWith("version:")), "present invalid version blocks timestamp fallback");
            memory.Content[(HLPropertyStore.FileType.Primary, "a")] = Text("a", "8");
            memory.Content[(HLPropertyStore.FileType.Primary, "b")] = Text("b", "9");
            memory.Content[(HLPropertyStore.FileType.Primary, "c")] = Text("c", "9");
            Require(store.GetLastSaveIdentifier(new[] { "a", "b", "c" }) == "b", "greatest explicit version, first on ties");
            Require(store.GetLastSaveIdentifier(null) == "", "null collection default identifier");
            RequireThrows<ArgumentOutOfRangeException>(() => store.GetLastSaveIdentifier(new List<string>()), "empty collection preserves original indexing error");
            int callbacks = 0; HLPropertyStore.AddSaveHandler(properties => ++callbacks);
            HLPropertyStore.AddSaveSuccessHandler(properties => ++callbacks);
            Require(store.TrySetPropertyValueOnSave("b", "changed", 22) && callbacks == 0 && store.GetPropertyValue("changed") == "", "direct cross-slot write bypasses save callbacks and active list");
            Require(store.GetPropertyValueFromSave("b", "changed") == "22", "cross-slot write retains target properties");
            Require(store.TrySetPropertyValueOnSave("", "changed", 33) && callbacks == 0 && store.GetPropertyValue("changed") == "33", "direct active write also bypasses callbacks");
        }

        private static void VerifyLocalDisk()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ProjectLucidPropertyProof-" + Guid.NewGuid().ToString("N"));
            try
            {
                var adapter = new ProjectLucid.Offline.LocalPropertySave("unused", "save-", directory);
                Require(adapter.LoadData(HLPropertyStore.FileType.Primary, "slot") == null && !Directory.Exists(directory), "loading absent data creates no directory");
                var builder = new StringBuilder(Text("value", "first"));
                Require(adapter.SaveData(HLPropertyStore.FileType.Primary, builder, "slot") && adapter.BackupData("slot"), "atomic local primary and backup writes");
                var restarted = new ProjectLucid.Offline.LocalPropertySave("unused", "save-", directory);
                Require(restarted.LoadData(HLPropertyStore.FileType.Primary, "slot") == builder.ToString() && restarted.TryGetSaveVersion("slot", out long version) && version > 0,
                    "fresh adapter reads disk payload/version");
                string primary = Path.Combine(directory, "save-slot"), backup = primary + "-backup";
                File.WriteAllText(primary, "damaged");
                Require(restarted.LoadData(HLPropertyStore.FileType.Primary, "slot") == null && restarted.LoadData(HLPropertyStore.FileType.Backup, "slot") == builder.ToString(), "corrupt primary falls back to valid backup");
                Require(File.ReadAllText(primary) == "damaged", "reads preserve corrupt primary");
                File.WriteAllText(Path.Combine(directory, ".save-slot.interrupted.tmp"), "partial");
                Require(restarted.GetAllSaveIdentifiers().SequenceEqual(new[] { "slot" }), "interrupted temp excluded from slot inventory");
                File.Delete(primary);
                Require(restarted.GetAllSaveIdentifiers().SequenceEqual(new[] { "slot" }) && restarted.LoadData(HLPropertyStore.FileType.Backup, "slot") == builder.ToString(), "backup-only slot remains discoverable");
                File.WriteAllText(primary, "bad primary"); File.WriteAllText(backup, "bad backup");
                RequireThrows<InvalidDataException>(() => restarted.LoadData(HLPropertyStore.FileType.Backup, "slot"), "both corrupt copies produce usable error");
                Require(File.ReadAllText(primary) == "bad primary" && File.ReadAllText(backup) == "bad backup", "corrupt copies retained after error");
                Require(restarted.SaveData(HLPropertyStore.FileType.Primary, builder, "slot") && restarted.BackupData("slot"), "explicit later save can commit valid data");
                File.Delete(backup); Directory.CreateDirectory(backup);
                Require(!restarted.SaveData(HLPropertyStore.FileType.Primary, new StringBuilder(Text("value", "second")), "slot"), "blocked backup destination fails atomic replacement");
                Require(restarted.LoadData(HLPropertyStore.FileType.Primary, "slot") == builder.ToString(), "failed replacement retains previous primary");
                Directory.Delete(backup);
                RequireThrows<ArgumentException>(() => restarted.LoadData(HLPropertyStore.FileType.Primary, "../escape"), "portable slot components cannot escape save directory");
                restarted.WipeSaveFile("slot");
                Require(restarted.GetAllSaveIdentifiers().Count == 0 && File.Exists(Path.Combine(directory, ".save-slot.interrupted.tmp")), "explicit slot deletion preserves unrelated temp evidence");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static void VerifyDeferredWrite()
        {
            var firstMemory = new MemoryStorage(); HLPropertyStore owner = CreateStore(firstMemory);
            IEnumerator iterator = (IEnumerator)typeof(HLPropertyStore).GetMethod("SaveAtEndOfFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(owner, new object[] { true });
            Require(iterator.MoveNext() && ReferenceEquals(iterator.Current, StaticField("s_waitForEndOfFrame").GetValue(null)) && firstMemory.Calls.Count == 0,
                "deferred iterator yields shared end-of-frame marker before saving");
            var secondMemory = new MemoryStorage(); HLPropertyStore current = CreateStore(secondMemory);
            bool modifyCalled = false;
            HLPropertyStore.AddModifySaveHandler((HLPropertyList properties, ref bool resolved) => { modifyCalled = true; return false; });
            Require(!iterator.MoveNext() && modifyCalled && !current.IsLoaded && secondMemory.Calls.SequenceEqual(new[] { "save:", "backup:" }) && firstMemory.Calls.Count == 0,
                "deferred iterator writes current singleton with captured flag, bypassing IsLoaded");
            iterator = (IEnumerator)typeof(HLPropertyStore).GetMethod("SaveAtEndOfFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(owner, new object[] { false });
            Require(iterator.MoveNext(), "second deferred iterator reaches wait marker");
            current.Shutdown(); secondMemory.Calls.Clear();
            Require(!iterator.MoveNext() && secondMemory.Calls.Count == 0 && firstMemory.Calls.Count == 0, "absent singleton skips delayed write");
        }

        private static bool Parse(HLPropertyStore store, HLPropertyList list, MemoryStorage memory, string id)
        {
            try
            {
                return (bool)typeof(HLPropertyStore).GetMethod("LoadPropertyData", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(store, new object[] { HLPropertyStore.FileType.Primary, list, id });
            }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }

        private static string Text(string key, string value) { return "4\n1\n" + HLPropertyStore.GetCRC(key) + "\n" + value + "\n"; }
        private static FieldInfo StaticField(string name) { return typeof(HLPropertyStore).GetField(name, BindingFlags.NonPublic | BindingFlags.Static); }
        private static void Require(bool condition, string message) { ++checks; if (!condition) throw new InvalidOperationException(message); }
        private static void RequireThrows<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { ++checks; return; } throw new InvalidOperationException(message);
        }

        private sealed class MemoryStorage : IHLSaveMethod
        {
            internal readonly Dictionary<(HLPropertyStore.FileType, string), string> Content = new Dictionary<(HLPropertyStore.FileType, string), string>();
            internal readonly List<string> Calls = new List<string>();
            internal bool SaveResult = true, BackupResult = true;
            public bool TryGetSaveVersion(string id, out long version) { Calls.Add("version:" + id); version = 77; return true; }
            public IReadOnlyList<string> GetAllSaveIdentifiers() { return Content.Keys.Select(key => key.Item2).Distinct().ToArray(); }
            public bool SaveData(HLPropertyStore.FileType type, StringBuilder content, string id)
            { Calls.Add("save:" + id); if (SaveResult) Content[(type, id)] = content.ToString(); return SaveResult; }
            public bool BackupData(string id)
            { Calls.Add("backup:" + id); if (BackupResult) Content[(HLPropertyStore.FileType.Backup, id)] = Content[(HLPropertyStore.FileType.Primary, id)]; return BackupResult; }
            public void WipeSaveFile(string id) { Calls.Add("wipe:" + id); Content.Remove((HLPropertyStore.FileType.Primary, id)); Content.Remove((HLPropertyStore.FileType.Backup, id)); }
            public void WipeAllSaveFiles() { Calls.Add("wipe-all"); Content.Clear(); }
            public string LoadData(HLPropertyStore.FileType type, string id) { Calls.Add("load:" + type + ":" + id); return Content.TryGetValue((type, id), out string value) ? value : null; }
        }
    }
}
