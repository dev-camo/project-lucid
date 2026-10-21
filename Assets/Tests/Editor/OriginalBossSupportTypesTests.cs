using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldMemberAccess;
using Owned = ProjectLucid.Tests.WorldTriggerTestSupport.Owned;

namespace ProjectLucid.Tests
{
    // The boss support components of Game.Runtime: BossHomingTarget (02000abb, 0x6E22E0..0x6E2D40),
    // BossBurstableComponent (020001f3, 0x7008D0), BossCrabManager (020001f4, 0x700910), BossDebug
    // (020001f5, 0x700A20), BossManagerTrigger (020001f9, 0x7036B0), BossVisualProxyModifier
    // (020009e8, 0x6A6E90), FourmenManager (02000253), GuardianManager (02000254, nested 02000255) and
    // FourmenVisuals (02000662 with the iterators 02000663 and 02000664, 0x579CE0..0x57A930). Every
    // component is a real one on an owned host; no Awake or OnEnable runs in Edit Mode, so a case
    // calls the lifecycle entry it covers itself. Bosses come from their real constructor, and the
    // BossManager that lists them from its own, with the SaveManager row its field initialiser reads
    // leased. Compiler-generated members (the cached callbacks) are left out of the declaration rows.
    public sealed class OriginalBossSupportTypesTests
    {
        private const string NullAndBounds = "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False)";
        private const string BoundsAndNull = "Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)";

        // The system a row currently holds, read through its untyped reference without registering anything.
        private static ISystem RegisteredSystem<T>() where T : class, ISystem =>
            ProcessManager.GetSystemRef(ProcessManager.GetDefaultName<T>()).GetSafe();

        private static string[] DeclaredMethods(Type type) => Methods(type).Where(name => !name.StartsWith("<")).ToArray();

        private static BossManager NewBossManager(Owned owned)
        {
            // BossManager's field initialiser caches a typed SaveManager reference.
            owned.LeaseRow<SaveManager>();
            return owned.Host("Lucid boss manager", false).AddComponent<BossManager>();
        }

        private static Boss NewBoss(Owned owned, string name, Vector3 position)
        {
            Boss boss = owned.Host(name, false).AddComponent<Boss>();
            boss.SetWorldPosition(position);
            return boss;
        }

        private sealed class FourmenRig
        {
            public FourmenManager Fourmen;
            public BossManager Manager;
            public Boss[] Bosses;
        }

        // A FourmenManager that has received its BossManager through its own callback, and that
        // manager listing the given number of bosses.
        private static FourmenRig NewFourmenRig(Owned owned, int count)
        {
            var rig = new FourmenRig { Manager = NewBossManager(owned), Bosses = new Boss[count] };
            rig.Fourmen = owned.Host("Lucid fourmen manager", false).AddComponent<FourmenManager>();
            Call(rig.Fourmen, "OnBossManagerValid", rig.Manager);
            for (int i = 0; i < count; i++)
            {
                rig.Bosses[i] = NewBoss(owned, "Lucid fourmen boss " + i, Vector3.zero);
                rig.Manager.Bosses.Add(rig.Bosses[i]);
            }
            return rig;
        }

        // ---- declarations ----

        [Test]
        public void BossHomingTargetDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Type type = typeof(BossHomingTarget);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(HomingTarget)));
            Assert.That(AttributeRow(type), Is.EqualTo("RequireComponentAttribute(Collider) " + BoundsAndNull));
            Assert.That(Fields(type), Is.EqualTo(new[]
            {
                "m_useAnimatedTargetIndicator:Boolean:0001", "m_animatedTargetIndicator:AnimatedTargetIndicator:0001",
                "m_characterTriggerTargetAbilities:CharacterAbilityTypeGroup:0001", "m_alwaysTargetableWhenActive:Boolean:0001",
                "m_instantiatedTargetIndicator:AnimatedTargetIndicator:0001", "m_instantiated:Boolean:0001", "m_characterManager:CharacterManager:0001"
            }));
            Assert.That(DeclaredMethods(type), Is.EqualTo(new[]
            {
                "get_AnimatedTargetIndicator:0886", "Awake:00c4", "OnTriggerEnter:00c4", "OnUpdate:00c6", "OnLateUpdate:00c6",
                "OnFixedUpdate:00c6", "RunUpdate:0081", "OnDisable:00c4", "OnEnable:00c4", ".ctor:1886"
            }));
            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(type.GetField("m_useAnimatedTargetIndicator", field)), Is.EqualTo("SerializeField()"));
            Assert.That(AttributeRow(type.GetField("m_characterTriggerTargetAbilities", field)), Is.EqualTo("SerializeField()"));
            Assert.That(AttributeRow(type.GetField("m_instantiated", field)), Is.Empty);
        }

        [Test]
        public void BossBurstableCrabManagerAndTriggerDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Type burstable = typeof(BossBurstableComponent);
            Assert.That((int)burstable.Attributes, Is.EqualTo(0x00100001));
            Assert.That(burstable.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(burstable.GetInterfaces(), Does.Contain(typeof(IPlayerDamageable)));
            Assert.That(AttributeRow(burstable), Is.EqualTo(NullAndBounds));
            Assert.That(Fields(burstable), Is.EqualTo(new[] { "m_boss:Boss:0001", "m_bossHomingTarget:BossHomingTarget:0001" }));
            Assert.That(DeclaredMethods(burstable), Is.EqualTo(new[] { "ReceiveHit:01e6", "RegisterHitPlayer:01e6", ".ctor:1886" }));

            Type crab = typeof(BossCrabManager);
            Assert.That((int)crab.Attributes, Is.EqualTo(0x00100001));
            Assert.That(crab.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(crab.GetInterfaces(), Does.Contain(typeof(ISystem)));
            Assert.That(AttributeRow(crab), Is.EqualTo(NullAndBounds));
            Assert.That(Fields(crab), Is.EqualTo(new[] { "m_attackWaveType:PrefabPoolType:0001" }));
            Assert.That(DeclaredMethods(crab), Is.EqualTo(new[] { "DespawnAttackWaves:0086", "OnDestroy:0081", ".ctor:1886" }));

            Type trigger = typeof(BossManagerTrigger);
            Assert.That((int)trigger.Attributes, Is.EqualTo(0x00100001));
            Assert.That(trigger.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeRow(trigger), Is.EqualTo(NullAndBounds));
            Assert.That(Fields(trigger), Is.EqualTo(new[] { "m_bossManagerRef:SystemRef`1:0001", "m_bossManager:BossManager:0001" }));
            Assert.That(DeclaredMethods(trigger), Is.EqualTo(new[] { "Start:0081", "Action_SetPhaseProgression:0086", ".ctor:1886" }));
        }

        [Test]
        public void BossDebugAndVisualProxyModifierDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Type debug = typeof(BossDebug);
            Assert.That((int)debug.Attributes, Is.EqualTo(0x00100001));
            Assert.That(debug.BaseType, Is.EqualTo(typeof(ActorDebug)));
            Assert.That(AttributeRow(debug), Is.EqualTo(NullAndBounds));
            Assert.That(Fields(debug), Is.Empty);
            Assert.That(DeclaredMethods(debug), Is.EqualTo(new[]
            {
                "get_ContainerIdentifierName:08c4", "Init:00c6", "OnBossInfo:0081", "OnDestroy:0081", ".ctor:1886"
            }));

            Type modifier = typeof(BossVisualProxyModifier);
            Assert.That((int)modifier.Attributes, Is.EqualTo(0x00100101));
            Assert.That(modifier.BaseType, Is.EqualTo(typeof(ActorVisualProxyModifier)));
            Assert.That(AttributeRow(modifier), Is.EqualTo(BoundsAndNull));
            Assert.That(Fields(modifier), Is.EqualTo(new[] { "m_bossDefinition:BossDefinition:0001", "m_bossManagerRef:SystemRef`1:0021" }));
            Assert.That(AttributeRow(modifier.GetField("m_bossDefinition", BindingFlags.Instance | BindingFlags.NonPublic)), Is.EqualTo("SerializeField()"));
            Assert.That(DeclaredMethods(modifier), Is.EqualTo(new[]
            {
                "Action_ShowVisualProxy:0086", "Action_HideVisualProxy:0086", "Action_DisableVisualProxy:0086", "Action_EnableVisualProxy:0086", ".ctor:1886"
            }));
        }

        [Test]
        public void FourmenAndGuardianManagerDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Type fourmen = typeof(FourmenManager);
            Assert.That((int)fourmen.Attributes, Is.EqualTo(0x00100001));
            Assert.That(fourmen.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(fourmen.GetInterfaces(), Does.Contain(typeof(ISystem)));
            Assert.That(AttributeRow(fourmen), Is.EqualTo(BoundsAndNull));
            Assert.That(Fields(fourmen), Is.EqualTo(new[]
            {
                "m_activeBossIndex:Int32:0001", "m_bossManagerRef:SystemRef`1:0001", "m_bossManager:BossManager:0001",
                "<BossesInMotion>k__BackingField:List`1:0001"
            }));
            Assert.That(DeclaredMethods(fourmen), Is.EqualTo(new[]
            {
                "get_BossesInMotion:0886", "set_BossesInMotion:0886", "Awake:0081", "OnBossManagerValid:0081", "ResetActiveBossIndex:0086",
                "IncrementActiveBossIndex:0086", "HasActiveBossUnit:0086", "ConfigureActiveBoss:0086", "ClearActiveBosses:0086",
                "ClearLoopStatus:0086", "HasCompletedActiveLoop:0086", "GetAveragePosition:0086", "CalculateLaserPositions:0086",
                "GetIndexForBoss:0086", "BossCount:0086", "OnDestroy:0081", ".ctor:1886"
            }));

            Type guardian = typeof(GuardianManager);
            Assert.That((int)guardian.Attributes, Is.EqualTo(0x00100001));
            Assert.That(guardian.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(guardian.GetInterfaces(), Does.Contain(typeof(ISystem)));
            Assert.That(AttributeRow(guardian), Is.EqualTo(BoundsAndNull));
            Assert.That(Fields(guardian), Is.EqualTo(new[] { "m_eggpawnRestPoints:EggpawnRestPoints[]:0001", "FinalHitReady:Action:0006" }));
            Assert.That(AttributeRow(guardian.GetField("m_eggpawnRestPoints", BindingFlags.Instance | BindingFlags.NonPublic)), Is.EqualTo(
                "TooltipAttribute(Arranged in order of closeness to the boss. These will be occupied by returning eggpawns in Phase 4.) SerializeField()"));
            Assert.That(DeclaredMethods(guardian), Is.EqualTo(new[]
            {
                "Awake:0081", "TryGetFreeReturnPoint:0086", "BossReadyForFinalHit:0086", "OnDestroy:0081", ".ctor:1886"
            }));

            Type points = typeof(GuardianManager.EggpawnRestPoints);
            Assert.That((int)points.Attributes, Is.EqualTo(0x0010210a));
            Assert.That(points.BaseType, Is.EqualTo(typeof(System.ValueType)));
            Assert.That(Fields(points), Is.EqualTo(new[] { "Location:Transform:0006", "Occupied:Boolean:0006" }));
            Assert.That(AttributeRow(points.GetField("Occupied")), Is.EqualTo("HideInInspector()"));
            Assert.That(Methods(points), Is.Empty);
        }

        [Test]
        public void FourmenVisualsDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Type type = typeof(FourmenVisuals);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeRow(type), Is.EqualTo(NullAndBounds));
            Assert.That(Fields(type), Is.EqualTo(new[]
            {
                "m_boss:Transform:0001", "m_rotator:TransformRotator:0001", "m_depthToDrill:Single:0001", "m_drillDuration:Single:0001",
                "m_rotationDurationMultiplier:Single:0001", "m_animation:Animation:0001", "m_openAnimName:String:0001", "m_closeAnimName:String:0001",
                "m_materialRenderer:Renderer:0001", "m_neutralMaterial:Material:0001", "m_attackingMaterial:Material:0001", "m_deadMaterial:Material:0001",
                "m_dangerousBossIndicator:ParticleEffectWrapper:0001", "m_maxDepth:Single:0001", "m_surfaceDepth:Single:0001", "m_rotateDuration:Single:0001",
                "m_drillCoroutine:Coroutine:0001", "m_faceCoroutine:Coroutine:0001", "m_timeManager:TimeManager:0001"
            }));
            Assert.That(DeclaredMethods(type), Is.EqualTo(new[]
            {
                "Awake:0084", "Start:0081", "Drill:0086", "DrillOverTime:0081", "OpenHelmet:0086", "CloseHelmet:0086", "SetFaceNeutral:0086",
                "SetFaceTargeting:0086", "SetFaceAttacking:0086", "SetFaceDying:0086", "SetFaceCharging:0086", "SetFaceDead:0086",
                "FlashBetweenFaces:0081", "SetDangerousParticlesActive:0086", "OnDisable:0084", ".ctor:1886"
            }));
            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(type.GetField("m_boss", field)), Is.EqualTo("SerializeField() HeaderAttribute(Drill Bit)"));
            Assert.That(AttributeRow(type.GetField("m_animation", field)), Is.EqualTo("HeaderAttribute(Hinged Helmet) SerializeField()"));
            Assert.That(AttributeRow(type.GetField("m_materialRenderer", field)), Is.EqualTo("HeaderAttribute(Material States) SerializeField()"));
            Assert.That(AttributeRow(type.GetField("m_dangerousBossIndicator", field)), Is.EqualTo("SerializeField() HeaderAttribute(Particles)"));
            Assert.That(AttributeRow(type.GetField("m_maxDepth", field)), Is.Empty);
        }

        // ---- BossHomingTarget ----

        private static BossHomingTarget NewHomingTarget(Owned owned, OwnedTimeScaledObjects time)
        {
            // HomingTarget's field initialiser caches a typed CharacterManager reference.
            owned.LeaseRow<CharacterManager>();
            GameObject host = time.Objects.Create("Lucid boss homing target");
            host.SetActive(false);
            host.AddComponent<BoxCollider>();
            return host.AddComponent<BossHomingTarget>();
        }

        // The constructor 0x6E2D40 stores m_useAnimatedTargetIndicator = true at +0xC1 and the inlined
        // HomingTarget initialisers (targetable from the ground and IsTargetable, +0x9C/+0x9D) before the typed
        // CharacterManager reference; nothing is instantiated yet and the accessor 0x6E22E0 returns that field.
        [Test]
        public void BossHomingTargetConstructorKeepsTheIndicatorOnAndTheTargetTargetable()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-boss-homing-defaults"))
                {
                    BossHomingTarget target = NewHomingTarget(owned, time);
                    Assert.That(Get(target, "m_useAnimatedTargetIndicator"), Is.True);
                    Assert.That(Get(target, "m_alwaysTargetableWhenActive"), Is.False);
                    Assert.That(Get(target, "m_instantiated"), Is.False);
                    Assert.That(target.AnimatedTargetIndicator == null, Is.True);
                    Assert.That(Get(target, "m_characterManager"), Is.Null);
                    Assert.That(target.IsTargetable, Is.True);
                    Assert.That(target.TargetableFromGround, Is.True);
                    var reference = (SystemRef<CharacterManager>)Get(target, "m_characterManagerRef");
                    Assert.That(reference.SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<CharacterManager>()));
                }
            });
        }

        private static int[] CountAwakeEvent(HomingTarget target)
        {
            var count = new int[1];
            if (Get(target, "m_onAwake") == null) Set(target, "m_onAwake", new UnityEvent());
            ((UnityEvent)Get(target, "m_onAwake")).AddListener(() => count[0]++);
            return count;
        }

        // Awake 0x6E22F0 runs the HomingTarget Awake first (it caches the collider and raises the authored
        // awake event) and only then, with the indicator switched on, instantiates it and resolves the
        // CharacterManager; with it switched off it returns, so nothing is created and no manager is read.
        [Test]
        public void BossHomingTargetAwakeWithoutAnIndicatorOnlyRunsTheHomingTargetAwake()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-boss-homing-awake"))
                {
                    BossHomingTarget target = NewHomingTarget(owned, time);
                    int[] awakened = CountAwakeEvent(target);
                    Set(target, "m_useAnimatedTargetIndicator", false);
                    Assert.That(target.Collider == null, Is.True, "the collider is cached by Awake");

                    Call(target, "Awake");

                    Assert.That(awakened[0], Is.EqualTo(1));
                    Assert.That(target.Collider, Is.SameAs(target.GetComponent<BoxCollider>()));
                    Assert.That(Get(target, "m_instantiated"), Is.False);
                    Assert.That(target.AnimatedTargetIndicator == null, Is.True);
                    Assert.That(Get(target, "m_characterManager"), Is.Null);
                }
            });
        }

        // RunUpdate 0x6E2A90 leaves at once while no indicator was instantiated (+0xE8), before it reaches the
        // always-targetable branch that would make the target targetable; OnUpdate, OnLateUpdate and OnFixedUpdate
        // (0x6E2A80, 0x6E2C10, 0x6E2C20) are each a tail jump to it.
        [Test]
        public void BossHomingTargetUpdatesChangeNothingBeforeAnIndicatorExists()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-boss-homing-update"))
                {
                    BossHomingTarget target = NewHomingTarget(owned, time);
                    Set(target, "m_alwaysTargetableWhenActive", true);
                    MethodInfo setTargetable = typeof(HomingTarget).GetProperty("IsTargetable").GetSetMethod(true);
                    foreach (string entry in new[] { "OnUpdate", "OnLateUpdate", "OnFixedUpdate" })
                    {
                        setTargetable.Invoke(target, new object[] { false });
                        Call(target, entry, 0.02f);
                        Assert.That(target.IsTargetable, Is.False, entry);
                    }
                }
            });
        }

        // ---- BossBurstableComponent ----

        // RegisterHitPlayer 0x7008F0 is a shipped RET body; ReceiveHit 0x7008D0 tail-calls Boss.ReceiveHit(HomingTarget)
        // with the stored homing target and ignores its collider, which this probes through the boss's own lookup.
        [Test]
        public void BossBurstableComponentReportsTheHomingTargetNotTheColliderToItsBoss()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-boss-burstable"))
                {
                    BossHomingTarget target = NewHomingTarget(owned, time);
                    Boss boss = NewBoss(owned, "Lucid burstable boss", Vector3.zero);
                    var burstable = owned.Host("Lucid burstable", false).AddComponent<BossBurstableComponent>();
                    Set(burstable, "m_boss", boss);
                    Set(burstable, "m_bossHomingTarget", target);

                    Assert.DoesNotThrow(() => burstable.RegisterHitPlayer(DamageAction.HardFailure, HazardType.BossDamageDealer));

                    // The boss looks the target up in its homing-target dictionary, which is empty here: the
                    // lookup fails, so the call did reach Boss.ReceiveHit(HomingTarget).
                    Assert.Throws<KeyNotFoundException>(() => burstable.ReceiveHit(target.GetComponent<BoxCollider>()));
                    var bodyType = typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic);
                    var lookup = (IDictionary)Get(boss, "m_homingTargetDictionary");
                    object body = Activator.CreateInstance(bodyType);
                    Set(body, "HitPoints", 1);
                    Set(body, "HomingTarget", target);
                    lookup.Add(target, body);
                    // With the stored target listed (as a destroyed part, which is ignored before the manager is
                    // consulted) the call succeeds even though the collider argument is null: the key is the target.
                    Set(body, "m_destroyed", true);
                    Assert.DoesNotThrow(() => burstable.ReceiveHit(null));
                }
            });
        }

        // ---- BossCrabManager ----

        private static BossTimedObjectPool NewTimedPool(Owned owned, PrefabPoolType type)
        {
            var pool = owned.Host("Lucid boss timed pool", false).AddComponent<BossTimedObjectPool>();
            Set(pool, "m_objectPoolType", type);
            return pool;
        }

        // DespawnAttackWaves 0x700910 reads the PrefabPoolManager with TryGet, looks up the pool of the authored
        // wave type and, only when that is a BossTimedObjectPool (class test at 0x7009c7), raises its force flag;
        // OnDestroy 0x700A00 tail-calls it.
        [Test]
        public void BossCrabManagerFlagsOnlyTheRegisteredTimedPoolOfItsWaveType()
        {
            Owned.Run(owned =>
            {
                var manager = owned.Register(owned.Host("Lucid prefab pool manager", false).AddComponent<PrefabPoolManager>());
                PrefabPoolType wave = owned.Track(ScriptableObject.CreateInstance<PrefabPoolType>());
                PrefabPoolType other = owned.Track(ScriptableObject.CreateInstance<PrefabPoolType>());
                PrefabPoolType unregistered = owned.Track(ScriptableObject.CreateInstance<PrefabPoolType>());
                BossTimedObjectPool wavePool = NewTimedPool(owned, wave);
                BossTimedObjectPool otherPool = NewTimedPool(owned, other);
                manager.RegisterPool(wavePool);
                manager.RegisterPool(otherPool);

                var crab = owned.Host("Lucid boss crab manager", false).AddComponent<BossCrabManager>();
                Set(crab, "m_attackWaveType", wave);
                crab.DespawnAttackWaves();
                Assert.That(Get(wavePool, "m_forceDespawn"), Is.True);
                Assert.That(Get(otherPool, "m_forceDespawn"), Is.False, "only the pool of the wave type");

                Set(wavePool, "m_forceDespawn", false);
                Call(crab, "OnDestroy");
                Assert.That(Get(wavePool, "m_forceDespawn"), Is.True, "OnDestroy despawns the waves");

                Set(wavePool, "m_forceDespawn", false);
                Set(crab, "m_attackWaveType", unregistered);
                crab.DespawnAttackWaves();
                Assert.That(Get(wavePool, "m_forceDespawn"), Is.False);
                Assert.That(Get(otherPool, "m_forceDespawn"), Is.False);
            });
        }

        [Test]
        public void BossCrabManagerDoesNothingWithoutAPrefabPoolManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<PrefabPoolManager>();
                var crab = owned.Host("Lucid boss crab manager", false).AddComponent<BossCrabManager>();
                Set(crab, "m_attackWaveType", owned.Track(ScriptableObject.CreateInstance<PrefabPoolType>()));
                Assert.DoesNotThrow(() => crab.DespawnAttackWaves());
                Assert.DoesNotThrow(() => Call(crab, "OnDestroy"));
            });
        }

        // ---- BossManagerTrigger ----

        // Start 0x7036B0 takes the BossManager reference with GetSystemRef(null, true) and keeps the manager the
        // reference delivers (<Start>b__2_0 0x7037D0); Action_SetPhaseProgression 0x7037A0 stores 1 into the
        // manager's continue-phase flag (+0xA8), the one BossManager.SetPhaseContinue sets.
        [Test]
        public void BossManagerTriggerRaisesThePhaseContinueFlagOfTheManagerItStarted()
        {
            Owned.Run(owned =>
            {
                BossManager manager = owned.Register(NewBossManager(owned));
                var trigger = owned.Host("Lucid boss manager trigger", false).AddComponent<BossManagerTrigger>();
                Assert.That(Get(trigger, "m_bossManager"), Is.Null);

                Call(trigger, "Start");

                Assert.That(Get(trigger, "m_bossManager"), Is.SameAs(manager));
                Assert.That(Get(manager, "m_bossContinuePhase"), Is.False);
                trigger.Action_SetPhaseProgression();
                Assert.That(Get(manager, "m_bossContinuePhase"), Is.True);
                Assert.That(manager.HasFlagForPhaseContinuation(), Is.True);
                Assert.That(manager.HasFlagForPhaseContinuation(), Is.False, "the manager consumes the flag");
            });
        }

        // ---- BossVisualProxyModifier ----

        private static Boss NewProxyBoss(Owned owned, BossDefinition definition, out GameObject proxyHost)
        {
            Boss boss = NewBoss(owned, "Lucid proxy boss", Vector3.zero);
            Set(boss, "m_definition", definition);
            Set(boss, "m_formHandler", boss.gameObject.AddComponent<ActorFormHandler>());
            proxyHost = owned.Host("Lucid boss visual proxy", true);
            CharacterTriggerTestSupport.SetActorProperty(boss, "VisualProxy", proxyHost.AddComponent<SmoothedTransformProxy>());
            return boss;
        }

        // Each action of 0x6A6E90..0x6A7010 resolves the boss of the authored definition through the registered
        // BossManager (GetBossByDefinition) and tail-calls one inherited modifier on it: Show and Hide toggle the
        // form renderers with 1 and 0, Enable and Disable switch the visual proxy object with 1 and 0. A second
        // boss with another definition is left alone by every action.
        [Test]
        public void BossVisualProxyModifierActsOnlyOnTheBossOfItsDefinition()
        {
            Owned.Run(owned =>
            {
                BossManager manager = owned.Register(NewBossManager(owned));
                var ours = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
                var theirs = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
                Boss bossOurs = NewProxyBoss(owned, ours, out GameObject proxyOurs);
                Boss bossTheirs = NewProxyBoss(owned, theirs, out GameObject proxyTheirs);
                manager.Bosses.Add(bossTheirs);
                manager.Bosses.Add(bossOurs);
                // The modifier is built after the BossManager row holds its system.
                var modifier = owned.Host("Lucid boss proxy modifier", false).AddComponent<BossVisualProxyModifier>();
                Set(modifier, "m_bossDefinition", ours);

                modifier.Action_HideVisualProxy();
                Assert.That(bossOurs.FormRenderersOn, Is.False);
                Assert.That(bossTheirs.FormRenderersOn, Is.True);
                modifier.Action_ShowVisualProxy();
                Assert.That(bossOurs.FormRenderersOn, Is.True);
                Assert.That(bossTheirs.FormRenderersOn, Is.True);

                modifier.Action_DisableVisualProxy();
                Assert.That(proxyOurs.activeSelf, Is.False);
                Assert.That(proxyTheirs.activeSelf, Is.True);
                Assert.That(bossOurs.FormRenderersOn, Is.True, "the proxy switch leaves the renderers alone");
                modifier.Action_EnableVisualProxy();
                Assert.That(proxyOurs.activeSelf, Is.True);

                Set(modifier, "m_bossDefinition", theirs);
                modifier.Action_HideVisualProxy();
                Assert.That(bossTheirs.FormRenderersOn, Is.False);
                Assert.That(bossOurs.FormRenderersOn, Is.True);
                var reference = (SystemRef<BossManager>)Get(modifier, "m_bossManagerRef");
                Assert.That(reference.SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<BossManager>()));
            });
        }

        // ---- BossDebug ----

        // Init 0x700A50 stores the actor itself (the ActorDebug.Init body, inlined) and subscribes OnBossInfo to
        // BossDebugInfo (0x878DD742) with the Down trigger and joystick -1 (the tables Subscribe fills for Down);
        // OnDestroy 0x700B20 removes it again; the container name is the constant string at 0x700A20.
        [Test]
        public void BossDebugBindsItsInfoToggleToTheBossDebugInfoInput()
        {
            ControlTableLease.Run(() => Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, "Lucid debug boss", Vector3.zero);
                var debug = owned.Host("Lucid boss debug", false).AddComponent<BossDebug>();
                Assert.That(typeof(BossDebug).GetProperty("ContainerIdentifierName", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(debug),
                    Is.EqualTo("uicontaineridentifier_boss_debug_info"));
                Assert.That(Get(debug, "m_actor"), Is.Null);

                debug.Init(boss);

                Assert.That(Get(debug, "m_actor"), Is.SameAs(boss));
                Assert.That(ControlTables.Subscriptions(GameInput.BossDebugInfo, debug),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "BossDebug.OnBossInfo")));

                Call(debug, "OnDestroy");
                Assert.That(ControlTables.Subscriptions(GameInput.BossDebugInfo, debug), Is.Empty);
            }), GameInput.BossDebugInfo);
        }

        // ---- GuardianManager ----

        private static GuardianManager NewGuardian(Owned owned, bool[] occupied, out Transform[] locations)
        {
            var guardian = owned.Host("Lucid guardian manager", false).AddComponent<GuardianManager>();
            var points = new GuardianManager.EggpawnRestPoints[occupied.Length];
            locations = new Transform[occupied.Length];
            for (int i = 0; i < occupied.Length; i++)
            {
                locations[i] = owned.Host("Lucid rest point " + i, false).transform;
                points[i] = new GuardianManager.EggpawnRestPoints { Location = locations[i], Occupied = occupied[i] };
            }
            Set(guardian, "m_eggpawnRestPoints", points);
            return guardian;
        }

        // TryGetFreeReturnPoint 0x711B50 clears the out value, then claims the first unoccupied point in array
        // order: it stores Occupied = 1 in the array element, hands out its Location and returns true; with no
        // free point it returns false and leaves the out value null.
        [Test]
        public void GuardianManagerClaimsTheFirstFreeRestPointInOrder()
        {
            Owned.Run(owned =>
            {
                GuardianManager guardian = NewGuardian(owned, new[] { true, false, true, false }, out Transform[] locations);
                var points = (GuardianManager.EggpawnRestPoints[])Get(guardian, "m_eggpawnRestPoints");

                Transform claimed = locations[0];
                Assert.That(guardian.TryGetFreeReturnPoint(out claimed), Is.True);
                Assert.That(claimed, Is.SameAs(locations[1]), "the first free point, not the last");
                Assert.That(points.Select(point => point.Occupied), Is.EqualTo(new[] { true, true, true, false }));
                Assert.That(guardian.TryGetFreeReturnPoint(out claimed), Is.True);
                Assert.That(claimed, Is.SameAs(locations[3]));
                Assert.That(points.Select(point => point.Occupied), Is.EqualTo(new[] { true, true, true, true }));

                claimed = locations[2];
                Assert.That(guardian.TryGetFreeReturnPoint(out claimed), Is.False);
                Assert.That(claimed == null, Is.True, "the out value is cleared when nothing is free");
                Assert.That(points.Select(point => point.Occupied), Is.EqualTo(new[] { true, true, true, true }));
            });
        }

        [Test]
        public void GuardianManagerWithoutRestPointsHasNoFreeReturnPoint()
        {
            Owned.Run(owned =>
            {
                GuardianManager guardian = NewGuardian(owned, new bool[0], out Transform[] locations);
                Transform claimed = owned.Host("Lucid stale point", false).transform;
                Assert.That(guardian.TryGetFreeReturnPoint(out claimed), Is.False);
                Assert.That(claimed == null, Is.True);
                Assert.That(locations, Is.Empty);
            });
        }

        // BossReadyForFinalHit 0x706F90 invokes FinalHitReady once when it is set (a null delegate is skipped).
        [Test]
        public void GuardianManagerRaisesFinalHitReadyOnlyWhenSubscribed()
        {
            Owned.Run(owned =>
            {
                GuardianManager guardian = NewGuardian(owned, new bool[0], out Transform[] _);
                Assert.DoesNotThrow(() => guardian.BossReadyForFinalHit());
                int raised = 0;
                guardian.FinalHitReady = () => raised++;
                guardian.BossReadyForFinalHit();
                Assert.That(raised, Is.EqualTo(1));
                guardian.BossReadyForFinalHit();
                Assert.That(raised, Is.EqualTo(2));
            });
        }

        // Awake 0x711AF0 registers the manager as a system (RegisterSystem(this, null, false, false)); OnDestroy
        // 0x711C00 unregisters it again.
        [Test]
        public void GuardianManagerRegistersItselfOnAwakeAndLeavesOnDestroy()
        {
            Owned.Run(owned =>
            {
                GuardianManager guardian = NewGuardian(owned, new bool[0], out Transform[] _);
                var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<GuardianManager>()));
                row.RegisterUsing(guardian, () => Call(guardian, "Awake"));
                Assert.That(RegisteredSystem<GuardianManager>(), Is.SameAs(guardian));

                Call(guardian, "OnDestroy");
                Assert.That(RegisteredSystem<GuardianManager>(), Is.Null);
            });
        }

        // ---- FourmenManager ----

        [Test]
        public void FourmenManagerStartsWithAnEmptyBossesInMotionListOfItsOwn()
        {
            Owned.Run(owned =>
            {
                var first = owned.Host("Lucid first fourmen manager", false).AddComponent<FourmenManager>();
                var second = owned.Host("Lucid second fourmen manager", false).AddComponent<FourmenManager>();
                Assert.That(first.BossesInMotion, Is.Empty);
                Assert.That(second.BossesInMotion, Is.Empty);
                Assert.That(first.BossesInMotion, Is.Not.SameAs(second.BossesInMotion));
                var replacement = new List<Boss>();
                first.BossesInMotion = replacement;
                Assert.That(first.BossesInMotion, Is.SameAs(replacement));
            });
        }

        // Awake 0x711900 registers the system first (RegisterSystem(this, null, false, false)), then takes the
        // BossManager reference and keeps the manager it delivers (OnBossManagerValid 0x711A00); OnDestroy
        // 0x711A20 unregisters.
        [Test]
        public void FourmenManagerRegistersOnAwakeTakesTheBossManagerAndLeavesOnDestroy()
        {
            Owned.Run(owned =>
            {
                BossManager manager = owned.Register(NewBossManager(owned));
                var fourmen = owned.Host("Lucid fourmen manager", false).AddComponent<FourmenManager>();
                var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<FourmenManager>()));
                row.RegisterUsing(fourmen, () => Call(fourmen, "Awake"));

                Assert.That(RegisteredSystem<FourmenManager>(), Is.SameAs(fourmen));
                Assert.That(Get(fourmen, "m_bossManager"), Is.SameAs(manager));
                Assert.That(((SystemRef<BossManager>)Get(fourmen, "m_bossManagerRef")).GetSafe(), Is.SameAs(manager));

                Call(fourmen, "OnDestroy");
                Assert.That(RegisteredSystem<FourmenManager>(), Is.Null);
            });
        }

        // ResetActiveBossIndex 0x703F90 stores 0 and IncrementActiveBossIndex 0x703B60 adds one to the field.
        [Test]
        public void FourmenManagerCountsTheActiveBossIndexUpAndResetsIt()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, 0);
                Assert.That(Get(rig.Fourmen, "m_activeBossIndex"), Is.EqualTo(0));
                for (int step = 1; step <= 3; step++)
                {
                    rig.Fourmen.IncrementActiveBossIndex();
                    Assert.That(Get(rig.Fourmen, "m_activeBossIndex"), Is.EqualTo(step));
                }
                rig.Fourmen.ResetActiveBossIndex();
                Assert.That(Get(rig.Fourmen, "m_activeBossIndex"), Is.EqualTo(0));
            });
        }

        // BossCount 0x70AAB0 is the BossManager's list count and GetIndexForBoss 0x70AAF0 the first index whose
        // boss is the argument, -1 when it is not listed.
        [Test]
        public void FourmenManagerCountsTheBossesAndFindsTheirIndex()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, 3);
                Boss stranger = NewBoss(owned, "Lucid unlisted boss", Vector3.zero);
                Assert.That(rig.Fourmen.BossCount(), Is.EqualTo(3));
                for (int i = 0; i < 3; i++) Assert.That(rig.Fourmen.GetIndexForBoss(rig.Bosses[i]), Is.EqualTo(i));
                Assert.That(rig.Fourmen.GetIndexForBoss(stranger), Is.EqualTo(-1));

                rig.Manager.Bosses.Add(rig.Bosses[0]);
                Assert.That(rig.Fourmen.BossCount(), Is.EqualTo(4));
                Assert.That(rig.Fourmen.GetIndexForBoss(rig.Bosses[0]), Is.EqualTo(0), "the first of two listings");

                rig.Manager.Bosses.Clear();
                Assert.That(rig.Fourmen.BossCount(), Is.EqualTo(0));
                Assert.That(rig.Fourmen.GetIndexForBoss(rig.Bosses[0]), Is.EqualTo(-1));
            });
        }

        // ConfigureActiveBoss 0x703B70 takes the active index modulo the boss count (idiv at 0x703bb2), marks that
        // boss the active entity, resets every boss's movement priority to 0 and then gives the active one 1. It
        // never clears another boss's active flag.
        [Test]
        public void FourmenManagerConfiguresTheBossAtTheWrappedActiveIndex()
        {
            foreach ((int index, int count, int expected) in new[] { (0, 3, 0), (2, 3, 2), (3, 3, 0), (4, 3, 1), (5, 2, 1), (7, 4, 3) })
            {
                Owned.Run(owned =>
                {
                    FourmenRig rig = NewFourmenRig(owned, count);
                    foreach (Boss boss in rig.Bosses) boss.SetMovementPriority(7);
                    for (int step = 0; step < index; step++) rig.Fourmen.IncrementActiveBossIndex();

                    rig.Fourmen.ConfigureActiveBoss();

                    string label = "index " + index + " of " + count;
                    Assert.That(rig.Bosses.Select(boss => boss.IsActivePartOfMultiBodyBoss),
                        Is.EqualTo(Enumerable.Range(0, count).Select(i => i == expected)), label);
                    Assert.That(rig.Bosses.Select(boss => boss.MovementPriority),
                        Is.EqualTo(Enumerable.Range(0, count).Select(i => i == expected ? 1 : 0)), label);
                });
            }
        }

        [Test]
        public void FourmenManagerKeepsEarlierActiveBossesActiveWhenConfiguringAnother()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, 3);
                rig.Fourmen.ConfigureActiveBoss();
                rig.Fourmen.IncrementActiveBossIndex();
                rig.Fourmen.ConfigureActiveBoss();
                Assert.That(rig.Bosses.Select(boss => boss.IsActivePartOfMultiBodyBoss), Is.EqualTo(new[] { true, true, false }));
                Assert.That(rig.Bosses.Select(boss => boss.MovementPriority), Is.EqualTo(new[] { 0, 1, 0 }));
            });
        }

        // HasActiveBossUnit 0x7041F0 is true when any listed boss is an active entity; ClearActiveBosses 0x703AD0
        // clears every boss's active flag (marking the active ones as having been active this loop);
        // HasCompletedActiveLoop 0x710040 is true when no boss lacks that mark (so for an empty list too);
        // ClearLoopStatus 0x703FA0 removes the mark from every boss.
        [Test]
        public void FourmenManagerTracksTheActiveBossAndTheCompletedLoop()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, 3);
                Assert.That(rig.Fourmen.HasActiveBossUnit(), Is.False);
                Assert.That(rig.Fourmen.HasCompletedActiveLoop(), Is.False);

                for (int i = 0; i < 3; i++)
                {
                    rig.Fourmen.ResetActiveBossIndex();
                    for (int step = 0; step < i; step++) rig.Fourmen.IncrementActiveBossIndex();
                    rig.Fourmen.ConfigureActiveBoss();
                    Assert.That(rig.Fourmen.HasActiveBossUnit(), Is.True, "boss " + i + " is active");

                    rig.Fourmen.ClearActiveBosses();
                    Assert.That(rig.Fourmen.HasActiveBossUnit(), Is.False, "boss " + i + " cleared");
                    Assert.That(rig.Bosses.Select(boss => boss.HasBeenActiveThisLoop),
                        Is.EqualTo(Enumerable.Range(0, 3).Select(index => index <= i)), "after boss " + i);
                    Assert.That(rig.Fourmen.HasCompletedActiveLoop(), Is.EqualTo(i == 2), "after boss " + i);
                }

                rig.Fourmen.ClearLoopStatus();
                Assert.That(rig.Bosses.Select(boss => boss.HasBeenActiveThisLoop), Is.EqualTo(new[] { false, false, false }));
                Assert.That(rig.Fourmen.HasCompletedActiveLoop(), Is.False);

                rig.Manager.Bosses.Clear();
                Assert.That(rig.Fourmen.HasActiveBossUnit(), Is.False);
                Assert.That(rig.Fourmen.HasCompletedActiveLoop(), Is.True, "no boss lacks the mark");
            });
        }

        // GetAveragePosition 0x70C2D0 sums the bosses' world positions and divides by the count (divps/divss),
        // so an empty list gives 0 / 0 on every axis.
        [Test]
        public void FourmenManagerAveragesTheBossPositions()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, 3);
                rig.Bosses[0].SetWorldPosition(new Vector3(0f, 0f, 0f));
                rig.Bosses[1].SetWorldPosition(new Vector3(3f, 6f, 9f));
                rig.Bosses[2].SetWorldPosition(new Vector3(6f, 0f, 3f));
                Assert.That(rig.Fourmen.GetAveragePosition(), Is.EqualTo(new Vector3(3f, 2f, 4f)));

                rig.Manager.Bosses.RemoveAt(0);
                Assert.That(rig.Fourmen.GetAveragePosition(), Is.EqualTo(new Vector3(4.5f, 3f, 6f)));
                rig.Manager.Bosses.RemoveAt(0);
                Assert.That(rig.Fourmen.GetAveragePosition(), Is.EqualTo(new Vector3(6f, 0f, 3f)));
                rig.Manager.Bosses.Clear();
                IsNaN(rig.Fourmen.GetAveragePosition(), "empty list");
            });
        }

        private const ActorAttachPointType LaserAttachPoint = ActorAttachPointType.BossFourman_LaserEmitter_GreenHand;

        private static void GiveLaserEmitter(Owned owned, Boss boss, ActorAttachPointType attachPoint, Vector3 position)
        {
            GameObject emitter = owned.Host("Lucid laser emitter", false);
            emitter.transform.position = position;
            object body = Activator.CreateInstance(typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic));
            Set(body, "LaserEmitters", emitter.AddComponent<VariableLengthLasers>());
            ((IDictionary)Get(boss, "m_attachPointDictionary"))[attachPoint] = body;
        }

        // CalculateLaserPositions 0x70AE30 returns two positions: the given boss's laser emitter and that of the
        // next boss around the ring (index modulo the count), for the same attach point. Everything stays zero for a
        // boss that is not listed.
        [Test]
        public void FourmenManagerPairsEachBossLaserWithTheNextBossAroundTheRing()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, 3);
                Vector3[] emitters = { new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 9f) };
                for (int i = 0; i < 3; i++) GiveLaserEmitter(owned, rig.Bosses[i], LaserAttachPoint, emitters[i]);
                // Another attach point on the boss is not consulted.
                GiveLaserEmitter(owned, rig.Bosses[1], ActorAttachPointType.BossFourman_LaserEmitter_GreenHand_Hip, new Vector3(-1f, -1f, -1f));

                Assert.That(rig.Fourmen.CalculateLaserPositions(rig.Bosses[0], LaserAttachPoint), Is.EqualTo(new[] { emitters[0], emitters[1] }));
                Assert.That(rig.Fourmen.CalculateLaserPositions(rig.Bosses[1], LaserAttachPoint), Is.EqualTo(new[] { emitters[1], emitters[2] }));
                Assert.That(rig.Fourmen.CalculateLaserPositions(rig.Bosses[2], LaserAttachPoint), Is.EqualTo(new[] { emitters[2], emitters[0] }), "wraps around");

                Boss stranger = NewBoss(owned, "Lucid unlisted laser boss", Vector3.zero);
                Assert.That(rig.Fourmen.CalculateLaserPositions(stranger, LaserAttachPoint), Is.EqualTo(new[] { Vector3.zero, Vector3.zero }));

                rig.Manager.Bosses.RemoveRange(1, 2);
                Assert.That(rig.Fourmen.CalculateLaserPositions(rig.Bosses[0], LaserAttachPoint), Is.EqualTo(new[] { emitters[0], emitters[0] }), "a single boss pairs with itself");
            });
        }

        // ---- FourmenVisuals ----

        private sealed class VisualsRig
        {
            public FourmenVisuals Visuals;
            public Transform Boss;
            public TransformRotator Rotator;
            public TimeManager Time;
        }

        // A FourmenVisuals with a registered TimeManager (it holds the EnemyMovement category at the given scale) for its
        // Awake to take. The boss sits 10 up, drills 6 over the given duration
        // and spins its rotator at twice that time.
        private static VisualsRig NewVisualsRig(Owned owned, OwnedTimeScaledObjects time, float drillDuration, bool active = false, float timeScale = 1f)
        {
            var rig = new VisualsRig();
            rig.Time = owned.Register(CharacterTriggerTestSupport.NewTimeManager(owned, TimeCategory.EnemyMovement, timeScale));
            rig.Boss = time.Objects.Create("Lucid drilling boss", new Vector3(1f, 10f, 3f), Quaternion.Euler(0f, 90f, 0f)).transform;
            rig.Rotator = time.Add<TransformRotator>("Lucid drill rotator");
            GameObject host = time.Objects.Create("Lucid fourmen visuals");
            host.SetActive(active);
            rig.Visuals = host.AddComponent<FourmenVisuals>();
            Set(rig.Visuals, "m_boss", rig.Boss);
            Set(rig.Visuals, "m_rotator", rig.Rotator);
            Set(rig.Visuals, "m_depthToDrill", 6f);
            Set(rig.Visuals, "m_drillDuration", drillDuration);
            Set(rig.Visuals, "m_rotationDurationMultiplier", 2f);
            return rig;
        }

        private static Material NewMaterial(Owned owned) =>
            owned.Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { hideFlags = HideFlags.HideAndDontSave });

        // Awake 0x579CE0 reads the boss height as the surface depth, takes the maximum depth one drill below it and
        // sets the rotation time to drill duration times the multiplier, then subscribes to the TimeManager.
        [Test]
        public void FourmenVisualsAwakeDerivesTheDrillDepthsAndTakesTheTimeManager()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-fourmen-visuals-awake"))
                {
                    VisualsRig rig = NewVisualsRig(owned, time, 4f);
                    Assert.That(Get(rig.Visuals, "m_timeManager"), Is.Null);

                    Call(rig.Visuals, "Awake");

                    Assert.That(Get(rig.Visuals, "m_surfaceDepth"), Is.EqualTo(10f));
                    Assert.That(Get(rig.Visuals, "m_maxDepth"), Is.EqualTo(4f));
                    Assert.That(Get(rig.Visuals, "m_rotateDuration"), Is.EqualTo(8f));
                    Assert.That(Get(rig.Visuals, "m_timeManager"), Is.SameAs(rig.Time));
                }
            });
        }

        // DrillOverTime 0x57A430 waits (yield null) while the TimeManager is missing without touching the boss.
        [Test]
        public void FourmenVisualsDrillWaitsForTheTimeManager()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-fourmen-visuals-wait"))
                {
                    VisualsRig rig = NewVisualsRig(owned, time, 4f);
                    var drill = (IEnumerator)Call(rig.Visuals, "DrillOverTime", true);
                    for (int i = 0; i < 3; i++)
                    {
                        Assert.That(drill.MoveNext(), Is.True);
                        Assert.That(drill.Current, Is.Null);
                    }
                    Assert.That(rig.Boss.position, Is.EqualTo(new Vector3(1f, 10f, 3f)));
                    Assert.That(Quaternion.Angle(rig.Boss.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.1f));
                }
            });
        }

        // The first step 0x57A430 adds the EnemyMovement delta time to the timer and moves only the height, from the
        // surface towards the maximum depth when drilling down (and the other way up), in proportion to timer over
        // duration; drilling down also turns the boss towards identity by that fraction over the rotation time.
        // The delta time is whatever the engine reports for this call, so the expected values follow from it.
        [Test]
        public void FourmenVisualsDrillMovesTheHeightByTheElapsedFraction()
        {
            foreach (bool down in new[] { true, false })
            {
                Owned.Run(owned =>
                {
                    using (var time = new OwnedTimeScaledObjects("lucid-fourmen-visuals-drill"))
                    {
                        VisualsRig rig = NewVisualsRig(owned, time, 4f);
                        Call(rig.Visuals, "Awake");
                        if (!down) rig.Boss.position = new Vector3(1f, 4f, 3f);
                        float delta = rig.Time.GetDeltaTime(TimeCategory.EnemyMovement);
                        float fraction = delta / 4f;
                        float start = down ? 10f : 4f;
                        float target = down ? 4f : 10f;

                        var drill = (IEnumerator)Call(rig.Visuals, "DrillOverTime", down);
                        Assert.That(drill.MoveNext(), Is.True);
                        Assert.That(drill.Current, Is.Null);

                        Assert.That(rig.Boss.position.y, Is.EqualTo(start + (target - start) * Mathf.Clamp01(fraction)).Within(1e-4f));
                        Assert.That(rig.Boss.position.x, Is.EqualTo(1f));
                        Assert.That(rig.Boss.position.z, Is.EqualTo(3f));
                        Quaternion expected = down
                            ? Quaternion.Slerp(Quaternion.Euler(0f, 90f, 0f), Quaternion.identity, fraction / 8f)
                            : Quaternion.Euler(0f, 90f, 0f);
                        Assert.That(Quaternion.Angle(rig.Boss.rotation, expected), Is.LessThan(0.1f), down ? "down" : "up");
                    }
                });
            }
        }

        // With nothing left of the duration the loop is skipped and the boss lands exactly on the target height:
        // the maximum depth when drilling down, the surface when coming back up.
        [Test]
        public void FourmenVisualsDrillEndsOnTheTargetHeight()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-fourmen-visuals-land"))
                {
                    VisualsRig rig = NewVisualsRig(owned, time, 0f);
                    Call(rig.Visuals, "Awake");

                    var down = (IEnumerator)Call(rig.Visuals, "DrillOverTime", true);
                    Assert.That(down.MoveNext(), Is.False);
                    Assert.That(rig.Boss.position, Is.EqualTo(new Vector3(1f, 4f, 3f)));
                    Assert.That(Quaternion.Angle(rig.Boss.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.1f));

                    var up = (IEnumerator)Call(rig.Visuals, "DrillOverTime", false);
                    Assert.That(up.MoveNext(), Is.False);
                    Assert.That(rig.Boss.position, Is.EqualTo(new Vector3(1f, 10f, 3f)));
                }
            });
        }

        // Drill 0x579E40 sets the rotator's speed multiplier to -1 when drilling down and 1 otherwise, then replaces
        // the drill coroutine (SafeStopCoroutine, then StartCoroutine).
        [Test]
        public void FourmenVisualsDrillSetsTheRotatorDirectionAndRestartsTheCoroutine()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-fourmen-visuals-direction"))
                {
                    VisualsRig rig = NewVisualsRig(owned, time, 4f, true);
                    Call(rig.Visuals, "Awake");
                    Assert.That(Get(rig.Rotator, "m_multiplier"), Is.EqualTo(1f));

                    rig.Visuals.Drill(true);
                    Assert.That(Get(rig.Rotator, "m_multiplier"), Is.EqualTo(-1f));
                    var first = (Coroutine)Get(rig.Visuals, "m_drillCoroutine");
                    Assert.That(first, Is.Not.Null);

                    rig.Visuals.Drill(false);
                    Assert.That(Get(rig.Rotator, "m_multiplier"), Is.EqualTo(1f));
                    var second = (Coroutine)Get(rig.Visuals, "m_drillCoroutine");
                    Assert.That(second, Is.Not.Null);
                    Assert.That(second, Is.Not.SameAs(first));

                    Call(rig.Visuals, "OnDisable");
                    Assert.That(Get(rig.Visuals, "m_drillCoroutine"), Is.Null, "OnDisable stops the drill");
                }
            });
        }

        // OpenHelmet 0x579FB0 and CloseHelmet 0x579FD0 each play their own named clip of the Animation.
        [Test]
        public void FourmenVisualsHelmetPlaysTheOpenAndCloseClips()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid fourmen helmet", true);
                var animation = host.AddComponent<Animation>();
                foreach (string name in new[] { "LucidHelmetOpen", "LucidHelmetClose" })
                {
                    var clip = owned.Track(new AnimationClip { name = name, legacy = true, hideFlags = HideFlags.HideAndDontSave });
                    clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                    animation.AddClip(clip, name);
                }
                var visuals = host.AddComponent<FourmenVisuals>();
                Set(visuals, "m_animation", animation);
                Set(visuals, "m_openAnimName", "LucidHelmetOpen");
                Set(visuals, "m_closeAnimName", "LucidHelmetClose");

                visuals.OpenHelmet();
                Assert.That(animation.IsPlaying("LucidHelmetOpen"), Is.True);
                Assert.That(animation.IsPlaying("LucidHelmetClose"), Is.False);
                animation.Stop();
                visuals.CloseHelmet();
                Assert.That(animation.IsPlaying("LucidHelmetClose"), Is.True);
                Assert.That(animation.IsPlaying("LucidHelmetOpen"), Is.False);
            });
        }

        // SetFaceNeutral 0x579FF0, SetFaceAttacking 0x57A1A0 and SetFaceDead 0x57A370 stop the face coroutine and
        // show their own material directly.
        [Test]
        public void FourmenVisualsSteadyFacesShowTheirMaterial()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid fourmen face", false);
                var renderer = host.AddComponent<MeshRenderer>();
                Material neutral = NewMaterial(owned);
                Material attacking = NewMaterial(owned);
                Material dead = NewMaterial(owned);
                var visuals = host.AddComponent<FourmenVisuals>();
                Set(visuals, "m_materialRenderer", renderer);
                Set(visuals, "m_neutralMaterial", neutral);
                Set(visuals, "m_attackingMaterial", attacking);
                Set(visuals, "m_deadMaterial", dead);

                visuals.SetFaceAttacking();
                Assert.That(renderer.sharedMaterial, Is.EqualTo(attacking));
                visuals.SetFaceDead();
                Assert.That(renderer.sharedMaterial, Is.EqualTo(dead));
                visuals.SetFaceNeutral();
                Assert.That(renderer.sharedMaterial, Is.EqualTo(neutral));
            });
        }

        // FlashBetweenFaces 0x57A780 waits for the TimeManager, shows the first face, then adds the delta time to a
        // timer and, once it exceeds the interval, swaps the face and restarts the timer, step after step.
        [Test]
        public void FourmenVisualsFlashSwapsTheFacesOncePerElapsedInterval()
        {
            Owned.Run(owned =>
            {
                using (var time = new OwnedTimeScaledObjects("lucid-fourmen-visuals-flash"))
                {
                    VisualsRig rig = NewVisualsRig(owned, time, 4f);
                    var renderer = rig.Visuals.gameObject.AddComponent<MeshRenderer>();
                    Set(rig.Visuals, "m_materialRenderer", renderer);
                    Material first = NewMaterial(owned);
                    Material second = NewMaterial(owned);

                    var waiting = (IEnumerator)Call(rig.Visuals, "FlashBetweenFaces", first, second, -1f);
                    Assert.That(waiting.MoveNext(), Is.True);
                    Assert.That(waiting.Current, Is.Null);
                    Assert.That(renderer.sharedMaterial, Is.Not.EqualTo(first), "no face before the TimeManager arrives");

                    Call(rig.Visuals, "Awake");
                    // A negative interval is always exceeded, so every step swaps, starting from the first face.
                    var flash = (IEnumerator)Call(rig.Visuals, "FlashBetweenFaces", first, second, -1f);
                    foreach (Material expected in new[] { second, first, second, first })
                    {
                        Assert.That(flash.MoveNext(), Is.True);
                        Assert.That(flash.Current, Is.Null);
                        Assert.That(renderer.sharedMaterial, Is.EqualTo(expected));
                    }

                    // An interval that is never reached keeps the first face.
                    var steady = (IEnumerator)Call(rig.Visuals, "FlashBetweenFaces", second, first, 1e9f);
                    for (int i = 0; i < 3; i++)
                    {
                        Assert.That(steady.MoveNext(), Is.True);
                        Assert.That(renderer.sharedMaterial, Is.EqualTo(second));
                    }
                }
            });
        }

        // SetFaceTargeting 0x57A020, SetFaceDying 0x57A1D0 and SetFaceCharging 0x57A2A0 start a flash that begins on
        // the attacking, the dead and the neutral material (their first face). Starting a coroutine runs it to its
        // first yield at once; the manager's scale is 0, so no time passes and the interval is never reached.
        [Test]
        public void FourmenVisualsFlashingFacesStartOnTheirFirstMaterial()
        {
            foreach ((string face, int first) in new[] { ("SetFaceTargeting", 0), ("SetFaceDying", 1), ("SetFaceCharging", 2) })
            {
                Owned.Run(owned =>
                {
                    using (var time = new OwnedTimeScaledObjects("lucid-fourmen-visuals-faces"))
                    {
                        VisualsRig rig = NewVisualsRig(owned, time, 4f, true, 0f);
                        var renderer = rig.Visuals.gameObject.AddComponent<MeshRenderer>();
                        Material neutral = NewMaterial(owned);
                        Material attacking = NewMaterial(owned);
                        Material dead = NewMaterial(owned);
                        Set(rig.Visuals, "m_materialRenderer", renderer);
                        Set(rig.Visuals, "m_neutralMaterial", neutral);
                        Set(rig.Visuals, "m_attackingMaterial", attacking);
                        Set(rig.Visuals, "m_deadMaterial", dead);
                        Call(rig.Visuals, "Awake");

                        Call(rig.Visuals, face);

                        Assert.That(renderer.sharedMaterial, Is.EqualTo(new[] { attacking, dead, neutral }[first]), face);
                        Assert.That(Get(rig.Visuals, "m_faceCoroutine"), Is.Not.Null, face);
                        Call(rig.Visuals, "OnDisable");
                        Assert.That(Get(rig.Visuals, "m_faceCoroutine"), Is.Null, face);
                    }
                });
            }
        }
    }
}
