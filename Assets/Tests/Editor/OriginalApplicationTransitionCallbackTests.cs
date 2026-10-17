using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The DoOnEnter transition callbacks of ApplicationStateFadeTransition and
    // ApplicationStateStartUITransition, run through the compiler closures the
    // transition manager would receive. Each case owns its unregistered
    // machine, its storage and its event; the event is destroyed in finally.
    // HLOutput errors go to the owned OwnedHLOutputErrorCapture only.
    public sealed class OriginalApplicationTransitionCallbackTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string TimeoutError = "Failed to complete UI transition: attempting to continue|0";

        private static string Name() => "Lucid-app-transition-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static object Closure(Type owner, string name, object state, IGraphUser user)
        {
            Type type = owner.GetNestedType(name, BindingFlags.NonPublic);
            Assert.That(type, Is.Not.Null, owner.Name + "+" + name);
            object closure = Activator.CreateInstance(type);
            type.GetField("<>4__this").SetValue(closure, state);
            type.GetField("user").SetValue(closure, user);
            return closure;
        }

        private static void Invoke(object closure, string method)
        {
            try { closure.GetType().GetMethod(method, AnyInstance).Invoke(closure, null); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        private static List<ApplicationStateEvent> StateEvents(IGraphUser user)
        {
            var events = new List<ApplicationStateEvent>();
            user.Storage.SetValue(AppFSMKeys.StateEvents, events);
            return events;
        }

        // Midpoint 0x0600065b (x86-64 0x645300) invokes the callback (0x645352,
        // skipped when null 0x645348) before user.Storage (0x6453bb) removes
        // TransitionFadeParameters. b__0 0x645640 passes the captured user and
        // fadeParameters.OnMidpointCallback; b__1 0x645660 calls PublishMessage
        // 0x0600065c, whose GetValue default is null (0x645508) and whose
        // AddUnique 0x645620 keeps one entry. Removing before the callback, or
        // publishing at the midpoint, fails here.
        [Test]
        public void FadeMidpointRunsTheCallbackBeforeRemovingItsParametersAndCompletionPublishesOnce()
        {
            var midpointEvent = ScriptableObject.CreateInstance<ApplicationStateEvent>();
            try
            {
                ConstructorInfo ctor = typeof(ApplicationStateFadeTransition).GetConstructor(AnyInstance, null,
                    new[] { typeof(FiniteStateMachine), typeof(string), typeof(ApplicationStateEvent) }, null);
                object state = ctor.Invoke(new object[] { Machine(), Name(), midpointEvent });
                var user = new FSMUser(new FSMStorage(0));
                List<ApplicationStateEvent> events = StateEvents(user);

                var seen = new List<bool>();
                var parameters = new FadeTransitionParameters(default, () => seen.Add(user.Storage.HasValue(AppFSMKeys.TransitionFadeParameters)));
                user.Storage.SetValue(AppFSMKeys.TransitionFadeParameters, parameters);
                object closure = Closure(typeof(ApplicationStateFadeTransition), "<>c__DisplayClass4_0", state, user);
                closure.GetType().GetField("fadeParameters").SetValue(closure, parameters);

                Invoke(closure, "<DoOnEnter>b__0");
                Assert.That(seen, Is.EqualTo(new[] { true }), "the callback runs once, while the parameters are still stored");
                Assert.That(user.Storage.HasValue(AppFSMKeys.TransitionFadeParameters), Is.False);
                Assert.That(events, Is.Empty, "the midpoint does not publish");

                var silent = new FadeTransitionParameters(default, null);
                user.Storage.SetValue(AppFSMKeys.TransitionFadeParameters, silent);
                closure.GetType().GetField("fadeParameters").SetValue(closure, silent);
                Invoke(closure, "<DoOnEnter>b__0");
                Assert.That(user.Storage.HasValue(AppFSMKeys.TransitionFadeParameters), Is.False, "a null callback still removes");

                Invoke(closure, "<DoOnEnter>b__1");
                Invoke(closure, "<DoOnEnter>b__1");
                Assert.That(events, Is.EqualTo(new[] { midpointEvent }), "completion adds the event once");
                Assert.That(seen.Count, Is.EqualTo(1), "completion does not run the midpoint callback");
            }
            finally { Object.DestroyImmediate(midpointEvent); }
        }

        // StartUI b__0 0x06000762 (x86-64 0x654c80) jumps to PublishMessage
        // 0x0600075e. b__1 0x06000763 (0x654ca0) runs OnTransitionTimeout
        // 0x0600075f: HLOutput.LogError 0x654c5b with the literal, then the
        // PublishMessage tail jump 0x654c6a. A user whose storage throws shows
        // the error is reported before the publish reads the storage.
        [Test]
        public void StartUIMidpointPublishesAndTimeoutReportsBeforePublishing()
        {
            var midpointEvent = ScriptableObject.CreateInstance<ApplicationStateEvent>();
            try
            {
                using (var capture = new OwnedHLOutputErrorCapture())
                {
                    ConstructorInfo ctor = typeof(ApplicationStateStartUITransition).GetConstructor(AnyInstance, null,
                        new[] { typeof(FiniteStateMachine), typeof(string), typeof(ApplicationStateEvent), typeof(UIContainerIdentifier), typeof(bool), typeof(bool) }, null);
                    object state = ctor.Invoke(new object[] { Machine(), Name(), midpointEvent, null, false, true });

                    var user = new FSMUser(new FSMStorage(0));
                    List<ApplicationStateEvent> events = StateEvents(user);
                    object closure = Closure(typeof(ApplicationStateStartUITransition), "<>c__DisplayClass7_0", state, user);
                    Invoke(closure, "<DoOnEnter>b__0");
                    Assert.That(events, Is.EqualTo(new[] { midpointEvent }));
                    Assert.That(capture.Errors, Is.Empty, "the midpoint reports nothing");

                    Invoke(closure, "<DoOnEnter>b__1");
                    Assert.That(capture.Errors, Is.EqualTo(new[] { TimeoutError }));
                    Assert.That(events, Is.EqualTo(new[] { midpointEvent }), "the timeout publish keeps one entry");

                    object failing = Closure(typeof(ApplicationStateStartUITransition), "<>c__DisplayClass7_0", state, new ThrowingStorageUser());
                    Assert.Throws<ThrowingStorageUser.StorageRead>(() => Invoke(failing, "<DoOnEnter>b__1"));
                    Assert.That(capture.Errors, Is.EqualTo(new[] { TimeoutError, TimeoutError }), "the error precedes the storage read");
                }
            }
            finally { Object.DestroyImmediate(midpointEvent); }
        }

        private sealed class ThrowingStorageUser : IGraphUser
        {
            public sealed class StorageRead : Exception { }

            public IGraphStorage Storage => throw new StorageRead();

            public void DestroyUser() { }
        }
    }
}
