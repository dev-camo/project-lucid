using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // DefinitionDataProgressView (TypeDef 0x02000955, x86-64 0x684BC0..0x684D90) and GameCenterView (TypeDef
    // 0x02000964, 0x689A20..0x689BC0) on inactive owned hosts. Awake and Update are invoked directly because
    // Edit Mode sends no messages.
    public sealed class ProgressAndGameCenterViewTests
    {
        // dump.cs TypeDefIndex 2388 and 2403 with the DummyDll rows. The progress view carries no IL2CPP options.
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(DefinitionDataProgressView), 0x00100001,
                new (string, int)[]
                {
                    ("DataLoadProgress", 0x0026),
                    ("m_dataManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnSetup", 0x00c4),
                    ("Update", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(DefinitionDataProgressView));
            Assert.That(typeof(DefinitionDataProgressView).BaseType, Is.EqualTo(typeof(ViewBase)));
            AssertMembers(typeof(GameCenterView), 0x00100001,
                new (string, int)[]
                {
                    ("GameCenterAvailable", 0x0026),
                    ("m_gameCenter", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("Update", 0x0081),
                    ("OnSetup", 0x00c4),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(GameCenterView), 1, 2);
            Assert.That(typeof(GameCenterView).BaseType, Is.EqualTo(typeof(ViewBase)));
        }

        // Awake 0x684BC0 stores GetSystemRef<DataManager>(null, true) in the field (+0x40); the constructor
        // 0x684D10 allocated the progress bindable (+0x38) first.
        [Test]
        public void AwakeTakesTheDataManagerReferenceFromTheRegistry()
        {
            Owned.Run(owned =>
            {
                var data = owned.Host("Lucid data manager", false).AddComponent<DataManager>();
                owned.Register(data);
                var view = owned.Host("Lucid progress view", false).AddComponent<DefinitionDataProgressView>();
                Assert.That(view.DataLoadProgress, Is.Not.Null);
                Assert.That(view.DataLoadProgress.Value, Is.EqualTo(0f));
                Assert.That(Get<object>(view, "m_dataManager"), Is.Null, "taken on Awake, not at construction");
                Call(view, "Awake");
                Assert.That(Get<SystemRef<DataManager>>(view, "m_dataManager").Get(), Is.SameAs(data));
                Assert.DoesNotThrow(() => Call(view, "OnSetup", (object)null), "OnSetup is a lone ret");
            });
        }

        // Update 0x684C50 returns when the reference holds no manager (IsNull 0x684c94) and otherwise publishes
        // the manager's Progress (+0x48) through minss(1.0, p) masked by the p < 0 compare (0x684cca..0x684ce3):
        // below zero is 0, above one is 1, NaN stays NaN. Truth table: (progress, published).
        [Test]
        public void UpdatePublishesTheLoadProgressClampedToTheUnitRange()
        {
            var table = new[]
            {
                (progress: -0.5f, published: 0f),
                (progress: -float.MaxValue, published: 0f),
                (progress: float.NegativeInfinity, published: 0f),
                (progress: 0f, published: 0f),
                (progress: 0.25f, published: 0.25f),
                (progress: 0.999f, published: 0.999f),
                (progress: 1f, published: 1f),
                (progress: 1.5f, published: 1f),
                (progress: float.MaxValue, published: 1f),
                (progress: float.PositiveInfinity, published: 1f),
            };
            Owned.Run(owned =>
            {
                var data = owned.Host("Lucid data manager", false).AddComponent<DataManager>();
                owned.Register(data);
                var view = owned.Host("Lucid progress view", false).AddComponent<DefinitionDataProgressView>();
                Call(view, "Awake");
                foreach (var row in table)
                {
                    // Progress has a private setter and only the data load advances it.
                    Set(data, "<Progress>k__BackingField", row.progress);
                    view.DataLoadProgress.Value = 77f;
                    Call(view, "Update");
                    Assert.That(view.DataLoadProgress.Value, Is.EqualTo(row.published), "progress " + row.progress);
                }
                Set(data, "<Progress>k__BackingField", float.NaN);
                view.DataLoadProgress.Value = 77f;
                Call(view, "Update");
                Assert.That(float.IsNaN(view.DataLoadProgress.Value), Is.True, "NaN passes through minss and the compare");
            });
        }

        // Without a manager in the reference Update publishes nothing; with no reference at all (Awake never ran)
        // the unguarded IsNull call faults.
        [Test]
        public void UpdatePublishesNothingWithoutADataManager()
        {
            Owned.Run(owned =>
            {
                var view = owned.Host("Lucid progress view", false).AddComponent<DefinitionDataProgressView>();
                Assert.Throws<NullReferenceException>(() => Call(view, "Update"), "no reference yet");
                Set(view, "m_dataManager", new SystemRef<DataManager>("lucid-absent-data-manager", null));
                view.DataLoadProgress.Value = 77f;
                Call(view, "Update");
                Assert.That(view.DataLoadProgress.Value, Is.EqualTo(77f));
            });
        }

        // Awake 0x689A20 stores GetSystemSafe<GameCenter>(null, true) in the field (+0x40) and publishes
        // m_gameCenter != null && IsLoggedIn(); Update 0x689AE0 republishes IsLoggedIn only while the system exists.
        // The GameCenter row is created empty by the lookup, so it is owned here.
        [Test]
        public void WithoutAGameCenterAwakePublishesFalseAndUpdateLeavesTheValue()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameCenter>();
                var view = owned.Host("Lucid game center view", false).AddComponent<GameCenterView>();
                view.GameCenterAvailable.Value = true;
                Call(view, "Awake");
                Assert.That(Get<object>(view, "m_gameCenter"), Is.Null);
                Assert.That(view.GameCenterAvailable.Value, Is.False, "published false for a missing system");
                view.GameCenterAvailable.Value = true;
                Call(view, "Update");
                Assert.That(view.GameCenterAvailable.Value, Is.True, "Update does not touch the value without a system");
            });
        }

        // With the system registered both publish its IsLoggedIn; the offline Game Center never reports a login.
        [Test]
        public void WithAGameCenterTheViewPublishesItsLoginState()
        {
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
            throw new InvalidOperationException("Owned offline case forbids original GameCenter.");
#endif
            Owned.Run(owned =>
            {
                // GameCenter subscribes Initialise, AppShutdown and Update and constructs the GameCenterLocalPlayer, which
                // subscribes AppInitialise and AppShutdown; these action rows and both system rows are owned first.
                owned.LeaseActions(SystemAction.Initialise, SystemAction.AppInitialise, SystemAction.AppShutdown, SystemAction.Update);
                var centerRow = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<GameCenter>()));
                centerRow.Register(null);
                var playerRow = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<GameCenterLocalPlayer>()));
                playerRow.Register(null);
                var gameCenter = new GameCenter();
                // The constructor registered itself and its local player into the owned rows.
                centerRow.AdoptCurrent();
                playerRow.AdoptCurrent();

                var view = owned.Host("Lucid game center view", false).AddComponent<GameCenterView>();
                view.GameCenterAvailable.Value = true;
                Call(view, "Awake");
                Assert.That(Get<GameCenter>(view, "m_gameCenter"), Is.SameAs(gameCenter));
                Assert.That(gameCenter.IsLoggedIn(), Is.False, "the offline Game Center has no login");
                Assert.That(view.GameCenterAvailable.Value, Is.False);
                view.GameCenterAvailable.Value = true;
                Call(view, "Update");
                Assert.That(view.GameCenterAvailable.Value, Is.False, "Update republishes the login state");
                Assert.DoesNotThrow(() => Call(view, "OnSetup", (object)null), "OnSetup is a lone ret");
            });
        }
    }
}
