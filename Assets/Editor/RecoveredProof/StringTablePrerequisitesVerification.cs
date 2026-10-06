using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class StringTablePrerequisitesVerification
    {
        public static int RunManaged()
        {
            int checks = 0;
            Action<bool, string> check = (ok, why) => { if (!ok) throw new Exception(why); checks++; };
            VersionChecks(check);
            CoroutineChecks(check);
            return checks;
        }

        public static void Run()
        {
            int checks = RunManaged();
            Debug.Log("Original StringTable prerequisite checks passed: " + checks +
                "; engine stop/lifecycle execution remains a separate PlayMode proof.");
        }

        private static void VersionChecks(Action<bool, string> check)
        {
            Type t = typeof(VersionChecker);
            check(t.Assembly.GetName().Name == "HLUnityCore.Runtime", "original version assembly");
            check(t.IsPublic && !t.IsSealed && !t.IsAbstract && (t.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original version class flags");
            check(t.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Length == 0, "original fieldless class");
            check(t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static).Select(m => m.Name).SequenceEqual(new[] { "IsVersionUpToDate" }), "complete original version method API");
            check(t.GetConstructors().Length == 1 && new VersionChecker() != null, "original public base-only constructor");
            var options = t.GetCustomAttributes<Il2CppSetOptionAttribute>().ToArray();
            check(options.Length == 2 && options[0].Option == Option.ArrayBoundsChecks && (bool)options[0].Value == false && options[1].Option == Option.NullChecks && (bool)options[1].Value == false, "original ordered version options");
            check(Enum.GetNames(typeof(VersionChecker.VersionComponent)).SequenceEqual(new[] { "None", "Major", "Minor", "Point" }), "original version component names");
            check(Enum.GetValues(typeof(VersionChecker.VersionComponent)).Cast<int>().SequenceEqual(new[] { 0, 1, 2, 3 }), "original version component literals");
            string[,] rows = {
                {"1", "1", "None"}, {"1", "1.0.0", "None"}, {"1.0.0", "1", "None"},
                {"0.99.99", "1.0.0", "Major"}, {"2.0.0", "1.99.99", "None"},
                {"1.1.99", "1.2.0", "Minor"}, {"1.2.0", "1.1.99", "None"},
                {"1.2.2", "1.2.3", "Point"}, {"1.2.4", "1.2.3", "None"},
                {"1", "1.1", "Minor"}, {"1.1", "1.1.1", "Point"},
                {"0", "-1.99.99", "None"}, {"-2", "-1", "Major"},
                {"1.-1.99", "1.0", "Minor"}, {"1.0.-1", "1.0.0", "Point"},
                {" +01 . 02 . +3 ", "1.2.3", "None"},
                {"1.2.3.bad", "1.2.3.999999999999", "None"},
                {"2147483647.0.0", "-2147483648.0.0", "None"},
                {"-2147483648.0.0", "2147483647.0.0", "Major"},
                {"1.2147483647.0", "1.-2147483648.0", "None"},
                {"1.0.-2147483648", "1.0.2147483647", "Point"}
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                VersionChecker.VersionComponent expected = (VersionChecker.VersionComponent)Enum.Parse(typeof(VersionChecker.VersionComponent), rows[i, 2]);
                VersionChecker.VersionComponent component = (VersionChecker.VersionComponent)99;
                bool result = VersionChecker.IsVersionUpToDate(rows[i, 0], rows[i, 1], out component);
                check(component == expected, "native component row " + i);
                check(result == (expected == VersionChecker.VersionComponent.None), "native return row " + i);
            }
            ExpectVersionFailure(check, null, "1", typeof(NullReferenceException), "null current split");
            ExpectVersionFailure(check, "", null, typeof(NullReferenceException), "expected split occurs before invalid current parse");
            ExpectVersionFailure(check, "", "1", typeof(FormatException), "empty current");
            ExpectVersionFailure(check, "1", "", typeof(FormatException), "empty expected");
            ExpectVersionFailure(check, "1.", "1", typeof(FormatException), "empty minor preserved");
            ExpectVersionFailure(check, "1.2.", "1", typeof(FormatException), "empty point preserved");
            ExpectVersionFailure(check, "2147483648", "bad", typeof(OverflowException), "current parsed before expected");
            ExpectVersionFailure(check, "2.bad", "1", typeof(FormatException), "current minor parsed before major comparison");
            ExpectVersionFailure(check, "2.0.bad", "1", typeof(FormatException), "current point parsed before major comparison");
            ExpectVersionFailure(check, "2", "1.bad", typeof(FormatException), "expected minor parsed before major comparison");
            ExpectVersionFailure(check, "2", "1.0.2147483648", typeof(OverflowException), "expected point parsed before major comparison");
            ExpectVersionFailure(check, "bad", "2147483648", typeof(FormatException), "current parse priority");
        }

        private static void ExpectVersionFailure(Action<bool, string> check, string current, string expected, Type kind, string why)
        {
            var component = (VersionChecker.VersionComponent)99;
            Exception caught = null;
            try { VersionChecker.IsVersionUpToDate(current, expected, out component); } catch (Exception e) { caught = e; }
            check(caught != null && caught.GetType() == kind, why);
            check(component == (VersionChecker.VersionComponent)99, "failed parse leaves original out value: " + why);
        }

        private static void CoroutineChecks(Action<bool, string> check)
        {
            Type t = typeof(MonoBehaviourExtensions);
            check(t.Assembly.GetName().Name == "HLUnityCore.Runtime" && t.IsAbstract && t.IsSealed, "original extension class");
            check(t.GetFields(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Length == 0, "original extension fields0");
            var methods = t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.MetadataToken).ToArray();
            check(methods.Select(m => m.Name).SequenceEqual(new[] { "SafeStopCoroutine", "SafeStopCoroutine", "WaitForUI" }), "complete3 outer method order");
            check(t.IsDefined(typeof(ExtensionAttribute), false) && methods.All(m => m.IsDefined(typeof(ExtensionAttribute), false)), "original extension markers");
            var stateType = methods[2].GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            check(stateType.Name == "<WaitForUI>d__2" && stateType.IsNestedPrivate && stateType.IsSealed, "natural original outer closure ordinal2");
            check(stateType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(new[] { "<>1__state", "<>2__current", "action" }), "original outer closure field order");
            check(stateType.GetFields().Single().FieldType == typeof(Action), "action is sole public retained parameter");
            var frameMethod = typeof(CoroutineUtils).GetMethod("WaitNumberOfFramesCoroutine");
            check(frameMethod.IsPublic && frameMethod.IsStatic && frameMethod.ReturnType == typeof(IEnumerator), "genuine frame dependency API");
            var hostOptions = typeof(CoroutineUtils).GetCustomAttributes<Il2CppSetOptionAttribute>().ToArray();
            check(hostOptions.Length == 2 && hostOptions[0].Option == Option.ArrayBoundsChecks && (bool)hostOptions[0].Value == false && hostOptions[1].Option == Option.NullChecks && (bool)hostOptions[1].Value == false, "original host options restored on shared partial type");
            IEnumerator nullEnumerator = null;
            Coroutine nullHandle = null;
            MonoBehaviour nullBehaviour = null;
            nullBehaviour.SafeStopCoroutine(ref nullEnumerator);
            nullBehaviour.SafeStopCoroutine(ref nullHandle);
            check(nullEnumerator == null && nullHandle == null, "null references skip null receiver");
            IEnumerator supplied = CoroutineUtils.WaitNumberOfFramesCoroutine(() => { }, 1);
            IEnumerator original = supplied;
            Exception failure = null;
            try { nullBehaviour.SafeStopCoroutine(ref supplied); } catch (Exception e) { failure = e; }
            check(failure is NullReferenceException && object.ReferenceEquals(supplied, original), "failed receiver preserves IEnumerator handle");
            foreach (int frames in new[] { -3, -1, 0, 1, 2, 3 })
            {
                int calls = 0;
                IEnumerator iterator = CoroutineUtils.WaitNumberOfFramesCoroutine(() => calls++, frames);
                check(iterator.Current == null && calls == 0, "construction is lazy for " + frames);
                int yields = 0;
                while (iterator.MoveNext())
                {
                    check(iterator.Current == null && calls == 0, "null frame yield before callback for " + frames);
                    yields++;
                }
                check(yields == Math.Max(0, frames) && calls == 1, "native exact frame count " + frames);
                check(!iterator.MoveNext() && calls == 1, "terminal frame callback runs once " + frames);
                Exception reset = null;
                try { iterator.Reset(); } catch (Exception e) { reset = e; }
                check(reset is NotSupportedException, "native iterator reset " + frames);
            }
            IEnumerator disposed = CoroutineUtils.WaitNumberOfFramesCoroutine(() => { }, 2);
            check(disposed.MoveNext(), "dispose fixture first yield");
            ((IDisposable)disposed).Dispose();
            check(disposed.MoveNext(), "native empty Dispose preserves suspended state");
            check(!disposed.MoveNext(), "disposed iterator still invokes callback");
            int reenteredCalls = 0;
            IEnumerator reentered = null;
            bool innerAdvance = true;
            reentered = CoroutineUtils.WaitNumberOfFramesCoroutine(() => { reenteredCalls++; innerAdvance = reentered.MoveNext(); }, 0);
            check(!reentered.MoveNext() && !innerAdvance && reenteredCalls == 1, "state terminal before callback reentry");
            IEnumerator throwing = CoroutineUtils.WaitNumberOfFramesCoroutine(() => { throw new InvalidOperationException("sentinel"); }, 1);
            check(throwing.MoveNext(), "throw callback fixture yields");
            failure = null;
            try { throwing.MoveNext(); } catch (Exception e) { failure = e; }
            check(failure is InvalidOperationException && !throwing.MoveNext(), "throwing callback cannot be repeated");
            IEnumerator nullAction = CoroutineUtils.WaitNumberOfFramesCoroutine(null, 1);
            check(nullAction.MoveNext(), "null callback is deferred until after frame");
            failure = null;
            try { nullAction.MoveNext(); } catch (Exception e) { failure = e; }
            check(failure is NullReferenceException && !nullAction.MoveNext(), "null callback is unguarded and terminal");
            int outerCalls = 0;
            IEnumerator outer = nullBehaviour.WaitForUI(() => outerCalls++);
            check(outer.Current == null && outerCalls == 0, "UI construction lazy and receiver unused");
            check(outer.MoveNext() && outer.Current is IEnumerator && outerCalls == 0, "UI yields genuine nested iterator");
            IEnumerator child = (IEnumerator)outer.Current;
            check(!outer.MoveNext() && outerCalls == 0 && object.ReferenceEquals(outer.Current, child), "manual outer completion does not run or clear child");
            check(child.MoveNext() && child.Current == null && outerCalls == 0, "nested first frame");
            check(child.MoveNext() && child.Current == null && outerCalls == 0, "nested second frame");
            check(!child.MoveNext() && outerCalls == 1, "nested callback after exactly2 frames");
            check(!outer.MoveNext() && !child.MoveNext() && outerCalls == 1, "nested and outer terminal");
            IEnumerator replacedOuter = nullBehaviour.WaitForUI(() => { throw new Exception("original action should have been replaced"); });
            int replacementCalls = 0;
            stateType.GetField("action").SetValue(replacedOuter, (Action)(() => replacementCalls++));
            check(replacedOuter.MoveNext(), "outer action read on first MoveNext");
            IEnumerator replacedChild = (IEnumerator)replacedOuter.Current;
            stateType.GetField("action").SetValue(replacedOuter, (Action)(() => { throw new Exception("child must retain first action"); }));
            check(replacedChild.MoveNext() && replacedChild.MoveNext() && !replacedChild.MoveNext() && replacementCalls == 1, "child captures action once at first outer advance");
            int changedCalls = 0;
            IEnumerator changed = CoroutineUtils.WaitNumberOfFramesCoroutine(() => { throw new Exception("replaced action should not run"); }, 5);
            check(changed.MoveNext(), "mutable frame dependency initial yield");
            changed.GetType().GetField("numberOfFramesToWait").SetValue(changed, 1);
            changed.GetType().GetField("action").SetValue(changed, (Action)(() => changedCalls++));
            check(!changed.MoveNext() && changedCalls == 1, "frame bound and callback read again after suspension");
            IEnumerator overflowed = CoroutineUtils.WaitNumberOfFramesCoroutine(() => { throw new Exception("unchecked wrapped count must remain suspended"); }, int.MaxValue);
            check(overflowed.MoveNext(), "overflow fixture initial yield");
            FieldInfo frameCounter = overflowed.GetType().GetField("<frameCount>5__2", BindingFlags.NonPublic | BindingFlags.Instance);
            frameCounter.SetValue(overflowed, int.MaxValue);
            check(overflowed.MoveNext() && (int)frameCounter.GetValue(overflowed) == int.MinValue, "native frame counter increment wraps unchecked");
            var childFields = child.GetType().GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.MetadataToken).Select(f => f.Name);
            check(childFields.SequenceEqual(new[] { "<>1__state", "<>2__current", "numberOfFramesToWait", "action", "<frameCount>5__2" }), "original frame iterator complete field order");
        }
    }
}
