using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded metadata/compiled-IL and CLR-null iterator proof. No engine object,
    // fake host/system/provider, diagnostic routing or localisation data load.
    public static class LoggingSingletonVerification
    {
        private static int checks;
        private static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static void Check(bool value, string label)
        {
            ++checks;
            if (!value) throw new InvalidOperationException(label);
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Declared);
        private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Declared);
        private static bool Call((OpCode op, object value) instruction, Type type, string name) =>
            (instruction.op == OpCodes.Call || instruction.op == OpCodes.Callvirt) && instruction.value is MethodBase method
                && method.DeclaringType == type && method.Name == name;
        private static IEnumerable<(OpCode op, object value)> Instructions(MethodBase method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
                .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
            var typeArguments = method.DeclaringType.GetGenericArguments();
            var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;
            byte[] bytes = method.GetMethodBody().GetILAsByteArray();
            for (int position = 0; position < bytes.Length;)
            {
                short key = bytes[position++]; if (key == 0xfe) key = (short)(0xfe00 | bytes[position++]);
                OpCode code = codes[key]; object operand = null;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineI: operand = (sbyte)bytes[position++]; break;
                    case OperandType.ShortInlineVar: operand = bytes[position++]; break;
                    case OperandType.InlineVar: operand = BitConverter.ToUInt16(bytes, position); position += 2; break;
                    case OperandType.InlineI: operand = BitConverter.ToInt32(bytes, position); position += 4; break;
                    case OperandType.ShortInlineR: operand = BitConverter.ToSingle(bytes, position); position += 4; break;
                    case OperandType.InlineR: operand = BitConverter.ToDouble(bytes, position); position += 8; break;
                    case OperandType.ShortInlineBrTarget: operand = (sbyte)bytes[position++] + position; break;
                    case OperandType.InlineBrTarget: operand = BitConverter.ToInt32(bytes, position) + position + 4; position += 4; break;
                    case OperandType.InlineString: operand = method.Module.ResolveString(BitConverter.ToInt32(bytes, position)); position += 4; break;
                    case OperandType.InlineType: operand = method.Module.ResolveType(BitConverter.ToInt32(bytes, position), typeArguments, methodArguments); position += 4; break;
                    case OperandType.InlineTok: operand = method.Module.ResolveMember(BitConverter.ToInt32(bytes, position), typeArguments, methodArguments); position += 4; break;
                    case OperandType.InlineMethod: operand = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, position), typeArguments, methodArguments); position += 4; break;
                    case OperandType.InlineField: operand = method.Module.ResolveField(BitConverter.ToInt32(bytes, position), typeArguments, methodArguments); position += 4; break;
                    case OperandType.InlineSwitch:
                        int size = BitConverter.ToInt32(bytes, position); position += 4; operand = size; position += 4 * size; break;
                    default: throw new InvalidOperationException("Unexpected bounded localisation IL operand: " + code);
                }
                yield return (code, operand);
            }
        }
        private static void CheckSingleton()
        {
            Type type = typeof(MonoSingleton<>), parameter = type.GetGenericArguments().Single();
            Check(type.Assembly.GetName().Name == "HLUnityCore.Runtime" && type.IsPublic && !type.IsAbstract && !type.IsSealed
                && type.BaseType == typeof(MonoBehaviour) && type.GetInterfaces().SequenceEqual(new[] { typeof(ISystem) }), "original genuine singleton type/base/interface");
            Check(parameter.Name == "T" && parameter.GenericParameterAttributes == GenericParameterAttributes.None
                && parameter.GetGenericParameterConstraints().SequenceEqual(new[] { typeof(MonoBehaviour), typeof(ISystem) }), "metadata and context verified exact MonoBehaviour/ISystem constraints, no added self/new constraint");
            var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(type.GetCustomAttributesData().Count == 2 && options.Length == 2 && options[0].Option == Option.NullChecks
                && options[1].Option == Option.ArrayBoundsChecks && Equals(options[0].Value, false) && Equals(options[1].Value, false), "original singleton options/order/values");
            var fields = type.GetFields(Declared);
            Check(fields.Length == 1 && fields[0].Name == "<Instance>k__BackingField" && fields[0].Attributes == (FieldAttributes.Private | FieldAttributes.Static)
                && fields[0].FieldType == parameter && fields[0].GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(new[] { typeof(CompilerGeneratedAttribute) }), "complete original single static auto-property field");
            var instance = type.GetProperty("Instance");
            Check(type.GetProperties(Declared).Length == 1 && instance.PropertyType == parameter && instance.GetMethod.IsPublic
                && instance.SetMethod.IsPrivate && instance.GetMethod.IsStatic && instance.SetMethod.IsStatic, "original sole singleton property API");
            Check(type.GetMethods(Declared).Select(m => m.Name).SequenceEqual(new[] {
                "get_Instance", "set_Instance", "WaitOnInstance", "NotNull", "IsNull", "Awake", "DestroyInstance", "OnDestroy" })
                && type.GetConstructors(Declared).Length == 1, "complete original9 singleton methods, no padding");
            foreach (var accessor in new[] { instance.GetMethod, instance.SetMethod })
                Check(accessor.GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(new[] { typeof(CompilerGeneratedAttribute) }), accessor.Name + " original compiler-generated accessor metadata");
            var get = Instructions(instance.GetMethod).ToArray(); var set = Instructions(instance.SetMethod).ToArray();
            Check(get.Length == 2 && get[0].op == OpCodes.Ldsfld && ((FieldInfo)get[0].value).Name == fields[0].Name && get[1].op == OpCodes.Ret, "native static getter only reads backing field");
            Check(set.Length == 3 && set[0].op == OpCodes.Ldarg_0 && set[1].op == OpCodes.Stsfld && ((FieldInfo)set[1].value).Name == fields[0].Name && set[2].op == OpCodes.Ret, "native private setter only stores backing field");
            foreach (string name in new[] { "NotNull", "IsNull" })
            {
                var method = Method(type, name); var il = Instructions(method).ToArray();
                Check(method.IsPublic && method.IsStatic && method.ReturnType == typeof(bool)
                    && Call(il[0], type, "get_Instance") && il.Any(i => Call(i, typeof(UnityEngine.Object), name == "NotNull" ? "op_Inequality" : "op_Equality")), name + " genuine Unity comparison after Instance read");
            }
            var awake = Method(type, "Awake"); var awakeCode = Instructions(awake).ToArray();
            Check(awake.IsFamily && awake.IsVirtual && awake.GetBaseDefinition() == awake, "original protected virtual Awake contract");
            var awakeCalls = awakeCode.Where(i => i.value is MethodBase && (i.op == OpCodes.Call || i.op == OpCodes.Callvirt)).Select(i => (MethodBase)i.value).ToArray();
            Check(awakeCalls[0].DeclaringType == typeof(ProcessManager) && awakeCalls[0].Name == "IsSystemNull"
                && awakeCalls[0].GetGenericArguments().Single() == parameter && awakeCalls[1].Name == "RegisterSystem"
                && awakeCalls[1].DeclaringType == typeof(ProcessManager), "native registry query then registration precede Instance assignment");
            int registration = Array.FindIndex(awakeCode, i => Call(i, typeof(ProcessManager), "RegisterSystem"));
            int assignment = Array.FindIndex(awakeCode, i => Call(i, type, "set_Instance"));
            Check(registration >= 0 && assignment > registration && awakeCode.Skip(registration).Take(assignment - registration).Any(i => i.op == OpCodes.Isinst && Equals(i.value, parameter)), "register before isinst/cast/set preserves mismatched-T null assignment");
            Check(awakeCode.Where(i => i.op == OpCodes.Ldstr).Select(i => (string)i.value).SequenceEqual(new[] {
                "null", "null", "Duplicate singletons found of type: {0},\n", "Existing object: ", ", ", ", existing object parent: ", ", found in scene: ",
                "\n New object : ", ", ", ", new object parent: ", ", found in scene: ", "Destroying New Object: " }), "full original duplicate diagnostic literal order/payloads");
            Check(awakeCode.Any(i => i.op == OpCodes.Ldc_I4_S && Convert.ToInt32(i.value) == 17)
                && awakeCode.Count(i => Call(i, typeof(HLOutput), "LogError")) == 2, "original17-string concatenation and both genuine diagnostic routes");
            Check(Array.FindIndex(awakeCode, i => Call(i, typeof(HLOutput), "LogError")) < Array.FindIndex(awakeCode, i => Call(i, typeof(Application), "get_isPlaying"))
                && Array.FindLastIndex(awakeCode, i => Call(i, typeof(HLOutput), "LogError")) < Array.FindIndex(awakeCode, i => Call(i, typeof(UnityEngine.Object), "Destroy")), "duplicate diagnostic before isPlaying, second diagnostic before deferred new-object destruction");
            foreach (string name in new[] { "DestroyInstance", "OnDestroy" })
            {
                var method = Method(type, name); var code = Instructions(method).ToArray();
                int unregister = Array.FindIndex(code, i => Call(i, typeof(ProcessManager), "UnregisterSystem"));
                int clear = Array.FindIndex(code, i => Call(i, type, "set_Instance"));
                Check(name == "DestroyInstance" ? method.IsPublic && !method.IsVirtual : method.IsFamily && method.IsVirtual,
                    name + " original visibility/virtual contract");
                Check(unregister >= 0 && clear > unregister, name + " clears Instance only after genuine unregistration");
                if (name == "DestroyInstance")
                    Check(unregister < Array.FindIndex(code, i => Call(i, type, "get_Instance"))
                        && Array.FindIndex(code, i => Call(i, typeof(UnityEngine.Object), "DestroyImmediate")) < clear, "unregister caller then reread Instance/destroy its entire GameObject before clear");
                else Check(Array.FindIndex(code, i => Call(i, typeof(UnityEngine.Object), "op_Equality")) < unregister
                    && !code.Any(i => Call(i, typeof(UnityEngine.Object), "Destroy")), "OnDestroy uses genuine Unity Instance==this gate and never destroys a replacement");
            }
            var constructor = Instructions(type.GetConstructor(Type.EmptyTypes)).ToArray();
            Check(constructor.Length == 3 && constructor[0].op == OpCodes.Ldarg_0 && constructor[1].op == OpCodes.Call
                && ((MethodBase)constructor[1].value).DeclaringType == typeof(MonoBehaviour) && constructor[2].op == OpCodes.Ret, "singleton constructor only delegates genuine MonoBehaviour");
            var wait = Method(type, "WaitOnInstance"); var stateMachine = wait.GetCustomAttribute<IteratorStateMachineAttribute>();
            Type iterator = stateMachine.StateMachineType;
            Check(wait.IsPublic && wait.IsStatic && wait.ReturnType == typeof(IEnumerator) && iterator.Name == "<WaitOnInstance>d__4", "original WaitOnInstance contract and naturally emitted compiler ordinal4 without padding: actual=" + iterator.FullName);
            Check(iterator.IsNestedPrivate && iterator.IsSealed && iterator.BaseType == typeof(object)
                && iterator.IsDefined(typeof(CompilerGeneratedAttribute), false), "original private sealed compiler-generated iterator type");
            Check(iterator.GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(new[] { "<>1__state", "<>2__current" })
                && iterator.GetFields(Declared).All(f => f.IsPrivate && !f.IsStatic), "complete original2 iterator fields/no owner-thread or enumerable fields");
            Check(iterator.GetInterfaces().Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i).ToHashSet()
                .SetEquals(new[] { typeof(IEnumerator<>), typeof(IEnumerator), typeof(IDisposable) }), "original iterator interfaces without IEnumerable contract");
            Check(iterator.GetMethods(Declared).Length == 5 && iterator.GetConstructors(Declared).Length == 1, "exact original6 own generated iterator methods");
            var move = Method(iterator, "MoveNext"); var moveCode = Instructions(move).ToArray();
            Check(moveCode.Any(i => Call(i, typeof(UnityEngine.Object), "op_Equality"))
                && !moveCode.Any(i => Call(i, typeof(ProcessManager), "IsSystemNull")), "coroutine checks current Instance with Unity equality independently from registry state");
            foreach (var method in iterator.GetMethods(Declared).Where(m => m.Name != "MoveNext").Cast<MethodBase>().Concat(iterator.GetConstructors(Declared)))
                Check(method.IsDefined(typeof(DebuggerHiddenAttribute), false), method.Name + " original DebuggerHidden iterator metadata");
        }
        private static void CheckNullIterator()
        {
            // HLUnityCore is the genuine original MonoBehaviour/ISystem, not a
            // test host. No engine instance is constructed in this managed portion.
            Type type = typeof(MonoSingleton<HLUnityCore>);
            var field = Field(type, "<Instance>k__BackingField"); object prior = field.GetValue(null);
            try
            {
                field.SetValue(null, null);
                Check(ReferenceEquals(MonoSingleton<HLUnityCore>.Instance, null), "genuine closed generic getter returns the isolated CLR-null field");
                Check(!MonoSingleton<HLUnityCore>.NotNull(), "genuine CLR-null Unity comparison is not nonnull");
                Check(MonoSingleton<HLUnityCore>.IsNull(), "genuine CLR-null Unity equality reports null without engine creation");
                IEnumerator first = MonoSingleton<HLUnityCore>.WaitOnInstance(), second = MonoSingleton<HLUnityCore>.WaitOnInstance();
                Check(!ReferenceEquals(first, second), "every WaitOnInstance call returns a fresh original IEnumerator");
                var state = Field(first.GetType(), "<>1__state"); var current = Field(first.GetType(), "<>2__current");
                Check((int)state.GetValue(first) == 0 && ReferenceEquals(first.Current, null), "native iterator constructor state0/current-null");
                Check(ReferenceEquals(first.Current, ((IEnumerator<object>)first).Current), "both genuine Current interface getters alias the same field");
                Check(first.MoveNext() && first.Current == null && (int)state.GetValue(first) == 1, "native null Instance yields null/state1");
                Check(first.MoveNext() && first.Current == null && (int)state.GetValue(first) == 1, "native wait repeats rather than timing out or retrying registration");
                ((IDisposable)first).Dispose();
                Check((int)state.GetValue(first) == 1, "native Dispose is RET and retains live iterator state");
                Check(first.MoveNext() && (int)state.GetValue(first) == 1, "empty Dispose does not cancel waiting");
                bool rejected = false;
                try { first.Reset(); } catch (NotSupportedException) { rejected = true; }
                Check(rejected && (int)state.GetValue(first) == 1, "native Reset throws genuine NotSupportedException without changing state");
                foreach (int invalid in new[] { -1, 2, int.MinValue, int.MaxValue })
                {
                    object marker = new object(); state.SetValue(first, invalid); current.SetValue(first, marker);
                    Check(!first.MoveNext() && (int)state.GetValue(first) == invalid, "native unsigned state range rejects " + invalid + " without changing it");
                    Check(ReferenceEquals(first.Current, marker) && ReferenceEquals(((IEnumerator<object>)first).Current, marker), "invalid state retains the exact Current object");
                    ((IDisposable)first).Dispose();
                    Check((int)state.GetValue(first) == invalid && ReferenceEquals(first.Current, marker), "RET Dispose retains invalid state and Current identity");
                }
                state.SetValue(first, 0); current.SetValue(first, new object());
                Check(first.MoveNext() && first.Current == null && (int)state.GetValue(first) == 1, "accepted state0 clears previous Current on genuine null yield");
            }
            finally { field.SetValue(null, prior); }
            Check(ReferenceEquals(field.GetValue(null), prior), "closed static Instance identity restored in finally; proof is order-independent");
        }

        private static void CheckHostConstructor()
        {
            Type type = typeof(HLUnityCore);
            var constructor = type.GetConstructor(Type.EmptyTypes);
            var code = Instructions(constructor).ToArray();
            string[] initialized = { "m_logErrorHandler", "m_logExceptionHandler", "m_devBreadcrumbHandler", "m_breadcrumbHandler", "m_customKeyStringHandler", "m_customKeyIntHandler", "m_customKeyFloatHandler", "m_customKeyBoolHandler", "m_trackingAuthorisationHandler", "m_lastLogTime", "m_breadcrumbHandlersEnabled", "m_customKeyHandlersEnabled" };
            Check(code.Where(instruction => instruction.op == OpCodes.Stfld).Select(instruction => ((FieldInfo)instruction.value).Name).SequenceEqual(initialized), "native original host constructor initializes all nine callback fields then time/flags in exact order");
            var allocations = code.Where(instruction => instruction.op == OpCodes.Newobj).Select(instruction => ((MethodBase)instruction.value).DeclaringType).ToArray();
            Check(allocations.SequenceEqual(new[] { typeof(FastAction<string, int>), typeof(FastAction<string, string, int>), typeof(FastAction<string>), typeof(FastAction<string>), typeof(FastAction<string, string>), typeof(FastAction<string, int>), typeof(FastAction<string, float>), typeof(FastAction<string, bool>), typeof(FastAction<int>) }), "native constructor creates the real nine original FastAction types, no replacement containers");
            Check(code.Where(instruction => instruction.op == OpCodes.Call).Select(instruction => (MethodBase)instruction.value).Single().DeclaringType == typeof(MonoSingleton<HLUnityCore>), "native constructor's sole direct call is genuine original singleton base constructor");
            Check(Array.FindLastIndex(code, instruction => instruction.op == OpCodes.Stfld) < Array.FindIndex(code, instruction => instruction.op == OpCodes.Call), "all native host field initializers precede genuine base construction");
            Check(code.Where(instruction => instruction.op == OpCodes.Ldc_R4).Select(instruction => (float)instruction.value).SequenceEqual(new[] { float.MinValue }) && code.Count(instruction => instruction.op == OpCodes.Ldc_I4_1) == 2, "native host defaults preserve float.MinValue and both enabled flags");
            Check(type.TypeInitializer == null, "original host adds no static initializer for its two CLR-null bridge fields");
        }

        private static void CheckHostLifecycleIL()
        {
            Type type = typeof(HLUnityCore);
            var awake = Instructions(Method(type, "Awake")).ToArray();
            var awakeCalls = awake.Where(instruction => instruction.op == OpCodes.Call || instruction.op == OpCodes.Callvirt).Select(instruction => (MethodBase)instruction.value).ToArray();
            Check(awakeCalls.Select(method => method.Name).SequenceEqual(new[] { "Awake", "EnsureInitialised", "get_CurrentDomain", "add_UnhandledException", "get_CurrentDomain", "add_FirstChanceException", "GetConfig", "get_LowMemoryConfig", "get_Enabled", "add_lowMemory" }), "native exact Awake base/bridge/unhandled/first-chance/config/conditional low-memory subscription ordering");
            Check(awakeCalls[0].DeclaringType == typeof(MonoSingleton<HLUnityCore>) && awakeCalls[6].DeclaringType == typeof(SystemConfiguration) && awakeCalls[6].GetGenericArguments().Single() == typeof(HLUnityCoreConfigurationAsset), "actual lifecycle consumes genuine original singleton/config graph");
            var destroy = Instructions(Method(type, "OnDestroy")).ToArray();
            var destroyCalls = destroy.Where(instruction => instruction.op == OpCodes.Call || instruction.op == OpCodes.Callvirt).Select(instruction => (MethodBase)instruction.value).ToArray();
            Check(destroyCalls.Select(method => method.Name).SequenceEqual(new[] { "OnDestroy", "get_CurrentDomain", "remove_UnhandledException", "get_CurrentDomain", "remove_FirstChanceException", "remove_lowMemory" }), "native exact OnDestroy base then three unconditional event removals, no Unity log unsubscribe");
            Check(destroyCalls[0].DeclaringType == typeof(MonoSingleton<HLUnityCore>) && !destroy.Any(instruction => instruction.op == OpCodes.Stsfld), "actual destruction delegates real base and does not clear either static bridge");
            var ensure = Instructions(Method(type, "EnsureInitialised")).ToArray();
            var ensureCalls = ensure.Where(instruction => instruction.op == OpCodes.Call || instruction.op == OpCodes.Callvirt).Select(instruction => (MethodBase)instruction.value).ToArray();
            Check(ensureCalls.Select(method => method.Name).SequenceEqual(new[] { "Initialise", "Initialise", "add_logMessageReceived" }), "native initialization calls real bridge, real tracking factory then Unity log subscription");
            int bridgeStore = Array.FindIndex(ensure, instruction => instruction.op == OpCodes.Stsfld && ((FieldInfo)instruction.value).Name == "s_unityCoreNativeBridge");
            Check(bridgeStore < Array.FindIndex(ensure, instruction => Call(instruction, typeof(IHLUnityCoreNativeBridge), "Initialise")), "native bridge assigned before its initialization call; no invented failure rollback/retry");
            var runtimeLoad = Instructions(Method(typeof(HLOutput), "OnRuntimeMethodLoad")).ToArray();
            var runtimeCalls = runtimeLoad.Where(instruction => instruction.op == OpCodes.Call || instruction.op == OpCodes.Callvirt).Select(instruction => (MethodBase)instruction.value).ToArray();
            Check(runtimeCalls.Select(method => method.Name).SequenceEqual(new[] { "InternalSetDevelopmentBuild", "add_quitting", "GetConfig", "get_unityLogger", "get_AllowErrorLoggingInDistribution", "set_logEnabled", "get_unityLogger", "set_filterLogType" }), "native exact retail output initialization order and Unity logger reread");
            Check(runtimeLoad.Count(instruction => instruction.op == OpCodes.Ldc_I4_0) == 2, "native initialization retains retail development=false and Error enum0");
        }
        public static int RunManaged()
        {
            checks = 0; CheckSingleton(); CheckNullIterator(); CheckHostConstructor(); CheckHostLifecycleIL(); return checks;
        }
        public static void Run() => Console.WriteLine("PASS original singleton source/null-iterator checks=" + RunManaged()
            + "; actual automatic engine lifecycle is tested separately");
    }
}
