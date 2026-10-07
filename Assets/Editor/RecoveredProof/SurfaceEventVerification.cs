using System;
using System.Collections;
using System.Reflection;
using HardlightProject;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectLucid.Verification
{
    public static class SurfaceEventVerification
    {
        private static readonly Type RoutineType = typeof(SurfaceEvent).GetNestedType("<ProcessStayEvents>d__7", BindingFlags.NonPublic);
        private static readonly FieldInfo State = RoutineType.GetField("<>1__state", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Enter = typeof(SurfaceEvent).GetField("m_onEnter", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Stay = typeof(SurfaceEvent).GetField("m_onStay", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Exit = typeof(SurfaceEvent).GetField("m_onExit", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Coroutine = typeof(SurfaceEvent).GetField("m_stayCoroutine", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Wait = typeof(SurfaceEvent).GetField("m_waitForFixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Process = typeof(SurfaceEvent).GetMethod("ProcessStayEvents", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void Require(bool value, string detail, ref int count)
        {
            if (!value) throw new InvalidOperationException(detail);
            ++count;
        }
        private static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return true; }
            return false;
        }
        private static IEnumerator NewRoutine(int state)
        {
            // This is the real generated Object-based constructor. No owner or
            // component is synthesized, and no constructor is bypassed.
            return (IEnumerator)Activator.CreateInstance(RoutineType, BindingFlags.Instance | BindingFlags.Public,
                null, new object[] { state }, null);
        }
        public static int RunManaged()
        {
            int checks = 0;
            foreach (int state in new[] { -1, -2, 2, int.MinValue, int.MaxValue })
            {
                var routine = NewRoutine(state);
                Require(!routine.MoveNext(), "inactive state returns false", ref checks);
                Require(routine.Current == null, "inactive state leaves initial Current null", ref checks);
            }
            foreach (int state in new[] { 0, 1 })
            {
                var routine = NewRoutine(state);
                Require(Throws<NullReferenceException>(() => routine.MoveNext()), "real iterator needs its missing owner", ref checks);
                Require((int)State.GetValue(routine) == -1, "state invalidated before owner fault", ref checks);
                Require(!routine.MoveNext(), "faulted iterator does not retry callback", ref checks);
            }
            var pending = NewRoutine(0);
            ((IDisposable)pending).Dispose();
            Require((int)State.GetValue(pending) == 0, "original Dispose preserves state", ref checks);
            Require(Throws<NotSupportedException>(() => pending.Reset()), "original Reset throws", ref checks);
            Require(pending.Current == null, "Reset did not set Current", ref checks);
            return checks;
        }
        public static int RunEngine()
        {
            int checks = 0;
            GameObject owned = new GameObject("Lucid surface event verification");
            try
            {
                var surface = owned.AddComponent<SurfaceEvent>();
                var enter = new UnityEvent(); var stay = new UnityEvent(); var exit = new UnityEvent();
                Enter.SetValue(surface, enter); Stay.SetValue(surface, stay); Exit.SetValue(surface, exit);
                int entered = 0, stayed = 0, exited = 0;
                enter.AddListener(() => ++entered); stay.AddListener(() => ++stayed); exit.AddListener(() => ++exited);
                Require(Wait.GetValue(surface) is WaitForFixedUpdate, "genuine cached fixed-update wait", ref checks);
                Require(Coroutine.GetValue(surface) == null, "no tracked coroutine initially", ref checks);
                surface.OnSurfaceEnter();
                Require(entered == 1, "enter callback runs", ref checks);
                Require(stayed == 0, "runtime stay listener alone does not start routine", ref checks);
                Require(Coroutine.GetValue(surface) == null, "runtime-only stay leaves handle null", ref checks);
                surface.OnSurfaceExit();
                Require(exited == 1, "exit callback runs with no coroutine", ref checks);
                var routine = (IEnumerator)Process.Invoke(surface, null);
                Require(routine.Current == null, "genuine routine initial Current", ref checks);
                Require(routine.MoveNext() && stayed == 1, "stay invokes before first yield", ref checks);
                Require(ReferenceEquals(routine.Current, Wait.GetValue(surface)), "original shared wait yielded", ref checks);
                ((IDisposable)routine).Dispose();
                Require(routine.MoveNext() && stayed == 2, "empty Dispose leaves active routine resumable", ref checks);
                Require(ReferenceEquals(routine.Current, Wait.GetValue(surface)), "same wait instance each iteration", ref checks);
                var replacement = new UnityEvent(); int replacementCalls = 0;
                replacement.AddListener(() => ++replacementCalls); Stay.SetValue(surface, replacement);
                Require(routine.MoveNext() && replacementCalls == 1 && stayed == 2, "resume reloads replacement event", ref checks);
                Require(Throws<NotSupportedException>(() => routine.Reset()), "genuine active Reset throws", ref checks);
                Require(routine.MoveNext() && replacementCalls == 2, "Reset failure leaves routine state intact", ref checks);
                Stay.SetValue(surface, null);
                Require(Throws<NullReferenceException>(() => surface.OnSurfaceEnter()) && entered == 2,
                    "enter callback precedes missing stay failure", ref checks);
                Require(Throws<NullReferenceException>(() => routine.MoveNext()), "stay null fault is preserved", ref checks);
                Require(!routine.MoveNext(), "fault invalidates iterator before retry", ref checks);
                var fault = new UnityEvent(); fault.AddListener(() => { throw new InvalidOperationException("fixture callback fault"); });
                Stay.SetValue(surface, fault); var throwing = (IEnumerator)Process.Invoke(surface, null);
                Require(Throws<InvalidOperationException>(() => throwing.MoveNext()), "stay callback fault propagates", ref checks);
                Require(!throwing.MoveNext(), "callback fault leaves invalid state", ref checks);
                Enter.SetValue(surface, fault); Stay.SetValue(surface, replacement);
                Require(Throws<InvalidOperationException>(() => surface.OnSurfaceEnter()), "enter callback fault propagates", ref checks);
                Require(Coroutine.GetValue(surface) == null, "enter fault does not publish coroutine", ref checks);
                Exit.SetValue(surface, fault);
                Require(Throws<InvalidOperationException>(() => surface.OnSurfaceExit()), "exit callback fault propagates", ref checks);
                Require(Coroutine.GetValue(surface) == null, "exit fault retains null handle", ref checks);
                return checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(owned); }
        }
        public static int Run() { return RunManaged() + RunEngine(); }
    }
}
