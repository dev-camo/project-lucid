using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid
{
    public static class FastActionDispatchVerification
    {
        private static int checks;
        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }
        private static object BaseField(object value, string name) => value.GetType().BaseType
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value);

        // These checks execute maintained original callback classes only. They do
        // not start the logging host, native plugin, authored startup or content.
        public static void Run() => Console.WriteLine("PASS FastAction dispatch checks=" + RunManaged());

        public static int RunManaged()
        {
            checks = 0;
            CheckMetadata();
            CheckTwoArguments();
            CheckThreeArguments();
            CheckExtensions();
            return checks;
        }

        private static void CheckMetadata()
        {
            foreach (Type type in new[] { typeof(FastAction<,>), typeof(FastAction<,,>), typeof(FastActionExtensions) })
            {
                Check(type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original Core assembly identity");
                Check(type.Namespace == "Hardlight" && type.IsPublic, "original public namespace");
                Check(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0, "no added original fields");
                IList<CustomAttributeData> attrs = type.GetCustomAttributesData();
                CustomAttributeData[] options = attrs.Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute)).ToArray();
                Check(options.Length == 2, "exact original class options count");
                Option[] expected = type == typeof(FastActionExtensions)
                    ? new[] { Option.ArrayBoundsChecks, Option.NullChecks }
                    : new[] { Option.NullChecks, Option.ArrayBoundsChecks };
                for (int i = 0; i < 2; i++)
                {
                    Check(options[i].Constructor.DeclaringType.Assembly.GetName().Name == "HLUnityCore.Runtime", "original option dependency assembly");
                    Check(options[i].ConstructorArguments.Count == 2 && options[i].NamedArguments.Count == 0, "exact option argument shape");
                    Check(options[i].ConstructorArguments[0].ArgumentType == typeof(Option) && (int)options[i].ConstructorArguments[0].Value == (int)expected[i], "original option order/value");
                    CustomAttributeTypedArgument flag = options[i].ConstructorArguments[1];
                    Check(flag.ArgumentType == typeof(bool) && !(bool)flag.Value, "original boxed false option");
                }
            }
            Check(typeof(FastAction<,>).BaseType.GetGenericTypeDefinition() == typeof(FastActionBase<,>), "original arity2 base");
            Check(typeof(FastAction<,,>).BaseType.GetGenericTypeDefinition() == typeof(FastActionBase<,>), "original arity3 base");
            Check(typeof(FastAction<,>).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Single().Name == "Invoke", "complete arity2 own method surface");
            Check(typeof(FastAction<,,>).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Single().Name == "Invoke", "complete arity3 own method surface");
            Check(typeof(FastActionExtensions).IsAbstract && typeof(FastActionExtensions).IsSealed, "original static extension type");
            MethodInfo[] methods = typeof(FastActionExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Check(methods.Length == 5, "all five original extension methods");
            Check(methods.All(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)), "all original methods are extensions");
            Type[] args = methods.Single(m => m.Name == "AddUnique").GetGenericArguments();
            Check((args[0].GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0, "original self constructor constraint");
            Check(args[1].GetGenericParameterConstraints().Single() == typeof(Delegate), "original delegate constraint");
        }

        private static void CheckTwoArguments()
        {
            FastAction<object, int> empty = new FastAction<object, int>();
            Check(empty.GetInvocationListCount() == 0, "original base constructor starts empty");
            Check(empty.GetInvocationList().Capacity == 1, "original base invocation capacity one");
            Check(!(bool)BaseField(empty, "m_invocationActive"), "original initial flag false");
            FastAction<object, int>.Invoke(null, null, -23);
            Check(true, "direct arity2 null receiver no-op");
            object marker = new object();
            List<string> trace = new List<string>();
            FastAction<object, int> action = new FastAction<object, int>();
            Action<object, int> later = (value, code) => { Check(ReferenceEquals(value, marker) && code == -23, "deferred callback arguments unchanged"); trace.Add("later"); };
            Action<object, int> second = (value, code) => { Check(ReferenceEquals(value, marker) && code == -23, "removed callback still receives arguments"); trace.Add("second"); };
            Action<object, int> first = (value, code) =>
            {
                Check(ReferenceEquals(value, marker) && code == -23, "first callback arguments unchanged");
                trace.Add("first"); action += later; action -= second;
            };
            action += first; action += second;
            action.Invoke(marker, -23);
            Check(string.Join(",", trace) == "first,second", "queued removals do not skip current callbacks");
            Check(action.GetInvocationListCount() == 2 && action.GetInvocationList()[1] == later, "add then remove flush");
            action -= first; trace.Clear(); action.Invoke(marker, -23);
            Check(string.Join(",", trace) == "later", "added callback appears on later dispatch");
            Check(!(bool)BaseField(action, "m_invocationActive"), "successful arity2 clears flag");

            FastAction<object, int> addAndRemove = new FastAction<object, int>();
            Action<object, int> transient = (o, i) => { throw new InvalidOperationException("should not run"); };
            addAndRemove += (o, i) => { addAndRemove += transient; addAndRemove -= transient; };
            addAndRemove.Invoke(null, 0);
            Check(addAndRemove.GetInvocationListCount() == 1, "queued addition precedes removal of same listener");

            FastAction<object, int> live = new FastAction<object, int>();
            trace.Clear(); live += (o, i) => { trace.Add("a"); live.GetInvocationList().Add((p, q) => trace.Add("c")); };
            live += (o, i) => trace.Add("b"); live.Invoke(null, 0);
            Check(string.Join(",", trace) == "a,b,c", "direct list edits use live count");

            FastAction<object, int> failed = new FastAction<object, int>();
            InvalidOperationException sentinel = new InvalidOperationException("sentinel"); bool throwNow = true;
            Action<object, int> queued = (o, i) => trace.Add("queued");
            Action<object, int> throwing = (o, i) => { failed += queued; if (throwNow) throw sentinel; };
            failed += throwing;
            Exception seen = null; try { failed.Invoke(null, 0); } catch (Exception ex) { seen = ex; }
            Check(ReferenceEquals(seen, sentinel), "exception identity propagates");
            Check((bool)BaseField(failed, "m_invocationActive"), "failure retains active flag");
            Check(failed.GetInvocationListCount() == 1, "failure does not flush queued addition");
            Action<object, int> afterFailure = (o, i) => trace.Add("after");
            failed += afterFailure; failed -= throwing;
            Check(failed.GetInvocationListCount() == 1, "postfailure edits remain queued");
            throwNow = false; failed.Invoke(null, 0);
            Check(failed.GetInvocationListCount() == 3, "next success flushes both queued duplicates then removes thrower");
            Check(!(bool)BaseField(failed, "m_invocationActive"), "next success clears retained flag");
        }

        private static void CheckThreeArguments()
        {
            FastAction<object, string, int>.Invoke(null, null, null, 0);
            Check(true, "direct arity3 null receiver no-op");
            object marker = new object(); string text = new string(new[] { 'a', 'b' });
            FastAction<object, string, int> action = new FastAction<object, string, int>();
            List<string> trace = new List<string>();
            action += (a, b, c) => { Check(ReferenceEquals(a, marker) && ReferenceEquals(b, text) && c == int.MinValue, "arity3 all arguments unchanged"); trace.Add("a"); };
            action += (a, b, c) => trace.Add("b");
            action.Invoke(marker, text, int.MinValue);
            Check(string.Join(",", trace) == "a,b", "arity3 callback order");
            Check(!(bool)BaseField(action, "m_invocationActive"), "arity3 successful flag clear");
            FastAction<float, bool, int> values = new FastAction<float, bool, int>();
            values += (a, b, c) => Check(float.IsNaN(a) && b && c == -7, "generic value types preserved");
            values.Invoke(float.NaN, true, -7);

            FastAction<object, string, int> failed = new FastAction<object, string, int>();
            Action<object, string, int> pending = (a, b, c) => { };
            Exception sentinel = new Exception("arity3 sentinel");
            failed += (a, b, c) => { failed += pending; throw sentinel; };
            Exception seen = null; try { failed.Invoke(null, null, 0); } catch (Exception ex) { seen = ex; }
            Check(ReferenceEquals(seen, sentinel), "arity3 propagates callback exception");
            Check((bool)BaseField(failed, "m_invocationActive") && failed.GetInvocationListCount() == 1, "arity3 leaves flag and addition queue on failure");

            FastAction<object, string, int> reentrant = new FastAction<object, string, int>();
            int depth = 0; trace.Clear();
            reentrant += (a, b, c) =>
            {
                trace.Add("a" + depth);
                if (depth != 0) return;
                depth = 1; reentrant.Invoke(a, b, c); depth = 0;
                reentrant += (p, q, r) => trace.Add("c" + depth);
            };
            reentrant += (a, b, c) => trace.Add("b" + depth);
            reentrant.Invoke(null, null, 0);
            Check(string.Join(",", trace) == "a0,a1,b1,b0,c0", "inner success clears shared flag before outer edit/live loop");
            Check(reentrant.GetInvocationListCount() == 3, "original nested dispatch has no depth guard");
        }

        private static void CheckExtensions()
        {
            ((FastAction)null).Invoke(); ((FastAction<int>)null).Invoke(0);
            ((FastAction<object, int>)null).Invoke(null, 0); ((FastAction<object, object, int>)null).Invoke(null, null, 0);
            Check(true, "all four null extension receivers forward safely");
            int count = 0; FastAction zero = null; zero += () => count++; zero.Invoke();
            Check(count == 1, "nongeneric extension forwards original call");
            FastAction<int> one = null; one += i => count += i; one.Invoke(4);
            Check(count == 5, "arity1 extension forwards original argument");
            FastAction<object, int> unique = null;
            Check(unique.AddUnique((Action<object, int>)null) == null, "unique null listener and receiver remain null");
            Action<object, int> callback = (a, b) => count++;
            unique = unique.AddUnique(callback);
            Check(unique != null && unique.GetInvocationListCount() == 1, "unique extension creates genuine original receiver");
            Check(ReferenceEquals(unique, unique.AddUnique(callback)) && unique.GetInvocationListCount() == 1, "unique duplicate returns same receiver");
            Check(ReferenceEquals(unique, unique.AddUnique((Action<object, int>)null)), "unique null listener preserves existing receiver");
            unique.Invoke(null, 0); Check(count == 6, "unique listener invoked once");
            FastAction<object, int> pending = new FastAction<object, int>();
            Action<object, int> added = (a, b) => { };
            pending += (a, b) => { pending = pending.AddUnique(added); pending = pending.AddUnique(added); };
            pending.Invoke(null, 0);
            Check(pending.GetInvocationListCount() == 2, "unique extension respects pending addition queue");
        }
    }
}
