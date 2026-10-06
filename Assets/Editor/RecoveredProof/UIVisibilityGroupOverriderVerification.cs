using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class UIVisibilityGroupOverriderVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Original visibility overrides: " + label); checks++; }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            { var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); if (field != null) return field; }
            throw new MissingFieldException(name);
        }
        private static object Read(object value, string name) => Field(value.GetType(), name).GetValue(value);
        private static void Set(object value, string name, object data) => Field(value.GetType(), name).SetValue(value, data);
        private static void Call(object value, string name)
        {
            try { value.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(value, null); }
            catch (TargetInvocationException failure) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); throw; }
        }
        private static void Throws<T>(Action action, string label) where T : Exception
        { try { action(); } catch (T) { Check(true, label); return; } throw new Exception("Expected original fault: " + label); }
        private static UIVisibilityGroupOverrider.UIVisibilityGroupOverride Row(UIVisibilityGroupDefinition definition, bool visible)
        {
            var row = new UIVisibilityGroupOverrider.UIVisibilityGroupOverride();
            Set(row, "m_visibilityGroupDefinition", definition); Set(row, "m_visible", visible); return row;
        }
        private static UIVisibilityGroupDefinition Definition(string guid)
        { var definition = Raw<UIVisibilityGroupDefinition>(); Set(definition, "m_guid", guid); return definition; }
        private static UIVisibilityGroupOverrider Component(Dictionary<string, bool> values)
        { var value = Raw<UIVisibilityGroupOverrider>(); Set(value, "m_overridesDictionary", values); return value; }
        private static StackableDataHandle Handle(UIVisibilityGroupOverrider value) => (StackableDataHandle)Read(value, "m_stackableDataHandle");
        private static int Count(StackableData stack) => ((IDictionary)Read(stack, "m_dataDictionary")).Count;
        private static List<DictionaryEntry> Snapshot(IDictionary dictionary)
        { var rows = new List<DictionaryEntry>(); foreach (DictionaryEntry row in dictionary) rows.Add(row); return rows; }
        private static void Restore(IDictionary dictionary, List<DictionaryEntry> rows)
        { dictionary.Clear(); foreach (DictionaryEntry row in rows) dictionary.Add(row.Key, row.Value); }
        private static int VerifyAwakeRows()
        {
            // Exercise the real ProcessManager registry while preserving its row
            // identities and all existing typed-reference dictionaries exactly.
            var registry = (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
            var before = Snapshot(registry); var typed = new Dictionary<IDictionary, List<DictionaryEntry>>();
            foreach (DictionaryEntry row in registry) { var cache = (IDictionary)Read(row.Value, "SystemRefDictionary"); typed.Add(cache, Snapshot(cache)); }
            try
            {
                var values = new Dictionary<string, bool> { { "retained", true } };
                var value = Component(values); var first = Definition("same"); var second = Definition("other");
                Set(value, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride> { Row(first, false), Row(first, true), Row(second, false) });
                Call(value, "Awake");
                Check(ReferenceEquals(Read(value, "m_uiManagerSystemRef"), ProcessManager.GetSystemRef<UIManager>()), "Awake publishes genuine cached UI reference");
                Check(values.Count == 3 && values["same"] && !values["other"] && values["retained"], "duplicate rows overwrite and prior keys survive");
                Set(first, "m_guid", "new"); Set(value, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride> { Row(first, false) }); Call(value, "Awake");
                Check(values.Count == 4 && !values["new"] && values["same"], "repeated Awake retains earlier authored keys");
                Set(value, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride> { Row(second, true), null });
                Throws<NullReferenceException>(() => Call(value, "Awake"), "null row is not skipped");
                Check(values["other"] && Read(value, "m_uiManagerSystemRef") != null, "fault retains preceding dictionary mutation and published reference");
                Set(value, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride> { Row(null, true) });
                Throws<NullReferenceException>(() => Call(value, "Awake"), "null definition is not normalized");
                Set(value, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride> { Row(Definition(null), true) });
                Throws<ArgumentNullException>(() => Call(value, "Awake"), "null GUID preserves dictionary failure");
                Set(value, "m_overrides", null); Throws<NullReferenceException>(() => Call(value, "Awake"), "null authored list preserves enumerator failure");
                Check(values.Count == 4, "failed Awake does not clear accumulated dictionary");
            }
            finally
            {
                foreach (var pair in typed) Restore(pair.Key, pair.Value);
                Restore(registry, before);
            }
            return checks;
        }
        public static int RunManaged()
        {
            checks = 0;
            var row = new UIVisibilityGroupOverrider.UIVisibilityGroupOverride();
            Check(ReferenceEquals(row.VisibilityGroupDefinition, null) && !row.Visible, "real nested base-only constructor defaults");
            var definition = Definition("g"); row = Row(definition, true);
            Check(ReferenceEquals(row.VisibilityGroupDefinition, definition) && row.Visible, "nested exact authored getters");
            Set(row, "m_visible", false); Check(!row.Visible, "nested false is retained");
            var dictionary = new Dictionary<string, bool> { { "g", false } }; var value = Component(dictionary);
            value.DeactivateOverrides(); Call(value, "OnDestroy"); Check(Handle(value) == null, "no handle short-circuits even without system reference");
            Throws<NullReferenceException>(() => value.ActivateOverrides(), "activation checks system before any handle");
            var retained = new StackableDataHandle(); Set(value, "m_stackableDataHandle", retained);
            Throws<NullReferenceException>(() => value.DeactivateOverrides(), "handle with missing reference is not silently discarded");
            Check(ReferenceEquals(Handle(value), retained), "missing-reference fault retains handle");
            Set(value, "m_uiManagerSystemRef", new SystemRef<UIManager>(null, null));
            value.ActivateOverrides(); value.DeactivateOverrides(); Call(value, "OnDestroy");
            Check(ReferenceEquals(Handle(value), retained), "unavailable service retains handle across activation/removal/destruction");
            Set(value, "m_stackableDataHandle", null); value.ActivateOverrides(); Check(Handle(value) == null, "unavailable service does not allocate override");
            var manager = Raw<UIManager>(); var stack = new StackableData(); Set(manager, "m_visibilityGroupsStack", stack);
            Set(value, "m_uiManagerSystemRef", new SystemRef<UIManager>(null, manager));
            int events = 0; var observed = new List<StackableDataHandle>(); var sizes = new List<int>();
            Action<int> observer = id => { Check(id == 0, "genuine UI visibility stack key"); events++; observed.Add(Handle(value)); sizes.Add(Count(stack)); };
            stack.OnDataUpdated += observer;
            value.ActivateOverrides(); var first = Handle(value);
            Check(first != null && Count(stack) == 2 && events == 1, "valid activation allocates one real stack override");
            Check(ReferenceEquals(stack.GetOverride<Dictionary<string, bool>>(first, 0), dictionary), "exact original dictionary passed by reference");
            Check(observed[0] == null && sizes[0] == 2, "new handle publication follows stack callback");
            value.ActivateOverrides(); var second = Handle(value);
            Check(second != null && !ReferenceEquals(first, second) && Count(stack) == 2 && events == 3, "reactivation removes old handle before fresh allocation");
            Check(ReferenceEquals(observed[1], first) && observed[2] == null && sizes[1] == 1 && sizes[2] == 2, "remove callback observes old handle and add callback observes cleared field");
            value.DeactivateOverrides(); Check(Handle(value) == null && Count(stack) == 1 && events == 4, "valid deactivation removes then clears");
            Check(ReferenceEquals(observed[3], second) && sizes[3] == 1, "handle clearing follows removal callback");
            value.DeactivateOverrides(); Call(value, "OnDestroy"); Check(events == 4, "empty removal/destruction does not invoke stack");
            stack.OnDataUpdated -= observer;
            value.ActivateOverrides(); var third = Handle(value);
            Set(value, "m_uiManagerSystemRef", new SystemRef<UIManager>(null, null)); value.DeactivateOverrides();
            Check(ReferenceEquals(Handle(value), third) && Count(stack) == 2, "revoked service keeps live original handle");
            Set(value, "m_uiManagerSystemRef", new SystemRef<UIManager>(null, manager)); value.DeactivateOverrides(); Check(Handle(value) == null && Count(stack) == 1, "restored service removes retained handle");
            value.ActivateOverrides(); Call(value, "OnDestroy"); Check(Handle(value) == null && Count(stack) == 1, "valid destruction routes real removal");
            value.ActivateOverrides(); var fourth = Handle(value); bool callbackRan = false;
            Action<int> failRemoval = id => { callbackRan = true; throw new InvalidOperationException("owned removal failure"); };
            stack.OnDataUpdated += failRemoval;
            try { Throws<InvalidOperationException>(() => value.DeactivateOverrides(), "remove callback failure propagates"); }
            finally { stack.OnDataUpdated -= failRemoval; }
            Check(callbackRan && ReferenceEquals(Handle(value), fourth) && Count(stack) == 1, "remove failure retains handle after underlying stack mutation");
            value.DeactivateOverrides(); Check(Handle(value) == null, "later removal of absent original entry clears retained handle");
            Action<int> failAddition = id => { throw new InvalidOperationException("owned addition failure"); };
            stack.OnDataUpdated += failAddition;
            try { Throws<InvalidOperationException>(() => value.ActivateOverrides(), "addition callback failure propagates"); }
            finally { stack.OnDataUpdated -= failAddition; }
            Check(Handle(value) == null && Count(stack) == 2, "add failure leaves original untracked stack entry before handle publication");
            // This stack and all raw Unity objects are fixture-owned; no actual
            // Unity constructor, manager initialization or native null parity claim.
            VerifyAwakeRows();
            return checks;
        }
        private static void DestroyAll(List<UnityEngine.Object> owned, int index)
        { if (index < 0) return; try { if (!ReferenceEquals(owned[index], null)) UnityEngine.Object.DestroyImmediate(owned[index]); } finally { DestroyAll(owned, index - 1); } }
        public static int RunEngine()
        {
            checks = 0; var owned = new List<UnityEngine.Object>();
            try
            {
                var go = new GameObject("ProjectLucid.VisibilityOverrideVerification"); owned.Add(go); go.SetActive(false);
                var value = go.AddComponent<UIVisibilityGroupOverrider>();
                Check(value != null, "actual genuine component creation");
                var dictionary = (Dictionary<string, bool>)Read(value, "m_overridesDictionary");
                Check(dictionary != null && dictionary.Count == 0, "actual readonly dictionary initializer");
                Check(Read(value, "m_overrides") == null && Read(value, "m_uiManagerSystemRef") == null && Handle(value) == null, "actual remaining own fields start null");
                var definition = ScriptableObject.CreateInstance<UIVisibilityGroupDefinition>(); owned.Add(definition); Set(definition, "m_guid", "actual");
                Set(value, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride> { Row(definition, true), Row(definition, false) });
                string json = JsonUtility.ToJson(value);
                Check(json.Contains("m_overrides") && json.Contains("m_visibilityGroupDefinition") && json.Contains("m_visible"), "actual authored list/nested field serialization");
                Check(!json.Contains("m_overridesDictionary") && !json.Contains("m_uiManagerSystemRef") && !json.Contains("m_stackableDataHandle"), "actual runtime state excluded from JSON");
                var copyGo = new GameObject("ProjectLucid.VisibilityOverrideCopy"); owned.Add(copyGo); copyGo.SetActive(false); var copy = copyGo.AddComponent<UIVisibilityGroupOverrider>();
                var copyDictionary = Read(copy, "m_overridesDictionary"); JsonUtility.FromJsonOverwrite(json, copy);
                var copiedRows = (List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride>)Read(copy, "m_overrides");
                Check(copiedRows.Count == 2 && ReferenceEquals(copiedRows[0].VisibilityGroupDefinition, definition) && ReferenceEquals(copiedRows[1].VisibilityGroupDefinition, definition), "actual nested definition references roundtrip");
                Check(copiedRows[0].Visible && !copiedRows[1].Visible, "actual duplicate rows retain authored values/order");
                Check(ReferenceEquals(Read(copy, "m_overridesDictionary"), copyDictionary), "JSON retains runtime dictionary initializer identity");
                value.DeactivateOverrides(); copy.DeactivateOverrides(); Check(Handle(value) == null && Handle(copy) == null, "actual no-handle removal short circuit");
                return checks;
            }
            finally { DestroyAll(owned, owned.Count - 1); }
        }
        public static void Run() { int managed = RunManaged(); if (managed != 38) throw new Exception("Original visibility managed count changed: " + managed); int engine = RunEngine(); if (engine != 9) throw new Exception("Original visibility engine count changed: " + engine); Debug.Log("Bounded original visibility override checks=" + (managed + engine) + " (managed=" + managed + ", actual Unity=" + engine + "); original Actor/Character/startup/asset binding and fullgame remain unapproved."); }
    }
}
