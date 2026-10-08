using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Engine-only cases require an empty, quiescent Unity test context.
    // Compilation alone proves no scheduling or object-lifetime behavior.
    // All created objects and callbacks are owned and cleaned up by these tests.
    public static class OriginalCoroutineEngineVerification
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly FieldInfo HostField = typeof(CoroutineUtils).GetField("s_instance", Static);
        private static void Check(ref int count, bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            count++;
        }
        private static bool Same(float a, float b) => BitConverter.ToInt32(BitConverter.GetBytes(a), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b), 0);
        private static FieldInfo Field(IEnumerator iterator, string name) => iterator.GetType().GetField(name, Instance);
        private static Dictionary<object, object> Rows(IDictionary dictionary)
        {
            var rows = new Dictionary<object, object>();
            foreach (DictionaryEntry entry in dictionary) rows.Add(entry.Key, entry.Value);
            return rows;
        }
        private static bool SameRows(IDictionary dictionary, Dictionary<object, object> rows)
        {
            if (dictionary.Count != rows.Count) return false;
            foreach (var row in rows)
                if (!dictionary.Contains(row.Key) || !ReferenceEquals(dictionary[row.Key], row.Value)) return false;
            return true;
        }
        private static void RestoreRows(IDictionary dictionary, Dictionary<object, object> rows)
        {
            dictionary.Clear();
            foreach (var row in rows) dictionary.Add(row.Key, row.Value);
        }

        private sealed class OwnedScope : IDisposable
        {
            private readonly object oldHost = HostField.GetValue(null);
            private readonly FieldInfo registryField = typeof(ProcessManager).GetField("s_systemDictionary", Static);
            private readonly FieldInfo actionField = typeof(ProcessManager).GetField("s_systemActionLookup", Static);
            private readonly FieldInfo listField = typeof(ProcessManager).GetField("s_actionList", Static);
            private readonly FieldInfo progressField = typeof(ProcessManager).GetField("s_systemActionInProgress", Static);
            private readonly IDictionary registry;
            private readonly IDictionary actions;
            private readonly Dictionary<object, object> oldRegistry;
            private readonly Dictionary<object, object> oldActions;
            private readonly Dictionary<IDictionary, Dictionary<object, object>> oldInner = new Dictionary<IDictionary, Dictionary<object, object>>();
            private readonly IList list;
            private readonly object[] oldList;
            private readonly object oldProgress;
            private readonly float oldScale;
            private readonly List<GameObject> objects = new List<GameObject>();
            private readonly List<CoroutineUtils> components = new List<CoroutineUtils>();
            private bool closed;

            internal OwnedScope()
            {
                registry = (IDictionary)registryField.GetValue(null);
                actions = (IDictionary)actionField.GetValue(null);
                oldRegistry = Rows(registry); oldActions = Rows(actions);
                foreach (var row in oldActions)
                    if (row.Value is IDictionary inner) oldInner.Add(inner, Rows(inner));
                list = (IList)listField.GetValue(null);
                oldList = new object[list.Count]; list.CopyTo(oldList, 0);
                oldProgress = progressField.GetValue(null);
                oldScale = Time.timeScale;
                // Detach existing rows intact, so real Awake/OnDestroy cannot
                // replace or revoke any prior system/reference/listener.
                registry.Clear(); actions.Clear(); HostField.SetValue(null, null);
            }
            internal CoroutineUtils Create(bool active)
            {
                var gameObject = new GameObject("ProjectLucid.Coroutine.OwnedEngineProof");
                objects.Add(gameObject);
                gameObject.SetActive(active);
                CoroutineUtils component = gameObject.AddComponent<CoroutineUtils>();
                components.Add(component);
                return component;
            }
            public void Dispose()
            {
                if (closed) return;
                closed = true;
                Exception error = null;
                Action<Action> attempt = action => { try { action(); } catch (Exception failure) { if (error == null) error = failure; } };
                for (int i = components.Count - 1; i >= 0; i--)
                {
                    CoroutineUtils component = components[i];
                    if (component != null) attempt(component.StopAllCoroutines);
                }
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    GameObject gameObject = objects[i];
                    if (gameObject != null) attempt(() => UnityEngine.Object.DestroyImmediate(gameObject));
                }
                attempt(() => RestoreRows(registry, oldRegistry));
                attempt(() => RestoreRows(actions, oldActions));
                attempt(() => HostField.SetValue(null, oldHost));
                attempt(() => Time.timeScale = oldScale);
                if (error != null) throw error;
            }
            internal void AssertRestored(ref int count)
            {
                Check(ref count, closed && ReferenceEquals(HostField.GetValue(null), oldHost), "Original host reference restored after owned teardown");
                Check(ref count, ReferenceEquals(registryField.GetValue(null), registry) && SameRows(registry, oldRegistry), "Original registry/row/reference identities restored");
                bool intactInner = true;
                foreach (var row in oldInner) intactInner &= SameRows(row.Key, row.Value);
                Check(ref count, ReferenceEquals(actionField.GetValue(null), actions) && SameRows(actions, oldActions) && intactInner, "Original action rows/delegates retained intact");
                bool intactList = list.Count == oldList.Length;
                for (int i = 0; intactList && i < oldList.Length; i++) intactList &= ReferenceEquals(list[i], oldList[i]);
                Check(ref count, ReferenceEquals(listField.GetValue(null), list) && intactList, "Original action-list reference/order retained");
                Check(ref count, Equals(progressField.GetValue(null), oldProgress) && Same(Time.timeScale, oldScale), "Original dispatch flag and engine time scale retained");
            }
        }

        public static int OriginalScaledUnscaledAndRealtimeGetters()
        {
            int count = 0;
            var scope = new OwnedScope();
            using (scope)
            {
                foreach (bool unscaled in new[] { false, true })
                {
                    IEnumerator iterator = unscaled ? CoroutineUtils.WaitForSecondsUnscaled(1f) : CoroutineUtils.WaitForSeconds(1f);
                    float delta = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                    Check(ref count, iterator.MoveNext() && iterator.Current == null, "Ordered positive duration always subtracts before its first null yield");
                    Check(ref count, Same((float)Field(iterator, "time").GetValue(iterator), 1f - delta), "Actual installed time getter supplies the exact subtraction");
                    ((IDisposable)iterator).Dispose();
                    Field(iterator, "time").SetValue(iterator, -1f);
                    Check(ref count, !iterator.MoveNext(), "Original empty Dispose retains suspended state; later negative parameter terminates");
                }
                IEnumerator real = CoroutineUtils.WaitForRealSeconds(float.PositiveInfinity);
                float before = Time.realtimeSinceStartup;
                Check(ref count, real.MoveNext() && real.Current == null, "Actual realtime body advances lazily and yields for positive infinity");
                float after = Time.realtimeSinceStartup;
                float start = (float)Field(real, "<start>5__2").GetValue(real);
                Check(ref count, before <= start && start <= after, "Original start is captured inside the first genuine advance");
                ((IDisposable)real).Dispose();
                Check(ref count, real.MoveNext() && (float)Field(real, "<start>5__2").GetValue(real) == start, "Resume retains the original realtime start");
                Field(real, "<start>5__2").SetValue(real, float.NaN);
                Check(ref count, !real.MoveNext(), "Unordered deadline terminates the original strict-less comparison");
            }
            scope.AssertRestored(ref count);
            return count;
        }

        public static int OriginalRegistrationPublicationAndManagedFaultPrefix()
        {
            int count = 0;
            var scope = new OwnedScope();
            using (scope)
            {
                CoroutineUtils existing = scope.Create(false);
                CoroutineUtils candidate = scope.Create(false);
                Check(ref count, HostField.GetValue(null) == null, "Inactive genuine components have not published their Awake host");
                SystemRef registry = ProcessManager.RegisterSystem(existing);
                Check(ref count, ReferenceEquals(registry.GetSafe(), existing), "Only owned real registration is seeded");
                MethodInfo awake = typeof(CoroutineUtils).GetMethod("Awake", Instance);
                bool failed = false;
                try { awake.Invoke(candidate, null); }
                catch (TargetInvocationException exception)
                {
                    failed = exception.InnerException is InvalidOperationException;
                }
                Check(ref count, failed, "Actual original Awake registration failure propagates through reflection");
                Check(ref count, ReferenceEquals(HostField.GetValue(null), candidate), "Original host publication precedes the failing registry call");
                Check(ref count, ReferenceEquals(registry.GetSafe(), existing), "Preceding original registration survives the failed replacement");
                Coroutine handle = null;
                CoroutineUtils.StopUtilCoroutine(ref handle);
                Check(ref count, handle == null && ReferenceEquals(HostField.GetValue(null), candidate), "Live genuine host plus null ordinary handle returns without stopping or clearing host");
            }
            scope.AssertRestored(ref count);
            return count;
        }

        public static int OriginalRealHostDestroyRetainsUnityNullReference()
        {
            int count = 0;
            var scope = new OwnedScope();
            using (scope)
            {
                CoroutineUtils owner = scope.Create(true);
                // EditMode does not automatically enter runtime MonoBehaviour lifecycle.
                // Invoke the original entry body on a genuine owned component; the
                // separate Play case proves automatic Awake/OnDestroy scheduling.
                typeof(CoroutineUtils).GetMethod("Awake", Instance).Invoke(owner, null);
                Check(ref count, ReferenceEquals(HostField.GetValue(null), owner), "Explicit original Awake publishes the genuine owned host");
                SystemRef registry = ProcessManager.GetSystemRef(typeof(CoroutineUtils).ToString(), false);
                Check(ref count, ReferenceEquals(registry.GetSafe(), owner), "Explicit original Awake registers the exact real component");
#pragma warning disable 618
                Check(ref count, ReferenceEquals(CoroutineUtils.Instance, owner) && CoroutineUtils.NotNull() && !CoroutineUtils.IsNull(), "Original live Unity comparisons and unguarded getter");
                IEnumerator immediate = CoroutineUtils.WaitOnInstance();
#pragma warning restore 618
                Check(ref count, !immediate.MoveNext(), "Original live host exits the instance iterator without a yield");
                // Apply the original exit body explicitly in EditMode before Engine destruction.
                typeof(CoroutineUtils).GetMethod("OnDestroy", Instance).Invoke(owner, null);
                UnityEngine.Object.DestroyImmediate(owner.gameObject);
                Check(ref count, owner == null && !ReferenceEquals(owner, null), "Real destroyed component retains a managed reference with Unity-null equality");
                Check(ref count, ReferenceEquals(HostField.GetValue(null), owner) && registry.GetSafe() == null, "Explicit original OnDestroy revokes registration and real Engine destruction retains the static managed host reference");
#pragma warning disable 618
                Check(ref count, !CoroutineUtils.NotNull() && CoroutineUtils.IsNull(), "Original queries use Unity liveness after destruction");
                IEnumerator wait = CoroutineUtils.WaitOnInstance();
#pragma warning restore 618
                Check(ref count, wait.MoveNext() && wait.Current == null, "Original destroyed-host waiter yields");
                ((IDisposable)wait).Dispose();
                Check(ref count, wait.MoveNext() && wait.Current == null, "Original empty iterator disposal retains that waiting state");
                Coroutine handle = null;
                CoroutineUtils.StopUtilCoroutine(ref handle);
                Check(ref count, handle == null && ReferenceEquals(HostField.GetValue(null), owner), "Destroyed-host guard returns without rewriting the original reference");
            }
            scope.AssertRestored(ref count);
            return count;
        }

        public static IEnumerator OriginalScheduledFramesStopAndDuplicateLifetime(Action<int> completed)
        {
            int count = 0;
            var scope = new OwnedScope();
            using (scope)
            {
                Time.timeScale = 1f;
                CoroutineUtils owner = scope.Create(true);
                Check(ref count, ReferenceEquals(HostField.GetValue(null), owner), "Actual owned scheduled host published");
                SystemRef reference = ProcessManager.GetSystemRef(typeof(CoroutineUtils).ToString(), false);
                int updates = 0;
                Coroutine running = CoroutineUtils.Update(() => updates++);
                Check(ref count, running != null && updates == 1, "Actual StartCoroutine executes original update callback before returning");
                CoroutineUtils.StopUtilCoroutine(ref running);
                Check(ref count, running == null, "Successful original StopCoroutine clears the real by-reference handle");
                yield return null;
                yield return null;
                Check(ref count, updates == 1, "Stopped real update coroutine never invokes later owned callbacks");
                int first = Time.frameCount;
                int next = -1, ui = -1, supplied = -1;
                CoroutineUtils.OnNextFrame(() => next = Time.frameCount);
                Coroutine uiHandle = CoroutineUtils.WaitForUI(() => ui = Time.frameCount);
                Coroutine nullWaitHandle = CoroutineUtils.Delay(() => supplied = Time.frameCount, (WaitForSeconds)null);
                Check(ref count, next == -1 && ui == -1 && supplied == -1 && uiHandle != null && nullWaitHandle != null, "Three original factories schedule genuine delayed callbacks");
                for (int frame = 0; frame < 12 && (next < 0 || ui < 0 || supplied < 0); frame++) yield return null;
                Check(ref count, next >= first + 1 && supplied >= first + 1 && ui >= first + 2, "Original single-null and exact-two-frame callbacks complete through real engine scheduling");
                Check(ref count, next <= ui && supplied <= ui, "Original two-frame wait completes no earlier than one-frame factories");
                CoroutineUtils.StopUtilCoroutine(ref uiHandle);
                CoroutineUtils.StopUtilCoroutine(ref nullWaitHandle);
                Check(ref count, uiHandle == null && nullWaitHandle == null, "Original stop clears completed genuine handles after engine call");
                CoroutineUtils duplicate = scope.Create(true);
                GameObject duplicateObject = duplicate.gameObject;
                Check(ref count, ReferenceEquals(HostField.GetValue(null), owner) && ReferenceEquals(reference.GetSafe(), owner), "Duplicate Awake does not register over the original host");
                yield return null;
                Check(ref count, duplicate == null && duplicateObject != null, "Original duplicate branch destroys only the component after a real deferred frame");
                Check(ref count, ReferenceEquals(reference.GetSafe(), owner), "Duplicate OnDestroy leaves the original host registered");
                UnityEngine.Object.Destroy(owner.gameObject);
                yield return null;
                Check(ref count, owner == null && ReferenceEquals(HostField.GetValue(null), owner) && reference.GetSafe() == null, "Deferred original OnDestroy revokes the registry and preserves the destroyed static reference");
            }
            scope.AssertRestored(ref count);
            completed(count);
        }
    }
}
