using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Owned rebuilt-managed boundaries only. No SaveManager, App, AudioManager,
    // UIManager, camera or Addressables operation is constructed or invoked.
    public class OriginalMainMenuFSMTests
    {
        private static string Name() => "Lucid-mainmenu-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        private static FSMUser User() => new FSMUser(new FSMStorage(0));

        [Test]
        public void SaveLoadFactoryIgnoresJsonAndRegistersAnOwnedOrdinaryState()
        {
            var machine = Machine(); FSMIdentifier id = Name();
            var state = ApplicationStateLoadGameSave.ConstructInstance(machine, id, "not JSON; deliberately ignored");
            Assert.That(state, Is.TypeOf<ApplicationStateLoadGameSave>());
            Assert.That(state.StateId, Is.EqualTo(id.Id));
            Assert.That(machine.States[state.StateId], Is.SameAs(state));
            Assert.That(state.SerialiseRuntimeToJSON(), Is.EqualTo(string.Empty));
            Assert.That(state.GetStateTransitions().Count, Is.EqualTo(0));
            var other = ApplicationStateLoadGameSave.ConstructInstance(machine, Name(), null);
            Assert.That(other, Is.TypeOf<ApplicationStateLoadGameSave>());
            Assert.That(machine.States.Count, Is.EqualTo(2));
        }

        [Test]
        public void SaveLoadNullUserFaultsBeforeTheSaveAndCoroutineBoundary()
        {
            var machine = Machine();
            var state = new ApplicationStateLoadGameSave(machine, Name());
            // Original user.Storage access fails before ProcessManager.GetSystem
            // and CoroutineUtils.RunCoroutine. No nonnull-user entry is attempted.
            Assert.Throws<NullReferenceException>(() => state.OnEnter(null, null));
            Assert.That(machine.States[state.StateId], Is.SameAs(state));
            Assert.That(state.GetStateTransitions().Count, Is.EqualTo(0));
        }

        [Test]
        public void PublisherKeepsOwnedListIdentityOrderNullAndGenuineGuidEquality()
        {
            ApplicationStateEvent first = null, second = null;
            try
            {
                first = ScriptableObject.CreateInstance<ApplicationStateEvent>();
                second = ScriptableObject.CreateInstance<ApplicationStateEvent>();
                first.name = Name(); second.name = Name();
                // Normal GUID base construction leaves both original strings empty.
                // No serialized/private field is changed to manufacture inequality.
                Assert.That(first.GetGUID(), Is.EqualTo(string.Empty));
                Assert.That(second.GetGUID(), Is.EqualTo(string.Empty));
                Assert.That(first.Equals(second), Is.True);
                var user = User(); var machine = Machine();
                var events = new List<ApplicationStateEvent> { null };
                user.Storage.SetValue(AppFSMKeys.StateEvents, events);
                var publish = new ApplicationStatePublishStateMessage(machine, Name(), first);
                publish.OnEnter(user, null); publish.OnEnter(user, null);
                Assert.That(user.Storage.GetValueOnly<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents), Is.SameAs(events));
                Assert.That(events.Count, Is.EqualTo(2));
                Assert.That(ReferenceEquals(events[0], null), Is.True);
                Assert.That(ReferenceEquals(events[1], first), Is.True);
                new ApplicationStatePublishStateMessage(machine, Name(), second).OnEnter(user, null);
                Assert.That(events.Count, Is.EqualTo(2), "original GUID equality prevents adding the distinct empty-GUID object");
                Assert.That(ReferenceEquals(events[1], first), Is.True);
                new ApplicationStatePublishStateMessage(machine, Name(), null).OnEnter(user, null);
                Assert.That(events.Count, Is.EqualTo(2));
                events.Clear(); publish.OnEnter(user, null);
                Assert.That(events.Count, Is.EqualTo(1));
                Assert.That(ReferenceEquals(events[0], first), Is.True);
                Assert.That(user.Storage.GetCollection()[AppFSMKeys.StateEvents], Is.SameAs(events));
            }
            finally
            {
                try { if (!ReferenceEquals(second, null)) UnityEngine.Object.DestroyImmediate(second); }
                finally { if (!ReferenceEquals(first, null)) UnityEngine.Object.DestroyImmediate(first); }
            }
        }

        [Test]
        public void PublisherInsertsDefaultNullBeforeItsOriginalListFault()
        {
            var machine = Machine(); var user = User();
            var state = new ApplicationStatePublishStateMessage(machine, Name(), null);
            Assert.That(user.Storage.GetCollection().ContainsKey(AppFSMKeys.StateEvents), Is.False);
            Assert.Throws<NullReferenceException>(() => state.OnEnter(user, null));
            Assert.That(user.Storage.GetCollection().ContainsKey(AppFSMKeys.StateEvents), Is.True);
            Assert.That(user.Storage.GetCollection()[AppFSMKeys.StateEvents], Is.Null);
            var empty = new List<ApplicationStateEvent>();
            user.Storage.SetValue(AppFSMKeys.StateEvents, empty); state.OnEnter(user, null);
            Assert.That(empty.Count, Is.EqualTo(1)); Assert.That(ReferenceEquals(empty[0], null), Is.True);
            var factory = ApplicationStatePublishStateMessage.ConstructInstance(machine, Name(), "{\"EventId\":\"\"}");
            Assert.That(factory, Is.TypeOf<ApplicationStatePublishStateMessage>());
            Assert.That(Read<ApplicationStateEvent>(factory, typeof(ApplicationStatePublishStateMessage), "m_eventId"), Is.Null);
            factory.OnEnter(user, null);
            Assert.That(empty.Count, Is.EqualTo(1));
            Assert.That(user.Storage.GetCollection()[AppFSMKeys.StateEvents], Is.SameAs(empty));
        }

        [Test]
        public void AudioFactoriesCaptureEnumsFlagsAndTheOriginalReplayInitializer()
        {
            var machine = Machine(); FSMIdentifier id = Name();
            var defaults = ApplicationStatePlayAudio.ConstructInstance(machine, id,
                "{\"Clip\":\"None\",\"Source\":\"None\",\"OneShot\":true}");
            Assert.That(defaults, Is.TypeOf<ApplicationStatePlayAudio>());
            Assert.That(machine.States[id.Id], Is.SameAs(defaults));
            Assert.That(Read<HLAudioClipIdentifier>(defaults, typeof(ApplicationStatePlayAudio), "m_clip"), Is.EqualTo(HLAudioClipIdentifier.None));
            Assert.That(Read<HLAudioSourceIdentifier>(defaults, typeof(ApplicationStatePlayAudio), "m_source"), Is.EqualTo(HLAudioSourceIdentifier.None));
            Assert.That(Read<bool>(defaults, typeof(ApplicationStatePlayAudio), "m_oneShot"), Is.True);
            Assert.That(Read<bool>(defaults, typeof(ApplicationStatePlayAudio), "m_replay"), Is.True);
            var explicitFlags = ApplicationStatePlayAudio.ConstructInstance(machine, Name(),
                "{\"Clip\":\"77\",\"Source\":\"-17\",\"OneShot\":false,\"Replay\":false}");
            Assert.That((int)Read<HLAudioClipIdentifier>(explicitFlags, typeof(ApplicationStatePlayAudio), "m_clip"), Is.EqualTo(77));
            Assert.That((int)Read<HLAudioSourceIdentifier>(explicitFlags, typeof(ApplicationStatePlayAudio), "m_source"), Is.EqualTo(-17));
            Assert.That(Read<bool>(explicitFlags, typeof(ApplicationStatePlayAudio), "m_oneShot"), Is.False);
            Assert.That(Read<bool>(explicitFlags, typeof(ApplicationStatePlayAudio), "m_replay"), Is.False);
            // Field reads observe normally constructed originals. Entry is held:
            // it would obtain genuine AudioManager and may execute audio services.
        }

        [Test]
        public void AudioEnumParseFailuresPrecedeOwnedStateRegistration()
        {
            var machine = Machine(); int count = machine.States.Count;
            Assert.Throws<ArgumentException>(() => ApplicationStatePlayAudio.ConstructInstance(machine, Name(),
                "{\"Clip\":\"lucid_not_a_real_clip\",\"Source\":\"None\"}"));
            Assert.That(machine.States.Count, Is.EqualTo(count));
            Assert.Throws<ArgumentException>(() => ApplicationStatePlayAudio.ConstructInstance(machine, Name(),
                "{\"Clip\":\"None\",\"Source\":\"lucid_not_a_real_source\"}"));
            Assert.That(machine.States.Count, Is.EqualTo(count));
            Assert.Throws<ArgumentNullException>(() => ApplicationStatePlayAudio.ConstructInstance(machine, Name(),
                "{\"Clip\":\"None\"}"));
            Assert.That(machine.States.Count, Is.EqualTo(count));
        }

        [Test]
        public void UIFactoriesRetainNullLookupsAndTheRealGraphUserCastFault()
        {
            var machine = Machine(); var user = User();
            var message = ApplicationTransitionUIMessage.ConstructInstance(machine, Name(), "{\"EventId\":null}");
            var close = ApplicationTransitionUIMessageCloseUIContainer.ConstructInstance(machine, Name(),
                "{\"Container\":\"\",\"EventId\":\"\"}");
            Assert.That(message, Is.TypeOf<ApplicationTransitionUIMessage>());
            Assert.That(close, Is.TypeOf<ApplicationTransitionUIMessageCloseUIContainer>());
            Assert.That(Read<UIModernEvent>(message, typeof(ApplicationTransitionUIMessage), "m_eventId"), Is.Null);
            Assert.That(Read<UIModernEvent>(close, typeof(ApplicationTransitionUIMessage), "m_eventId"), Is.Null);
            Assert.That(Read<UIContainerIdentifier>(close, typeof(ApplicationTransitionUIMessageCloseUIContainer), "m_container"), Is.Null);
            Assert.That(machine.Transitions[message.TransitionId], Is.SameAs(message));
            Assert.That(machine.Transitions[close.TransitionId], Is.SameAs(close));
            // A genuine FSMUser is not an App. The original cast fails before
            // obtaining App's list or reaching UIManager.Close; no fake App is used.
            Assert.Throws<InvalidCastException>(() => message.Update(user, default));
            Assert.Throws<InvalidCastException>(() => close.Update(user, default));
            Assert.Throws<NullReferenceException>(() => message.Update(null, default));
            Assert.Throws<NullReferenceException>(() => close.Update(null, default));
            Assert.That(user.Storage.GetCollection().Count, Is.EqualTo(0));
        }

        [Test]
        public void MainMenuFamiliesRetainOriginalDeclarationsAndJsonFieldContracts()
        {
            Type[] families = { typeof(ApplicationStateLoadGameSave), typeof(ApplicationStateEnableMenuRenderScale),
                typeof(ApplicationStatePlayAudio), typeof(ApplicationStatePublishStateMessage),
                typeof(ApplicationTransitionUIMessage), typeof(ApplicationTransitionUIMessageCloseUIContainer) };
            Option[][] order = { new[] { Option.ArrayBoundsChecks, Option.NullChecks }, new[] { Option.ArrayBoundsChecks, Option.NullChecks },
                new[] { Option.ArrayBoundsChecks, Option.NullChecks }, new[] { Option.NullChecks, Option.ArrayBoundsChecks },
                new[] { Option.ArrayBoundsChecks, Option.NullChecks }, new[] { Option.NullChecks, Option.ArrayBoundsChecks } };
            for (int i = 0; i < families.Length; i++)
            {
                Type type = families[i]; Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
                Assert.That(type.Namespace, Is.EqualTo("HardlightProject")); Assert.That(type.IsSealed, Is.False);
                Assert.That(type.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Application/{0}"));
                var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Assert.That(options.Select(o => o.Option).ToArray(), Is.EqualTo(order[i]));
                Assert.That(options.All(o => Equals(o.Value, false)), Is.True);
            }
            Type audio = Dto(typeof(ApplicationStatePlayAudio)); Assert.That(audio.IsNestedPrivate, Is.True);
            Assert.That(audio.IsSerializable, Is.True);
            AssertFields(audio, new[] { "Clip", "Source", "OneShot", "Replay" }, new[] { typeof(string), typeof(string), typeof(bool), typeof(bool) });
            Assert.That(audio.GetField("Clip").GetCustomAttribute<GraphEnumPopupAttribute>().EnumType, Is.EqualTo(typeof(HLAudioClipIdentifier)));
            Assert.That(audio.GetField("Source").GetCustomAttribute<GraphEnumPopupAttribute>().EnumType, Is.EqualTo(typeof(HLAudioSourceIdentifier)));
            foreach (Type parent in new[] { typeof(ApplicationStatePublishStateMessage), typeof(ApplicationTransitionUIMessage), typeof(ApplicationTransitionUIMessageCloseUIContainer) })
            { Assert.That(Dto(parent).IsNestedFamily, Is.True); Assert.That(Dto(parent).IsSerializable, Is.True); }
            AssertFields(Dto(typeof(ApplicationStatePublishStateMessage)), new[] { "EventId" }, new[] { typeof(string) });
            AssertFields(Dto(typeof(ApplicationTransitionUIMessage)), new[] { "EventId" }, new[] { typeof(string) });
            AssertFields(Dto(typeof(ApplicationTransitionUIMessageCloseUIContainer)), new[] { "EventId", "Container" }, new[] { typeof(string), typeof(string) });
            Assert.That(Dto(typeof(ApplicationTransitionUIMessageCloseUIContainer)), Is.Not.EqualTo(Dto(typeof(ApplicationTransitionUIMessage))));
            var managerRef = typeof(ApplicationStateEnableMenuRenderScale).GetField("m_visualQualityManagerRef", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(managerRef.FieldType, Is.EqualTo(typeof(SystemRef<VisualQualityManager_SDT>)));
            Assert.That(managerRef.IsPrivate && managerRef.IsInitOnly, Is.True);
            // No render-scale constructor/entry is called: its original field
            // initializer acquires a shared SystemRef, and entry uses real cameras.
        }

        [Test]
        public void SaveCoroutineRetainsTheOriginalNaturalDeclarationShape()
        {
            MethodInfo method = typeof(ApplicationStateLoadGameSave).GetMethod("OpenSave", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.That(method, Is.Not.Null); Assert.That(method.ReturnType, Is.EqualTo(typeof(IEnumerator)));
            Assert.That(method.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(SaveManager), typeof(IGraphUser) }));
            Assert.That(method.GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "saveManager", "user" }));
            var iterator = method.GetCustomAttribute<IteratorStateMachineAttribute>(false);
            Assert.That(iterator, Is.Not.Null); Type child = iterator.StateMachineType;
            Assert.That(child.DeclaringType, Is.EqualTo(typeof(ApplicationStateLoadGameSave)));
            Assert.That(child.Name, Is.EqualTo("<OpenSave>d__3"));
            Assert.That(child.IsNestedPrivate && child.IsSealed, Is.True);
            Assert.That(child.GetCustomAttribute<CompilerGeneratedAttribute>(false), Is.Not.Null);
            Assert.That(typeof(IEnumerator).IsAssignableFrom(child), Is.True);
            Assert.That(typeof(IEnumerator<object>).IsAssignableFrom(child), Is.True);
            Assert.That(typeof(IDisposable).IsAssignableFrom(child), Is.True);
            var fields = child.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(fields.Length, Is.EqualTo(4));
            Assert.That(child.GetField("saveManager", BindingFlags.Instance | BindingFlags.Public).FieldType, Is.EqualTo(typeof(SaveManager)));
            Assert.That(child.GetField("user", BindingFlags.Instance | BindingFlags.Public).FieldType, Is.EqualTo(typeof(IGraphUser)));
            Assert.That(child.GetField("<>1__state", BindingFlags.Instance | BindingFlags.NonPublic).FieldType, Is.EqualTo(typeof(int)));
            Assert.That(child.GetField("<>2__current", BindingFlags.Instance | BindingFlags.NonPublic).FieldType, Is.EqualTo(typeof(object)));
            // Metadata only. OpenSave, MoveNext, SaveManager and coroutine hosts
            // remain uninvoked; readiness/save callback and IO behavior are held.
        }

        private static T Read<T>(object owner, Type declaration, string name)
        {
            FieldInfo field = declaration.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null); Assert.That(field.IsPrivate && field.IsInitOnly, Is.True);
            Assert.That(field.FieldType, Is.EqualTo(typeof(T)));
            return (T)field.GetValue(owner); // Read only; never assign a private field.
        }
        private static Type Dto(Type owner)
        {
            Type dto = owner.GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(dto, Is.Not.Null); return dto;
        }
        private static void AssertFields(Type dto, string[] names, Type[] types)
        {
            var fields = dto.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(names));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(types));
        }
    }
}
