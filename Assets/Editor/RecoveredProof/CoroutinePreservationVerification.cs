using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight.Utils;
using UnityEngine;

namespace ProjectLucid.Verification
{
    internal static class CoroutinePreservationVerification
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static void Check(ref int count, bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            count++;
        }
        private static FieldInfo Field(IEnumerator value, string name) => value.GetType().GetField(name, All);
        private static int State(IEnumerator value) => (int)Field(value, "<>1__state").GetValue(value);
        private static IEnumerator Private(string name, Type[] types, params object[] args)
        {
            // The genuine receiver supplies only the original private iterator factory;
            // no MonoBehaviour constructor, engine object, StartCoroutine or time getter
            // is executed. Original generated iterators capture no receiver.
            object receiver = FormatterServices.GetUninitializedObject(typeof(CoroutineUtils));
            return (IEnumerator)typeof(CoroutineUtils).GetMethod(name, All, null, types, null).Invoke(receiver, args);
        }
        private static IEnumerator ActionFactory(string name, Action action) => Private(name, new[] { typeof(Action) }, action);
        private static IEnumerator PredicateFactory(string name, Action action, Func<bool> predicate) => Private(name, new[] { typeof(Action), typeof(Func<bool>) }, action, predicate);
        private static IEnumerator IntervalFactory(string name, Action action, float interval) => Private(name, new[] { typeof(Action), typeof(float) }, action, interval);
        private static IEnumerator PredicateIntervalFactory(Action action, Func<bool> predicate, float interval) => Private("PredicateIntervalCoroutine", new[] { typeof(Action), typeof(Func<bool>), typeof(float) }, action, predicate, interval);
        private static IEnumerator ActiveIntervalFactory(Func<bool> start, Action action, Func<bool> predicate, float interval) => Private("PredicateActiveIntervalCoroutine", new[] { typeof(Func<bool>), typeof(Action), typeof(Func<bool>), typeof(float) }, start, action, predicate, interval);
        private static void Throws<T>(ref int count, Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { count++; return; }
            throw new InvalidOperationException(message);
        }
        private static float Seconds(object wait) => (float)typeof(WaitForSeconds).GetField("m_Seconds", All).GetValue(wait);
        private static int FloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

        internal static int OriginalDeclarations()
        {
            int count = 0;
            Type t = typeof(CoroutineUtils);
            Check(ref count, t.BaseType == typeof(MonoBehaviour) && !t.IsSealed, "Original owner/base");
            Check(ref count, t.GetInterfaces().Length == 1 && t.GetInterfaces()[0].FullName == "Hardlight.ISystem", "Original system contract");
            FieldInfo[] fields = t.GetFields(All | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Check(ref count, fields.Length == 1 && fields[0].Name == "s_instance" && fields[0].IsPrivate && fields[0].IsStatic, "Original sole field");
            MethodInfo[] methods = t.GetMethods(All | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Check(ref count, methods.Length == 36, "Original direct method count excluding ctor");
            string[] names = { "Awake", "OnDestroy", "RunCoroutine", "OnFrameEnd", "EndOfFrameCoroutine", "OnNextFrame", "NextFrameCoroutine", "WaitForUI", "WaitNumberOfFrames", "WaitNumberOfFramesCoroutine", "Delay", "DelayCoroutine", "Delay", "DelayCoroutine", "OnFixedUpdate", "FixedUpdateCoroutine", "OnPredicateActive", "StopUtilCoroutine", "PredicateActiveCoroutine", "Predicate", "PredicateCoroutine", "Update", "UpdateCoroutine", "Interval", "IntervalCoroutine", "PredicateInterval", "PredicateIntervalCoroutine", "PredicateActiveInterval", "PredicateActiveIntervalCoroutine", "WaitForRealSeconds", "WaitForSeconds", "WaitForSecondsUnscaled", "get_Instance", "WaitOnInstance", "NotNull", "IsNull" };
            Array.Sort(methods, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            for (int i = 0; i < names.Length; i++) Check(ref count, methods[i].Name == names[i], "Original member order " + i);
            MethodInfo stop = t.GetMethod("StopUtilCoroutine", BindingFlags.Public | BindingFlags.Static);
            Check(ref count, stop.GetParameters()[0].ParameterType == typeof(Coroutine).MakeByRefType() && !stop.GetParameters()[0].IsOut, "Original ref handle");
            string[] iteratorNames = { "<EndOfFrameCoroutine>d__5", "<NextFrameCoroutine>d__7", "<WaitNumberOfFramesCoroutine>d__10", "<DelayCoroutine>d__12", "<DelayCoroutine>d__14", "<FixedUpdateCoroutine>d__16", "<PredicateActiveCoroutine>d__19", "<PredicateCoroutine>d__21", "<UpdateCoroutine>d__23", "<IntervalCoroutine>d__25", "<PredicateIntervalCoroutine>d__27", "<PredicateActiveIntervalCoroutine>d__29", "<WaitForRealSeconds>d__30", "<WaitForSeconds>d__31", "<WaitForSecondsUnscaled>d__32", "<WaitOnInstance>d__35" };
            Check(ref count, t.GetNestedTypes(All).Length == iteratorNames.Length, "Whole original generated type count");
            foreach (string name in iteratorNames)
            {
                Type n = t.GetNestedType(name, All);
                Check(ref count, n != null && n.IsSealed && n.IsNestedPrivate, "Original iterator identity " + name);
                Check(ref count, n.GetField("<>4__this", All) == null, "Original receiver not captured " + name);
            }
            return count;
        }

        internal static int SingleYieldCallbacksAndFaults()
        {
            int count = 0;
            string[] names = { "NextFrameCoroutine", "EndOfFrameCoroutine", "FixedUpdateCoroutine" };
            Type[] waits = { null, typeof(WaitForEndOfFrame), typeof(WaitForFixedUpdate) };
            for (int i = 0; i < names.Length; i++)
            {
                int callbacks = 0;
                IEnumerator e = ActionFactory(names[i], () => callbacks++);
                Check(ref count, State(e) == 0 && e.Current == null && callbacks == 0, "Lazy factory");
                Check(ref count, e.MoveNext() && callbacks == 0 && State(e) == 1, "Original first yield");
                Check(ref count, waits[i] == null ? e.Current == null : e.Current.GetType() == waits[i], "Genuine wait type");
                object current = e.Current;
                ((IDisposable)e).Dispose();
                Check(ref count, State(e) == 1 && ReferenceEquals(current, e.Current), "Original empty Dispose retains suspension");
                Check(ref count, !e.MoveNext() && callbacks == 1 && State(e) == -1, "Complete callback");
                Check(ref count, ReferenceEquals(current, e.Current) && !e.MoveNext() && callbacks == 1, "Completed Current retained");
                Throws<NotSupportedException>(ref count, () => e.Reset(), "Original Reset unsupported");
                Check(ref count, State(e) == -1, "Reset failure retains state");
                IEnumerator missing = ActionFactory(names[i], null);
                Check(ref count, missing.MoveNext(), "Null action is lazy");
                Throws<NullReferenceException>(ref count, () => missing.MoveNext(), "Null callback faults after yield");
                Check(ref count, State(missing) == -1 && !missing.MoveNext(), "Null callback terminal");
                IEnumerator failed = ActionFactory(names[i], () => { throw new ApplicationException("callback"); });
                Check(ref count, failed.MoveNext(), "Throwing action lazy");
                Throws<ApplicationException>(ref count, () => failed.MoveNext(), "Callback exception preserved");
                Check(ref count, State(failed) == -1 && !failed.MoveNext(), "Throwing callback terminal");
                IEnumerator recursive = null;
                bool recursiveResult = true;
                recursive = ActionFactory(names[i], () => recursiveResult = recursive.MoveNext());
                Check(ref count, recursive.MoveNext() && !recursive.MoveNext() && !recursiveResult, "Callback reentry observes terminal");
            }
            return count;
        }

        internal static int FrameCountsSignedAndResume()
        {
            int count = 0;
            int[] inputs = { int.MinValue, -1, 0, 1, 2, 4 };
            int[] yields = { 0, 0, 0, 1, 2, 4 };
            for (int i = 0; i < inputs.Length; i++)
            {
                int callbacks = 0;
                IEnumerator e = CoroutineUtils.WaitNumberOfFramesCoroutine(() => callbacks++, inputs[i]);
                Check(ref count, callbacks == 0 && State(e) == 0 && e.Current == null, "Frame factory lazy");
                for (int frame = 0; frame < yields[i]; frame++)
                {
                    Check(ref count, e.MoveNext() && e.Current == null && State(e) == 1 && callbacks == 0, "Exact frame yield");
                    ((IDisposable)e).Dispose();
                    Check(ref count, State(e) == 1, "Frame Dispose empty");
                }
                Check(ref count, !e.MoveNext() && State(e) == -1 && callbacks == 1, "Exact signed frame completion");
                Check(ref count, !e.MoveNext() && callbacks == 1, "Frame completion stable");
            }
            IEnumerator overflow = CoroutineUtils.WaitNumberOfFramesCoroutine(() => { throw new ApplicationException(); }, int.MaxValue);
            Check(ref count, overflow.MoveNext(), "Overflow probe initial yield");
            Field(overflow, "<frameCount>5__2").SetValue(overflow, int.MaxValue);
            Check(ref count, overflow.MoveNext() && (int)Field(overflow, "<frameCount>5__2").GetValue(overflow) == int.MinValue, "Original unchecked resumed increment");
            IEnumerator missing = CoroutineUtils.WaitNumberOfFramesCoroutine(null, -1);
            Throws<NullReferenceException>(ref count, () => missing.MoveNext(), "Nonpositive null callback faults immediately");
            Check(ref count, !missing.MoveNext() && State(missing) == -1, "Frame failure terminal");
            IEnumerator recursive = null;
            bool reentered = true;
            recursive = CoroutineUtils.WaitNumberOfFramesCoroutine(() => reentered = recursive.MoveNext(), 0);
            Check(ref count, !recursive.MoveNext() && !reentered, "Zero-frame callback reentry terminal");
            return count;
        }

        internal static int PredicateOrderAndPartialFaults()
        {
            int count = 0;
            List<string> order = new List<string>();
            int calls = 0;
            IEnumerator active = PredicateFactory("PredicateActiveCoroutine", () => order.Add("action"), () => { order.Add("predicate"); return ++calls == 3; });
            Check(ref count, order.Count == 0 && active.MoveNext() && active.Current == null, "Active first false yield");
            Check(ref count, active.MoveNext() && active.Current == null, "Active second false yield");
            Check(ref count, !active.MoveNext() && string.Join(",", order) == "predicate,predicate,predicate,action", "Active callback after true");
            Check(ref count, !active.MoveNext() && calls == 3, "Active terminates");
            calls = 0; order.Clear();
            IEnumerator loop = PredicateFactory("PredicateCoroutine", () => order.Add("action"), () => { order.Add("predicate"); return ++calls <= 2; });
            Check(ref count, loop.MoveNext() && string.Join(",", order) == "predicate,action", "Predicate before immediate callback");
            Check(ref count, loop.MoveNext() && loop.Current == null, "Second action/null yield");
            Check(ref count, !loop.MoveNext() && string.Join(",", order) == "predicate,action,predicate,action,predicate", "Predicate false stops");
            IEnumerator none = PredicateFactory("PredicateCoroutine", null, () => false);
            Check(ref count, !none.MoveNext(), "False predicate never dereferences action");
            foreach (string name in new[] { "PredicateCoroutine", "PredicateActiveCoroutine" })
            {
                IEnumerator nullPredicate = PredicateFactory(name, () => { }, null);
                Throws<NullReferenceException>(ref count, () => nullPredicate.MoveNext(), "Null predicate fault");
                Check(ref count, State(nullPredicate) == -1 && !nullPredicate.MoveNext(), "Predicate fault terminal");
                IEnumerator nullAction = PredicateFactory(name, null, () => true);
                Throws<NullReferenceException>(ref count, () => nullAction.MoveNext(), "True predicate then null action");
                Check(ref count, State(nullAction) == -1 && !nullAction.MoveNext(), "Action fault terminal");
                IEnumerator throwPredicate = PredicateFactory(name, () => { }, () => { throw new ApplicationException(); });
                Throws<ApplicationException>(ref count, () => throwPredicate.MoveNext(), "Predicate exception retained");
                Check(ref count, !throwPredicate.MoveNext(), "Throwing predicate terminal");
            }
            int callbacks = 0;
            IEnumerator update = ActionFactory("UpdateCoroutine", () => callbacks++);
            Check(ref count, update.MoveNext() && callbacks == 1 && update.Current == null, "Update immediate action");
            ((IDisposable)update).Dispose();
            Check(ref count, update.MoveNext() && callbacks == 2, "Update original Dispose empty");
            IEnumerator recursive = null;
            bool reentered = true;
            recursive = ActionFactory("UpdateCoroutine", () => reentered = recursive.MoveNext());
            Check(ref count, recursive.MoveNext() && !reentered && State(recursive) == 1, "Update reentry terminal until new yield");
            IEnumerator broken = ActionFactory("UpdateCoroutine", null);
            Throws<NullReferenceException>(ref count, () => broken.MoveNext(), "Update null action fault");
            Check(ref count, !broken.MoveNext() && State(broken) == -1, "Update fault terminal");
            return count;
        }

        internal static int WaitObjectsAndIntervalOrder()
        {
            int count = 0;
            float[] times = { -3f, -0f, 2.5f, float.NaN, float.PositiveInfinity };
            foreach (float time in times)
            {
                int callbacks = 0;
                IEnumerator delay = Private("DelayCoroutine", new[] { typeof(Action), typeof(float) }, (Action)(() => callbacks++), time);
                Check(ref count, delay.Current == null && callbacks == 0, "Float delay lazy");
                Check(ref count, delay.MoveNext() && delay.Current.GetType() == typeof(WaitForSeconds) && FloatBits(Seconds(delay.Current)) == FloatBits(time), "Exact wait float bits");
                object wait = delay.Current;
                Check(ref count, !delay.MoveNext() && callbacks == 1 && ReferenceEquals(wait, delay.Current), "Float delay callback/retained wait");
                IEnumerator interval = IntervalFactory("IntervalCoroutine", () => callbacks++, time);
                Check(ref count, interval.MoveNext() && callbacks == 2, "Interval first action immediate");
                wait = interval.Current;
                Check(ref count, FloatBits(Seconds(wait)) == FloatBits(time) && interval.MoveNext() && callbacks == 3 && ReferenceEquals(wait, interval.Current), "One wait reused each interval");
            }
            WaitForSeconds supplied = new WaitForSeconds(0.125f);
            foreach (WaitForSeconds wait in new[] { supplied, null })
            {
                int callbacks = 0;
                IEnumerator e = Private("DelayCoroutine", new[] { typeof(Action), typeof(WaitForSeconds) }, (Action)(() => callbacks++), wait);
                Check(ref count, e.MoveNext() && ReferenceEquals(e.Current, wait) && callbacks == 0, "Supplied wait exact identity including null");
                Check(ref count, !e.MoveNext() && callbacks == 1 && ReferenceEquals(e.Current, wait), "Supplied wait completion");
            }
            List<string> order = new List<string>();
            IEnumerator falseLoop = PredicateIntervalFactory(null, () => { order.Add("predicate"); return false; }, 7f);
            Check(ref count, !falseLoop.MoveNext() && string.Join(",", order) == "predicate", "False timed predicate no callback");
            object allocated = Field(falseLoop, "<wait>5__2").GetValue(falseLoop);
            Check(ref count, allocated is WaitForSeconds && Seconds(allocated) == 7f && falseLoop.Current == null, "Wait allocated before false predicate");
            int starts = 0, predicates = 0;
            order.Clear();
            IEnumerator active = ActiveIntervalFactory(() => { order.Add("start"); return ++starts <= 2; }, () => order.Add("action"), () => { order.Add("predicate"); return ++predicates <= 2; }, 0.75f);
            Check(ref count, active.MoveNext() && active.Current == null && order.Count == 1, "Start true waits");
            Check(ref count, active.MoveNext() && active.Current == null && starts == 2, "Start still true waits");
            Check(ref count, active.MoveNext() && active.Current is WaitForSeconds && predicates == 0 && string.Join(",", order) == "start,start,start", "Start false first timed yield before main predicate");
            object shared = active.Current;
            Check(ref count, active.MoveNext() && ReferenceEquals(shared, active.Current) && string.Join(",", order) == "start,start,start,predicate,action", "Main predicate then action");
            Check(ref count, active.MoveNext() && ReferenceEquals(shared, active.Current) && predicates == 2 && starts == 3, "Timed loop reuses wait without restarting guard");
            Check(ref count, !active.MoveNext() && predicates == 3 && ReferenceEquals(shared, active.Current), "Timed completion retains current");
            IEnumerator nullStart = ActiveIntervalFactory(null, () => { }, () => true, 1f);
            Throws<NullReferenceException>(ref count, () => nullStart.MoveNext(), "Null start predicate first fault");
            Check(ref count, Field(nullStart, "<wait>5__2").GetValue(nullStart) == null && !nullStart.MoveNext(), "Start fault before wait allocation");
            IEnumerator nullMain = ActiveIntervalFactory(() => false, () => { }, null, 1f);
            Check(ref count, nullMain.MoveNext() && nullMain.Current is WaitForSeconds, "Null main lazy past initial timed yield");
            Throws<NullReferenceException>(ref count, () => nullMain.MoveNext(), "Null main predicate fault");
            Check(ref count, State(nullMain) == -1 && !nullMain.MoveNext(), "Main fault terminal");
            IEnumerator intervalFault = IntervalFactory("IntervalCoroutine", null, 3f);
            Throws<NullReferenceException>(ref count, () => intervalFault.MoveNext(), "Interval wait before null callback");
            Check(ref count, Field(intervalFault, "<wait>5__2").GetValue(intervalFault) is WaitForSeconds && intervalFault.Current == null && !intervalFault.MoveNext(), "Allocated wait survives callback fault before yield");
            return count;
        }

        internal static int LazyTimeAndNullInstanceBranches()
        {
            int count = 0;
            float[] ends = { float.NegativeInfinity, -1f, -0f, 0f, float.NaN };
            foreach (float time in ends)
            {
                foreach (IEnumerator e in new[] { CoroutineUtils.WaitForSeconds(time), CoroutineUtils.WaitForSecondsUnscaled(time) })
                {
                    Check(ref count, State(e) == 0 && FloatBits((float)Field(e, "time").GetValue(e)) == FloatBits(time), "Lazy timed factory retains parameter");
                    Check(ref count, !e.MoveNext() && State(e) == -1 && e.Current == null, "Nonpositive/unordered time ends before engine getter");
                    Check(ref count, !e.MoveNext(), "Timed terminal stable");
                }
                IEnumerator real = CoroutineUtils.WaitForRealSeconds(time);
                Check(ref count, State(real) == 0 && FloatBits((float)Field(real, "time").GetValue(real)) == FloatBits(time) && (float)Field(real, "<start>5__2").GetValue(real) == 0f, "Realtime factory lazy only; engine advance unexecuted");
            }
            FieldInfo instance = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.NonPublic | BindingFlags.Static);
            object before = instance.GetValue(null);
            try
            {
                instance.SetValue(null, null);
#pragma warning disable 618
                Check(ref count, CoroutineUtils.Instance == null && !CoroutineUtils.NotNull() && CoroutineUtils.IsNull(), "Actual null host comparisons");
                IEnumerator e = CoroutineUtils.WaitOnInstance();
#pragma warning restore 618
                Check(ref count, State(e) == 0 && e.Current == null, "Instance iterator lazy");
                Check(ref count, e.MoveNext() && State(e) == 1 && e.Current == null, "Null instance yields");
                ((IDisposable)e).Dispose();
                Check(ref count, e.MoveNext() && State(e) == 1, "Original instance Dispose empty");
                Field(e, "<>1__state").SetValue(e, 2);
                Check(ref count, !e.MoveNext(), "Invalid state ends before host check");
                Coroutine handle = null;
                CoroutineUtils.StopUtilCoroutine(ref handle);
                Check(ref count, handle == null && instance.GetValue(null) == null, "Null host stop leaves original field");
            }
            finally { instance.SetValue(null, before); }
            Check(ref count, ReferenceEquals(before, instance.GetValue(null)), "Global host reference identity restored");
            return count;
        }
    }
}
