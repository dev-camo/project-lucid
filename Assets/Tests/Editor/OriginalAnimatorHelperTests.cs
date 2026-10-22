using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // Behaviour of the restored Game.Runtime Animator helpers (AnimatorDrivenLifetimeObject,
    // the three boss ability setters, AnimatorGetBoolEvent, AnimatorModifyFloat with
    // AnimatorNormaliseFloat and AnimatorSmoothFloat, AnimatorParameterBindable<T> with
    // AnimatorParameterBindableFloat, AnimatorRandomTrigger, AnimatorSampler,
    // AnimatorSetBoolEvent, AnimatorSetFloatCurve, AnimatorSuppressWarnings,
    // AnimatorToCinemachineVirtualCamera, AnimatorToMaterialPropertyData,
    // AnimatorToMaterialProperty and AnimatorToRendererEnabled) against the shipped 1.10.1
    // x86-64 bodies cited per case. Each case owns its GameObjects, assets and in-memory
    // AnimatorControllers and destroys them, owns any ProcessManager row it touches, and
    // restores the Random state and the static caches it replaces. Messages are invoked
    // directly; the Editor's own Reset and OnValidate on AddComponent are allowed for and
    // overwritten before each check.
    [TestFixture]
    public sealed class OriginalAnimatorHelperTests : RandomStateFixture
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");
        private static readonly Regex EditModeMaterialInstance = new Regex("^Instantiating material due to calling renderer\\.material during edit mode");

        private static FieldInfo Field(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Any | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.Name, name);
        }

        private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        private static object Call(object target, string name, params object[] args)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethod(name, Any | BindingFlags.DeclaredOnly);
                if (method == null) continue;
                try { return method.Invoke(target, args); }
                catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
            }
            throw new MissingMethodException(target.GetType().Name, name);
        }

        // AnimatorStateInfo has no public constructor; only its normalised time is read here.
        private static AnimatorStateInfo StateAt(float normalizedTime)
        {
            object info = default(AnimatorStateInfo);
            typeof(AnimatorStateInfo).GetField("m_NormalizedTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(info, normalizedTime);
            return (AnimatorStateInfo)info;
        }

        // Every Unity object a case creates, destroyed newest first. AnimatorControllers
        // also lose their states, layer state machines and undo history.
        private sealed class Owned : IDisposable
        {
            private readonly List<Object> objects = new List<Object>();

            public T Add<T>(T value) where T : Object
            {
                objects.Add(value);
                return value;
            }

            public GameObject Inactive(string name)
            {
                var gameObject = Add(new GameObject(name) { hideFlags = HideFlags.HideAndDontSave });
                gameObject.SetActive(false);
                return gameObject;
            }

            public GameObject Active(string name) => Add(new GameObject(name) { hideFlags = HideFlags.HideAndDontSave });

            // A one-second clip moving localPosition.x linearly from 0 to endX.
            public AnimationClip Clip(string name, float endX)
            {
                var clip = Add(new AnimationClip { name = name, hideFlags = HideFlags.HideAndDontSave });
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, endX));
                return clip;
            }

            public Animator BoundAnimator(GameObject owner, Action<AnimatorController> build)
            {
                var controller = Add(new AnimatorController { name = "Lucid animator helper fixture", hideFlags = HideFlags.HideAndDontSave });
                controller.AddLayer("Base");
                Add(controller.layers[0].stateMachine);
                build(controller);
                Undo.ClearUndo(controller);
                var animator = owner.GetComponent<Animator>();
                if (animator == null) animator = owner.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                Assert.That(animator.isInitialized, Is.True, "fixture Animator binds the owned controller");
                return animator;
            }

            public void Dispose()
            {
                for (int i = objects.Count - 1; i >= 0; --i)
                {
                    Object value = objects[i];
                    if (value == null) continue;
                    if (value is AnimatorController controller)
                    {
                        foreach (AnimatorControllerLayer layer in controller.layers)
                        {
                            if (layer.stateMachine == null) continue;
                            foreach (ChildAnimatorState child in layer.stateMachine.states)
                                if (child.state != null) Object.DestroyImmediate(child.state);
                            Object.DestroyImmediate(layer.stateMachine);
                        }
                        Undo.ClearUndo(controller);
                    }
                    Object.DestroyImmediate(value);
                }
                objects.Clear();
            }
        }

        // Runs body with a fixed Random seed and restores the caller's Random state.
        private static void WithSeed(int seed, Action body)
        {
            Random.State prior = Random.state;
            try
            {
                Random.InitState(seed);
                body();
            }
            finally
            {
                Random.state = prior;
            }
        }

        // ---- AnimatorDrivenLifetimeObject ----

        private sealed class DamageableProbe : IPlayerDamageable
        {
            public void ReceiveHit(Collider damagedCollider) { }
            public void RegisterHitPlayer(DamageAction damageAction, HazardType hazardType) { }
        }

        // The Editor sends Reset on AddComponent, and TimeScaledComponent_SDT's default category
        // reads its s_config lookup, which would otherwise register the SystemConfiguration
        // row. The case installs an owned, empty TimeCategoryLookup there and restores the
        // prior cache, and owns the TimeManager row the TimeScaledComponent static reference
        // may create.
        private static void WithLifetimeObject(Action<Owned, AnimatorDrivenLifetimeObject, Animator> body)
        {
            FieldInfo config = Field(typeof(TimeScaledComponent_SDT), "s_config_Internal");
            object priorConfig = config.GetValue(null);
            using (var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>()))
            using (var owned = new Owned())
            {
                try
                {
                    config.SetValue(null, owned.Add(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                    GameObject root = owned.Active("AnimatorDrivenLifetimeObject fixture");
                    Animator animator = owned.BoundAnimator(root, controller =>
                    {
                        controller.AddMotion(owned.Clip("Lifetime", 10f));
                        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                    });
                    var lifetime = root.AddComponent<AnimatorDrivenLifetimeObject>();
                    Set(lifetime, "m_animator", animator);
                    Set(lifetime, "m_hazards", new Hazard[0]);
                    body(owned, lifetime, animator);
                }
                finally
                {
                    config.SetValue(null, priorConfig);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Initialise 0x06002109 (0x56B280) disables the Animator (set_enabled 0 at 0x56b2a2),
        // passes the damageable to RegisterPlayerDamageableInterface of every hazard
        // (0x56b2ca) and tail-calls TimedLifetimeObject.Initialise (0x56b2f4), which flags
        // the object and stores the lifetime.
        [Test]
        public void LifetimeObjectInitialiseDisablesTheAnimatorAndRegistersEveryHazard()
        {
            WithLifetimeObject((owned, lifetime, animator) =>
            {
                GameObject hazards = owned.Inactive("Lifetime hazards fixture");
                Hazard first = hazards.AddComponent<Hazard>();
                Hazard second = hazards.AddComponent<Hazard>();
                Set(lifetime, "m_hazards", new[] { first, second });
                var damageable = new DamageableProbe();

                lifetime.Initialise(damageable, 3f);
                Assert.That(animator.enabled, Is.False);
                foreach (Hazard hazard in new[] { first, second })
                {
                    Assert.That(Get(hazard, "m_playerDamageable"), Is.SameAs(damageable));
                    Assert.That(Get(hazard, "m_playerDamageableSet"), Is.True);
                }
                Assert.That(lifetime.m_initialised, Is.True);
                Assert.That(Get(lifetime, "m_maxLifetime"), Is.EqualTo(3f));
            });
        }

        // ResetTimedLifetimeObject 0x0600210a (0x56B300) calls Animator.Rebind (0x56b30f) and
        // Update(0) (0x56b31d) before the base reset clears the elapsed time (0x56b32d).
        [Test]
        public void LifetimeObjectResetRebindsTheAnimatorAndClearsElapsedTime()
        {
            WithLifetimeObject((owned, lifetime, animator) =>
            {
                animator.SetFloat("Speed", 5f);
                Set(lifetime, "m_currentLifetime", 2f);

                lifetime.ResetTimedLifetimeObject();
                Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0f), "the rebind restores the parameter default");
                Assert.That(Get(lifetime, "m_currentLifetime"), Is.EqualTo(0f));
            });
        }

        // InternalUpdate 0x0600210b (0x56B340) returns while IsPaused (+0x2d, 0x56b346).
        // Otherwise it steps the Animator by deltaTime (0x56b361), runs the base update
        // (0x56b370) and, only with m_selfInitialise (+0x70) and ObjectExpired (0x56b380),
        // deactivates the GameObject (0x56b3a0).
        [Test]
        public void LifetimeObjectUpdateStepsTheAnimatorAndDeactivatesOnlyASelfInitialisedExpiredObject()
        {
            WithLifetimeObject((owned, lifetime, animator) =>
            {
                lifetime.Initialise(null, 0.5f);
                animator.Update(0f);

                lifetime.IsPaused = true;
                Call(lifetime, "InternalUpdate", 0.25f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0f).Within(1e-5f), "paused, the Animator is not stepped");
                Assert.That(Get(lifetime, "m_currentLifetime"), Is.EqualTo(0f));

                lifetime.IsPaused = false;
                Call(lifetime, "InternalUpdate", 0.25f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0.25f).Within(1e-5f), "the disabled Animator is stepped manually");
                Assert.That(Get(lifetime, "m_currentLifetime"), Is.EqualTo(0.25f));

                Call(lifetime, "InternalUpdate", 0.5f);
                Assert.That(lifetime.ObjectExpired, Is.True);
                Assert.That(lifetime.gameObject.activeSelf, Is.True, "an externally initialised object stays active");

                Set(lifetime, "m_selfInitialise", true);
                Call(lifetime, "InternalUpdate", 0.125f);
                Assert.That(lifetime.gameObject.activeSelf, Is.False, "a self-initialised expired object deactivates itself");
            });
        }

        // OnEnable 0x0600210c (0x56B3B0) calls the virtual Initialise (slot 31, +0x328) with a
        // null damageable and m_lifetime (+0x74) only when m_selfInitialise (+0x70) is set and
        // m_initialised (+0x58) is clear, then tail-calls TimeScaledComponent.OnEnable
        // (0x56b3ea). The base is admitted through its m_ignoreEnable branch, which only
        // clears that flag, so no TimeManager subscription is attempted.
        [Test]
        public void LifetimeObjectEnableSelfInitialisesOnceThenRunsTheBaseEnable()
        {
            WithLifetimeObject((owned, lifetime, animator) =>
            {
                GameObject hazards = owned.Inactive("Lifetime enable hazard fixture");
                Hazard hazard = hazards.AddComponent<Hazard>();
                var prior = new DamageableProbe();
                hazard.RegisterPlayerDamageableInterface(prior);
                Set(lifetime, "m_hazards", new[] { hazard });
                Set(lifetime, "m_lifetime", 4f);

                Set(lifetime, "m_ignoreEnable", true);
                Call(lifetime, "OnEnable");
                Assert.That(lifetime.m_initialised, Is.False, "without m_selfInitialise nothing is initialised");
                Assert.That(animator.enabled, Is.True);
                Assert.That(Get(lifetime, "m_ignoreEnable"), Is.False, "the base enable ran");

                Set(lifetime, "m_selfInitialise", true);
                Set(lifetime, "m_ignoreEnable", true);
                Call(lifetime, "OnEnable");
                Assert.That(lifetime.m_initialised, Is.True);
                Assert.That(Get(lifetime, "m_maxLifetime"), Is.EqualTo(4f));
                Assert.That(animator.enabled, Is.False);
                Assert.That(Get(hazard, "m_playerDamageable"), Is.SameAs(prior), "the null damageable leaves the hazard's own");
                Assert.That(Get(lifetime, "m_ignoreEnable"), Is.False);

                Set(lifetime, "m_lifetime", 9f);
                Set(lifetime, "m_ignoreEnable", true);
                Call(lifetime, "OnEnable");
                Assert.That(Get(lifetime, "m_maxLifetime"), Is.EqualTo(4f), "an initialised object is not initialised again");
            });
        }

        // ---- Boss ability setters ----

        private sealed class ProbeAbilityDefinition : AbilityDefinition
        {
            public override Type ScriptType => typeof(SignalProbe);
        }

        private class PlainAbility : ActorAbility
        {
            public override AbilityDefinition DefinitionBase => null;
        }

        // Implements both ability interfaces so each setter's single call can be told apart.
        private sealed class SignalProbe : PlainAbility, IAbilityAnimatorTriggerable, IOffensiveAbility
        {
            public readonly List<string> Calls = new List<string>();
            public void SetAnimationTriggered() => Calls.Add("SetAnimationTriggered");
            public void SetAnimationComplete() => Calls.Add("SetAnimationComplete");
            public bool StartWindUp => false;
            public bool WoundUp => false;
            public bool CanAttack(Boss boss, Character character, ActorAnimationDefinition actorAnimationDefinition, float deltaTime) => false;
            public void SetAttackFlow(bool abilityActive) => Calls.Add("SetAttackFlow");
            public void SetAttackWindingUp() => Calls.Add("SetAttackWindingUp");
            public void SetAttackWoundUp() => Calls.Add("SetAttackWoundUp");
            public void SetAttackAnimStarted() => Calls.Add("SetAttackAnimStarted");
            public void SkipAttackPreparation(ActorAnimationDefinition actorAnimationDefinition) => Calls.Add("SkipAttackPreparation");
            public void ResetProjectileGroupCounter() => Calls.Add("ResetProjectileGroupCounter");
        }

        private sealed class OnlyTriggerable : PlainAbility, IAbilityAnimatorTriggerable
        {
            public int Calls;
            public void SetAnimationTriggered() => ++Calls;
            public void SetAnimationComplete() => ++Calls;
        }

        private sealed class BossFixture
        {
            public Owned Owned;
            public BossManager Manager;
            public Boss Boss;
            public Animator Animator;
            public Animator OtherAnimator;
            public ProbeAbilityDefinition Definition;
            public Dictionary<ActorAbilityType, ActorAbility> Abilities;
        }

        // BossManager's field initialiser takes a SaveManager reference; the fixture owns that
        // row and the BossManager row it registers into, as the AnimatorCompleteAbilitySetter
        // case does.
        private static void WithBoss(Action<BossFixture> body)
        {
            var fixture = new BossFixture { Owned = new Owned() };
            var saveRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<SaveManager>());
            VerificationFixtureOwnership.RegistryRow bossRow = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                bossRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<BossManager>());
                fixture.Manager = fixture.Owned.Inactive("BossManager fixture").AddComponent<BossManager>();
                bossRow.Register(fixture.Manager);

                fixture.Boss = fixture.Owned.Inactive("Boss fixture").AddComponent<Boss>();
                fixture.Animator = fixture.Owned.Inactive("Boss animator fixture").AddComponent<Animator>();
                fixture.OtherAnimator = fixture.Owned.Inactive("Other animator fixture").AddComponent<Animator>();
                typeof(Actor).GetProperty(nameof(Actor.Animator)).GetSetMethod(true).Invoke(fixture.Boss, new object[] { new ActorAnimator(fixture.Animator) });
                fixture.Manager.Bosses.Add(fixture.Boss);

                fixture.Definition = fixture.Owned.Add(ScriptableObject.CreateInstance<ProbeAbilityDefinition>());
                // Test-only fixture value, not an original constant: any value works because
                // the fixture boss owns its ability dictionary.
                fixture.Definition.AbilityType = (ActorAbilityType)0x4C554344;
                fixture.Abilities = (Dictionary<ActorAbilityType, ActorAbility>)Get(fixture.Boss, "m_abilities");
                body(fixture);
            },
            () => { if (fixture.Manager != null) fixture.Manager.Bosses.Clear(); },
            () => bossRow?.Dispose(),
            fixture.Owned.Dispose,
            saveRow.Dispose);
            LogAssert.NoUnexpectedReceived();
        }

        // AnimatorEnteredAttackStateAbilitySetter.OnStateEnter 0x06000e77 (0x726A20) stores
        // GetSystemRef<BossManager>(null, true) (0x726abe), InvokeOnValid(RegisterBossManager)
        // (0x726b10; 0x06000e76 stores the manager) and GetBossForAnimator(animator)
        // (0x726b1e) whatever m_setAttackOnEnter is. With the flag, TryGetAbilityFromType
        // (0x726b4e) and an IOffensiveAbility cast (0x726b69) lead to interface slot 6,
        // SetAttackAnimStarted (0x726bb2).
        [Test]
        public void AttackStateSetterResolvesTheBossOnEntryAndStartsTheAttackAnimation()
        {
            WithBoss(fixture =>
            {
                var probe = new SignalProbe();
                fixture.Abilities.Add(fixture.Definition.AbilityType, probe);
                var setter = fixture.Owned.Add(ScriptableObject.CreateInstance<AnimatorEnteredAttackStateAbilitySetter>());
                Set(setter, "m_attackAbilityDefinition", fixture.Definition);

                setter.OnStateEnter(fixture.Animator, default, 0);
                Assert.That(Get(setter, "m_bossManager"), Is.SameAs(fixture.Manager));
                Assert.That(((SystemRef<BossManager>)Get(setter, "m_bossManagerRef")).GetSafe(), Is.SameAs(fixture.Manager));
                Assert.That(Get(setter, "m_boss"), Is.SameAs(fixture.Boss), "the boss is resolved without m_setAttackOnEnter");
                Assert.That(probe.Calls, Is.Empty);

                Set(setter, "m_setAttackOnEnter", true);
                setter.OnStateEnter(fixture.Animator, default, 0);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "SetAttackAnimStarted" }));

                var triggerable = new OnlyTriggerable();
                fixture.Abilities[fixture.Definition.AbilityType] = triggerable;
                Assert.DoesNotThrow(() => setter.OnStateEnter(fixture.Animator, default, 0), "an ability that is not offensive is skipped");
                Assert.That(triggerable.Calls, Is.Zero, "the skipped ability is not signalled through its other interface");

                Set(setter, "m_setAttackOnEnter", false);
                setter.OnStateEnter(fixture.OtherAnimator, default, 0);
                Assert.That(Get(setter, "m_boss"), Is.Null, "an animator no boss owns resolves to null");
            });
        }

        // AnimatorTriggeredAbilitySetter.OnStateEnter 0x06000e92 (0x727770) resolves the
        // manager and boss as above and, with m_triggerOnEntry (+0x18), calls interface slot 0
        // of IAbilityAnimatorTriggerable, SetAnimationTriggered (0x727924). OnStateExit
        // 0x06000e93 (0x727940) tests m_triggerOnExit (+0x19) on the boss cached by the last
        // entry and calls the same slot 0, not SetAnimationComplete.
        [Test]
        public void TriggeredSetterSignalsTheTriggerOnEntryAndExitOfTheCachedBoss()
        {
            WithBoss(fixture =>
            {
                var probe = new SignalProbe();
                fixture.Abilities.Add(fixture.Definition.AbilityType, probe);
                var setter = fixture.Owned.Add(ScriptableObject.CreateInstance<AnimatorTriggeredAbilitySetter>());
                Set(setter, "m_abilityDefinition", fixture.Definition);

                setter.OnStateEnter(fixture.Animator, default, 0);
                setter.OnStateExit(fixture.OtherAnimator, default, 0);
                Assert.That(Get(setter, "m_boss"), Is.SameAs(fixture.Boss));
                Assert.That(probe.Calls, Is.Empty, "neither flag is set");

                Set(setter, "m_triggerOnEntry", true);
                setter.OnStateEnter(fixture.Animator, default, 0);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "SetAnimationTriggered" }));

                Set(setter, "m_triggerOnExit", true);
                setter.OnStateExit(fixture.OtherAnimator, default, 0);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "SetAnimationTriggered", "SetAnimationTriggered" }),
                    "exit triggers again on the cached boss, ignoring its animator");
            });
        }

        // AnimatorWindUpAbilitySetter.OnStateEnter 0x06000e96 (0x727A40) resolves the manager
        // and boss and, with m_windUpOnEnter (+0x18), calls IOffensiveAbility slot 4,
        // SetAttackWindingUp (0x727bd2). OnStateExit 0x06000e97 (0x727C10) tests
        // m_woundUpOnExit (+0x19) on the cached boss and calls slot 5, SetAttackWoundUp
        // (0x727cb2).
        [Test]
        public void WindUpSetterWindsUpOnEntryAndWindsDownOnExit()
        {
            WithBoss(fixture =>
            {
                var probe = new SignalProbe();
                fixture.Abilities.Add(fixture.Definition.AbilityType, probe);
                var setter = fixture.Owned.Add(ScriptableObject.CreateInstance<AnimatorWindUpAbilitySetter>());
                Set(setter, "m_attackAbilityDefinition", fixture.Definition);

                setter.OnStateEnter(fixture.Animator, default, 0);
                setter.OnStateExit(fixture.Animator, default, 0);
                Assert.That(Get(setter, "m_bossManager"), Is.SameAs(fixture.Manager));
                Assert.That(Get(setter, "m_boss"), Is.SameAs(fixture.Boss));
                Assert.That(probe.Calls, Is.Empty);

                Set(setter, "m_windUpOnEnter", true);
                Set(setter, "m_woundUpOnExit", true);
                setter.OnStateEnter(fixture.Animator, default, 0);
                setter.OnStateExit(fixture.OtherAnimator, default, 0);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "SetAttackWindingUp", "SetAttackWoundUp" }));

                var triggerable = new OnlyTriggerable();
                fixture.Abilities[fixture.Definition.AbilityType] = triggerable;
                Assert.DoesNotThrow(() => setter.OnStateExit(fixture.Animator, default, 0), "an ability that is not offensive is skipped");
                Assert.That(triggerable.Calls, Is.Zero, "the skipped ability is not signalled through its other interface");
            });
        }

        // ---- AnimatorGetBoolEvent ----

        // Awake 0x0600052e (0x52EFD0) hashes m_boolName. Update 0x0600052f (0x52EFF0) stores
        // GetBool(hash) in m_boolValue (+0x40), then makes two separate tests: true and not
        // m_wasInState (+0x41) invokes a non-null m_trueEvent and sets the flag; afterwards a
        // reloaded false m_boolValue with the flag set (0x52f049) invokes a non-null
        // m_falseEvent and clears it. A listener that clears m_boolValue therefore gets both
        // events in one frame, which an else-if would not.
        [Test]
        public void GetBoolEventFiresEachEdgeOnceAndTestsBothEdgesInOneFrame()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorGetBoolEvent fixture");
                Animator animator = owned.BoundAnimator(root, controller => controller.AddParameter("Open", AnimatorControllerParameterType.Bool));
                var listener = root.AddComponent<AnimatorGetBoolEvent>();
                var log = new List<string>();
                var trueEvent = new UnityEvent();
                trueEvent.AddListener(() => log.Add("true"));
                var falseEvent = new UnityEvent();
                falseEvent.AddListener(() => log.Add("false"));
                Set(listener, "m_animator", animator);
                Set(listener, "m_boolName", "Open");
                Set(listener, "m_trueEvent", trueEvent);
                Set(listener, "m_falseEvent", falseEvent);

                Call(listener, "Awake");
                Assert.That(Get(listener, "m_boolHash"), Is.EqualTo(Animator.StringToHash("Open")));

                Call(listener, "Update");
                Assert.That(log, Is.Empty, "false while not in the state");
                animator.SetBool("Open", true);
                Call(listener, "Update");
                Call(listener, "Update");
                Assert.That(log, Is.EqualTo(new[] { "true" }), "the rising edge fires once");
                Assert.That(Get(listener, "m_wasInState"), Is.True);
                animator.SetBool("Open", false);
                Call(listener, "Update");
                Call(listener, "Update");
                Assert.That(log, Is.EqualTo(new[] { "true", "false" }), "the falling edge fires once");
                Assert.That(Get(listener, "m_wasInState"), Is.False);

                log.Clear();
                trueEvent.AddListener(() => Set(listener, "m_boolValue", false));
                animator.SetBool("Open", true);
                Call(listener, "Update");
                Assert.That(log, Is.EqualTo(new[] { "true", "false" }), "the false test follows the true event");
                Assert.That(Get(listener, "m_wasInState"), Is.False);

                Set(listener, "m_trueEvent", null);
                Set(listener, "m_falseEvent", null);
                Assert.DoesNotThrow(() => Call(listener, "Update"));
                Assert.That(Get(listener, "m_wasInState"), Is.True, "a missing event still records the edge");
                animator.SetBool("Open", false);
                Assert.DoesNotThrow(() => Call(listener, "Update"));
                Assert.That(Get(listener, "m_wasInState"), Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorModifyFloat, AnimatorNormaliseFloat, AnimatorSmoothFloat ----

        private static readonly MethodInfo ProcessFloat = typeof(AnimatorModifyFloat).GetMethod("ProcessFloat", Any);

        private static float Process(AnimatorModifyFloat modifier, float value) => (float)ProcessFloat.Invoke(modifier, new object[] { value });

        // OnStateEnter 0x06000e79 (0x726C00) clears m_valid for an empty source (0x726c18),
        // otherwise stores !IsNullOrEmpty(target) (0x726c33) and only when valid hashes both
        // names and calls the virtual DoOnEnter (slot 19, +0x268). OnStateUpdate 0x06000e7a
        // (0x726C80) writes SetFloat(target, ProcessFloat(GetFloat(source))) (slot 18, +0x258)
        // only while valid.
        [Test]
        public void ModifyFloatNeedsBothNamesAndWritesTheProcessedSource()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorModifyFloat fixture");
                Animator animator = owned.BoundAnimator(root, controller =>
                {
                    controller.AddParameter("Source", AnimatorControllerParameterType.Float);
                    controller.AddParameter("Target", AnimatorControllerParameterType.Float);
                });
                var modifier = owned.Add(ScriptableObject.CreateInstance<AnimatorNormaliseFloat>());
                Set(modifier, "m_rangeMin", 0f);
                Set(modifier, "m_rangeMax", 4f);
                animator.SetFloat("Source", -1f);

                foreach ((string source, string target) in new[] { ("", "Target"), (null, "Target"), ("Source", ""), ("Source", null) })
                {
                    Set(modifier, "m_sourceFloatParameter", source);
                    Set(modifier, "m_targetFloatParameter", target);
                    modifier.OnStateEnter(animator, default, 0);
                    Assert.That(Get(modifier, "m_valid"), Is.False, source + "/" + target);
                    modifier.OnStateUpdate(animator, default, 0);
                    Assert.That(animator.GetFloat("Target"), Is.EqualTo(0f));
                }
                Assert.That(Get(modifier, "m_sourceFloatParameterHash"), Is.EqualTo(0), "an invalid entry hashes nothing");

                Set(modifier, "m_sourceFloatParameter", "Source");
                Set(modifier, "m_targetFloatParameter", "Target");
                modifier.OnStateEnter(animator, default, 0);
                Assert.That(Get(modifier, "m_valid"), Is.True);
                Assert.That(Get(modifier, "m_sourceFloatParameterHash"), Is.EqualTo(Animator.StringToHash("Source")));
                Assert.That(Get(modifier, "m_targetFloatParameterHash"), Is.EqualTo(Animator.StringToHash("Target")));
                modifier.OnStateUpdate(animator, default, 0);
                Assert.That(animator.GetFloat("Target"), Is.EqualTo(0.25f), "|-1| normalised over 0..4");
                Assert.That(animator.GetFloat("Source"), Is.EqualTo(-1f));

                var smooth = owned.Add(ScriptableObject.CreateInstance<AnimatorSmoothFloat>());
                Set(smooth, "m_sourceFloatParameter", "Source");
                Set(smooth, "m_targetFloatParameter", "");
                smooth.OnStateEnter(animator, default, 0);
                Assert.That(Get(smooth, "m_samples"), Is.Null, "an invalid entry skips DoOnEnter");
                Set(smooth, "m_targetFloatParameter", "Target");
                smooth.OnStateEnter(animator, default, 0);
                Assert.That(Get(smooth, "m_samples"), Is.Not.Null, "a valid entry runs DoOnEnter");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // AnimatorNormaliseFloat.ProcessFloat 0x06000e7e (0x726CE0) returns 0 when the bounds
        // compare equal (ucomiss/jne/jnp, 0x726cee), else (|v| - min) / (max - min) with the
        // abs mask 0x29a6c20, then 0 when below 0 (cmpltss/andnps) and min(1, x) with the
        // constant 1.0 at 0x29a6be0: Mathf.InverseLerp over the magnitude. The rows separate
        // a signed source, an unclamped ratio and a missing equal-bounds guard.
        [Test]
        public void NormaliseFloatClampsTheMagnitudeAndReturnsZeroForAnEmptyRange()
        {
            using (var owned = new Owned())
            {
                var modifier = owned.Add(ScriptableObject.CreateInstance<AnimatorNormaliseFloat>());
                (float min, float max, float value, float expected)[] rows =
                {
                    (2f, 6f, 4f, 0.5f),
                    (2f, 6f, -4f, 0.5f),
                    (2f, 6f, 10f, 1f),
                    (2f, 6f, 1f, 0f),
                    (2f, 6f, -1f, 0f),
                    (6f, 2f, 3f, 0.75f),
                    (3f, 3f, 3f, 0f),
                    (3f, 3f, 7f, 0f),
                };
                foreach ((float min, float max, float value, float expected) in rows)
                {
                    Set(modifier, "m_rangeMin", min);
                    Set(modifier, "m_rangeMax", max);
                    Assert.That(Process(modifier, value), Is.EqualTo(expected), min + ".." + max + " @ " + value);
                }
                Set(modifier, "m_rangeMin", 0f);
                Set(modifier, "m_rangeMax", 1f);
                Assert.That(float.IsNaN(Process(modifier, float.NaN)), Is.True, "NaN fails both clamps");
                Assert.DoesNotThrow(() => Call(modifier, "DoOnEnter"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // AnimatorSmoothFloat..ctor 0x06000e90 stores m_sampleCount 10 (0x727734). DoOnEnter
        // 0x06000e8e (0x7275D0) allocates new float[m_sampleCount] only while m_samples is
        // null (0x727606), then Array.Fill(m_defaultValue) (0x727643) and m_index = 0.
        // ProcessFloat 0x06000e8f (0x727660) stores at m_index, advances it with a cmovl wrap
        // at m_sampleCount (0x72767c), sums the whole buffer and divides by m_sampleCount
        // (cvtsi2ss 0x72771d), not by the buffer length. A count below the kept buffer's
        // length separates that wrap from one at the length: 2 < 2 fails, so the third
        // sample goes back to slot 0 and slot 2 keeps the default.
        [Test]
        public void SmoothFloatAveragesTheRingOverTheConfiguredCount()
        {
            using (var owned = new Owned())
            {
                var smooth = owned.Add(ScriptableObject.CreateInstance<AnimatorSmoothFloat>());
                Assert.That(Get(smooth, "m_sampleCount"), Is.EqualTo(10));

                Set(smooth, "m_sampleCount", 3);
                Set(smooth, "m_defaultValue", 1f);
                Call(smooth, "DoOnEnter");
                var samples = (float[])Get(smooth, "m_samples");
                Assert.That(samples, Is.EqualTo(new[] { 1f, 1f, 1f }));

                Assert.That(Process(smooth, 4f), Is.EqualTo(2f));
                Assert.That(Process(smooth, 7f), Is.EqualTo(4f));
                Assert.That(Process(smooth, 1f), Is.EqualTo(4f));
                Assert.That(Get(smooth, "m_index"), Is.EqualTo(0), "the index wraps at a count equal to the length");
                Assert.That(Process(smooth, 10f), Is.EqualTo(6f), "the oldest sample is replaced");
                Assert.That(samples, Is.EqualTo(new[] { 10f, 7f, 1f }));

                Set(smooth, "m_sampleCount", 6);
                Set(smooth, "m_defaultValue", 2f);
                Call(smooth, "DoOnEnter");
                Assert.That(Get(smooth, "m_samples"), Is.SameAs(samples), "the buffer is allocated once");
                Assert.That(samples, Is.EqualTo(new[] { 2f, 2f, 2f }));
                Assert.That(Get(smooth, "m_index"), Is.EqualTo(0));
                Assert.That(Process(smooth, 8f), Is.EqualTo(2f), "(8 + 2 + 2) / 6, not / 3");

                Set(smooth, "m_sampleCount", 2);
                Set(smooth, "m_defaultValue", 1f);
                Call(smooth, "DoOnEnter");
                Assert.That(samples, Is.EqualTo(new[] { 1f, 1f, 1f }));
                Assert.That(Process(smooth, 4f), Is.EqualTo(3f), "(4 + 1 + 1) / 2");
                Assert.That(Get(smooth, "m_index"), Is.EqualTo(1));
                Assert.That(Process(smooth, 6f), Is.EqualTo(5.5f), "(4 + 6 + 1) / 2");
                Assert.That(Get(smooth, "m_index"), Is.EqualTo(0), "the index wraps at the count, not the buffer length");
                Assert.That(Process(smooth, 8f), Is.EqualTo(7.5f), "(8 + 6 + 1) / 2");
                Assert.That(samples, Is.EqualTo(new[] { 8f, 6f, 1f }), "the slot past the count keeps its default");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorParameterBindable<T>, AnimatorParameterBindableFloat ----

        // Metadata: AnimatorParameterBindable`1 (0x020009e3) is public abstract BeforeFieldInit
        // with one unconstrained, non-variant T; fields m_animator, m_propertyName (family),
        // Bindable (public initonly Bindable<T>), m_propertyHash; GetAnimatorValue is a
        // family abstract new slot. AnimatorParameterBindableFloat (0x020009e4) closes it over
        // float. The shared ..ctor (0x10578A0) creates the Bindable before MonoBehaviour..ctor.
        [Test]
        public void ParameterBindableKeepsTheOriginalGenericShape()
        {
            Type open = typeof(AnimatorParameterBindable<>);
            Assert.That((int)open.Attributes, Is.EqualTo(0x00100081));
            Type parameter = open.GetGenericArguments().Single();
            Assert.That(parameter.Name, Is.EqualTo("T"));
            Assert.That(parameter.GenericParameterAttributes, Is.EqualTo(GenericParameterAttributes.None));
            Assert.That(parameter.GetGenericParameterConstraints(), Is.Empty);
            Assert.That(open.GetFields(Any | BindingFlags.DeclaredOnly).Select(f => f.Name + ":" + ((int)f.Attributes).ToString("x4")),
                Is.EqualTo(new[] { "m_animator:0001", "m_propertyName:0004", "Bindable:0026", "m_propertyHash:0001" }));
            Assert.That(open.GetField("Bindable").FieldType, Is.EqualTo(typeof(Hardlight.UI.Binding.Bindable<>).MakeGenericType(parameter)));
            MethodInfo getValue = open.GetMethod("GetAnimatorValue", Any);
            Assert.That((int)getValue.Attributes & 0xffff, Is.EqualTo(0x05c4));
            Assert.That(typeof(AnimatorParameterBindableFloat).BaseType, Is.EqualTo(typeof(AnimatorParameterBindable<float>)));
        }

        // Reset 0x060038df (0x1057750) returns while m_animator (+0x20) is not null
        // (op_Inequality, 0x10577a5), else stores GetComponent<Animator>(). Awake 0x060038e0
        // (0x10577E0) hashes m_propertyName into m_propertyHash (+0x38) only for a nonempty
        // name and a non-null Animator. Update 0x060038e1 (0x1057860) passes m_animator and
        // the hash to GetAnimatorValue (slot 4, +0x178; Float's 0x060038e4 is GetFloat) and
        // writes Bindable.Value (Bindable<T> slot 9, set_Value, +0x1c8). Update reads the
        // cached hash (+0x38, 0x1057870) and never m_propertyName.
        [Test]
        public void ParameterBindablePublishesTheFloatThroughTheAwakeHash()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorParameterBindableFloat fixture");
                Animator animator = owned.BoundAnimator(root, controller => controller.AddParameter("Blend", AnimatorControllerParameterType.Float));
                var other = owned.Inactive("Other animator fixture").AddComponent<Animator>();
                var bindable = root.AddComponent<AnimatorParameterBindableFloat>();
                Assert.That(bindable.Bindable, Is.Not.Null);
                Assert.That(bindable.Bindable.Value, Is.EqualTo(0f));

                Set(bindable, "m_animator", other);
                Call(bindable, "Reset");
                Assert.That(Get(bindable, "m_animator"), Is.SameAs(other), "an assigned Animator is kept");
                Set(bindable, "m_animator", null);
                Call(bindable, "Reset");
                Assert.That(Get(bindable, "m_animator"), Is.SameAs(animator));

                Set(bindable, "m_propertyName", "");
                Call(bindable, "Awake");
                Assert.That(Get(bindable, "m_propertyHash"), Is.EqualTo(0), "no name, no hash");
                Set(bindable, "m_propertyName", "Blend");
                Set(bindable, "m_animator", null);
                Call(bindable, "Awake");
                Assert.That(Get(bindable, "m_propertyHash"), Is.EqualTo(0), "no Animator, no hash");
                Set(bindable, "m_animator", animator);
                Call(bindable, "Awake");
                Assert.That(Get(bindable, "m_propertyHash"), Is.EqualTo(Animator.StringToHash("Blend")));

                int changes = 0;
                bindable.Bindable.AddListener(() => ++changes);
                animator.SetFloat("Blend", 0.75f);
                Call(bindable, "Update");
                Assert.That(bindable.Bindable.Value, Is.EqualTo(0.75f));
                Assert.That(changes, Is.EqualTo(1));
                Call(bindable, "Update");
                Assert.That(changes, Is.EqualTo(1), "an unchanged value is not republished");

                Set(bindable, "m_propertyName", "Missing");
                animator.SetFloat("Blend", 0.4f);
                Call(bindable, "Update");
                Assert.That(bindable.Bindable.Value, Is.EqualTo(0.4f), "a renamed parameter is not rehashed until Awake");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorRandomTrigger ----

        // The constructor's GetSystemRef<TimeManager>(null, true) touches the default
        // TimeManager row; these cases register no TimeManager, so GetTotalTime 0x06000e83
        // (0x726E50) takes its IsNull branch (0x726e8f) and reads Time.realtimeSinceStartup.
        // RandomTriggerReadsTheRegisteredTimeManagerForItsCategory covers the resolved branch.
        private static void WithRandomTrigger(Action<Owned, AnimatorRandomTrigger, Animator> body)
        {
            using (var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>()))
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorRandomTrigger fixture");
                Animator animator = owned.BoundAnimator(root, controller =>
                {
                    controller.AddParameter("Idle_Index[Int]", AnimatorControllerParameterType.Int);
                    controller.AddParameter("Fidget", AnimatorControllerParameterType.Trigger);
                });
                var trigger = owned.Add(ScriptableObject.CreateInstance<AnimatorRandomTrigger>());
                Assert.That(((SystemRef<TimeManager>)Get(trigger, "m_timeManagerRef")).IsNull(), Is.True, "fixture has no TimeManager");
                body(owned, trigger, animator);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ..ctor 0x06000e84 (0x726FF0) stores "Idle_Index[Int]", the delays 1 and 5 (the pair
        // at 0x29a7860), TimeCategory 0x39573b00 (PlayerAnimation), m_nextTriggerTime -1
        // (0xbf800000) and GetSystemRef<TimeManager>(null, true). OnStateEnter 0x06000e81
        // (0x726D50) hashes each nonempty name, writes SetInteger(m_randomIndexParamHash,
        // m_randomIntSetOnEnter) even through an earlier hash (0x726daa), raises m_maxDelay
        // to m_minDelay when it is smaller (0x726db4), and schedules GetTotalTime() +
        // Random.Range(min, max) (0x726e38).
        [Test]
        public void RandomTriggerEntryWritesTheEntryIndexAndSchedulesFromRealTime()
        {
            WithRandomTrigger((owned, trigger, animator) =>
            {
                Assert.That(Get(trigger, "m_randomIntName"), Is.EqualTo("Idle_Index[Int]"));
                Assert.That(Get(trigger, "m_minDelay"), Is.EqualTo(1f));
                Assert.That(Get(trigger, "m_maxDelay"), Is.EqualTo(5f));
                Assert.That(Get(trigger, "m_timeCategory"), Is.EqualTo(TimeCategory.PlayerAnimation));
                Assert.That(Get(trigger, "m_nextTriggerTime"), Is.EqualTo(-1f));
                Assert.That(((SystemRef<TimeManager>)Get(trigger, "m_timeManagerRef")).SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<TimeManager>()));

                Set(trigger, "m_triggerName", "Fidget");
                Set(trigger, "m_randomIntSetOnEnter", 3);
                WithSeed(0x4C55, () =>
                {
                    float delay = Random.Range(1f, 5f);
                    Random.InitState(0x4C55);
                    float before = Time.realtimeSinceStartup;
                    trigger.OnStateEnter(animator, default, 0);
                    float after = Time.realtimeSinceStartup;
                    Assert.That((float)Get(trigger, "m_nextTriggerTime"), Is.InRange(before + delay, after + delay));
                });
                Assert.That(animator.GetInteger("Idle_Index[Int]"), Is.EqualTo(3));
                Assert.That(Get(trigger, "m_triggerHash"), Is.EqualTo(Animator.StringToHash("Fidget")));
                Assert.That(Get(trigger, "m_randomIndexParamHash"), Is.EqualTo(Animator.StringToHash("Idle_Index[Int]")));

                Set(trigger, "m_randomIntName", "");
                Set(trigger, "m_randomIntSetOnEnter", 7);
                Set(trigger, "m_minDelay", 4f);
                Set(trigger, "m_maxDelay", 2f);
                float start = Time.realtimeSinceStartup;
                trigger.OnStateEnter(animator, default, 0);
                float end = Time.realtimeSinceStartup;
                Assert.That(animator.GetInteger("Idle_Index[Int]"), Is.EqualTo(7), "an empty name writes through the earlier hash");
                Assert.That(Get(trigger, "m_maxDelay"), Is.EqualTo(4f), "the maximum is raised to the minimum");
                Assert.That((float)Get(trigger, "m_nextTriggerTime"), Is.InRange(start + 4f, end + 4f));
            });
        }

        // OnStateUpdate 0x06000e82 (0x726ED0) reads GetTotalTime first and fires only when
        // m_nextTriggerTime > 0 (ja, 0x726f32) and time >= it (jb skips, 0x726f69). Firing
        // writes SetInteger(hash, Random.Range(1, m_numberOfStates + 1)) for a nonempty int
        // name (inc esi / edi 1, 0x726f82), SetTrigger for a nonempty trigger name (0x726fb6),
        // then reschedules time + Random.Range(min, max).
        [Test]
        public void RandomTriggerUpdateFiresOnlyAfterAPositiveScheduledTime()
        {
            WithRandomTrigger((owned, trigger, animator) =>
            {
                Set(trigger, "m_triggerName", "Fidget");
                Set(trigger, "m_numberOfStates", 3);
                trigger.OnStateEnter(animator, default, 0);

                foreach (float scheduled in new[] { 0f, -1f, float.MaxValue })
                {
                    Set(trigger, "m_nextTriggerTime", scheduled);
                    animator.SetInteger("Idle_Index[Int]", -5);
                    trigger.OnStateUpdate(animator, default, 0);
                    Assert.That(animator.GetInteger("Idle_Index[Int]"), Is.EqualTo(-5), "no fire at " + scheduled);
                    Assert.That(animator.GetBool("Fidget"), Is.False);
                    Assert.That(Get(trigger, "m_nextTriggerTime"), Is.EqualTo(scheduled));
                }

                var drawn = new HashSet<int>();
                WithSeed(0x4C56, () =>
                {
                    for (int i = 0; i < 64; ++i)
                    {
                        Set(trigger, "m_nextTriggerTime", float.Epsilon);
                        animator.ResetTrigger("Fidget");
                        float before = Time.realtimeSinceStartup;
                        trigger.OnStateUpdate(animator, default, 0);
                        float after = Time.realtimeSinceStartup;
                        drawn.Add(animator.GetInteger("Idle_Index[Int]"));
                        Assert.That(animator.GetBool("Fidget"), Is.True);
                        Assert.That((float)Get(trigger, "m_nextTriggerTime"), Is.InRange(before + 1f, after + 5f));
                    }
                });
                Assert.That(drawn, Is.EquivalentTo(new[] { 1, 2, 3 }), "1..m_numberOfStates inclusive");

                Set(trigger, "m_randomIntName", "");
                Set(trigger, "m_triggerName", null);
                Set(trigger, "m_nextTriggerTime", float.Epsilon);
                animator.SetInteger("Idle_Index[Int]", -5);
                animator.ResetTrigger("Fidget");
                trigger.OnStateUpdate(animator, default, 0);
                Assert.That(animator.GetInteger("Idle_Index[Int]"), Is.EqualTo(-5));
                Assert.That(animator.GetBool("Fidget"), Is.False);
                Assert.That((float)Get(trigger, "m_nextTriggerTime"), Is.GreaterThan(1f), "still rescheduled");
            });
        }

        // With a registered TimeManager both messages take the resolved branch: SystemRef.Get()
        // and TimeExtensions.GetTotalTime(manager, m_timeCategory), with the category read
        // from the field (+0x38; OnStateEnter 0x726e0e-0x726e22, OnStateUpdate 0x726f4c-
        // 0x726f54), so a changed m_timeCategory selects the other total. OnStateUpdate fires at time >= the
        // scheduled time (ucomiss xmm0, xmm2; jb 0x726f69), so equality fires, and it
        // reschedules from the time it read (0x726fcc). The manager is never awakened: the
        // Editor's Reset on AddComponent faults on its null configuration, as in the real
        // TimeManager verification, and its PlayerAnimation and PlayerMovement totals are
        // written directly. Both totals are far above Time.realtimeSinceStartup, so the
        // real-time branch, the other category or a strict > could not fire where it fires.
        [Test]
        public void RandomTriggerReadsTheRegisteredTimeManagerForItsCategory()
        {
            const float animationTime = 1048576f;
            const float movementTime = 4194304f;
            FieldInfo lookupField = typeof(TimeExtensions).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
            object priorLookup = lookupField.GetValue(null);
            var owned = new Owned();
            VerificationFixtureOwnership.LifecycleRow timeRow = null;
            TimeManager manager = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                // TimeScaledComponent's static reference can leave this row behind, so it is
                // borrowed while idle rather than required absent.
                timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>());
                var animation = owned.Add(ScriptableObject.CreateInstance<TimeCategoryObject>());
                var movement = owned.Add(ScriptableObject.CreateInstance<TimeCategoryObject>());
                // TimeCategoryObject equality is by GUID, so the two keys need distinct ones.
                Set(animation, "m_guid", "lucid-animator-random-trigger-animation");
                Set(movement, "m_guid", "lucid-animator-random-trigger-movement");
                var lookup = owned.Add(ScriptableObject.CreateInstance<TimeCategoryLookup>());
                lookup.Dictionary.Add(TimeCategory.PlayerAnimation, animation);
                lookup.Dictionary.Add(TimeCategory.PlayerMovement, movement);
                lookupField.SetValue(null, lookup);

                LogAssert.Expect(LogType.Exception, new Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
                manager = owned.Inactive("TimeManager fixture").AddComponent<TimeManager>();
                ProcessManager.RegisterSystem(manager);
                var totals = (Dictionary<TimeCategoryObject, float>)Get(manager, "m_totalTimeLookup");
                totals[animation] = animationTime;
                totals[movement] = movementTime;
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(animationTime), "the fixture times are not reachable in real time");

                GameObject root = owned.Active("AnimatorRandomTrigger time fixture");
                Animator animator = owned.BoundAnimator(root, controller =>
                {
                    controller.AddParameter("Idle_Index[Int]", AnimatorControllerParameterType.Int);
                    controller.AddParameter("Fidget", AnimatorControllerParameterType.Trigger);
                });
                var trigger = owned.Add(ScriptableObject.CreateInstance<AnimatorRandomTrigger>());
                Assert.That(((SystemRef<TimeManager>)Get(trigger, "m_timeManagerRef")).GetSafe(), Is.SameAs(manager));
                Set(trigger, "m_triggerName", "Fidget");
                Set(trigger, "m_numberOfStates", 3);

                WithSeed(0x4C57, () =>
                {
                    float delay = Random.Range(1f, 5f);
                    Random.InitState(0x4C57);
                    trigger.OnStateEnter(animator, default, 0);
                    Assert.That(Get(trigger, "m_nextTriggerTime"), Is.EqualTo(animationTime + delay), "entry schedules from the category's total time");
                });

                Set(trigger, "m_nextTriggerTime", 2097152f);
                animator.SetInteger("Idle_Index[Int]", -5);
                animator.ResetTrigger("Fidget");
                trigger.OnStateUpdate(animator, default, 0);
                Assert.That(animator.GetBool("Fidget"), Is.False, "PlayerMovement's later total is not read");
                Assert.That(animator.GetInteger("Idle_Index[Int]"), Is.EqualTo(-5));
                Assert.That(Get(trigger, "m_nextTriggerTime"), Is.EqualTo(2097152f));

                WithSeed(0x4C58, () =>
                {
                    int index = Random.Range(1, 4);
                    float delay = Random.Range(1f, 5f);
                    Random.InitState(0x4C58);
                    Set(trigger, "m_nextTriggerTime", animationTime);
                    trigger.OnStateUpdate(animator, default, 0);
                    Assert.That(animator.GetBool("Fidget"), Is.True, "a scheduled time equal to the total fires");
                    Assert.That(animator.GetInteger("Idle_Index[Int]"), Is.EqualTo(index));
                    Assert.That(Get(trigger, "m_nextTriggerTime"), Is.EqualTo(animationTime + delay));
                });

                Set(trigger, "m_timeCategory", TimeCategory.PlayerMovement);
                animator.ResetTrigger("Fidget");
                WithSeed(0x4C59, () =>
                {
                    int index = Random.Range(1, 4);
                    float delay = Random.Range(1f, 5f);
                    Random.InitState(0x4C59);
                    Set(trigger, "m_nextTriggerTime", 2097152f);
                    trigger.OnStateUpdate(animator, default, 0);
                    Assert.That(animator.GetBool("Fidget"), Is.True, "the configured category's total is read");
                    Assert.That(animator.GetInteger("Idle_Index[Int]"), Is.EqualTo(index));
                    Assert.That(Get(trigger, "m_nextTriggerTime"), Is.EqualTo(movementTime + delay));
                });
            },
            () => { if (manager != null) ProcessManager.UnregisterSystem(manager); },
            () => timeRow?.Dispose(),
            owned.Dispose,
            () => lookupField.SetValue(null, priorLookup));
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorSampler ----

        // SampleAnimatorState 0x060024fc (0x59CC00) reads GetCurrentAnimatorStateInfo(0)
        // (0x59cc1a) and plays its shortNameHash on layer 0 at m_sampleFrame (+0x28, 0x59cc52)
        // before Update(0) (0x59cc60). OnEnable 0x060024fb (0x59CB70) does the same, then
        // disables the Animator (0x59cbdd) and sets cullingMode 0, AlwaysAnimate (0x59cbea).
        // The current state B, not the default A, is the one sampled.
        [Test]
        public void SamplerPlaysTheCurrentStateAtTheSampleFrameAndDisablesTheAnimator()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorSampler fixture");
                Animator animator = owned.BoundAnimator(root, controller =>
                {
                    controller.AddMotion(owned.Clip("A", 10f));
                    controller.AddMotion(owned.Clip("B", -10f));
                });
                var sampler = root.AddComponent<AnimatorSampler>();
                Set(sampler, "m_animator", animator);
                animator.Play("B", 0, 0f);
                animator.Update(0f);

                Set(sampler, "m_sampleFrame", 0.25f);
                sampler.SampleAnimatorState();
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(state.shortNameHash, Is.EqualTo(Animator.StringToHash("B")));
                Assert.That(state.normalizedTime, Is.EqualTo(0.25f).Within(1e-5f));
                Assert.That(root.transform.localPosition.x, Is.EqualTo(-2.5f).Within(1e-4f));
                Assert.That(animator.enabled, Is.True, "sampling alone keeps the Animator enabled");

                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                Set(sampler, "m_sampleFrame", 0.75f);
                Call(sampler, "OnEnable");
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0.75f).Within(1e-5f));
                Assert.That(root.transform.localPosition.x, Is.EqualTo(-7.5f).Within(1e-4f));
                Assert.That(animator.enabled, Is.False);
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnValidate 0x060024fd (0x59CC70): a null m_animator (op_Equality, 0x59ccdb) takes
        // gameObject.GetComponentInChildren<Animator>() (0x59ccfb) and, only when that is a
        // null reference (0x59cd0f), AddComponent<Animator>() (0x59cd2d). It then samples only
        // while the Animator's GameObject is active in the hierarchy (0x59cd50).
        [Test]
        public void SamplerValidationFindsAChildAnimatorBeforeAddingOne()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorSampler validation fixture");
                var child = owned.Active("AnimatorSampler child fixture");
                child.transform.SetParent(root.transform, false);
                Animator animator = owned.BoundAnimator(child, controller => controller.AddMotion(owned.Clip("A", 10f)));
                var sampler = root.AddComponent<AnimatorSampler>();
                Assert.That(root.GetComponent<Animator>(), Is.Null, "fixture root has no Animator of its own");

                Set(sampler, "m_animator", null);
                Set(sampler, "m_sampleFrame", 0.5f);
                Call(sampler, "OnValidate");
                Assert.That(Get(sampler, "m_animator"), Is.SameAs(animator));
                Assert.That(root.GetComponent<Animator>(), Is.Null, "a found child Animator adds none");
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0.5f).Within(1e-5f));
                Assert.That(child.transform.localPosition.x, Is.EqualTo(5f).Within(1e-4f));

                child.SetActive(false);
                Set(sampler, "m_sampleFrame", 0.1f);
                // Play and Update on an inactive Animator are no-ops in edit mode, so the
                // activeInHierarchy guard has no observable effect here; this checks only that
                // validation does not throw and leaves the earlier pose in place.
                Assert.DoesNotThrow(() => Call(sampler, "OnValidate"), "validating with an inactive Animator does not throw");
                Assert.That(child.transform.localPosition.x, Is.EqualTo(5f).Within(1e-4f), "the earlier pose is kept");

                // The Animator added here has no controller: SampleAnimatorState (called at 0x59cd50)
                // makes the engine warn twice (run30 output order).
                LogAssert.Expect(LogType.Warning, "Animator is not playing an AnimatorController");
                LogAssert.Expect(LogType.Warning, "Animator does not have an AnimatorController");
                Set(sampler, "m_animator", null);
                Call(sampler, "OnValidate");
                var added = root.GetComponent<Animator>();
                Assert.That(added, Is.Not.Null, "without an active child Animator one is added");
                Assert.That(Get(sampler, "m_animator"), Is.SameAs(added));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorSetBoolEvent ----

        // InvokeEvent 0x06000531 (0x52F070) invokes m_boolEvent (+0x20) with m_settingString
        // (+0x28) and m_settingBool (+0x30). AnimationBoolEvent 0x0200009f is a Serializable
        // UnityEvent<string, bool>.
        [Test]
        public void SetBoolEventInvokesWithTheConfiguredStringAndBool()
        {
            using (var owned = new Owned())
            {
                var setter = owned.Inactive("AnimatorSetBoolEvent fixture").AddComponent<AnimatorSetBoolEvent>();
                Assert.That(typeof(AnimatorSetBoolEvent.AnimationBoolEvent).BaseType, Is.EqualTo(typeof(UnityEvent<string, bool>)));
                Assert.That((int)typeof(AnimatorSetBoolEvent.AnimationBoolEvent).Attributes, Is.EqualTo(0x00102002));
                var boolEvent = new AnimatorSetBoolEvent.AnimationBoolEvent();
                var calls = new List<(string, bool)>();
                boolEvent.AddListener((name, value) => calls.Add((name, value)));
                Set(setter, "m_boolEvent", boolEvent);
                Set(setter, "m_settingString", "Open");
                Set(setter, "m_settingBool", true);

                setter.InvokeEvent();
                Set(setter, "m_settingBool", false);
                setter.InvokeEvent();
                Assert.That(calls, Is.EqualTo(new[] { ("Open", true), ("Open", false) }));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorSetFloatCurve ----

        // OnStateEnter 0x06000e89 (0x727410) stores m_valid = !IsNullOrEmpty(name) (0x727433)
        // and only when valid hashes it and runs UpdateValue. UpdateValue 0x06000e8b
        // (0x7274A0) writes SetFloat(hash, curve.Evaluate(stateInfo.normalizedTime)).
        // OnStateUpdate 0x06000e8a repeats it while valid. OnStateExit 0x06000e8c (0x727560)
        // writes curve.Evaluate(curve.TotalTime()) (0x727585, the last key's time) only when
        // valid and m_snapToEndOnExit (+0x28).
        [Test]
        public void SetFloatCurveEvaluatesNormalisedTimeAndSnapsToTheLastKeyOnExit()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorSetFloatCurve fixture");
                Animator animator = owned.BoundAnimator(root, controller => controller.AddParameter("Glow", AnimatorControllerParameterType.Float));
                var setter = owned.Add(ScriptableObject.CreateInstance<AnimatorSetFloatCurve>());
                var curve = new AnimationCurve(new Keyframe(0f, 0f, 4f, 4f), new Keyframe(1f, 4f, 6f, 6f), new Keyframe(2f, 10f, 6f, 6f));
                Set(setter, "m_valueOverTime", curve);

                Set(setter, "m_floatParameterToSet", "");
                setter.OnStateEnter(animator, StateAt(0.5f), 0);
                setter.OnStateUpdate(animator, StateAt(1.5f), 0);
                Set(setter, "m_snapToEndOnExit", true);
                setter.OnStateExit(animator, StateAt(1.5f), 0);
                Assert.That(Get(setter, "m_valid"), Is.False);
                Assert.That(animator.GetFloat("Glow"), Is.EqualTo(0f), "an empty name writes nothing");

                Set(setter, "m_snapToEndOnExit", false);
                Set(setter, "m_floatParameterToSet", "Glow");
                setter.OnStateEnter(animator, StateAt(0.5f), 0);
                Assert.That(Get(setter, "m_parameterHash"), Is.EqualTo(Animator.StringToHash("Glow")));
                Assert.That(animator.GetFloat("Glow"), Is.EqualTo(curve.Evaluate(0.5f)));
                setter.OnStateUpdate(animator, StateAt(1.5f), 0);
                Assert.That(animator.GetFloat("Glow"), Is.EqualTo(curve.Evaluate(1.5f)), "past 1 the curve keeps being followed");

                setter.OnStateExit(animator, StateAt(0.25f), 0);
                Assert.That(animator.GetFloat("Glow"), Is.EqualTo(curve.Evaluate(1.5f)), "exit without the snap writes nothing");
                Set(setter, "m_snapToEndOnExit", true);
                setter.OnStateExit(animator, StateAt(0.25f), 0);
                Assert.That(animator.GetFloat("Glow"), Is.EqualTo(10f), "exit snaps to the value at the last key, not at time 1");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorSuppressWarnings ----

        // Reset 0x06000534 (0x52F110) stores GetComponent<Animator>() unconditionally;
        // Awake 0x06000535 (0x52F160) sets logWarnings false (0x52f16d).
        [Test]
        public void SuppressWarningsResetFindsTheAnimatorAndAwakeSilencesIt()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Inactive("AnimatorSuppressWarnings fixture");
                var suppress = root.AddComponent<AnimatorSuppressWarnings>();
                var animator = root.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null, "RequireComponent(Animator)");
                var other = owned.Inactive("Other animator fixture").AddComponent<Animator>();

                Set(suppress, "m_animator", other);
                Call(suppress, "Reset");
                Assert.That(Get(suppress, "m_animator"), Is.SameAs(animator), "Reset replaces an assigned Animator");

                animator.logWarnings = true;
                Call(suppress, "Awake");
                Assert.That(animator.logWarnings, Is.False);
                Assert.That(other.logWarnings, Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorToCinemachineVirtualCamera ----

        // LateUpdate 0x06000537 (0x52F190) returns for a null m_animatedCameraData (0x52f1dc),
        // copies FocalLengthOutput (+0x68) to a non-null m_testCamera's focalLength
        // (0x52f21d), returns for a null virtual camera (0x52f23e), and otherwise stores
        // Camera.FocalLengthToFieldOfView(FocalLengthOutput, 24) (0x29a7500, 0x52f25e) in
        // m_Lens.FieldOfView (+0xb8). The expected angle is 2 * atan(24 / (2 * f)) in degrees.
        // The virtual camera's Reset, sent on AddComponent, builds its pipeline child through
        // the static CreatePipelineOverride; the case leases that hook as null so the child is
        // a plain owned GameObject with no undo record.
        [Test]
        public void CinemachineCameraTakesTheAnimatedFocalLengthAsATwentyFourMillimetreFieldOfView()
        {
            CinemachineVirtualCamera.CreatePipelineDelegate priorOverride = CinemachineVirtualCamera.CreatePipelineOverride;
            using (var owned = new Owned())
            {
                CinemachineVirtualCamera virtualCamera;
                try
                {
                    CinemachineVirtualCamera.CreatePipelineOverride = null;
                    virtualCamera = owned.Inactive("CinemachineVirtualCamera fixture").AddComponent<CinemachineVirtualCamera>();
                }
                finally
                {
                    CinemachineVirtualCamera.CreatePipelineOverride = priorOverride;
                }
                var data = owned.Inactive("AnimationCustomDataCamera fixture").AddComponent<AnimationCustomDataCamera>();
                var testCamera = owned.Inactive("Test camera fixture").AddComponent<Camera>();
                testCamera.usePhysicalProperties = true;
                testCamera.focalLength = 35f;
                var driver = owned.Inactive("AnimatorToCinemachineVirtualCamera fixture").AddComponent<AnimatorToCinemachineVirtualCamera>();
                typeof(AnimationCustomDataCamera).GetProperty(nameof(AnimationCustomDataCamera.FocalLengthOutput)).SetValue(data, 50f);
                virtualCamera.m_Lens.FieldOfView = 77f;

                Set(driver, "m_cinemachineVirtualCamera", virtualCamera);
                Set(driver, "m_testCamera", testCamera);
                Call(driver, "LateUpdate");
                Assert.That(virtualCamera.m_Lens.FieldOfView, Is.EqualTo(77f), "no camera data, nothing written");
                Assert.That(testCamera.focalLength, Is.EqualTo(35f));

                Set(driver, "m_animatedCameraData", data);
                Call(driver, "LateUpdate");
                float expected = (float)(2.0 * Math.Atan(24.0 / (2.0 * 50.0)) * 180.0 / Math.PI);
                Assert.That(virtualCamera.m_Lens.FieldOfView, Is.EqualTo(expected).Within(1e-4f), "24 mm sensor height");
                Assert.That(testCamera.focalLength, Is.EqualTo(50f).Within(1e-4f));

                typeof(AnimationCustomDataCamera).GetProperty(nameof(AnimationCustomDataCamera.FocalLengthOutput)).SetValue(data, 24f);
                Set(driver, "m_testCamera", null);
                Call(driver, "LateUpdate");
                Assert.That(virtualCamera.m_Lens.FieldOfView, Is.EqualTo((float)(2.0 * Math.Atan(0.5) * 180.0 / Math.PI)).Within(1e-4f), "a missing test camera still drives the lens");

                Set(driver, "m_testCamera", testCamera);
                Set(driver, "m_cinemachineVirtualCamera", null);
                Assert.DoesNotThrow(() => Call(driver, "LateUpdate"));
                Assert.That(testCamera.focalLength, Is.EqualTo(24f).Within(1e-4f), "the test camera is written before the virtual camera check");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorToMaterialPropertyData, AnimatorToMaterialProperty ----

        private static AnimatorToMaterialPropertyData.ParameterToPropertyPair Pair(string animParameter, string materialProperty)
        {
            var pair = new AnimatorToMaterialPropertyData.ParameterToPropertyPair();
            Set(pair, "m_animParameter", animParameter);
            Set(pair, "m_materialProperty", materialProperty);
            return pair;
        }

        // Awake 0x06000540 (0x52F850) loads m_parametersToProperties (+0x18) and its length (0x52f85e)
        // with no null test, and CreateInstance sends Awake before the array is assigned, so the
        // original body faults there and Unity logs the exception.
        private static AnimatorToMaterialPropertyData CreateMaterialPropertyData()
        {
            LogAssert.Expect(LogType.Exception, new Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
            return ScriptableObject.CreateInstance<AnimatorToMaterialPropertyData>();
        }

        // ParameterToPropertyPair.UpdateCachedValues 0x06000547 (0x52F8B0) stores
        // Animator.StringToHash(m_animParameter) (+0x20) and Shader.PropertyToID(
        // m_materialProperty) (+0x24); both hash fields are NonSerialized (flags 0x0081).
        // Awake 0x06000540, OnValidate 0x0600053f and UpdateCachedValues 0x06000541 each
        // refresh every pair of m_parametersToProperties.
        [Test]
        public void MaterialPropertyDataCachesEveryPairOnAwakeValidateAndUpdate()
        {
            using (var owned = new Owned())
            {
                Type pairType = typeof(AnimatorToMaterialPropertyData.ParameterToPropertyPair);
                Assert.That((int)Field(pairType, "m_animParameterHash").Attributes, Is.EqualTo(0x0081));
                Assert.That((int)Field(pairType, "m_materialPropertyHash").Attributes, Is.EqualTo(0x0081));

                var data = owned.Add(CreateMaterialPropertyData());
                AnimatorToMaterialPropertyData.ParameterToPropertyPair first = Pair("Glow", "_Glow");
                AnimatorToMaterialPropertyData.ParameterToPropertyPair second = Pair("Fade", "_Fade");
                var pairs = new[] { first, second };
                Set(data, "m_parametersToProperties", pairs);
                Assert.That(data.ParametersToProperties, Is.SameAs(pairs));
                Assert.That(first.AnimParameterHash, Is.EqualTo(0));

                foreach (string refresh in new[] { "Awake", "OnValidate", "UpdateCachedValues" })
                {
                    Set(first, "m_animParameter", refresh + "Glow");
                    Set(second, "m_materialProperty", "_" + refresh + "Fade");
                    Call(data, refresh);
                    Assert.That(first.AnimParameter, Is.EqualTo(refresh + "Glow"));
                    Assert.That(first.AnimParameterHash, Is.EqualTo(Animator.StringToHash(refresh + "Glow")), refresh);
                    Assert.That(first.MaterialPropertyHash, Is.EqualTo(Shader.PropertyToID("_Glow")), refresh);
                    Assert.That(second.MaterialProperty, Is.EqualTo("_" + refresh + "Fade"));
                    Assert.That(second.MaterialPropertyHash, Is.EqualTo(Shader.PropertyToID("_" + refresh + "Fade")), refresh);
                    Assert.That(second.AnimParameterHash, Is.EqualTo(Animator.StringToHash("Fade")), refresh);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        // LateUpdate 0x0600053b (0x52F420) walks ParametersToProperties, skips a pair whose
        // AnimParameterHash is 0 (0x52f4f5) or whose MaterialProperty is empty (0x52f501),
        // reads GetFloat(hash) (0x52f517) and, for m_targetMaterialIndex >= 0 (jns, 0x52f52b),
        // writes only m_materials[index]; a negative index writes every listed material
        // (0x52f58d). An empty parameter name hashes to 0, so its pair is skipped. Both
        // skipped pairs target _Fade, which starts at a sentinel: the zero-hash pair would
        // write GetFloat(0) = 0, and the empty-property pair is given _Fade's ID by hand so
        // that dropping the IsNullOrEmpty test would write 0.5 there.
        [Test]
        public void MaterialPropertyDrivesTheIndexedMaterialOrEveryMaterial()
        {
            using (var owned = new Owned())
            {
                var shader = owned.Add(ShaderUtil.CreateShaderAsset(
                    "Shader \"Hidden/ProjectLucid/AnimatorProperties\" { Properties { _Glow (\"\", Float) = 0 _Fade (\"\", Float) = 0 } SubShader { Pass { } } }"));
                var first = owned.Add(new Material(shader) { hideFlags = HideFlags.HideAndDontSave });
                var second = owned.Add(new Material(shader) { hideFlags = HideFlags.HideAndDontSave });
                GameObject root = owned.Active("AnimatorToMaterialProperty fixture");
                Animator animator = owned.BoundAnimator(root, controller =>
                {
                    controller.AddParameter("Glow", AnimatorControllerParameterType.Float);
                    controller.AddParameter("Fade", AnimatorControllerParameterType.Float);
                });
                var data = owned.Add(CreateMaterialPropertyData());
                AnimatorToMaterialPropertyData.ParameterToPropertyPair noProperty = Pair("Fade", "");
                Set(data, "m_parametersToProperties", new[] { Pair("Glow", "_Glow"), Pair("", "_Fade"), noProperty });
                data.UpdateCachedValues();
                Set(noProperty, "m_materialPropertyHash", Shader.PropertyToID("_Fade"));
                first.SetFloat("_Fade", 0.9f);
                second.SetFloat("_Fade", 0.9f);
                var driver = root.AddComponent<AnimatorToMaterialProperty>();
                Assert.That(Get(driver, "m_targetMaterialIndex"), Is.EqualTo(-1));
                Assert.That(((List<Material>)Get(driver, "m_materials")).Capacity, Is.EqualTo(1), "new List<Material>(1)");
                Set(driver, "m_animator", animator);
                Set(driver, "m_propertyData", data);
                Set(driver, "m_materials", new List<Material> { first, second });
                animator.SetFloat("Glow", 0.75f);
                animator.SetFloat("Fade", 0.5f);

                Set(driver, "m_targetMaterialIndex", 1);
                Call(driver, "LateUpdate");
                Assert.That(second.GetFloat("_Glow"), Is.EqualTo(0.75f));
                Assert.That(first.GetFloat("_Glow"), Is.EqualTo(0f), "only the indexed material is written");
                Assert.That(second.GetFloat("_Fade"), Is.EqualTo(0.9f), "pairs with a zero hash or no property are skipped");

                animator.SetFloat("Glow", 0.25f);
                Set(driver, "m_targetMaterialIndex", -1);
                Call(driver, "LateUpdate");
                Assert.That(first.GetFloat("_Glow"), Is.EqualTo(0.25f));
                Assert.That(second.GetFloat("_Glow"), Is.EqualTo(0.25f), "a negative index writes every material");
                Assert.That(first.GetFloat("_Fade"), Is.EqualTo(0.9f));
                Assert.That(second.GetFloat("_Fade"), Is.EqualTo(0.9f));

                animator.SetFloat("Glow", 0.6f);
                Set(driver, "m_targetMaterialIndex", 0);
                Call(driver, "LateUpdate");
                Assert.That(first.GetFloat("_Glow"), Is.EqualTo(0.6f));
                Assert.That(second.GetFloat("_Glow"), Is.EqualTo(0.25f), "index 0 takes the indexed path (jns), not every material");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Start 0x06000539 (0x52F290) tail-calls Renderer.GetMaterials (0x52f2a2, 0x060005f7)
        // with m_materials, which replaces the list's contents with per-renderer instances,
        // not the shared materials; the Editor reports that instantiation outside play mode.
        // OnDestroy 0x0600053a (0x52F2B0) calls Object.Destroy on every listed material
        // (0x52f365) and then nulls the list (0x52f3a5). Edit mode refuses Destroy with an
        // error per material and keeps them.
        [Test]
        public void MaterialPropertyStartFillsAndDestroyReleasesTheMaterialList()
        {
            using (var owned = new Owned())
            {
                var stale = owned.Add(new Material(Shader.Find("Hidden/InternalErrorShader")) { hideFlags = HideFlags.HideAndDontSave });
                var kept = owned.Add(new Material(Shader.Find("Hidden/InternalErrorShader")) { hideFlags = HideFlags.HideAndDontSave });
                var shared = owned.Add(new Material(Shader.Find("Hidden/InternalErrorShader")) { hideFlags = HideFlags.HideAndDontSave });
                GameObject root = owned.Inactive("AnimatorToMaterialProperty lifetime fixture");
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { shared };
                var driver = root.AddComponent<AnimatorToMaterialProperty>();
                Set(driver, "m_renderer", renderer);
                var list = (List<Material>)Get(driver, "m_materials");
                list.Add(stale);

                LogAssert.Expect(LogType.Error, EditModeMaterialInstance);
                Call(driver, "Start");
                Assert.That(Get(driver, "m_materials"), Is.SameAs(list));
                Assert.That(list.Count, Is.EqualTo(1), "the renderer's materials replace the list contents");
                Material instance = owned.Add(list[0]);
                Assert.That(instance, Is.Not.SameAs(shared), "GetMaterials, not GetSharedMaterials");
                Assert.That(instance.shader, Is.SameAs(shared.shader));

                list.Add(stale);
                list.Add(kept);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                Call(driver, "OnDestroy");
                Assert.That(Get(driver, "m_materials"), Is.Null);
                Assert.That(instance != null && stale != null && kept != null, Is.True, "edit mode refuses the destroys");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnValidate 0x0600053c (0x52F620) reads sharedMaterials once (0x52f6aa) and, only when
        // the index is at or past its length (jge, 0x52f6b5), stores length - 1 (0x52f6bc) and
        // tail-calls HLOutput.LogError with the literal and this (0x52f6ec). A missing renderer
        // skips the check and there is no lower bound.
        [Test]
        public void MaterialPropertyValidationClampsOnlyAnIndexPastTheEnd()
        {
            using (var owned = new Owned())
            {
                var material = owned.Add(new Material(Shader.Find("Hidden/InternalErrorShader")) { hideFlags = HideFlags.HideAndDontSave });
                GameObject root = owned.Inactive("AnimatorToMaterialProperty validation fixture");
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { material, material };
                var driver = root.AddComponent<AnimatorToMaterialProperty>();

                using (var capture = new OwnedHLOutputErrorCapture())
                {
                    Set(driver, "m_renderer", null);
                    Set(driver, "m_targetMaterialIndex", 9);
                    Call(driver, "OnValidate");
                    Assert.That(Get(driver, "m_targetMaterialIndex"), Is.EqualTo(9), "no renderer, no clamp");

                    Set(driver, "m_renderer", renderer);
                    foreach (int inRange in new[] { 1, 0, -1, -7 })
                    {
                        Set(driver, "m_targetMaterialIndex", inRange);
                        Call(driver, "OnValidate");
                        Assert.That(Get(driver, "m_targetMaterialIndex"), Is.EqualTo(inRange));
                    }
                    Assert.That(capture.Errors, Is.Empty);

                    foreach (int past in new[] { 2, 6 })
                    {
                        Set(driver, "m_targetMaterialIndex", past);
                        Call(driver, "OnValidate");
                        Assert.That(Get(driver, "m_targetMaterialIndex"), Is.EqualTo(1));
                    }
                    const string error = "Target material index is out of bounds for the target renderer.";
                    Assert.That(capture.Errors, Is.EqualTo(new[] { error + "|0", error + "|0" }));
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---- AnimatorToRendererEnabled ----

        // ..ctor 0x0600054e stores m_threshold 0.5 (0x3f000000). Start 0x0600054a (0x52F980)
        // hashes m_animParameter and calls SetRenderers(false). SetRenderers 0x0600054c
        // (0x52F9B0) sets each non-null met renderer (op_Equality skip, 0x52fa39) to the flag,
        // then, only when the not-met array is not null (0x52fa59), sets every one of them,
        // without a per-element test, to its negation. LateUpdate 0x0600054b (0x52FAA0)
        // computes GetFloat > m_threshold (seta, so equality is not met) and calls
        // SetRenderers only when that differs from m_thresholdMet (0x52fac5).
        [Test]
        public void RendererEnabledStartsNotMetAndSwitchesOnlyOnAStrictCrossing()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorToRendererEnabled fixture");
                Animator animator = owned.BoundAnimator(root, controller => controller.AddParameter("Open", AnimatorControllerParameterType.Float));
                var met = owned.Inactive("Met renderer fixture").AddComponent<MeshRenderer>();
                var notMet = owned.Inactive("Not met renderer fixture").AddComponent<MeshRenderer>();
                var driver = root.AddComponent<AnimatorToRendererEnabled>();
                Assert.That(Get(driver, "m_threshold"), Is.EqualTo(0.5f));
                Set(driver, "m_animator", animator);
                Set(driver, "m_animParameter", "Open");
                Set(driver, "m_onAnimThresholdMet", new Renderer[] { null, met });
                Set(driver, "m_onAnimThresholdNotMet", null);

                met.enabled = true;
                Assert.DoesNotThrow(() => Call(driver, "Start"), "a null met entry and a null not-met list are skipped");
                Assert.That(Get(driver, "m_animParameterHash"), Is.EqualTo(Animator.StringToHash("Open")));
                Assert.That(met.enabled, Is.False);

                Set(driver, "m_onAnimThresholdNotMet", new Renderer[] { notMet });
                notMet.enabled = false;
                Call(driver, "Start");
                Assert.That(notMet.enabled, Is.True, "Start shows the not-met renderers");

                animator.SetFloat("Open", 0.5f);
                Call(driver, "LateUpdate");
                Assert.That(Get(driver, "m_thresholdMet"), Is.False, "the threshold itself is not met");
                Assert.That(met.enabled, Is.False);

                animator.SetFloat("Open", 0.75f);
                Call(driver, "LateUpdate");
                Assert.That(Get(driver, "m_thresholdMet"), Is.True);
                Assert.That(met.enabled, Is.True);
                Assert.That(notMet.enabled, Is.False);

                notMet.enabled = true;
                Call(driver, "LateUpdate");
                Assert.That(notMet.enabled, Is.True, "an unchanged state touches no renderer");

                animator.SetFloat("Open", 0.25f);
                Call(driver, "LateUpdate");
                Assert.That(met.enabled, Is.False);
                Assert.That(notMet.enabled, Is.True);

                Set(driver, "m_onAnimThresholdNotMet", new Renderer[] { null });
                Assert.Throws<NullReferenceException>(() => Call(driver, "SetRenderers", true), "the not-met list has no per-renderer test");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnEnable 0x06000549 (0x52F940) disables the component for an empty m_animParameter
        // (0x52f965); Reset 0x0600054d (0x52FAF0) stores GetComponent<Animator>()
        // unconditionally.
        [Test]
        public void RendererEnabledDisablesItselfWithoutAParameterAndResetFindsTheAnimator()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Inactive("AnimatorToRendererEnabled enable fixture");
                var driver = root.AddComponent<AnimatorToRendererEnabled>();
                var animator = root.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null, "RequireComponent(Animator)");

                Set(driver, "m_animParameter", "Open");
                Call(driver, "OnEnable");
                Assert.That(driver.enabled, Is.True);
                Set(driver, "m_animParameter", "");
                Call(driver, "OnEnable");
                Assert.That(driver.enabled, Is.False);

                Set(driver, "m_animator", owned.Inactive("Other animator fixture").AddComponent<Animator>());
                Call(driver, "Reset");
                Assert.That(Get(driver, "m_animator"), Is.SameAs(animator));
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
