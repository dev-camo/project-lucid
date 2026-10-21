using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.UI.Binding;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.BreakableObject (TypeDef 0x02000abc, x86-64 0x6E2E70..0x6E2F40),
    // ActorVisualProxyModifier (0x020009df, 0x6A6660..0x6A66E0), ActorAnimationTrigger<T>
    // (0x0200076a, shared bodies 0xF19E70..0xF1A480) and AirRechargePickup (0x02000ab9,
    // 0x6E1100..0x6E2300 with its closure 0x02000aba). Components sit on owned objects (Edit Mode
    // runs no Awake or OnEnable); every registry row a constructor reads is leased. Covered directly for
    // ActorAnimationTrigger<T>: its bookkeeping (enable and disable resets, the filter, the pending list
    // and the effect-handle dictionary on enter, leave and disable). Still needs a started Actor (PlayMode):
    // what TryPlayAnimation, Action_OnLeave and OnDisable make the actor do, including which handle list
    // reaches it. Actor.TriggerAnimationEnter and TriggerAnimationLeave are not virtual and return at once
    // until Actor.Initialise (060002bd) has set m_initialised, so no directly callable method reaches that
    // behaviour; and Initialise cannot run here, because its TimeManager subscription needs a TimeManager
    // that has run Awake (which registers SystemConfiguration and changes Time.timeScale), and its FSM must
    // be acquired through the FSMManager. The flag has no other writer, and forcing it would fake the readiness.
    public sealed class OriginalCharCorePickupTests
    {
        // ---- BreakableObject ----

        [Test]
        public void BreakableObjectDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(BreakableObject).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(BreakableObject).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeRow(typeof(BreakableObject)), Is.EqualTo("Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False)"));
            Assert.That(Fields(typeof(BreakableObject)), Is.EqualTo(new[]
            {
                "m_audioSfxComponent:HLAudioSfxComponent:0001", "m_prefabSpawner:PrefabSpawner:0001",
                "m_objectsToHide:GameObject[]:0001", "m_onBreakEvent:UnityEvent:0001"
            }));
            Assert.That(Methods(typeof(BreakableObject)), Is.EqualTo(new[] { "Break:0086", ".ctor:1886" }));
            Assert.That(AttributeRow(typeof(BreakableObject).GetField("m_audioSfxComponent", BindingFlags.Instance | BindingFlags.NonPublic)),
                Is.EqualTo("SerializeField() HeaderAttribute(Optional components)"));
        }

        // Break 0x6E2E70: when the audio component is not Unity-null it plays PlayOneShotAtSource
        // (0x6E2ECB), when the spawner is not Unity-null it spawns (0x6E2EFB), then every object to hide is
        // deactivated in array order (SetActive(false), 0x6E2F19) and the break event is raised last
        // (0x6E2F3A). The audio request is read from the audio manager's own diagnostic: clip 0 at
        // source Sfx reports that no clip was specified.
        [Test]
        public void BreakPlaysSpawnsHidesAndRaisesTheEventInOrder()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                var breakable = owned.Add<BreakableObject>();
                var sfx = owned.Add<HLAudioSfxComponent>();
                Set(sfx, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                Set(breakable, "m_audioSfxComponent", sfx);

                var spawner = owned.Add<PrefabSpawner>();
                GameObject prefab = owned.Host("prefab");
                Set(spawner, "m_prefabsToSpawn", new[] { prefab });
                Set(spawner, "m_spawnPoint", spawner.transform);
                Set(spawner, "m_parentToSpawnPoint", true);
                Set(breakable, "m_prefabSpawner", spawner);

                GameObject first = owned.Host("first");
                GameObject second = owned.Host("second");
                first.SetActive(true);
                second.SetActive(true);
                Set(breakable, "m_objectsToHide", new[] { first, second });

                var log = new List<string>();
                var breakEvent = new UnityEvent();
                breakEvent.AddListener(() => log.Add("event: first " + first.activeSelf + ", second " + second.activeSelf +
                    ", spawned " + spawner.transform.childCount));
                Set(breakable, "m_onBreakEvent", breakEvent);

                breakable.Break();
                Assert.That(owned.Log.Errors, Is.EqualTo(new[] { "Will not play the given audio clip at source Sfx, no clip was specified.:0" }));
                Assert.That(first.activeSelf, Is.False);
                Assert.That(second.activeSelf, Is.False);
                Assert.That(spawner.transform.childCount, Is.EqualTo(1));
                Assert.That(log, Is.EqualTo(new[] { "event: first False, second False, spawned 1" }), "the event comes after the rest");

                // A second break repeats it all.
                owned.Log.Errors.Clear();
                breakable.Break();
                Assert.That(owned.Log.Errors.Count, Is.EqualTo(1));
                Assert.That(spawner.transform.childCount, Is.EqualTo(2));
                Assert.That(log.Count, Is.EqualTo(2));
            }
        }

        // Destroyed optional components fail the op_Inequality tests (0x6E2EBC, 0x6E2EEC) and are skipped.
        [Test]
        public void BreakSkipsDestroyedOptionalComponents()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                var breakable = owned.Add<BreakableObject>();
                var sfx = owned.Add<HLAudioSfxComponent>();
                var spawner = owned.Add<PrefabSpawner>();
                Set(spawner, "m_prefabsToSpawn", new[] { owned.Host("prefab") });
                Set(spawner, "m_spawnPoint", spawner.transform);
                Set(breakable, "m_audioSfxComponent", sfx);
                Set(breakable, "m_prefabSpawner", spawner);
                Set(breakable, "m_objectsToHide", new GameObject[0]);
                Set(breakable, "m_onBreakEvent", new UnityEvent());
                Object.DestroyImmediate(sfx);
                Object.DestroyImmediate(spawner);

                Assert.DoesNotThrow(breakable.Break);
                Assert.That(owned.Log.Errors, Is.Empty);
            }
        }

        // ---- ActorVisualProxyModifier ----

        [Test]
        public void ActorVisualProxyModifierDeclarationsKeepTheOriginalFlagsAndMethods()
        {
            Assert.That((int)typeof(ActorVisualProxyModifier).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(ActorVisualProxyModifier).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeRow(typeof(ActorVisualProxyModifier)), Is.EqualTo("Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False)"));
            Assert.That(Fields(typeof(ActorVisualProxyModifier)), Is.Empty);
            Assert.That(Methods(typeof(ActorVisualProxyModifier)), Is.EqualTo(new[]
            {
                "ShowVisualProxy:0084", "HideVisualProxy:0084", "DisableVisualProxy:0084", "EnableVisualProxy:0084", ".ctor:1886"
            }));
        }

        // ShowVisualProxy 0x6A6660 and HideVisualProxy 0x6A6680 tail-call Actor.ToggleFormRenderers
        // with 1 and 0; EnableVisualProxy 0x6A66C0 and DisableVisualProxy 0x6A66A0 tail-call
        // Actor.SetVisualProxyActive with 1 and 0. The character comes from its real constructor, its
        // form handler (which has no forms) is a real component and its visual proxy a real
        // SmoothedTransformProxy.
        [Test]
        public void TheModifierTogglesTheFormRenderersAndTheVisualProxy()
        {
            using (var islandRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>()))
            using (var objects = new OwnedWorldObjects("lucid-visual-proxy"))
            {
                GameObject characterHost = objects.Create("character");
                var character = characterHost.AddComponent<CharacterGravityProbe>();
                Set(character, "m_formHandler", characterHost.AddComponent<ActorFormHandler>());
                GameObject proxyHost = objects.Create("proxy");
                character.AssignVisualProxy(proxyHost.AddComponent<SmoothedTransformProxy>());
                var modifier = objects.Create("modifier").AddComponent<ActorVisualProxyModifierProbe>();

                Assert.That(character.FormRenderersOn, Is.True);
                modifier.Hide(character);
                Assert.That(character.FormRenderersOn, Is.False);
                modifier.Show(character);
                Assert.That(character.FormRenderersOn, Is.True);

                Assert.That(proxyHost.activeSelf, Is.True);
                modifier.Disable(character);
                Assert.That(proxyHost.activeSelf, Is.False);
                modifier.Enable(character);
                Assert.That(proxyHost.activeSelf, Is.True);
                Assert.That(character.FormRenderersOn, Is.True, "the proxy switch leaves the renderers alone");
            }
        }

        // ---- ActorAnimationTrigger<T> ----

        private sealed class TriggerScene : IDisposable
        {
            private readonly VerificationFixtureOwnership.LifecycleRow m_islandRow =
                new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>());
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-actor-animation-trigger");
            public readonly ActorAnimationTriggerProbe Trigger;
            public readonly Character First;
            public readonly Character Second;

            public TriggerScene()
            {
                try
                {
                    Trigger = Objects.Create("trigger").AddComponent<ActorAnimationTriggerProbe>();
                    Set(Trigger, "m_animation", Objects.Own(ScriptableObject.CreateInstance<ActorAnimationDefinition>()));
                    First = Objects.Create("first").AddComponent<Character>();
                    Second = Objects.Create("second").AddComponent<Character>();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public IList Pending => (IList)Get(Trigger, "m_actorsPendingAnimationLeave");
            public IDictionary Effects => (IDictionary)Get(Trigger, "m_actorEffectDictionary");

            public void Dispose()
            {
                Objects.Dispose();
                m_islandRow.Dispose();
            }
        }

        [Test]
        public void AnimationTriggerDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Type definition = typeof(ActorAnimationTrigger<>);
            Assert.That((int)definition.Attributes, Is.EqualTo(0x00100081));
            Assert.That(definition.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(definition.GetGenericArguments()[0].GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(Actor) }));
            Assert.That(AttributeRow(definition), Is.EqualTo("Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)"));
            Assert.That(Fields(definition), Is.EqualTo(new[]
            {
                "m_animation:ActorAnimationDefinition:0001", "m_onEnterOnly:Boolean:0001", "m_actorsPendingAnimationLeave:List`1:0021",
                "m_actorEffectDictionary:Dictionary`2:0021", "m_enabled:Boolean:0001"
            }));
            Assert.That(Methods(definition), Is.EqualTo(new[]
            {
                "OnEnable:0086", "OnDisable:0086", "Action_OnEnter:0086", "Action_OnEnter:0086", "FilterActor:05c4", "Action_OnLeave:0086",
                "Action_OnLeave:0086", "TryPlayAnimation:0081", ".ctor:1884"
            }));
            Assert.That(AttributeRow(definition.GetField("m_onEnterOnly", BindingFlags.Instance | BindingFlags.NonPublic)), Is.EqualTo(
                "SerializeField() TooltipAttribute(If true, only plays TriggerAnimationEnter for actors, and does not keep a list of actors currently playing.)"));
        }

        // Constructor 0xF1A480 builds the list (+0x30) and the dictionary (+0x38); m_enabled (+0x40)
        // stays false until OnEnable 0xF19E70, which also empties both. OnDisable 0xF19EC0 empties
        // them in the opposite order and clears the flag.
        [Test]
        public void EnablingAndDisablingResetTheBookkeeping()
        {
            using (var scene = new TriggerScene())
            {
                Assert.That(scene.Pending, Is.Empty);
                Assert.That(scene.Effects, Is.Empty);
                Assert.That(Get(scene.Trigger, "m_enabled"), Is.False);
                Assert.That(Get(scene.Trigger, "m_onEnterOnly"), Is.False);

                scene.Pending.Add(scene.First);
                scene.Effects.Add(scene.First, new List<FullscreenShaderManager.ParametersHandle>());
                scene.Trigger.OnEnable();
                Assert.That(Get(scene.Trigger, "m_enabled"), Is.True);
                Assert.That(scene.Pending, Is.Empty);
                Assert.That(scene.Effects, Is.Empty);

                scene.Pending.Add(scene.First);
                scene.Effects.Add(scene.First, new List<FullscreenShaderManager.ParametersHandle>());
                scene.Trigger.OnDisable();
                Assert.That(Get(scene.Trigger, "m_enabled"), Is.False);
                Assert.That(scene.Pending, Is.Empty);
                Assert.That(scene.Effects, Is.Empty);

                Set(scene.Trigger, "m_onEnterOnly", true);
                scene.Pending.Add(scene.Second);
                scene.Trigger.OnDisable();
                Assert.That(scene.Pending, Is.Empty, "cleared even when only enters are tracked");
            }
        }

        // Action_OnEnter(T) 0xF1A060: nothing while disabled (m_enabled, 0xF1A06A); then the filter
        // (vtable slot 4); then TryPlayAnimation 0xF1A380, which skips an actor already pending, takes
        // the actor's effect-handle list from the dictionary (TryGetOrNew) and, unless only enters
        // are tracked, adds the actor to the pending list.
        [Test]
        public void EnteringTracksAcceptedActorsOnceAndOnlyWhileEnabled()
        {
            using (var scene = new TriggerScene())
            {
                scene.Trigger.Action_OnEnter(scene.First);
                Assert.That(scene.Trigger.FilterCalls, Is.Zero, "disabled: the filter is not asked");
                Assert.That(scene.Pending, Is.Empty);

                scene.Trigger.OnEnable();
                scene.Trigger.Filter = actor => actor == scene.First;
                scene.Trigger.Action_OnEnter(scene.Second);
                Assert.That(scene.Trigger.FilterCalls, Is.EqualTo(1));
                Assert.That(scene.Pending, Is.Empty, "refused by the filter");
                Assert.That(scene.Effects, Is.Empty);

                scene.Trigger.Action_OnEnter(scene.First);
                Assert.That(scene.Pending, Is.EqualTo(new[] { scene.First }));
                Assert.That(scene.Effects.Contains(scene.First), Is.True);
                object handles = scene.Effects[scene.First];
                Assert.That(handles, Is.Empty.And.InstanceOf<List<FullscreenShaderManager.ParametersHandle>>());

                scene.Trigger.Action_OnEnter(scene.First);
                Assert.That(scene.Pending.Count, Is.EqualTo(1), "already pending");
                Assert.That(scene.Effects[scene.First], Is.SameAs(handles), "the handle list is kept");
                Assert.That(scene.Trigger.FilterCalls, Is.EqualTo(3), "the filter is asked before the pending test");

                // Only enters tracked: nothing is queued, and the same handle list is reused.
                Set(scene.Trigger, "m_onEnterOnly", true);
                scene.Trigger.Filter = _ => true;
                scene.Trigger.Action_OnEnter(scene.Second);
                scene.Trigger.Action_OnEnter(scene.Second);
                Assert.That(scene.Pending.Count, Is.EqualTo(1));
                Assert.That(scene.Effects.Contains(scene.Second), Is.True);
            }
        }

        // Action_OnLeave(T) 0xF1A1C0 needs m_enabled, not m_onEnterOnly and a successful
        // List.Remove (0xF1A1FD) before it leaves the actor; the effect list stays in the dictionary.
        [Test]
        public void LeavingForgetsOnlyPendingActors()
        {
            using (var scene = new TriggerScene())
            {
                scene.Trigger.OnEnable();
                scene.Trigger.Action_OnEnter(scene.First);
                scene.Trigger.Action_OnEnter(scene.Second);
                Assert.That(scene.Pending.Count, Is.EqualTo(2));

                scene.Trigger.Action_OnLeave(scene.First);
                Assert.That(scene.Pending, Is.EqualTo(new[] { scene.Second }));
                Assert.That(scene.Effects.Contains(scene.First), Is.True, "the dictionary keeps its entry");
                scene.Trigger.Action_OnLeave(scene.First);
                Assert.That(scene.Pending.Count, Is.EqualTo(1), "an actor not pending is ignored");

                Set(scene.Trigger, "m_onEnterOnly", true);
                scene.Trigger.Action_OnLeave(scene.Second);
                Assert.That(scene.Pending.Count, Is.EqualTo(1), "only enters tracked: leaving is ignored");
                Set(scene.Trigger, "m_onEnterOnly", false);
                scene.Trigger.OnDisable();
                scene.Pending.Add(scene.Second);
                scene.Trigger.Action_OnLeave(scene.Second);
                Assert.That(scene.Pending.Count, Is.EqualTo(1), "disabled: leaving is ignored");
            }
        }

        // The ActorComponentLookup overloads (0xF1A0C0, 0xF1A250) take the lookup's actor (+0x50), cast it
        // to T (IsInst, 0xF1A108) and do nothing for a missing actor or while disabled (m_enabled test at
        // 0xF1A0CE, before the actor is read).
        [Test]
        public void ComponentLookupOverloadsForwardTheLookupsActor()
        {
            using (var scene = new TriggerScene())
            {
                var lookup = scene.Objects.Create("lookup").AddComponent<ActorComponentLookup>();
                lookup.Initialise(scene.First);

                scene.Trigger.Action_OnEnter(lookup);
                Assert.That(scene.Trigger.FilterCalls, Is.Zero, "disabled");

                scene.Trigger.OnEnable();
                scene.Trigger.Action_OnEnter(lookup);
                Assert.That(scene.Pending, Is.EqualTo(new[] { scene.First }));
                scene.Trigger.Action_OnLeave(lookup);
                Assert.That(scene.Pending, Is.Empty);

                var empty = scene.Objects.Create("empty lookup").AddComponent<ActorComponentLookup>();
                int filterCalls = scene.Trigger.FilterCalls;
                scene.Trigger.Action_OnEnter(empty);
                scene.Trigger.Action_OnLeave(empty);
                Assert.That(scene.Trigger.FilterCalls, Is.EqualTo(filterCalls), "no actor: nothing asked");
                Assert.That(scene.Pending, Is.Empty);
            }
        }

        // ---- AirRechargePickup ----

        private sealed class PickupScene : IDisposable
        {
            private readonly List<IDisposable> m_leases = new List<IDisposable>();
            public readonly OwnedTimeScaledObjects Time;
            public readonly AirRechargePickup Pickup;
            public readonly Animation Animation;
            public readonly AnimationClip Idle, Collect, Cooldown;
            public readonly ParticleSystem Idling;
            public readonly List<string> Log = new List<string>();
            public OwnedCharacterManagers Characters;
            public CharacterManager Manager;
            public Character Character;
            public MessageManager Messages;

            // Rows the pickup's field initialisers read, then the pickup itself. With characters, a
            // real CharacterManager holding a real Character is registered under its row first. With
            // messages (which needs characters, for the Shutdown action rows), a real MessageManager is
            // registered as well.
            public PickupScene(bool withCharacter, float collectLength = 2f, bool withMessages = false)
            {
                try
                {
                    if (withMessages && !withCharacter)
                        throw new ArgumentException("messages need the character fixture's action rows");
                    if (withCharacter)
                    {
                        Characters = new OwnedCharacterManagers();
                        m_leases.Add(Characters);
                    }
                    Row<GameplayIslandManager>();
                    if (withCharacter)
                    {
                        Manager = Characters.NewManager();
                        var characterRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CharacterManager>());
                        m_leases.Add(characterRow);
                        characterRow.Register(Manager);
                    }
                    else
                    {
                        Row<CharacterManager>();
                    }
                    if (!withCharacter)
                        Row<App>();
                    Row<CinemachineCameraManager>();
                    if (withMessages)
                        RegisterMessageManager();
                    else
                        Row<MessageManager>();
                    Row<ParticleEffectManager>();
                    Row<TimeManager>();
                    Time = new OwnedTimeScaledObjects("lucid-air-recharge");
                    m_leases.Add(Time);

                    if (withCharacter)
                    {
                        Character = Time.Add<Character>("character");
                        Set(Manager, "m_currentCharacter", Character);
                    }
                    Pickup = Time.Add<AirRechargePickup>("pickup");
                    Animation = Pickup.gameObject.AddComponent<Animation>();
                    Idle = NewClip("idle", 1f);
                    Collect = NewClip("collect", collectLength);
                    Cooldown = NewClip("cooldown", 3f);
                    Set(Pickup, "m_animation", Animation);
                    Set(Pickup, "m_animationIdle", Idle);
                    Set(Pickup, "m_animationCollect", Collect);
                    Set(Pickup, "m_animationCooldown", Cooldown);
                    Idling = Time.Add<ParticleSystem>("idle particles");
                    Set(Pickup, "m_pfxIdle", Idling);
                    Set(Pickup, "m_pfxSpawnerCollect", NewSpawner("collect effect"));
                    Set(Pickup, "m_pfxSpawnerRecharged", NewSpawner("recharged effect"));
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private void Row<T>() where T : class, ISystem =>
                m_leases.Add(new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<T>()));

            // The manager subscribes itself and its exchanges to Shutdown; they are shut down and
            // unsubscribed before the row is released.
            private void RegisterMessageManager()
            {
                var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<MessageManager>());
                m_leases.Add(row);
                Messages = new MessageManager();
                MessageManager manager = Messages;
                m_leases.Add(new Release(() =>
                {
                    manager.ProcessSystemAction(SystemAction.Shutdown);
                    ProcessManager.UnsubscribeFromAllActions(manager);
                    ProcessManager.UnsubscribeFromAllActions(manager.ComponentMessagesWithCompletion);
                    ProcessManager.UnsubscribeFromAllActions(manager.StringMessagesWithCompletion);
                    ProcessManager.UnsubscribeFromAllActions(manager.ComponentMessagesWithoutCompletion);
                    ProcessManager.UnsubscribeFromAllActions(manager.StringMessagesWithoutCompletion);
                    ProcessManager.UnsubscribeFromAllActions(manager.ScriptableObjectMessagesWithoutCompletion);
                }));
                row.Register(manager);
            }

            private sealed class Release : IDisposable
            {
                private readonly Action m_release;
                public Release(Action release) { m_release = release; }
                public void Dispose() => m_release();
            }

            private AnimationClip NewClip(string name, float length)
            {
                var clip = Time.Objects.Own(new AnimationClip { name = name, legacy = true });
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, length, 1f));
                Animation.AddClip(clip, name);
                return clip;
            }

            // With no ParticleEffectManager, Spawn() invokes the stored completion callback and returns
            // (0x06003cf0), which is how a spawn is seen here.
            private ParticleEffectSpawner NewSpawner(string name)
            {
                var spawner = Time.Add<ParticleEffectSpawner>(name);
                Set(spawner, "m_onCompleteCallback", (Action)(() => Log.Add(name)));
                return spawner;
            }

            public void Dispose()
            {
                for (int i = m_leases.Count - 1; i >= 0; i--)
                    m_leases[i].Dispose();
                m_leases.Clear();
            }
        }

        [Test]
        public void PickupDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(AirRechargePickup).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(AirRechargePickup).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Assert.That(Fields(typeof(AirRechargePickup)), Is.EqualTo(new[]
            {
                "m_characterArchetype:CharacterArchetype:0001", "m_animation:Animation:0001", "m_animationIdle:AnimationClip:0001",
                "m_animationCollect:AnimationClip:0001", "m_animationCooldown:AnimationClip:0001", "m_pfxIdle:ParticleSystem:0001",
                "m_pfxSpawnerCollect:ParticleEffectSpawner:0001", "m_pfxSpawnerRecharged:ParticleEffectSpawner:0001",
                "m_rechargeTimeSeconds:Single:0001", "m_useCustomRenderBounds:Boolean:0001", "m_cullingBoundsSize:Single:0001",
                "m_particleRenderers:Renderer[]:0001", "IsValid:Bindable`1:0026", "m_appRef:SystemRef`1:0021",
                "m_characterManagerRef:SystemRef`1:0021", "m_cinemachineCameraManagerRef:SystemRef`1:0021",
                "m_messageManagerRef:SystemRef`1:0021", "m_enterIdleTimeRemainingSeconds:Single:0001",
                "m_enterCooldownAnimationTimeRemainingSeconds:Single:0001", "m_cullingGroup:CullingGroup:0001",
                "m_detectorArchetypeMessage:DetectorArchetypeUpdateMessage:0001"
            }));
            Assert.That(Methods(typeof(AirRechargePickup)), Is.EqualTo(new[]
            {
                "OnEnable:00c4", "OnDisable:00c4", "OnDetectorActive:0081", "OnDetectorCharacterActive:0081", "InternalUpdate:00c4",
                "SetAnimation:0081", "Action_CharacterEnter:0086", "Action_CharacterStay:0086", "Action_CharacterExit:0086",
                "EnterIdleState:0081", "EnterCollectState:0081", "RechargeAbilities:0081", "GetCharacter:0081",
                "GetDefaultTimeCategoryEnum:00c4", "GetDefaultUpdateOn:00c4", "SetupCustomCulling:0081", "OnStateChanged:0081", "Cull:0081",
                "SetRenderers:0081", "OnValidate:00c4", ".ctor:1886", "<OnEnable>b__21_0:0081"
            }));
            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(typeof(AirRechargePickup).GetField("m_rechargeTimeSeconds", field)), Is.EqualTo(
                "TooltipAttribute(How long to wait after character has left collision to re-enter the idle visual state and play the recharged PFX) SerializeField() MinAttribute(0)"));
            Assert.That(AttributeRow(typeof(AirRechargePickup).GetField("m_cullingBoundsSize", field)), Is.EqualTo(
                "ShowIfAttribute(m_useCustomRenderBounds,null) SerializeField()"));
            Assert.That(AttributeRow(typeof(AirRechargePickup).GetField("m_particleRenderers", field)), Is.EqualTo(
                "TooltipAttribute(Auto-populated when subsystem list regenerated, if using custom render bounds.) SerializeField() ShowIfAttribute(m_useCustomRenderBounds,null)"));
            Assert.That(AttributeRow(typeof(AirRechargePickup)), Is.Empty, "no Il2CppSetOption attributes");

            Type closure = typeof(AirRechargePickup).GetNestedType("<>c__DisplayClass32_0", BindingFlags.NonPublic);
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00100103));
            Assert.That(Fields(closure), Is.EqualTo(new[] { "isExit:Boolean:0006" }));
            Assert.That(Methods(closure), Is.EqualTo(new[] { ".ctor:1886", "<RechargeAbilities>b__0:0083" }));
            Assert.That(closure.GetMethod("<RechargeAbilities>b__0", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(CharacterAbility_Air)));
        }

        // Constructor 0x6E2040: Flying (0x453342BA) at +0x50, a culling bounds size of 2 (0x40000000,
        // +0x98), a new Bindable<bool> and the four typed references. 0x6E1B80 and 0x6E1B90 return the
        // Environment category (0x34DAB335) and the Update phase (0x97220D01).
        [Test]
        public void PickupConstructorStoresTheNativeDefaults()
        {
            using (var scene = new PickupScene(false))
            {
                Assert.That((int)(CharacterArchetype)Get(scene.Pickup, "m_characterArchetype"), Is.EqualTo(0x453342BA));
                Assert.That(Get(scene.Pickup, "m_cullingBoundsSize"), Is.EqualTo(2f));
                Assert.That(Get(scene.Pickup, "m_useCustomRenderBounds"), Is.False);
                Assert.That(Get(scene.Pickup, "m_rechargeTimeSeconds"), Is.Zero);
                Assert.That(scene.Pickup.IsValid, Is.Not.Null);
                Assert.That(scene.Pickup.IsValid.Value, Is.False);
                Assert.That(((SystemRef<App>)Get(scene.Pickup, "m_appRef")).SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<App>()));
                Assert.That(((SystemRef<CharacterManager>)Get(scene.Pickup, "m_characterManagerRef")).SystemName(),
                    Is.EqualTo(ProcessManager.GetDefaultName<CharacterManager>()));
                Assert.That(((SystemRef<CinemachineCameraManager>)Get(scene.Pickup, "m_cinemachineCameraManagerRef")).SystemName(),
                    Is.EqualTo(ProcessManager.GetDefaultName<CinemachineCameraManager>()));
                Assert.That(((SystemRef<MessageManager>)Get(scene.Pickup, "m_messageManagerRef")).SystemName(),
                    Is.EqualTo(ProcessManager.GetDefaultName<MessageManager>()));
                Assert.That((int)(TimeCategory)Call(scene.Pickup, "GetDefaultTimeCategoryEnum"), Is.EqualTo(0x34DAB335));
                Assert.That((int)(UpdateOn)Call(scene.Pickup, "GetDefaultUpdateOn"), Is.EqualTo(unchecked((int)0x97220D01)));
            }
        }

        // SetAnimation 0x6E18C0 stops the animation, makes the clip the default, sets the wrap mode
        // to Once (1) or Loop (2) from the flag (inc esi, 0x6E1903) and plays.
        [Test]
        public void SetAnimationReplacesTheDefaultClipWithTheWrapModeFromTheFlag()
        {
            using (var scene = new PickupScene(false))
            {
                Call(scene.Pickup, "SetAnimation", scene.Idle, true);
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Idle));
                Assert.That(scene.Animation.wrapMode, Is.EqualTo(WrapMode.Loop));
                Call(scene.Pickup, "SetAnimation", scene.Collect, false);
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Collect));
                Assert.That(scene.Animation.wrapMode, Is.EqualTo(WrapMode.Once));
            }
        }

        // EnterIdleState 0x6E12B0 zeroes both timers (the 64-bit store at +0xD0), loops the idle clip,
        // activates and plays the idle particle system and spawns the recharged effect only when asked.
        // EnterCollectState 0x6E1A60 zeroes the idle timer, plays the collect clip once, stops and clears
        // the particles, spawns the collect effect and starts the cooldown timer at the clip length.
        [Test]
        public void IdleAndCollectStatesSwitchTheClipTheParticlesAndTheEffects()
        {
            using (var scene = new PickupScene(false, 2f))
            {
                Set(scene.Pickup, "m_enterIdleTimeRemainingSeconds", 5f);
                Set(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds", 6f);
                scene.Idling.gameObject.SetActive(false);
                Call(scene.Pickup, "EnterIdleState", false);
                Assert.That(((float)Get(scene.Pickup, "m_enterIdleTimeRemainingSeconds"), (float)Get(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds")),
                    Is.EqualTo((0f, 0f)));
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Idle));
                Assert.That(scene.Animation.wrapMode, Is.EqualTo(WrapMode.Loop));
                Assert.That(scene.Idling.gameObject.activeSelf, Is.True);
                Assert.That(scene.Idling.isPlaying, Is.True);
                Assert.That(scene.Log, Is.Empty, "no recharged effect without the flag");

                Call(scene.Pickup, "EnterIdleState", true);
                Assert.That(scene.Log, Is.EqualTo(new[] { "recharged effect" }));

                Set(scene.Pickup, "m_enterIdleTimeRemainingSeconds", 5f);
                scene.Log.Clear();
                Call(scene.Pickup, "EnterCollectState");
                Assert.That(Get(scene.Pickup, "m_enterIdleTimeRemainingSeconds"), Is.Zero);
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Collect));
                Assert.That(scene.Animation.wrapMode, Is.EqualTo(WrapMode.Once));
                Assert.That(scene.Idling.isPlaying, Is.False);
                Assert.That(scene.Log, Is.EqualTo(new[] { "collect effect" }));
                Assert.That((float)Get(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds"), Is.EqualTo(2f).Within(1e-4f), "the collect clip length");
            }
        }

        // InternalUpdate 0x6E1840 counts the idle timer down while it is above zero and, once it is zero
        // or below (the jb at 0x6E1868 skips only a positive value), enters the idle state with the
        // recharged effect; the cooldown timer likewise plays the cooldown clip once. A timer that is
        // already zero is left alone.
        [Test]
        public void InternalUpdateCountsTheTimersDownAndFiresEachAtZero()
        {
            using (var scene = new PickupScene(false))
            {
                Set(scene.Pickup, "m_enterIdleTimeRemainingSeconds", 1f);
                Set(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds", 1f);
                Call(scene.Pickup, "InternalUpdate", 0.25f);
                Assert.That((float)Get(scene.Pickup, "m_enterIdleTimeRemainingSeconds"), Is.EqualTo(0.75f).Within(1e-6f));
                Assert.That((float)Get(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds"), Is.EqualTo(0.75f).Within(1e-6f));
                Assert.That(scene.Log, Is.Empty);

                // Exactly reaching zero fires the idle state (which zeroes both timers).
                Call(scene.Pickup, "InternalUpdate", 0.75f);
                Assert.That(scene.Log, Is.EqualTo(new[] { "recharged effect" }));
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Idle));
                Assert.That(((float)Get(scene.Pickup, "m_enterIdleTimeRemainingSeconds"), (float)Get(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds")),
                    Is.EqualTo((0f, 0f)));
                scene.Log.Clear();
                Call(scene.Pickup, "InternalUpdate", 1f);
                Assert.That(scene.Log, Is.Empty, "zero timers do nothing");
            }
            using (var scene = new PickupScene(false))
            {
                // The cooldown timer alone: passing zero plays the cooldown clip once and stays below zero.
                Set(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds", 0.5f);
                Call(scene.Pickup, "InternalUpdate", 0.75f);
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Cooldown));
                Assert.That(scene.Animation.wrapMode, Is.EqualTo(WrapMode.Once));
                Assert.That((float)Get(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds"), Is.EqualTo(-0.25f).Within(1e-6f), "not reset");
                Call(scene.Pickup, "SetAnimation", scene.Idle, true);
                Call(scene.Pickup, "InternalUpdate", 0.75f);
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Idle), "a negative timer does not play the cooldown again");
            }
        }

        // The character actions (0x6E1930, 0x6E1AD0, 0x6E1AE0) recharge the air abilities of the
        // manager's current character (GetCharacter 0x6E1B20 ignores the collider). Enter then collects,
        // Stay changes nothing else, and Exit starts the idle countdown from m_rechargeTimeSeconds.
        [Test]
        public void CharacterActionsCollectOnEnterAndStartTheCountdownOnExit()
        {
            using (var scene = new PickupScene(true))
            {
                Assert.That(Call(scene.Pickup, "GetCharacter", new object[] { null }), Is.SameAs(scene.Character), "the collider is not used");
                Set(scene.Pickup, "m_rechargeTimeSeconds", 1.5f);

                scene.Pickup.Action_CharacterStay(null);
                Assert.That(scene.Log, Is.Empty);
                Assert.That((float)Get(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds"), Is.Zero);

                scene.Pickup.Action_CharacterEnter(null);
                Assert.That(scene.Log, Is.EqualTo(new[] { "collect effect" }));
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Collect));
                Assert.That((float)Get(scene.Pickup, "m_enterCooldownAnimationTimeRemainingSeconds"), Is.EqualTo(2f).Within(1e-4f));

                scene.Pickup.Action_CharacterExit(null);
                Assert.That((float)Get(scene.Pickup, "m_enterIdleTimeRemainingSeconds"), Is.EqualTo(1.5f));
                Assert.That(scene.Log.Count, Is.EqualTo(1), "exit spawns nothing");
            }
        }

        // RechargeAbilities 0x6E1950 visits every CharacterAbility_Air of the character through
        // GetAbilitiesOfType with the closure 0x6E22C0 that calls ability.Recharge(isExit); the visit is
        // seen here because an ability with no definition faults in Recharge. Other abilities are not
        // visited.
        [Test]
        public void RechargingVisitsTheAirAbilitiesOfTheCurrentCharacter()
        {
            using (var scene = new PickupScene(true))
            {
                var abilities = (IDictionary)Get(scene.Character, "m_abilities");
                abilities.Add(ActorAbilityType.Character_ClimbingAir, new CharacterAbility_Idle());
                Assert.DoesNotThrow(() => scene.Pickup.Action_CharacterStay(null), "an ability that is not an air ability");
                abilities.Add(ActorAbilityType.Character_MovementAir, new CharacterAbility_Air());
                Assert.Throws<NullReferenceException>(() => scene.Pickup.Action_CharacterStay(null));
                Assert.Throws<NullReferenceException>(() => scene.Pickup.Action_CharacterExit(null));
                Assert.That((float)Get(scene.Pickup, "m_enterIdleTimeRemainingSeconds"), Is.Zero, "the exit countdown never started");
                Assert.Throws<NullReferenceException>(() => scene.Pickup.Action_CharacterEnter(null));
                Assert.That(scene.Log, Is.Empty, "collecting never started");
            }
        }

        // The detector message for the pickup's archetype, published on the exchange under the App that
        // OnEnable 0x6E1100 subscribed with (m_appRef.Get()).
        private static void PublishDetectorMessage(PickupScene scene, bool isValid)
        {
            Component app = ProcessManager.GetSystem<App>();
            var message = new DetectorArchetypeUpdateMessage { Archetype = CharacterArchetype.Flying, IsValid = isValid };
            scene.Messages.ComponentMessagesWithCompletion.PublishMessage(in app, in message, () => { }, -1);
        }

        // OnEnable 0x6E1100: the base enable (a TimeManager subscription, skipped here through
        // m_ignoreEnable as the other TimeScaledComponent probes do), EnterIdleState(false) (0x6E118C),
        // the camera manager subscription only with custom render bounds (the test at 0x6E1191),
        // IsValid.Value = true (vcall slot 9, 0x6E1207) and the OnDetectorActive subscription for the
        // App on the manager's ComponentMessagesWithCompletion exchange.
        [Test]
        public void EnablingIdlesTheAnimationMarksTheBindingValidAndSubscribesToDetectorMessages()
        {
            using (var scene = new PickupScene(true, withMessages: true))
            {
                Set(scene.Pickup, "m_ignoreEnable", true);
                scene.Idling.gameObject.SetActive(false);
                Assert.That(scene.Pickup.IsValid.Value, Is.False);

                Call(scene.Pickup, "OnEnable");

                Assert.That(scene.Pickup.IsValid.Value, Is.True);
                Assert.That(scene.Animation.clip, Is.SameAs(scene.Idle));
                Assert.That(scene.Animation.wrapMode, Is.EqualTo(WrapMode.Loop));
                Assert.That(scene.Idling.gameObject.activeSelf, Is.True, "EnterIdleState ran");
                Assert.That(scene.Log, Is.Empty, "no recharged effect on enabling");
                Assert.That(Get(ProcessManager.GetSystemRef<CinemachineCameraManager>(), "m_actionOnSystemValid"), Is.Null,
                    "no camera subscription without custom render bounds");

                Assert.That(Handlers(scene.Manager), Is.Empty);
                PublishDetectorMessage(scene, true);
                Delegate[] handlers = Handlers(scene.Manager);
                Assert.That(handlers.Length, Is.EqualTo(1), "the pickup answers a detector message for the App");
                Assert.That(handlers[0].Target, Is.SameAs(scene.Pickup));
                ((Action<Character>)handlers[0])(scene.Character);
                Assert.That(Handlers(scene.Manager), Is.Empty);
            }
        }

        // OnDisable 0x6E1340 withdraws the message subscription (guarded by the message manager and
        // App SystemRef.IsValid tests, 0x6E142D and 0x6E1516), the camera subscription behind its own
        // guard (0x6E153D) and releases the culling group: its handler is removed, it is disposed
        // and the field set to null (0x6E1664, 0x6E1669).
        [Test]
        public void DisablingWithdrawsTheSubscriptionAndReleasesTheCullingGroup()
        {
            using (var scene = new PickupScene(true, withMessages: true))
            {
                Set(scene.Pickup, "m_ignoreEnable", true);
                Set(scene.Pickup, "m_particleRenderers", new Renderer[0]);
                Call(scene.Pickup, "OnEnable");
                Camera camera = scene.Time.Add<Camera>("camera");
                Call(scene.Pickup, "SetupCustomCulling", camera);
                var group = (CullingGroup)Get(scene.Pickup, "m_cullingGroup");
                Assert.That(group.onStateChanged, Is.Not.Null);

                Assert.DoesNotThrow(() => Call(scene.Pickup, "OnDisable"), "an unregistered camera manager is skipped");
                Assert.That(Get(scene.Pickup, "m_cullingGroup"), Is.Null);
                Assert.That(group.onStateChanged, Is.Null, "the state handler was removed");
                PublishDetectorMessage(scene, true);
                Assert.That(Handlers(scene.Manager), Is.Empty, "the message subscription was withdrawn");
            }
        }

        // Without a registered message manager or App the guards skip the unsubscription, and the
        // culling group is still released.
        [Test]
        public void DisablingWithoutManagersStillReleasesTheCullingGroup()
        {
            using (var scene = new PickupScene(false))
            {
                Set(scene.Pickup, "m_particleRenderers", new Renderer[0]);
                Camera camera = scene.Time.Add<Camera>("camera");
                Call(scene.Pickup, "SetupCustomCulling", camera);
                Assert.That(Get(scene.Pickup, "m_cullingGroup"), Is.Not.Null);

                Assert.DoesNotThrow(() => Call(scene.Pickup, "OnDisable"));
                Assert.That(Get(scene.Pickup, "m_cullingGroup"), Is.Null);
                Assert.DoesNotThrow(() => Call(scene.Pickup, "OnDisable"), "no culling group is fine");
            }
        }

        // OnDetectorActive 0x6E16A0 keeps a message for the pickup's archetype (+0x50) and registers
        // OnDetectorCharacterActive with the manager without an immediate call (edx 0, 0x6E1747); other
        // archetypes change nothing and the completion callback is never called. OnDetectorCharacterActive
        // 0x6E1770 releases that registration and publishes the stored message's IsValid (+0xE4).
        [Test]
        public void DetectorMessagesRegisterAndReleaseTheCharacterCallback()
        {
            using (var scene = new PickupScene(true))
            {
                Action refused = () => Assert.Fail("the completion callback is not invoked");
                var other = new DetectorArchetypeUpdateMessage { Archetype = CharacterArchetype.Flying + 1, IsValid = true };
                Assert.That((CharacterArchetype)Get(scene.Pickup, "m_characterArchetype"), Is.EqualTo(CharacterArchetype.Flying));
                Call(scene.Pickup, "OnDetectorActive", other, refused);
                Assert.That(Handlers(scene.Manager), Is.Empty, "another archetype");

                var matching = new DetectorArchetypeUpdateMessage { Archetype = CharacterArchetype.Flying, IsValid = true };
                Call(scene.Pickup, "OnDetectorActive", matching, refused);
                Assert.That(((DetectorArchetypeUpdateMessage)Get(scene.Pickup, "m_detectorArchetypeMessage")).IsValid, Is.True);
                Delegate[] handlers = Handlers(scene.Manager);
                Assert.That(handlers.Length, Is.EqualTo(1));
                Assert.That(handlers[0].Target, Is.SameAs(scene.Pickup));
                Assert.That(handlers[0].Method.Name, Is.EqualTo("OnDetectorCharacterActive"));
                Assert.That(scene.Pickup.IsValid.Value, Is.False, "the character callback was not run at once");

                ((Action<Character>)handlers[0])(scene.Character);
                Assert.That(scene.Pickup.IsValid.Value, Is.True);
                Assert.That(Handlers(scene.Manager), Is.Empty, "released");

                // A matching message that is not valid publishes false.
                Call(scene.Pickup, "OnDetectorActive", new DetectorArchetypeUpdateMessage { Archetype = CharacterArchetype.Flying }, refused);
                Call(scene.Pickup, "OnDetectorCharacterActive", scene.Character);
                Assert.That(scene.Pickup.IsValid.Value, Is.False);
            }
        }

        private static Delegate[] Handlers(CharacterManager manager) =>
            ((Delegate)Get(manager, "m_onCharacterChange"))?.GetInvocationList() ?? Array.Empty<Delegate>();

        // SetRenderers 0x6E1F50 sets every renderer's enabled flag.
        [Test]
        public void SetRenderersEnablesOrDisablesEveryRenderer()
        {
            using (var scene = new PickupScene(false))
            {
                var renderers = new[]
                {
                    scene.Time.Add<MeshRenderer>("first renderer"), scene.Time.Add<MeshRenderer>("second renderer")
                };
                Set(scene.Pickup, "m_particleRenderers", renderers);
                Call(scene.Pickup, "SetRenderers", false);
                Assert.That(Array.ConvertAll(renderers, renderer => renderer.enabled), Is.EqualTo(new[] { false, false }));
                Call(scene.Pickup, "SetRenderers", true);
                Assert.That(Array.ConvertAll(renderers, renderer => renderer.enabled), Is.EqualTo(new[] { true, true }));
            }
        }

        // Cull 0x6E1E00 returns without a particle system (op_Equality, 0x6E1E4F); a visible pickup plays
        // the system and enables the renderers, an invisible one pauses it and disables them.
        // OnStateChanged 0x6E1F20 forwards CullingGroupEvent.isVisible (false for a default event).
        [Test]
        public void CullPlaysOrPausesTheParticlesAndTogglesTheRenderers()
        {
            using (var scene = new PickupScene(false))
            {
                var renderer = scene.Time.Add<MeshRenderer>("renderer");
                Set(scene.Pickup, "m_particleRenderers", new Renderer[] { renderer });
                Call(scene.Pickup, "Cull", true);
                Assert.That((scene.Idling.isPlaying, renderer.enabled), Is.EqualTo((true, true)));
                Call(scene.Pickup, "Cull", false);
                Assert.That((scene.Idling.isPaused, renderer.enabled), Is.EqualTo((true, false)));
                Call(scene.Pickup, "Cull", true);
                Assert.That((scene.Idling.isPlaying, renderer.enabled), Is.EqualTo((true, true)));
                Call(scene.Pickup, "OnStateChanged", default(CullingGroupEvent));
                Assert.That((scene.Idling.isPaused, renderer.enabled), Is.EqualTo((true, false)), "a default event is not visible");

                Object.DestroyImmediate(scene.Idling);
                renderer.enabled = true;
                Assert.DoesNotThrow(() => Call(scene.Pickup, "Cull", false), "a destroyed particle system is skipped");
                Assert.That(renderer.enabled, Is.True);
            }
        }

        // SetupCustomCulling 0x6E1BA0: nothing for a destroyed pickup (op_Equality at 0x6E1C22); otherwise a
        // culling group (created once) with one bounding sphere, a state-change handler, the camera as target
        // and enabled. A second camera only replaces the target.
        [Test]
        public void SetupCustomCullingCreatesOneCullingGroupForTheCamera()
        {
            using (var scene = new PickupScene(false))
            {
                Set(scene.Pickup, "m_particleRenderers", new Renderer[0]);
                Camera first = scene.Time.Add<Camera>("first camera");
                Camera second = scene.Time.Add<Camera>("second camera");
                try
                {
                    Call(scene.Pickup, "SetupCustomCulling", first);
                    var group = (CullingGroup)Get(scene.Pickup, "m_cullingGroup");
                    Assert.That(group, Is.Not.Null);
                    Assert.That(group.targetCamera, Is.SameAs(first));
                    Assert.That(group.enabled, Is.True);
                    Delegate[] handlers = group.onStateChanged.GetInvocationList();
                    Assert.That(handlers.Length, Is.EqualTo(1));
                    Assert.That(handlers[0].Target, Is.SameAs(scene.Pickup));
                    Assert.That(handlers[0].Method.Name, Is.EqualTo("OnStateChanged"));

                    Call(scene.Pickup, "SetupCustomCulling", second);
                    Assert.That(Get(scene.Pickup, "m_cullingGroup"), Is.SameAs(group), "created once");
                    Assert.That(group.targetCamera, Is.SameAs(second));
                    Assert.That(group.onStateChanged.GetInvocationList().Length, Is.EqualTo(1), "no second handler");
                }
                finally
                {
                    ((CullingGroup)Get(scene.Pickup, "m_cullingGroup"))?.Dispose();
                    Set(scene.Pickup, "m_cullingGroup", null);
                }
            }
            using (var scene = new PickupScene(false))
            {
                Camera camera = scene.Time.Add<Camera>("camera");
                Object.DestroyImmediate(scene.Pickup);
                Call(scene.Pickup, "SetupCustomCulling", camera);
                Assert.That(Get(scene.Pickup, "m_cullingGroup"), Is.Null, "a destroyed pickup does nothing");
            }
        }

        // OnValidate 0x6E1FB0 fills the renderer list from the idle particle system's children, but only
        // with custom render bounds and an empty list.
        [Test]
        public void OnValidateAutoPopulatesTheRenderersWhenCustomBoundsAreOn()
        {
            using (var scene = new PickupScene(false))
            {
                scene.Time.Add<MeshRenderer>("child renderer").transform.SetParent(scene.Idling.transform, false);
                Set(scene.Pickup, "m_particleRenderers", new Renderer[0]);
                Call(scene.Pickup, "OnValidate");
                Assert.That((Renderer[])Get(scene.Pickup, "m_particleRenderers"), Is.Empty, "custom bounds are off");

                Set(scene.Pickup, "m_useCustomRenderBounds", true);
                Call(scene.Pickup, "OnValidate");
                Renderer[] filled = (Renderer[])Get(scene.Pickup, "m_particleRenderers");
                Assert.That(filled, Has.Length.EqualTo(2), "the system's own renderer and the child's");
                Assert.That(filled, Has.Some.InstanceOf<ParticleSystemRenderer>());
                Assert.That(filled, Has.Some.InstanceOf<MeshRenderer>());

                Call(scene.Pickup, "OnValidate");
                Assert.That(Get(scene.Pickup, "m_particleRenderers"), Is.SameAs(filled), "a populated list is kept");
            }
        }
    }
}
