using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Verification
{
    // Owned, controlled checks. This helper never creates a module or invokes UI lifecycle services.
    public static class UIConcreteInputVerification
    {
        const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly string[] MappingNames = { "s_onPointer", "s_onScrollWheel", "s_onTap", "s_onTouch", "s_onTouchRelease", "s_onMultiTouch", "s_onMultiTouchRelease", "s_onVectorisedGameInput", "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis", "s_onAxisStart", "s_onAxisEnd", "s_onSwipe" };
        static readonly int[] EnumValues = { 897719803, -1162288346, -594121215, -1482781790, -1000566503, -1901056579, 1329525373, 1437681611, -31649603, 1838982690, 1208867107, -1984142849 };
        static readonly string[] EnumNames = { "DefaultController", "Keyboard", "MfiController", "Mouse", "NpadController", "PS4Controller", "PS5Controller", "Remote", "Touch", "Unknown", "Unsupported", "XboneController" };
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("UI concrete input: " + message); }
        static FieldInfo Field(Type type, string name, Type fieldType, FieldAttributes flags)
        {
            FieldInfo field = type.GetField(name, Declared);
            Require(field != null && field.DeclaringType == type && field.FieldType == fieldType && field.Attributes == flags, "exact field " + type.FullName + "." + name);
            return field;
        }
        static FieldInfo ModuleField() { return Field(typeof(HLInputBridge), "m_inputModule", typeof(HLInputModule), FieldAttributes.Private); }
        static FieldInfo LatchField() { return Field(typeof(HLInputBridge), "m_listenersRegistered", typeof(bool), FieldAttributes.Private); }
        static FieldInfo SupplierField() { return Field(typeof(HLInputSupplier), "m_input", typeof(GameInput), FieldAttributes.Private); }
        static MethodInfo Callback() { return Exact(typeof(HLInputBridge), "OnUpdateLastInputTypeBridge", typeof(void), 129, new[] { typeof(InputType) }); }
        static MethodInfo Shutdown() { return Exact(typeof(HLInputBridge), "InternalOnShutdown", typeof(void), 145, Type.EmptyTypes); }
        static MethodInfo Exact(Type owner, string name, Type result, int flags, Type[] args)
        {
            MethodInfo method = owner.GetMethod(name, Declared, null, args, null);
            Require(method != null && method.Name == name && method.DeclaringType == owner && method.ReturnType == result && (int)method.Attributes == flags && method.GetMethodImplementationFlags() == 0 && !method.IsGenericMethod && method.GetCustomAttributesData().Count == 0 && method.ReturnParameter.Attributes == ParameterAttributes.Retval && method.ReturnParameter.Position == -1 && method.ReturnParameter.ParameterType == method.ReturnType && method.ReturnParameter.GetCustomAttributesData().Count == 0 && method.CallingConvention == (method.IsStatic ? CallingConventions.Standard : CallingConventions.Standard | CallingConventions.HasThis), "exact method " + owner.FullName + "." + name);
            return method;
        }
        static void Call(MethodInfo method, object target, params object[] args)
        {
            try { method.Invoke(target, args); }
            catch (TargetInvocationException e) { if (e.InnerException == null) throw; ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        static void Fault<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (Exception e) { Require(e.GetType() == typeof(T), message + " exact exception"); return; }
            throw new InvalidOperationException(message + " did not fault");
        }
        static void SentinelFault(Action action, Exception sentinel)
        {
            try { action(); } catch (Exception e) { Require(ReferenceEquals(e, sentinel), "same owned callback exception"); return; }
            throw new InvalidOperationException("Owned callback exception did not propagate");
        }
        // Run every cleanup independently, even if the assertion, destruction or another cleanup faults.
        static void Finish(Action body, params Action[] cleanup)
        {
            var failures = new List<Exception>();
            try { body(); } catch (Exception e) { failures.Add(e); }
            foreach (Action action in cleanup) try { action(); } catch (Exception e) { failures.Add(e); }
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }
        static void Destroy(UnityEngine.Object value) { if (!ReferenceEquals(value, null)) UnityEngine.Object.DestroyImmediate(value); }
        sealed class StaticLease : IDisposable
        {
            readonly FieldInfo field; readonly object original; bool changed;
            public StaticLease(Type owner, string name, Type fieldType, FieldAttributes flags)
            {
                field = Field(owner, name, fieldType, flags); Require(field.IsStatic && !field.IsInitOnly && !field.IsLiteral, "mutable static lease");
                original = field.GetValue(null);
                try { field.SetValue(null, null); changed = true; }
                catch { field.SetValue(null, original); throw; }
            }
            public void Set(object value) { Require(changed, "open lease"); field.SetValue(null, value); }
            public object Get() { return field.GetValue(null); }
            public void Dispose() { if (changed) { field.SetValue(null, original); changed = false; Require(ReferenceEquals(field.GetValue(null), original), "exact static reference restored"); } }
        }
        sealed class LastTypeLease : IDisposable
        {
            readonly FieldInfo field = Field(typeof(ControlMapping), "s_lastInputType", typeof(InputType), FieldAttributes.Private | FieldAttributes.Static);
            readonly object original;
            public LastTypeLease() { original = field.GetValue(null); }
            public void Set(InputType value) { field.SetValue(null, value); }
            public void Dispose() { field.SetValue(null, original); Require(Equals(field.GetValue(null), original), "last input enum restored"); }
        }
        sealed class ActionState
        {
            readonly object target; readonly IList[] lists; readonly object[][] items; readonly FieldInfo active; readonly object wasActive;
            public ActionState(object target)
            {
                this.target = target; Type type = target.GetType().BaseType;
                Require(type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(FastActionBase<,>), "genuine action base");
                active = Field(type, "m_invocationActive", typeof(bool), FieldAttributes.Family); wasActive = active.GetValue(target);
                string[] names = { "m_invocationList", "m_removedList", "m_addedList" }; lists = new IList[3]; items = new object[3][];
                for (int i = 0; i < 3; i++)
                {
                    FieldInfo field = type.GetField(names[i], Declared);
                    Require(field != null && field.IsInitOnly && !field.IsStatic && field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>), "action container " + names[i]);
                    lists[i] = (IList)field.GetValue(target); items[i] = lists[i].Cast<object>().ToArray();
                }
            }
            public void Unchanged()
            {
                Require(Equals(active.GetValue(target), wasActive), "mapping action active unchanged");
                for (int i = 0; i < 3; i++) Require(lists[i].Count == items[i].Length && lists[i].Cast<object>().Zip(items[i], ReferenceEquals).All(x => x), "mapping action listeners unchanged");
            }
            public void Restore()
            {
                Finish(() => { }, () => RestoreList(0), () => RestoreList(1), () => RestoreList(2), () => active.SetValue(target, wasActive));
            }
            void RestoreList(int i) { lists[i].Clear(); foreach (object item in items[i]) lists[i].Add(item); }
        }
        sealed class MappingLease : IDisposable
        {
            readonly List<IDictionary> dictionaries = new List<IDictionary>(); readonly List<DictionaryEntry[]> entries = new List<DictionaryEntry[]>();
            readonly List<ActionState> actions = new List<ActionState>();
            public MappingLease()
            {
                // Acquisition only reads state. No existing callback is detached, invoked or mutated here.
                foreach (string name in MappingNames)
                {
                    FieldInfo field = typeof(ControlMapping).GetField(name, Declared);
                    Require(field != null && field.Attributes == (FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly) && field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>), "mapping dictionary " + name);
                    Type[] args = field.FieldType.GetGenericArguments();
                    Require(args[0].DeclaringType == typeof(ControlMapping) && args[0].Name == "GameInputKey" && args[1].IsGenericType && args[1].GetGenericTypeDefinition() == typeof(FastAction<>), "mapping key/action scope");
                    IDictionary dictionary = (IDictionary)field.GetValue(null); Require(dictionary != null, "mapping initialized");
                    var snapshot = new List<DictionaryEntry>(); foreach (DictionaryEntry entry in dictionary) { snapshot.Add(entry); if (entry.Value != null) actions.Add(new ActionState(entry.Value)); }
                    dictionaries.Add(dictionary); entries.Add(snapshot.ToArray());
                }
            }
            public void Unchanged()
            {
                for (int i = 0; i < dictionaries.Count; i++)
                {
                    Require(dictionaries[i].Count == entries[i].Length, "mapping dictionary count unchanged");
                    foreach (DictionaryEntry entry in entries[i]) Require(dictionaries[i].Contains(entry.Key) && ReferenceEquals(dictionaries[i][entry.Key], entry.Value), "mapping entry reference unchanged");
                }
                foreach (ActionState action in actions) action.Unchanged();
            }
            public void Dispose()
            {
                var cleanup = new List<Action>(); foreach (ActionState action in actions) cleanup.Add(action.Restore);
                for (int i = 0; i < dictionaries.Count; i++) { int index = i; cleanup.Add(() => { dictionaries[index].Clear(); foreach (DictionaryEntry entry in entries[index]) dictionaries[index].Add(entry.Key, entry.Value); }); }
                Finish(() => { }, cleanup.ToArray());
            }
        }
        static void Restore(IDisposable lease) { if (lease != null) lease.Dispose(); }
        static bool Active(object action) { return (bool)Field(action.GetType().BaseType, "m_invocationActive", typeof(bool), FieldAttributes.Family).GetValue(action); }
        static IList Removed(object action) { return (IList)action.GetType().BaseType.GetField("m_removedList", Declared).GetValue(action); }

        public static int VerifyDeclarationsAndDefaults()
        {
            HLInputBridge bridge = null; HLInputSupplier supplier = null; int checks = 0;
            Finish(() =>
            {
                CheckDeclarations(); checks++;
                bridge = ScriptableObject.CreateInstance<HLInputBridge>(); supplier = ScriptableObject.CreateInstance<HLInputSupplier>();
                Require(ReferenceEquals(ModuleField().GetValue(bridge), null) && !(bool)LatchField().GetValue(bridge), "bridge default fields"); checks++;
                Require((int)supplier.Input == 0 && (int)(GameInput)SupplierField().GetValue(supplier) == 0, "supplier zero default"); checks++;
                Require(!bridge.InputModuleExists(), "default null module"); checks++;
            }, () => Destroy(bridge), () => Destroy(supplier));
            return checks;
        }
        public static int VerifyNullModuleAndEnumBoundary()
        {
            HLInputBridge bridge = null; int checks = 0;
            Finish(() =>
            {
                CheckDeclarations(); bridge = ScriptableObject.CreateInstance<HLInputBridge>(); bridge.SetInputModule(null);
                Require(ReferenceEquals(ModuleField().GetValue(bridge), null) && !bridge.InputModuleExists(), "null module assignment"); checks++;
                Require(!Enum.IsDefined(typeof(InputType), int.MinValue), "undefined witness remains undefined");
                Require(!bridge.DoesInputTypeRequireSelection((UIInputType)int.MinValue), "invalid enum returns before null module"); checks++;
                foreach (InputType input in Enum.GetValues(typeof(InputType))) { Fault<NullReferenceException>(() => bridge.DoesInputTypeRequireSelection((UIInputType)input), "defined enum reaches null module"); checks++; }
                Action[] forwards = { () => bridge.SetHardwareCursorVisibility(true), () => bridge.GetHardwareCursorVisibility(), () => bridge.DeselectObjectIfCurrentlySelected(null), () => bridge.SetSelectedGameObjectWithMemory(), () => bridge.IsObjectSelected(null) };
                foreach (Action forward in forwards) { Fault<NullReferenceException>(forward, "null module forwarding"); checks++; }
            }, () => Destroy(bridge)); return checks;
        }
        public static int VerifyNullSupplierBeforeMapping()
        {
            HLInputBridge bridge = null; MappingLease mapping = null; int callbacks = 0, checks = 0;
            Finish(() =>
            {
                CheckDeclarations(); bridge = ScriptableObject.CreateInstance<HLInputBridge>(); mapping = new MappingLease();
                Action<Vector2> vector = _ => callbacks++; Action<List<Vector2>> points = _ => callbacks++; Action<float> scalar = _ => callbacks++;
                Action[] calls = { () => bridge.RegisterInputListener(null, vector), () => bridge.UnregisterInputListener(null, vector), () => bridge.RegisterInputListener(null, points), () => bridge.UnregisterInputListener(null, points), () => bridge.RegisterInputListenerOnDown(null, scalar), () => bridge.RegisterInputListenerOnHeld(null, scalar), () => bridge.RegisterInputListenerOnUp(null, scalar), () => bridge.UnregisterInputListener(null, scalar) };
                foreach (Action call in calls) { Fault<NullReferenceException>(call, "null supplier faults first"); mapping.Unchanged(); Require(callbacks == 0, "no owned callback entry"); checks++; }
            }, () => Destroy(bridge), () => Restore(mapping)); return checks;
        }
        public static int VerifySupplierFieldRoundtrip()
        {
            HLInputSupplier supplier = null; int checks = 0;
            Finish(() =>
            {
                CheckDeclarations(); supplier = ScriptableObject.CreateInstance<HLInputSupplier>(); FieldInfo field = SupplierField();
                foreach (GameInput value in Enum.GetValues(typeof(GameInput))) { field.SetValue(supplier, value); Require(supplier.Input == value, "real GameInput field/getter roundtrip"); checks++; }
                Require(checks > 0, "genuine GameInput enum values");
                field.SetValue(supplier, (GameInput)int.MinValue); Require((int)supplier.Input == int.MinValue, "getter does not clamp undefined enum"); checks++;
            }, () => Destroy(supplier)); return checks;
        }
        public static int VerifyTypedCallbackAndLastType()
        {
            HLInputBridge bridge = null; StaticLease action = null; LastTypeLease last = null; int checks = 0;
            Finish(() =>
            {
                CheckDeclarations(); bridge = ScriptableObject.CreateInstance<HLInputBridge>();
                action = new StaticLease(typeof(InputBridge), "OnUpdateLastInputType", typeof(FastAction<UIInputType>), FieldAttributes.Public | FieldAttributes.Static); last = new LastTypeLease();
                var observed = new List<UIInputType>(); var order = new List<int>(); var owned = new FastAction<UIInputType>();
                owned += value => { observed.Add(value); order.Add(1); }; owned += value => { observed.Add(value); order.Add(2); }; action.Set(owned);
                foreach (InputType value in Enum.GetValues(typeof(InputType)))
                {
                    observed.Clear(); order.Clear(); Call(Callback(), bridge, value);
                    Require(observed.Count == 2 && observed[0] == (UIInputType)value && observed[1] == (UIInputType)value && order.SequenceEqual(new[] { 1, 2 }), "typed callback literal and order"); checks++;
                    last.Set(value); Require(bridge.GetLastInputType() == (UIInputType)value, "real last input mapping"); checks++;
                }
                observed.Clear(); order.Clear(); Call(Callback(), bridge, (InputType)int.MinValue);
                Require(observed.SequenceEqual(new[] { UIInputType.Unsupported, UIInputType.Unsupported }), "unsupported callback fallback"); checks++;
                last.Set((InputType)int.MinValue); Require(bridge.GetLastInputType() == UIInputType.Unsupported, "unsupported last input fallback"); checks++;
                action.Set(null); Call(Callback(), bridge, InputType.Mouse); checks++;
                var sentinel = new InvalidOperationException("owned UI type callback fault"); var faulting = new FastAction<UIInputType>(); faulting += _ => { throw sentinel; }; action.Set(faulting);
                SentinelFault(() => Call(Callback(), bridge, InputType.Mouse), sentinel); Require(Active(faulting), "genuine FastAction retains active state after fault"); checks++;
            }, () => Destroy(bridge), () => Restore(action), () => Restore(last)); return checks;
        }
        public static int VerifyRegistrationAndShutdownOrdering()
        {
            HLInputBridge bridge = null; StaticLease mapping = null, module = null, notification = null; int checks = 0;
            Finish(() =>
            {
                CheckDeclarations(); bridge = ScriptableObject.CreateInstance<HLInputBridge>();
                mapping = new StaticLease(typeof(ControlMapping), "OnUpdateLastInputType", typeof(Action<InputType>), FieldAttributes.Private | FieldAttributes.Static);
                module = new StaticLease(typeof(HLInputModule), "OnShutdown", typeof(FastAction), FieldAttributes.Public | FieldAttributes.Static);
                notification = new StaticLease(typeof(InputBridge), "OnShutdown", typeof(FastAction), FieldAttributes.Public | FieldAttributes.Static);
                FieldInfo latch = LatchField(); MethodInfo callback = Callback(), shutdown = Shutdown();
                bridge.RegisterForInputUpdateEvents(); bridge.RegisterForInputUpdateEvents();
                Delegate[] listeners = ((Delegate)mapping.Get()).GetInvocationList();
                Require(listeners.Length == 2 && listeners.All(x => ReferenceEquals(x.Target, bridge) && Same(x.Method, callback)), "two exact instance event registrations");
                FastAction shutdowns = (FastAction)module.Get(); Require(shutdowns.GetInvocationListCount() == 2 && shutdowns.GetInvocationList().All(x => x.Target == null && Same(x.Method, shutdown)), "two exact static shutdown registrations");
                Require(!(bool)latch.GetValue(bridge), "registration leaves latch false"); checks++;
                bridge.UnregisterForInputUpdateEvents(); Require(((Delegate)mapping.Get()).GetInvocationList().Length == 2 && shutdowns.GetInvocationListCount() == 2 && !(bool)latch.GetValue(bridge), "false latch unregister returns"); checks++;
                latch.SetValue(bridge, true); bridge.RegisterForInputUpdateEvents(); Require(((Delegate)mapping.Get()).GetInvocationList().Length == 2 && shutdowns.GetInvocationListCount() == 2, "true latch registration returns"); checks++;
                bridge.UnregisterForInputUpdateEvents(); Require(((Delegate)mapping.Get()).GetInvocationList().Length == 1 && shutdowns.GetInvocationListCount() == 2 && (bool)latch.GetValue(bridge), "true latch unregister removes only first mapping listener"); checks++;
                int notices = 0; var noticesAction = new FastAction();
                noticesAction += () => { notices++; Require(ReferenceEquals(module.Get(), shutdowns) && shutdowns.GetInvocationListCount() == 2 - notices, "self removal published before owned notification"); };
                notification.Set(noticesAction); Call(shutdown, null); Call(shutdown, null); Require(notices == 2 && shutdowns.GetInvocationListCount() == 0 && ((Delegate)mapping.Get()).GetInvocationList().Length == 1, "direct self removal and asymmetric mapping retention"); checks++;
                // Real dispatch queues its removal while active. Observe the queue BEFORE notification, then the completed removal.
                module.Set(null); latch.SetValue(bridge, false); bridge.RegisterForInputUpdateEvents(); FastAction running = (FastAction)module.Get(); int during = 0;
                var duringAction = new FastAction(); duringAction += () => { during++; Require(ReferenceEquals(module.Get(), running) && Active(running) && running.GetInvocationListCount() == 1 && Removed(running).Count == 1, "queued self removal precedes notification during invocation"); };
                notification.Set(duringAction); running.Invoke(); Require(during == 1 && running.GetInvocationListCount() == 0 && !Active(running) && Removed(running).Count == 0, "successful genuine dispatch completes queued removal"); checks++;
                module.Set(null); bridge.RegisterForInputUpdateEvents(); var faultSource = (FastAction)module.Get(); var sentinel = new InvalidOperationException("owned shutdown notification fault");
                var faultNotice = new FastAction(); faultNotice += () => { Require(faultSource.GetInvocationListCount() == 0 && ReferenceEquals(module.Get(), faultSource), "removal publication before fault"); throw sentinel; }; notification.Set(faultNotice);
                SentinelFault(() => Call(shutdown, null), sentinel); Require(faultSource.GetInvocationListCount() == 0 && Active(faultNotice), "removal survives callback fault"); checks++;
            }, () => Destroy(bridge), () => Restore(mapping), () => Restore(module), () => Restore(notification)); return checks;
        }

        static bool Same(MemberInfo a, MemberInfo b) { return a != null && b != null && a.Module == b.Module && a.MetadataToken == b.MetadataToken && a.DeclaringType == b.DeclaringType; }
        static void EnumContract(Type type)
        {
            Require(type.IsEnum && Enum.GetUnderlyingType(type) == typeof(int) && Enum.GetValues(type).Length == 12, "complete twelve-value enum " + type.Name);
            for (int i = 0; i < EnumNames.Length; i++) Require(type.GetField(EnumNames[i]).GetRawConstantValue().Equals(EnumValues[i]), "enum literal " + EnumNames[i]);
        }
        sealed class Contract
        {
            public string Name, Parameters; public Type Result; public Type[] Args; public int Flags;
            public Contract(string name, Type result, int flags, string parameters, params Type[] args) { Name = name; Result = result; Flags = flags; Parameters = parameters; Args = args; }
        }
        static Contract[] Contracts()
        {
            Type vector = typeof(Action<Vector2>), points = typeof(Action<List<Vector2>>), scalar = typeof(Action<float>);
            return new[] {
                new Contract("SetInputModule", typeof(void), 198, "inputModule", typeof(BaseInputModule)), new Contract("InputModuleExists", typeof(bool), 198, ""),
                new Contract("SetHardwareCursorVisibility", typeof(void), 198, "visibility", typeof(bool)), new Contract("GetHardwareCursorVisibility", typeof(bool), 198, ""),
                new Contract("DoesInputTypeRequireSelection", typeof(bool), 198, "uiInputType", typeof(UIInputType)), new Contract("GetLastInputType", typeof(UIInputType), 198, ""),
                new Contract("DeselectObjectIfCurrentlySelected", typeof(void), 198, "gameObject", typeof(GameObject)), new Contract("SetSelectedGameObjectWithMemory", typeof(void), 198, "selectableGameObject,eventData", typeof(GameObject), typeof(BaseEventData)),
                new Contract("IsObjectSelected", typeof(bool), 198, "selectedObject", typeof(GameObject)), new Contract("RegisterForInputUpdateEvents", typeof(void), 198, ""), new Contract("UnregisterForInputUpdateEvents", typeof(void), 198, ""),
                new Contract("OnUpdateLastInputTypeBridge", typeof(void), 129, "inputType", typeof(InputType)), new Contract("InternalOnShutdown", typeof(void), 145, ""),
                new Contract("RegisterInputListener", typeof(void), 198, "inputSupplier,callback,joystickIndex", typeof(InputSupplier), vector, typeof(int)), new Contract("UnregisterInputListener", typeof(void), 198, "inputSupplier,callback,joystickIndex", typeof(InputSupplier), vector, typeof(int)),
                new Contract("RegisterInputListener", typeof(void), 198, "inputSupplier,callback,joystickIndex", typeof(InputSupplier), points, typeof(int)), new Contract("UnregisterInputListener", typeof(void), 198, "inputSupplier,callback,joystickIndex", typeof(InputSupplier), points, typeof(int)),
                new Contract("RegisterInputListenerOnDown", typeof(void), 198, "inputSupplier,callback,joystickIndex", typeof(InputSupplier), scalar, typeof(int)), new Contract("RegisterInputListenerOnHeld", typeof(void), 198, "inputSupplier,callback,joystickIndex", typeof(InputSupplier), scalar, typeof(int)), new Contract("RegisterInputListenerOnUp", typeof(void), 198, "inputSupplier,callback,joystickIndex", typeof(InputSupplier), scalar, typeof(int)),
                new Contract("UnregisterInputListener", typeof(void), 198, "inputSupplier,callback,joystickIndex", typeof(InputSupplier), scalar, typeof(int)) };
        }
        static void CheckDeclarations()
        {
            Type bridge = typeof(HLInputBridge), supplier = typeof(HLInputSupplier);
            Require(bridge.Assembly.GetName().Name == "HLUnityUI.Runtime" && supplier.Assembly == bridge.Assembly && typeof(InputBridge).Assembly == bridge.Assembly && typeof(InputSupplier).Assembly == bridge.Assembly && typeof(UIInputType).Assembly == bridge.Assembly, "genuine UI owners");
            Require(typeof(HLInputModule).Assembly.GetName().Name == "HLInput.Runtime" && typeof(ControlMapping).Assembly == typeof(HLInputModule).Assembly && typeof(InputType).Assembly == typeof(HLInputModule).Assembly, "genuine input owners");
            Require(typeof(FastAction).Assembly.GetName().Name == "HLUnityCore.Runtime" && typeof(FastAction<>).Assembly == typeof(FastAction).Assembly && typeof(FastActionExtensions).Assembly == typeof(FastAction).Assembly, "genuine action owners");
            Require(typeof(GameInput).Assembly.GetName().Name == "HLAutoGenerated", "genuine GameInput owner");
            foreach (Type type in new[] { bridge, supplier }) Require((int)type.Attributes == 1048577 && !type.IsGenericType && type.GetNestedTypes(Declared).Length == 0 && type.GetEvents(Declared).Length == 0 && type.GetInterfaces().Length == 0 && type.TypeInitializer == null, "complete concrete type shape");
            Require(bridge.BaseType == typeof(InputBridge) && supplier.BaseType == typeof(InputSupplier), "genuine abstract bases"); EnumContract(typeof(InputType)); EnumContract(typeof(UIInputType));
            FieldInfo[] fields = bridge.GetFields(Declared).OrderBy(x => x.MetadataToken).ToArray(); Require(fields.Length == 2 && Same(fields[0], ModuleField()) && Same(fields[1], LatchField()) && fields.All(x => x.GetCustomAttributesData().Count == 0), "complete ordered bridge fields");
            Require(supplier.GetFields(Declared).Length == 1 && supplier.GetProperties(Declared).Length == 1 && bridge.GetProperties(Declared).Length == 0, "complete property and supplier field counts");
            var fieldAttrs = SupplierField().GetCustomAttributesData(); Require(fieldAttrs.Count == 2 && fieldAttrs[0].AttributeType == typeof(SerializeField) && fieldAttrs[1].AttributeType == typeof(HashEnumAttribute) && fieldAttrs[1].ConstructorArguments.Count == 1 && Equals(fieldAttrs[1].ConstructorArguments[0].Value, typeof(GameInput)), "serialized GameInput and HashEnum order");
            Menu(bridge, "HLInputBridge", "Hardlight/UI/HLInputBridge", false); Menu(supplier, null, "Hardlight/HLInput/HLInputSupplier", true);
            var actual = bridge.GetMethods(Declared).OrderBy(x => x.MetadataToken).ToArray(); Contract[] expected = Contracts(); Require(actual.Length == 21 && expected.Length == 21, "all bridge method declarations"); int parameterCount = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                Contract c = expected[i]; MethodInfo method = Exact(bridge, c.Name, c.Result, c.Flags, c.Args); Require(Same(actual[i], method), "ordered bridge method " + i);
                ParameterInfo[] parameters = method.GetParameters(); string[] names = c.Parameters.Length == 0 ? new string[0] : c.Parameters.Split(','); Require(parameters.Length == names.Length, "parameter count"); parameterCount += parameters.Length;
                for (int j = 0; j < parameters.Length; j++)
                {
                    ParameterInfo p = parameters[j]; bool optional = i == 7 || i >= 13 && j == 2;
                    Require(p.Position == j && p.Name == names[j] && p.ParameterType == c.Args[j] && (int)p.Attributes == (optional ? 4112 : 0), "exact parameter name/type/flags");
                    // Reflection can expose the Optional flag as a pseudo attribute. Original metadata has no parameter custom-attribute rows.
                    var attributes = p.GetCustomAttributesData(); Require(attributes.Count <= (optional ? 1 : 0) && attributes.All(a => a.AttributeType == typeof(System.Runtime.InteropServices.OptionalAttribute) && a.ConstructorArguments.Count == 0 && a.NamedArguments.Count == 0), "no invented parameter custom attribute");
                    Require(p.HasDefaultValue == optional, "exact parameter default presence"); if (optional) Require(Equals(p.DefaultValue, i == 7 ? null : (object)(-1)), "exact null or -1 default");
                }
                if (c.Flags == 198) { MethodInfo basis = method.GetBaseDefinition(); Require(basis.DeclaringType == typeof(InputBridge) && basis.Name == c.Name && basis.ReturnType == c.Result && basis.GetParameters().Select(x => x.ParameterType).SequenceEqual(c.Args), "genuine abstract override slot"); }
                Body(method);
            }
            Require(parameterCount == 32, "all thirty-two bridge parameters");
            PropertyInfo property = supplier.GetProperty("Input", Declared); MethodInfo getter = Exact(supplier, "get_Input", typeof(GameInput), 2182, Type.EmptyTypes);
            Require(property != null && property.Name == "Input" && property.Attributes == 0 && property.PropertyType == typeof(GameInput) && Same(property.GetGetMethod(), getter) && property.GetSetMethod(true) == null && property.GetCustomAttributesData().Count == 0 && supplier.GetMethods(Declared).Length == 1, "sole linked getter");
            Straight(getter, "ldarg.0 ldfld ret", new MemberInfo[] { SupplierField() });
            foreach (Type type in new[] { bridge, supplier })
            {
                ConstructorInfo[] ctors = type.GetConstructors(Declared); Require(ctors.Length == 1 && ctors[0].IsPublic && (int)ctors[0].Attributes == 6278 && ctors[0].GetParameters().Length == 0 && ctors[0].GetCustomAttributesData().Count == 0 && ctors[0].GetMethodImplementationFlags() == 0, "public base-only constructor");
                Straight(ctors[0], "ldarg.0 call ret", new MemberInfo[] { type.BaseType.GetConstructor(Declared, null, Type.EmptyTypes, null) });
            }
        }
        static void Menu(Type type, string file, string menu, bool options)
        {
            var attrs = type.GetCustomAttributesData(); Require(attrs.Count == (options ? 3 : 1), "complete class attributes"); int offset = 0;
            if (options) for (int i = 0; i < 2; i++)
            {
                CustomAttributeData a = attrs[i]; Require(a.AttributeType == typeof(Il2CppSetOptionAttribute) && a.ConstructorArguments.Count == 2 && Convert.ToInt32(AttributeValue(a.ConstructorArguments[0])) == (i == 0 ? 2 : 1) && Equals(AttributeValue(a.ConstructorArguments[1]), false) && a.NamedArguments.Count == 0, "original IL2CPP options order"); offset++;
            }
            CustomAttributeData m = attrs[offset]; Require(m.AttributeType == typeof(CreateAssetMenuAttribute) && m.ConstructorArguments.Count == 0 && m.NamedArguments.Count == (file == null ? 1 : 2), "menu declaration");
            int n = 0; if (file != null) { Require(m.NamedArguments[0].MemberName == "fileName" && Equals(m.NamedArguments[0].TypedValue.Value, file), "menu file name"); n++; }
            Require(m.NamedArguments[n].MemberName == "menuName" && Equals(m.NamedArguments[n].TypedValue.Value, menu), "menu path");
        }
        static object AttributeValue(CustomAttributeTypedArgument argument)
        {
            object value = argument.Value; while (value is CustomAttributeTypedArgument nested) value = nested.Value; return value;
        }

        sealed class Instruction { public int Offset, End; public OpCode Code; public MemberInfo Member; public int Integer; public int? Target; }
        // Decode the complete loaded body. Every token is resolved in its physical declaring Module.
        // The complex guards below are prospective structure guards; root must separately join full emitted physical records.
        static List<Instruction> Decode(MethodBase method)
        {
            MethodBody body = method.GetMethodBody(); Require(body != null && body.ExceptionHandlingClauses.Count == 0, "whole managed body without EH"); byte[] bytes = body.GetILAsByteArray(); Require(bytes != null && bytes.Length > 0, "physical IL bytes");
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(x => x.FieldType == typeof(OpCode)).Select(x => (OpCode)x.GetValue(null)).ToDictionary(x => unchecked((ushort)x.Value));
            var result = new List<Instruction>(); int at = 0;
            while (at < bytes.Length)
            {
                var instruction = new Instruction { Offset = at }; ushort value = bytes[at++]; if (value == 0xfe) { Require(at < bytes.Length, "complete two-byte opcode"); value = (ushort)(0xfe00 | bytes[at++]); }
                Require(codes.ContainsKey(value), "known opcode"); instruction.Code = codes[value]; int size;
                switch (instruction.Code.OperandType)
                {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineI: case OperandType.ShortInlineVar: case OperandType.ShortInlineBrTarget: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI: case OperandType.InlineBrTarget: case OperandType.InlineField: case OperandType.InlineMethod: case OperandType.InlineType: case OperandType.InlineTok: size = 4; break;
                    default: throw new InvalidOperationException("Unexpected UI body operand " + instruction.Code.OperandType);
                }
                Require(at + size <= bytes.Length, "complete operand");
                if (size == 4) instruction.Integer = BitConverter.ToInt32(bytes, at); else if (size == 1) instruction.Integer = (sbyte)bytes[at]; else if (size == 2) instruction.Integer = BitConverter.ToUInt16(bytes, at);
                if (instruction.Code.OperandType == OperandType.InlineField || instruction.Code.OperandType == OperandType.InlineMethod || instruction.Code.OperandType == OperandType.InlineType || instruction.Code.OperandType == OperandType.InlineTok) instruction.Member = method.Module.ResolveMember(instruction.Integer);
                at += size; instruction.End = at;
                if (instruction.Code.OperandType == OperandType.InlineBrTarget || instruction.Code.OperandType == OperandType.ShortInlineBrTarget) instruction.Target = at + instruction.Integer;
                Require(instruction.Code != OpCodes.Nop, "no guessed debug NOP relaxation"); result.Add(instruction);
            }
            Require(result.Last().Code == OpCodes.Ret, "complete return boundary");
            foreach (Instruction instruction in result.Where(x => x.Target.HasValue)) Require(result.Any(x => x.Offset == instruction.Target.Value), "branch to actual instruction boundary");
            return result;
        }
        static void Straight(MethodBase method, string opcodes, MemberInfo[] members)
        {
            List<Instruction> body = Decode(method); Require(string.Join(" ", body.Select(x => x.Code.Name)) == opcodes && method.GetMethodBody().LocalVariables.Count == 0, "complete straight body " + method.Name);
            MemberInfo[] operands = body.Where(x => x.Member != null).Select(x => x.Member).ToArray(); Require(operands.Length == members.Length, "complete straight operands"); for (int i = 0; i < operands.Length; i++) Require(Same(operands[i], members[i]), "exact scoped straight operand");
        }
        static MethodInfo Provider(Type owner, string name, params Type[] args) { MethodInfo method = owner.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance, null, args, null); Require(method != null && method.DeclaringType == owner, "genuine provider " + name); return method; }
        static MethodInfo InvokeExtension(bool typed)
        {
            MethodInfo method = typeof(FastActionExtensions).GetMethods(Declared).Single(x => x.Name == "Invoke" && (typed ? x.IsGenericMethodDefinition && x.GetGenericArguments().Length == 1 : !x.IsGenericMethod)); return typed ? method.MakeGenericMethod(typeof(UIInputType)) : method;
        }
        static void Calls(List<Instruction> body, params MethodBase[] expected)
        {
            MethodBase[] actual = body.Where(x => x.Member is MethodBase).Select(x => (MethodBase)x.Member).ToArray(); Require(actual.Length == expected.Length, "complete call/ldftn operand count");
            for (int i = 0; i < actual.Length; i++)
            {
                Require(Same(actual[i], expected[i]), "exact ordered provider/callback operand " + i);
                if (actual[i] is MethodInfo a && expected[i] is MethodInfo e) Require(a.ReturnType == e.ReturnType && a.GetParameters().Select(x => x.ParameterType).SequenceEqual(e.GetParameters().Select(x => x.ParameterType)) && a.GetGenericArguments().SequenceEqual(e.GetGenericArguments()), "closed provider signature");
            }
        }
        static void FieldOperands(List<Instruction> body, params FieldInfo[] expected)
        {
            FieldInfo[] actual = body.Where(x => x.Member is FieldInfo).Select(x => (FieldInfo)x.Member).ToArray(); Require(actual.Length == expected.Length, "complete field operand count");
            for (int i = 0; i < actual.Length; i++) Require(Same(actual[i], expected[i]) && actual[i].FieldType == expected[i].FieldType, "exact ordered field operand " + i);
        }
        static void Body(MethodInfo method)
        {
            string name = method.Name; FieldInfo module = ModuleField(); Type[] args = method.GetParameters().Select(x => x.ParameterType).ToArray();
            if (name == "SetInputModule") { Straight(method, "ldarg.0 ldarg.1 isinst stfld ret", new MemberInfo[] { typeof(HLInputModule), module }); return; }
            if (name == "InputModuleExists") { Straight(method, "ldarg.0 ldfld ldnull call ret", new MemberInfo[] { module, Provider(typeof(UnityEngine.Object), "op_Inequality", typeof(UnityEngine.Object), typeof(UnityEngine.Object)) }); return; }
            if (name == "SetHardwareCursorVisibility" || name == "GetHardwareCursorVisibility" || name == "IsObjectSelected" || name == "SetSelectedGameObjectWithMemory" || name == "DeselectObjectIfCurrentlySelected")
            {
                bool deselect = name == "DeselectObjectIfCurrentlySelected"; Type[] providerArgs = deselect ? new[] { typeof(GameObject), typeof(BaseEventData) } : args;
                string loads = args.Length == 0 ? "" : args.Length == 1 ? " ldarg.1" : " ldarg.1 ldarg.2";
                Straight(method, "ldarg.0 ldfld" + loads + (deselect ? " ldnull" : "") + " callvirt ret", new MemberInfo[] { module, Provider(typeof(HLInputModule), name, providerArgs) }); return;
            }
            if (name.Contains("InputListener"))
            {
                bool register = name.StartsWith("Register", StringComparison.Ordinal), scalar = args[1] == typeof(Action<float>); int? trigger = name.EndsWith("OnDown", StringComparison.Ordinal) ? (int?)1 : name.EndsWith("OnHeld", StringComparison.Ordinal) ? (int?)0 : name.EndsWith("OnUp", StringComparison.Ordinal) ? (int?)2 : null;
                Type[] providerArgs = register && scalar ? new[] { typeof(GameInput), args[1], typeof(InputTrigger), typeof(int) } : new[] { typeof(GameInput), args[1], typeof(int) };
                string push = trigger.HasValue ? " ldc.i4." + trigger.Value : "";
                Straight(method, "ldarg.1 isinst callvirt ldarg.2" + push + " ldarg.3 call ret", new MemberInfo[] { typeof(HLInputSupplier), typeof(HLInputSupplier).GetProperty("Input").GetGetMethod(), Provider(typeof(ControlMapping), register ? "Subscribe" : "Unsubscribe", providerArgs) }); return;
            }
            List<Instruction> body = Decode(method);
            string[] permitted = "ldarg.0 ldarg.1 ldfld ldsfld stsfld ldnull ldftn newobj call callvirt ldtoken box stloc.0 ldloc.0 ldc.i4 ldc.i4.0 brtrue.s brfalse.s br.s brtrue brfalse br ret".Split(' ');
            Require(body.All(x => permitted.Contains(x.Code.Name)), "entire complex body opcode vocabulary");
            Require(!body.Any(x => x.Code == OpCodes.Stfld || x.Code == OpCodes.Stobj || x.Code == OpCodes.Calli), "no invented state write or indirect call");
            FieldInfo latch = LatchField(); MethodInfo defined = Provider(typeof(Enum), "IsDefined", typeof(Type), typeof(object)), handle = Provider(typeof(Type), "GetTypeFromHandle", typeof(RuntimeTypeHandle));
            if (name == "DoesInputTypeRequireSelection")
            {
                Calls(body, handle, defined, Provider(typeof(HLInputModule), name, typeof(InputType)));
                FieldOperands(body, module); Require(method.GetMethodBody().LocalVariables.All(x => x.LocalType == typeof(InputType) && !x.IsPinned), "input enum local only");
                Require(body.Single(x => x.Code == OpCodes.Ldtoken).Member == typeof(InputType) && body.Single(x => x.Code == OpCodes.Box).Member == typeof(InputType), "exact validated enum type");
                int gate = body.FindIndex(x => Same(x.Member, defined)), read = body.FindIndex(x => Same(x.Member, module)); Require(gate < read && body.Any(x => x.Target.HasValue && x.Offset > body[gate].Offset && x.Offset < body[read].Offset), "validation branch before module read");
            }
            else if (name == "GetLastInputType")
            {
                MethodInfo last = typeof(ControlMapping).GetProperty("LastInputType").GetGetMethod(); Calls(body, handle, last, defined, last);
                FieldOperands(body); Require(method.GetMethodBody().LocalVariables.Count == 0, "last input no invented snapshot local");
                Require(body.Single(x => x.Code == OpCodes.Ldtoken).Member == typeof(UIInputType) && body.Single(x => x.Code == OpCodes.Box).Member == typeof(UIInputType) && body.Count(x => x.Code == OpCodes.Ldc_I4 && x.Integer == 1208867107) == 1, "two fresh reads and exact unsupported literal");
            }
            else if (name == "OnUpdateLastInputTypeBridge")
            {
                Calls(body, handle, defined, InvokeExtension(true)); FieldInfo action = Field(typeof(InputBridge), "OnUpdateLastInputType", typeof(FastAction<UIInputType>), FieldAttributes.Public | FieldAttributes.Static);
                FieldOperands(body, action); Require(method.GetMethodBody().LocalVariables.All(x => x.LocalType == typeof(UIInputType) && !x.IsPinned), "UI enum local only");
                Require(body.Single(x => x.Code == OpCodes.Ldtoken).Member == typeof(UIInputType) && body.Single(x => x.Code == OpCodes.Box).Member == typeof(UIInputType) && body.Count(x => x.Code == OpCodes.Ldc_I4 && x.Integer == 1208867107) == 1 && body.FindIndex(x => Same(x.Member, defined)) < body.FindIndex(x => Same(x.Member, action)), "validate then fresh action load");
            }
            else if (name == "RegisterForInputUpdateEvents" || name == "UnregisterForInputUpdateEvents")
            {
                bool add = name == "RegisterForInputUpdateEvents"; MethodInfo eventMethod = typeof(ControlMapping).GetEvent("OnUpdateLastInputType").GetAddMethod(); if (!add) eventMethod = typeof(ControlMapping).GetEvent("OnUpdateLastInputType").GetRemoveMethod();
                ConstructorInfo typedCtor = typeof(Action<InputType>).GetConstructor(new[] { typeof(object), typeof(IntPtr) });
                if (add)
                {
                    Type actionBase = typeof(FastActionBase<FastAction, Action>); Calls(body, Callback(), typedCtor, eventMethod, Shutdown(), typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) }), Provider(actionBase, "op_Addition", actionBase, typeof(Action)));
                }
                else Calls(body, Callback(), typedCtor, eventMethod);
                FieldInfo moduleAction = Field(typeof(HLInputModule), "OnShutdown", typeof(FastAction), FieldAttributes.Public | FieldAttributes.Static);
                if (add) FieldOperands(body, latch, moduleAction, moduleAction); else FieldOperands(body, latch);
                Require(method.GetMethodBody().LocalVariables.Count == 0, "registration no invented locals");
                Require(Same(body.First(x => x.Member is FieldInfo).Member, latch) && body.Any(x => x.Target.HasValue && x.Offset < body.First(x => x.Member is MethodBase).Offset), "latch gates all registrations");
                Require(body.Count(x => x.Code == OpCodes.Stsfld) == (add ? 1 : 0), "only original shutdown publication");
            }
            else if (name == "InternalOnShutdown")
            {
                Type actionBase = typeof(FastActionBase<FastAction, Action>); Calls(body, Shutdown(), typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) }), Provider(actionBase, "op_Subtraction", actionBase, typeof(Action)), InvokeExtension(false));
                FieldInfo moduleAction = Field(typeof(HLInputModule), "OnShutdown", typeof(FastAction), FieldAttributes.Public | FieldAttributes.Static), notify = Field(typeof(InputBridge), "OnShutdown", typeof(FastAction), FieldAttributes.Public | FieldAttributes.Static);
                FieldOperands(body, moduleAction, moduleAction, notify, notify); Require(method.GetMethodBody().LocalVariables.Count == 0, "shutdown no invented snapshot local");
                Require(body.Count(x => x.Code == OpCodes.Stsfld) == 1 && Same(body.Single(x => x.Code == OpCodes.Stsfld).Member, moduleAction) && body.Single(x => x.Code == OpCodes.Stsfld).Offset < body.First(x => Same(x.Member, notify)).Offset && body.Count(x => x.Code == OpCodes.Ldsfld && Same(x.Member, notify)) == 2, "publish removal before separate notification reads");
            }
            else throw new InvalidOperationException("Unaccounted whole bridge method " + name);
        }
    }
}
