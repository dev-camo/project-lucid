using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Verification
{
    public static partial class ControllerActionVerification
    {
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Controller action fixture: " + message); }
        static FieldInfo Field(Type owner, string name, Type type, int flags)
        {
            FieldInfo field = owner.GetField(name, Declared); Require(field != null && field.DeclaringType == owner && field.FieldType == type && (int)field.Attributes == flags, "exact field " + owner.FullName + "." + name); return field;
        }
        static MethodInfo Method(Type owner, string name, Type result, int flags, params Type[] args)
        {
            MethodInfo method = owner.GetMethod(name, Declared, null, args, null); Require(method != null && method.Name == name && method.DeclaringType == owner && method.ReturnType == result && (int)method.Attributes == flags && !method.IsGenericMethod, "exact method " + owner.FullName + "." + name); return method;
        }
        static void Invoke(MethodInfo method, object target, params object[] args)
        {
            try { method.Invoke(target, args); } catch (TargetInvocationException e) { if (e.InnerException == null) throw; ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        static void Fault<T>(Action action, string name) where T : Exception
        {
            try { action(); } catch (Exception e) { Require(e.GetType() == typeof(T), name + " exact exception"); return; } throw new InvalidOperationException(name + " did not fault");
        }
        static void SameFault(Action action, Exception sentinel)
        {
            try { action(); } catch (Exception e) { Require(ReferenceEquals(e, sentinel), "same owned validator exception"); return; } throw new InvalidOperationException("Validator did not fault");
        }
        static void Finish(Action body, params Action[] cleanup)
        {
            var failures = new List<Exception>(); try { body(); } catch (Exception e) { failures.Add(e); }
            foreach (Action action in cleanup) try { action(); } catch (Exception e) { failures.Add(e); }
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw(); if (failures.Count > 1) throw new AggregateException(failures);
        }
        static void Dispose(IDisposable lease) { if (lease != null) lease.Dispose(); }
        static void Destroy(UnityEngine.Object value) { if (!ReferenceEquals(value, null)) UnityEngine.Object.DestroyImmediate(value); }
        static bool Down(InputActionSubscription value) { return (bool)Field(typeof(InputActionSubscription), "m_inputDown", typeof(bool), 1).GetValue(value); }
        static bool Held(InputActionSubscription value) { return (bool)Field(typeof(InputActionSubscription), "m_inputHeld", typeof(bool), 1).GetValue(value); }
        static MethodInfo HeldCallback() { return Method(typeof(InputActionSubscription), "OnCallbackHeld", typeof(void), 129, typeof(float)); }
        static MethodInfo UpCallback() { return Method(typeof(InputActionSubscription), "OnCallbackUp", typeof(void), 129, typeof(float)); }
        static MethodInfo DisabledCallback() { return Method(typeof(InputActionSubscription), "OnInputDisabled", typeof(void), 129, typeof(GameInput), typeof(bool)); }
        static GameInput Input() { return (GameInput)Enum.GetValues(typeof(GameInput)).GetValue(0); }
        static GameAction ActionValue() { return (GameAction)Enum.GetValues(typeof(GameAction)).GetValue(0); }
        static void Flags(InputActionSubscription value, bool down, bool held) { Require(Down(value) == down && Held(value) == held, "published down/held flags"); }
        static CharacterActionSubscription Construct(GameControlsBinding binding, InputSubscription.ValidatorFunc validator)
        {
            var value = new CharacterActionSubscription(ActionValue(), null, binding, validator);
            Require(ReferenceEquals(Field(typeof(InputSubscription), "m_binding", typeof(GameControlsBinding), 33).GetValue(value), binding) && ReferenceEquals(Field(typeof(InputSubscription), "m_validator", typeof(InputSubscription.ValidatorFunc), 33).GetValue(value), validator), "base readonly constructor storage");
            Require(Equals(Field(typeof(CharacterActionSubscription), "m_action", typeof(GameAction), 33).GetValue(value), ActionValue()) && ReferenceEquals(Field(typeof(CharacterActionSubscription), "m_brain", typeof(CharacterBrain), 33).GetValue(value), null), "derived readonly constructor storage"); Flags(value, false, false); return value;
        }
        static IDictionary Registry()
        {
            FieldInfo field = typeof(ProcessManager).GetField("s_systemDictionary", Declared); Require(field != null && (int)field.Attributes == 49 && field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>) && field.FieldType.GetGenericArguments()[0] == typeof(string), "genuine readonly registry"); return (IDictionary)field.GetValue(null);
        }
        static IDictionary TypedCache(object info)
        {
            FieldInfo field = info.GetType().GetField("SystemRefDictionary", Declared); Require(field != null && (int)field.Attributes == 38 && field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>), "readonly typed cache container"); return (IDictionary)field.GetValue(info);
        }
        static SystemRef Untyped(object info) { return (SystemRef)Field(info.GetType(), "SystemRef", typeof(SystemRef), 38).GetValue(info); }
        // Preserve the real first-use association: initialize once BEFORE any per-case mutable lease.
        // This never resets ProcessManager or assigns the readonly InputActionSubscription cache field.
        static SystemRef<InputSystem> Prepare()
        {
            CheckCompleteCurrentDeclarationsAndBodies();
            Require(typeof(ProcessManager).Assembly.GetName().Name == "HLUnityCore.Runtime" && typeof(SystemRef<>).Assembly == typeof(ProcessManager).Assembly && typeof(ControlMapping).Assembly.GetName().Name == "HLInput.Runtime" && typeof(GameInput).Assembly.GetName().Name == "HLAutoGenerated" && typeof(GameAction).Assembly == typeof(GameInput).Assembly && typeof(InputSystem).Assembly.GetName().Name == "Game.Runtime", "genuine supplier assemblies");
            Require(typeof(CharacterBrain).IsAbstract && typeof(CharacterBrain).Assembly == typeof(InputSystem).Assembly && typeof(GameControlsBinding).Assembly == typeof(InputSystem).Assembly && typeof(InputModifier).Assembly == typeof(ControlMapping).Assembly && typeof(ModifierMultiplier).Assembly == typeof(ControlMapping).Assembly, "genuine abstract brain/binding/modifier provider identities");
            string name = typeof(InputSystem).ToString(); IDictionary registry = Registry(); var before = new List<DictionaryEntry>(); foreach (DictionaryEntry row in registry) before.Add(row);
            object oldInfo = registry.Contains(name) ? registry[name] : null; IDictionary cacheBefore = oldInfo == null ? null : TypedCache(oldInfo); var oldCache = new List<DictionaryEntry>(); if (cacheBefore != null) foreach (DictionaryEntry row in cacheBefore) oldCache.Add(row);
            RuntimeHelpers.RunClassConstructor(typeof(InputActionSubscription).TypeHandle);
            var reference = (SystemRef<InputSystem>)Field(typeof(InputActionSubscription), "m_inputSystemRef", typeof(SystemRef<InputSystem>), 49).GetValue(null);
            Require(reference != null && registry.Contains(name), "genuine initializer created or reused default row");
            foreach (DictionaryEntry row in before) Require(registry.Contains(row.Key) && ReferenceEquals(registry[row.Key], row.Value), "initializer preserves all prior registry entries");
            Require(registry.Count == before.Count + (oldInfo == null ? 1 : 0), "only intended first-use row addition");
            object info = registry[name]; if (oldInfo != null) Require(ReferenceEquals(oldInfo, info), "existing default row retained");
            IDictionary cache = TypedCache(info); foreach (DictionaryEntry row in oldCache) Require(cache.Contains(row.Key) && ReferenceEquals(cache[row.Key], row.Value), "prior typed references retained");
            Require(cache.Contains(name) && ReferenceEquals(cache[name], reference), "readonly cached reference belongs to genuine future registry association");
            Require(ReferenceEquals(ProcessManager.GetSystemRef<InputSystem>(null, true), reference), "real cached reference identity"); return reference;
        }

        public static int VerifyDeclarationsConstructorsAndCache()
        {
            SystemRef<InputSystem> cache = Prepare(); var binding = new GameControlsBinding(Input()); int calls = 0; InputSubscription.ValidatorFunc validator = () => { calls++; return true; };
            CharacterActionSubscription value = Construct(binding, validator); Require(calls == 0, "constructor does not invoke validator");
            Invoke(Method(typeof(CharacterActionSubscription), "OnHeld", typeof(void), 196, typeof(float)), value, 1f); Flags(value, false, false); Require(calls == 0, "genuine empty OnHeld has no side effects");
            Require(ReferenceEquals(Field(typeof(InputActionSubscription), "m_inputSystemRef", typeof(SystemRef<InputSystem>), 49).GetValue(null), cache), "constructor retains first-use cached association"); return 5;
        }
        public static int VerifyHeldUpAndDisabledPrefixes()
        {
            Prepare(); int calls = 0; var value = Construct(new GameControlsBinding(Input()), () => { calls++; return true; });
            Fault<NullReferenceException>(() => Invoke(HeldCallback(), value, 1f), "first held null brain"); Flags(value, true, false); Require(calls == 1, "validator once before first down fault");
            Invoke(HeldCallback(), value, 1f); Flags(value, true, true); Require(calls == 2, "held path uses empty genuine callback");
            // The held branch is established through real calls; NEVER invoke the no-held coroutine path.
            Fault<NullReferenceException>(() => Invoke(UpCallback(), value, 0f), "held-up null brain"); Flags(value, false, false); Require(calls == 2, "up skips modifier/validator");
            var disabled = Construct(new GameControlsBinding(Input()), null); Fault<NullReferenceException>(() => Invoke(HeldCallback(), disabled, 1f), "disabled setup first down"); Invoke(HeldCallback(), disabled, 1f); Flags(disabled, true, true);
            Fault<NullReferenceException>(() => Invoke(DisabledCallback(), disabled, (GameInput)int.MinValue, false), "disabled false null brain"); Flags(disabled, false, true);
            Fault<NullReferenceException>(() => Invoke(DisabledCallback(), disabled, Input(), true), "disabled true null brain"); Flags(disabled, false, true); return 8;
        }
        public static int VerifyRejectedModifiersAndValidatorFaults()
        {
            Prepare(); ModifierMultiplier multiplier = null; int checks = 0;
            Finish(() =>
            {
                int calls = 0; bool accepted = false; var binding = new GameControlsBinding(Input()); var value = Construct(binding, () => { calls++; return accepted; });
                Invoke(HeldCallback(), value, 1f); Flags(value, false, false); Require(calls == 1, "rejected validator called once"); checks++;
                accepted = true; Fault<NullReferenceException>(() => Invoke(HeldCallback(), value, 1f), "accepted down null brain"); Invoke(HeldCallback(), value, 1f); Flags(value, true, true);
                accepted = false; int before = calls; Invoke(HeldCallback(), value, 1f); Flags(value, true, true); Require(calls == before + 1, "rejection retains established flags"); checks++;
                before = calls; Invoke(HeldCallback(), value, 0f); Flags(value, true, true); Require(calls == before, "zero tolerance rejection precedes validator"); checks++;
                multiplier = ScriptableObject.CreateInstance<ModifierMultiplier>(); Field(typeof(ModifierMultiplier), "m_multiplier", typeof(float), 1).SetValue(multiplier, 0f);
                Field(typeof(GameControlsBinding), "m_modifiers", typeof(List<InputModifier>), 1).SetValue(binding, new List<InputModifier> { multiplier });
                before = calls; Invoke(HeldCallback(), value, 1f); Flags(value, true, true); Require(calls == before, "real zero multiplier rejects before validator and flags"); checks++;
                Field(typeof(GameControlsBinding), "m_modifiers", typeof(List<InputModifier>), 1).SetValue(binding, new List<InputModifier> { null });
                before = calls; Fault<NullReferenceException>(() => Invoke(HeldCallback(), value, 1f), "real null modifier faults before validator"); Flags(value, true, true); Require(calls == before, "modifier fault retains flags and skips validator"); checks++;
                var sentinel = new InvalidOperationException("owned controller validator fault"); var faulting = Construct(new GameControlsBinding(Input()), () => { throw sentinel; });
                SameFault(() => Invoke(HeldCallback(), faulting, 1f), sentinel); Flags(faulting, false, false); checks++;
            }, () => Destroy(multiplier)); return checks;
        }
        public static int VerifySettingsMappingAndOwnedClosures()
        {
            Prepare(); int calls = 0; GameAction observed = default(GameAction); ButtonMappingType mapping = ButtonMappingType.None; GameAction action = ActionValue(); var primary = new GameControlsBinding(Input()); var secondary = new GameControlsBinding(Input());
            var settings = new CharacterActionSettingsSubscription(action, null, primary, secondary, value => { calls++; observed = value; return mapping; });
            Require(calls == 0 && settings.ActionPrimary != null && settings.ActionSecondary != null && !ReferenceEquals(settings.ActionPrimary, settings.ActionSecondary), "two constructed children before any mapping callback");
            Require(ReferenceEquals(Field(typeof(InputSubscription), "m_binding", typeof(GameControlsBinding), 33).GetValue(settings.ActionPrimary), primary) && ReferenceEquals(Field(typeof(InputSubscription), "m_binding", typeof(GameControlsBinding), 33).GetValue(settings.ActionSecondary), secondary), "primary/secondary binding identities");
            var p = (InputSubscription.ValidatorFunc)Field(typeof(InputSubscription), "m_validator", typeof(InputSubscription.ValidatorFunc), 33).GetValue(settings.ActionPrimary);
            var s = (InputSubscription.ValidatorFunc)Field(typeof(InputSubscription), "m_validator", typeof(InputSubscription.ValidatorFunc), 33).GetValue(settings.ActionSecondary);
            Require(p != null && s != null && ReferenceEquals(p.Target, s.Target) && p.Method.Name == "<.ctor>b__0" && s.Method.Name == "<.ctor>b__1", "two genuine generated validator lambdas share capture");
            Require(ReferenceEquals(p.Target.GetType().GetField("<>4__this", Declared).GetValue(p.Target), settings) && Equals(p.Target.GetType().GetField("action", Declared).GetValue(p.Target), action), "genuine captured owner and action"); int checks = 3;
            int[] literals = { 0, 1379978408, -671093917, -1603365627 }; string[] names = { "None", "All", "Primary", "Secondary" }; Require(Enum.GetValues(typeof(ButtonMappingType)).Length == 4, "complete mapping enum");
            for (int i = 0; i < literals.Length; i++) Require((int)Enum.Parse(typeof(ButtonMappingType), names[i]) == literals[i], "exact mapping literal");
            foreach (ButtonMappingType next in new[] { ButtonMappingType.None, ButtonMappingType.Primary, ButtonMappingType.Secondary, ButtonMappingType.All, (ButtonMappingType)int.MinValue })
            {
                mapping = next; int before = calls; bool first = p(); Require(calls == before + 1 && observed == action && first == (next == ButtonMappingType.Primary || next == ButtonMappingType.All), "primary mapping invokes once and accepts exact enum/All"); checks++;
                before = calls; bool second = s(); Require(calls == before + 1 && observed == action && second == (next == ButtonMappingType.Secondary || next == ButtonMappingType.All), "secondary mapping invokes once and accepts exact enum/All"); checks++;
            }
            var nullMapping = new CharacterActionSettingsSubscription(action, null, primary, secondary, null); Require(nullMapping.ActionPrimary != null && nullMapping.ActionSecondary != null, "null mapping construction still completes both children");
            var nullValidator = (InputSubscription.ValidatorFunc)Field(typeof(InputSubscription), "m_validator", typeof(InputSubscription.ValidatorFunc), 33).GetValue(nullMapping.ActionPrimary); Fault<NullReferenceException>(() => nullValidator(), "null mapping actual delegate fault"); checks++; return checks;
        }

        sealed class ReferenceLease : IDisposable
        {
            readonly object target; readonly FieldInfo[] fields; readonly object[] values;
            public ReferenceLease(object target)
            {
                this.target = target; Type owner = target.GetType(); while (owner != null && (!owner.IsGenericType || owner.GetGenericTypeDefinition() != typeof(SystemRef<>))) owner = owner.BaseType;
                Require(owner != null, "genuine SystemRef base"); Type argument = owner.GetGenericArguments()[0]; string[] names = { "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid", "m_system", "m_isystem" }; Type[] types = { typeof(Action<>).MakeGenericType(argument), typeof(Action<>).MakeGenericType(argument), typeof(FastAction<>).MakeGenericType(argument), argument, typeof(ISystem) }; fields = new FieldInfo[names.Length]; values = new object[names.Length];
                for (int i = 0; i < fields.Length; i++) { fields[i] = Field(owner, names[i], types[i], 1); values[i] = fields[i].GetValue(target); }
                try { foreach (FieldInfo field in fields) field.SetValue(target, null); } catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                Action[] restore = fields.Select((field, index) => (Action)(() => { field.SetValue(target, values[index]); Require(ReferenceEquals(field.GetValue(target), values[index]), "exact reference state restored"); })).ToArray(); Finish(() => { }, restore);
            }
        }
        sealed class RegistryRowLease : IDisposable
        {
            readonly IDictionary registry; readonly string name; readonly object info; readonly SystemRef untyped; readonly IDictionary cache; readonly DictionaryEntry[] rows; readonly List<ReferenceLease> references = new List<ReferenceLease>();
            public readonly SystemRef<InputSystem> Typed;
            public RegistryRowLease(SystemRef<InputSystem> typed)
            {
                Typed = typed; name = typeof(InputSystem).ToString(); registry = Registry(); info = registry[name]; untyped = Untyped(info); cache = TypedCache(info); var snapshot = new List<DictionaryEntry>(); foreach (DictionaryEntry row in cache) snapshot.Add(row); rows = snapshot.ToArray();
                // Do not remove the row, clear the registry/cache or reassign any readonly field.
                var targets = new List<object> { untyped }; foreach (DictionaryEntry row in rows) if (!targets.Any(x => ReferenceEquals(x, row.Value))) targets.Add(row.Value);
                try { foreach (object target in targets) references.Add(new ReferenceLease(target)); Require(ReferenceEquals(cache[name], typed) && ReferenceEquals(typed.GetSafe(), null) && ReferenceEquals(untyped.GetSafe(), null), "leased live values; original cached row/reference retained"); } catch { Dispose(); throw; }
            }
            public void Identity()
            {
                Require(registry.Contains(name) && ReferenceEquals(registry[name], info) && ReferenceEquals(Untyped(info), untyped) && ReferenceEquals(TypedCache(info), cache) && cache.Count == rows.Length, "row and typed-cache identities preserved");
                foreach (DictionaryEntry row in rows) Require(cache.Contains(row.Key) && ReferenceEquals(cache[row.Key], row.Value), "each typed reference retained");
            }
            public void Dispose() { var cleanup = references.Select(x => (Action)x.Dispose).ToList(); cleanup.Add(Identity); Finish(() => { }, cleanup.ToArray()); }
        }
        public static int VerifyOwnedInputSystemExplicitCallbacks()
        {
            SystemRef<InputSystem> reference = Prepare(); RegistryRowLease lease = null; GameObject owned = null; InputSystem system = null; int checks = 0;
            Finish(() =>
            {
                lease = new RegistryRowLease(reference); owned = new GameObject("Owned original input-system fixture"); owned.SetActive(false); system = owned.AddComponent<InputSystem>();
                Require(!owned.activeInHierarchy && ReferenceEquals(reference.GetSafe(), null), "never-active setup has not registered component automatically"); Require(ReferenceEquals(system.ControlMapping, null) && ReferenceEquals(system.InputMonitor, null), "original serialized getter defaults"); checks++;
                int starts = 0, stops = 0; reference.OnSystemStartup += value => { starts++; Require(ReferenceEquals(value, system) && ReferenceEquals(reference.GetSafe(), system), "typed publication precedes owned startup"); };
                reference.OnSystemShutdown += value => { stops++; Require(ReferenceEquals(value, system) && ReferenceEquals(reference.GetSafe(), system), "owned shutdown observes system before revoke"); };
                Invoke(Method(typeof(InputSystem), "Awake", typeof(void), 129), system); Require(starts == 1 && stops == 0 && ReferenceEquals(reference.GetSafe(), system), "explicit original Awake registers real owned ISystem"); lease.Identity(); checks++;
                Invoke(Method(typeof(InputSystem), "OnDestroy", typeof(void), 129), system); Require(starts == 1 && stops == 1 && ReferenceEquals(reference.GetSafe(), null), "explicit original OnDestroy revokes but retains cache row"); lease.Identity(); checks++;
                Invoke(Method(typeof(InputSystem), "OnDestroy", typeof(void), 129), system); Require(stops == 1 && ReferenceEquals(reference.GetSafe(), null), "repeat teardown no extra owned notification"); checks++;
            }, () => Destroy(owned), () => Dispose(lease)); return checks;
        }
    }
}
