using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // AnimatorSetBossStorageBool against the 1.10.1 x86-64 bodies cited per case. The behaviour is built on a real
    // BossManager registered in its row, a real Boss with the FSM storage Actor.Awake would create (an inactive host
    // runs no Awake) and the Boss's own animator wrapper, so GetBossForAnimator resolves the boss as it does in a scene.
    public sealed class AnimatorSetBossStorageBoolTests
    {
        private sealed class Scene
        {
            public BossManager Manager;
            public Boss Boss;
            public Animator Animator;
            public Animator OtherAnimator;
        }

        private static void WithScene(Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<SaveManager>();
            var scene = new Scene { Manager = owned.Register(owned.Host("Lucid boss storage manager", false).AddComponent<BossManager>()) };
            scene.Boss = owned.Host("Lucid boss storage boss", false).AddComponent<Boss>();
            typeof(Actor).GetProperty(nameof(Actor.Storage)).GetSetMethod(true).Invoke(scene.Boss, new object[] { new FSMStorage(0) });
            scene.Animator = owned.Host("Lucid boss storage animator", false).AddComponent<Animator>();
            scene.OtherAnimator = owned.Host("Lucid boss storage other animator", false).AddComponent<Animator>();
            typeof(Actor).GetProperty(nameof(Actor.Animator)).GetSetMethod(true).Invoke(scene.Boss, new object[] { new ActorAnimator(scene.Animator) });
            scene.Manager.Bosses.Add(scene.Boss);
            body(owned, scene);
        });

        private static AnimatorSetBossStorageBool NewSetter(Owned owned) => owned.Track(ScriptableObject.CreateInstance<AnimatorSetBossStorageBool>());

        private static GraphStorageKey Key(string name) => new GraphStorageKey(name, 0, 0);

        private static bool Has(Boss boss, GraphStorageKey key) => boss.Storage.GetCollection().ContainsKey(key);

        // OnStateEnter 0x06000e86 (0x7270D0) stores GetSystemRef<BossManager>(null, true) (0x727168), runs
        // InvokeOnValid(RegisterBossManager) (0x7271BA; 0x06000e85 stores the manager) and sets m_boss from
        // GetBossForAnimator (0x7271C8) whatever m_setOnEntry is. With the flag it stores m_entryValue (+0x28) under the
        // key built from m_fsmVarOnEntry (+0x20) into the boss's storage (+0x20 of the boss) through SetValue<bool>.
        [Test]
        public void EntryResolvesTheBossAndStoresTheEntryValueUnderTheNamedKey()
        {
            WithScene((owned, scene) =>
            {
                AnimatorSetBossStorageBool setter = NewSetter(owned);
                Set(setter, "m_fsmVarOnEntry", "Lucid-boss-entry-flag");
                Set(setter, "m_entryValue", true);

                setter.OnStateEnter(scene.Animator, default, 0);
                Assert.That(Get<BossManager>(setter, "m_bossManager"), Is.SameAs(scene.Manager));
                Assert.That(Get<SystemRef<BossManager>>(setter, "m_bossManagerRef").GetSafe(), Is.SameAs(scene.Manager));
                Assert.That(Get<Boss>(setter, "m_boss"), Is.SameAs(scene.Boss), "the boss is resolved without m_setOnEntry");
                Assert.That(Has(scene.Boss, Key("Lucid-boss-entry-flag")), Is.False, "nothing is stored without m_setOnEntry");

                Set(setter, "m_setOnEntry", true);
                setter.OnStateEnter(scene.Animator, default, 0);
                Assert.That(scene.Boss.Storage.GetValue(Key("Lucid-boss-entry-flag"), false, false), Is.True);

                Set(setter, "m_entryValue", false);
                setter.OnStateEnter(scene.Animator, default, 0);
                Assert.That(scene.Boss.Storage.GetValue(Key("Lucid-boss-entry-flag"), true, false), Is.False, "the configured value is stored, not a constant");

                Set(setter, "m_setOnEntry", false);
                setter.OnStateEnter(scene.OtherAnimator, default, 0);
                Assert.That(Get<Boss>(setter, "m_boss"), Is.Null, "an animator no boss owns resolves to null");
            });
        }

        // 0x7271DD tests m_varNamesCreated (+0x39): the first entry builds each key from its name when the name is not
        // empty (IsNullOrEmpty, 0x7271F4 and 0x727226; GraphStorageKey(name, 0, 0)) for both entry and exit, then sets the
        // flag, so later entries keep the keys of the first one.
        [Test]
        public void TheFirstEntryBuildsBothKeysAndLaterEntriesKeepThem()
        {
            WithScene((owned, scene) =>
            {
                AnimatorSetBossStorageBool setter = NewSetter(owned);
                Set(setter, "m_fsmVarOnEntry", "Lucid-boss-first-entry");
                Set(setter, "m_fsmVarOnExit", "Lucid-boss-first-exit");
                setter.OnStateEnter(scene.Animator, default, 0);
                Assert.That(Get<bool>(setter, "m_varNamesCreated"), Is.True);
                Assert.That(Get<GraphStorageKey>(setter, "m_entryVarName"), Is.EqualTo(Key("Lucid-boss-first-entry")));
                Assert.That(Get<GraphStorageKey>(setter, "m_exitVarName"), Is.EqualTo(Key("Lucid-boss-first-exit")));

                Set(setter, "m_fsmVarOnEntry", "Lucid-boss-second-entry");
                Set(setter, "m_fsmVarOnExit", "Lucid-boss-second-exit");
                Set(setter, "m_setOnEntry", true);
                Set(setter, "m_entryValue", true);
                setter.OnStateEnter(scene.Animator, default, 0);
                Assert.That(Get<GraphStorageKey>(setter, "m_entryVarName"), Is.EqualTo(Key("Lucid-boss-first-entry")));
                Assert.That(Get<GraphStorageKey>(setter, "m_exitVarName"), Is.EqualTo(Key("Lucid-boss-first-exit")));
                Assert.That(Has(scene.Boss, Key("Lucid-boss-first-entry")), Is.True);
                Assert.That(Has(scene.Boss, Key("Lucid-boss-second-entry")), Is.False);
            });
        }

        // An empty or missing name is skipped (0x7271FB, 0x72722D), so the key stays the default one.
        [Test]
        public void AnEmptyOrMissingNameLeavesTheKeyDefault()
        {
            WithScene((owned, scene) =>
            {
                AnimatorSetBossStorageBool setter = NewSetter(owned);
                Set(setter, "m_fsmVarOnEntry", "");
                setter.OnStateEnter(scene.Animator, default, 0);
                Assert.That(Get<bool>(setter, "m_varNamesCreated"), Is.True);
                Assert.That(Get<GraphStorageKey>(setter, "m_entryVarName"), Is.EqualTo(default(GraphStorageKey)));
                Assert.That(Get<GraphStorageKey>(setter, "m_exitVarName"), Is.EqualTo(default(GraphStorageKey)));
            });
        }

        // OnStateExit 0x06000e87 (0x727310) tests m_setOnExit (+0x29) and stores m_exitValue (+0x38) under the exit key
        // (+0x4C) into the storage of the boss found on the last entry, whatever animator it is given.
        [Test]
        public void ExitStoresTheExitValueIntoTheBossOfTheLastEntry()
        {
            WithScene((owned, scene) =>
            {
                AnimatorSetBossStorageBool setter = NewSetter(owned);
                Set(setter, "m_fsmVarOnExit", "Lucid-boss-exit-flag");
                Set(setter, "m_exitValue", true);
                setter.OnStateEnter(scene.Animator, default, 0);

                setter.OnStateExit(scene.OtherAnimator, default, 0);
                Assert.That(Has(scene.Boss, Key("Lucid-boss-exit-flag")), Is.False, "nothing is stored without m_setOnExit");
                Set(setter, "m_setOnExit", true);
                setter.OnStateExit(scene.OtherAnimator, default, 0);
                Assert.That(scene.Boss.Storage.GetValue(Key("Lucid-boss-exit-flag"), false, false), Is.True);
                Set(setter, "m_exitValue", false);
                setter.OnStateExit(scene.Animator, default, 0);
                Assert.That(scene.Boss.Storage.GetValue(Key("Lucid-boss-exit-flag"), true, false), Is.False);
            });
        }
    }
}
