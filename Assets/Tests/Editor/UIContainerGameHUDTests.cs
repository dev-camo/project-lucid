using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerGameHUD (Game.Runtime 020008be, x86-64 0x6323C0..0x633F90, closure 020008bf) as a real component on
    // an owned inactive host. Its field initialisers take six manager references, so those registry rows are leased
    // and the CollectableManager (whose row the cases read) is registered. Start, Update and OnDestroy are not
    // covered: they subscribe to the static control mapping and input bridge, instance the boss health prefab and
    // read the App, TimeManager, save and mission managers (the app's focus flag, the saved game speed, the active
    // mission's tracker and characters). The override branch of DetermineIfCharacterSelectIsActive needs an active
    // mission context (SetActiveMissionContext loads the mission group and save) and the data manager's characters, and
    // the ChangeCharacter wrap ends in CharacterManager.SwapCharacter, whose id is observable only through the swap
    // coroutine on the process-wide host with a started current character; CharacterSelectActive's CanSwitch passthrough
    // needs a character with modifiers and collision trackers. None of those is built here, so the index wrap, the
    // archetype override's Visible = false, Update's non-short-circuit evaluation and the game speed predicate are not covered.
    public sealed class UIContainerGameHUDTests
    {
        private static void WithHud(Action<Owned, UIContainerGameHUD, CollectableManager> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<CharacterManager>();
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<SaveManager>();
            owned.LeaseRow<ProgressionManager>(); // the collectable counters field initialisers
            owned.LeaseRow<TimeManager>();
            owned.LeaseRow<DataManager>();
            CollectableManager collectables = owned.Register(new CollectableManager());
            body(owned, owned.Host("Lucid game HUD", false).AddComponent<UIContainerGameHUD>(), collectables);
        });

        private static CollectableState State(int total, int collected)
        {
            var state = new CollectableState();
            state.AddCollectable(total);
            state.AdjustCollected(collected, true);
            return state;
        }

        // dump.cs TypeDefIndex 2238/2237 and the DummyDll rows (fields 0x040022cb..0x040022df).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerGameHUD), 0x00100101,
                new (string, int)[]
                {
                    ("m_childContainerParent", 0x0001),
                    ("m_collectableContainers", 0x0001),
                    ("m_bossHealthContainerPrefab", 0x0001),
                    ("m_focusedUIOverrides", 0x0001),
                    ("m_controllerUIOverrides", 0x0001),
                    ("CharacterSelectAllowed", 0x0026),
                    ("CharacterSelectVisible", 0x0026),
                    ("GameSpeedToggleVisible", 0x0026),
                    ("GameSpeedApplied", 0x0026),
                    ("m_collectableManagerRef", 0x0021),
                    ("m_characterManagerRef", 0x0021),
                    ("m_missionManagerRef", 0x0021),
                    ("m_saveManagerRef", 0x0021),
                    ("m_timeManagerRef", 0x0021),
                    ("m_dataManagerRef", 0x0021),
                    ("m_app", 0x0001),
                    ("m_inFocusMode", 0x0001),
                    ("m_isCollectingObjective", 0x0001),
                    ("m_inputBridge", 0x0001),
                    ("m_validCharacterIds", 0x0001),
                    ("m_bossHealthContainer", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_SaveDataSettings", 0x0881),
                    ("Start", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("Update", 0x0081),
                    ("EnterFocusMode", 0x0081),
                    ("LeaveFocusMode", 0x0081),
                    ("CharacterSelectActive", 0x0081),
                    ("OnCharacterInputSelectNext", 0x0081),
                    ("OnCharacterInputSelectPrev", 0x0081),
                    ("ChangeCharacter", 0x0081),
                    ("Action_GameSpeedToggle", 0x0086),
                    ("ShowCollectableViews", 0x0081),
                    ("SetupBossUI", 0x0081),
                    ("SetupCollectableViews", 0x0081),
                    ("OnCollectableStateUpdate", 0x0081),
                    ("OnUpdateLastInputType", 0x0081),
                    ("DetermineIfCharacterSelectIsActive", 0x0081),
                    ("ListenToMissionObjectiveCollect", 0x0081),
                    ("DisableCharacterSelectByMissionObjectiveCollected", 0x0081),
                    ("EnableCharacterSelectByMissionObjectiveCollected", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerGameHUD), 1, 2);
            Assert.That(typeof(UIContainerGameHUD).BaseType, Is.EqualTo(typeof(UIContainer)));

            Type closure = typeof(UIContainerGameHUD).GetNestedTypes(BindingFlags.NonPublic).Single();
            Assert.That(closure.Name, Is.EqualTo("<>c__DisplayClass31_0"));
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00100103), "NestedPrivate|Sealed|BeforeFieldInit");
            Assert.That(closure.GetFields().Select(field => (field.Name, field.FieldType)).ToArray(),
                Is.EqualTo(new[] { ("currentCharacterId", typeof(CharacterId)) }));
        }

        // The constructor 0x633cc0 creates the four bindables, takes the six references in field order and an empty
        // character list (+0xc8).
        [Test]
        public void ConstructionCreatesFourFalseBindablesAndAnEmptyCharacterList()
        {
            WithHud((owned, hud, collectables) =>
            {
                Assert.That(hud.CharacterSelectAllowed.Value, Is.False);
                Assert.That(hud.CharacterSelectVisible.Value, Is.False);
                Assert.That(hud.GameSpeedToggleVisible.Value, Is.False);
                Assert.That(hud.GameSpeedApplied.Value, Is.False);
                Assert.That(Get<List<CharacterId>>(hud, "m_validCharacterIds"), Is.Empty);
                Assert.That(Get<bool>(hud, "m_isCollectingObjective"), Is.False);
                Assert.That(Get<bool>(hud, "m_inFocusMode"), Is.False);
            });
        }

        // The objective callbacks 0x633ca0 and 0x633cb0 set and clear the collecting flag; the enable form ignores its
        // tracker argument.
        [Test]
        public void TheObjectiveCallbacksSetAndClearTheCollectingFlag()
        {
            WithHud((owned, hud, collectables) =>
            {
                Call(hud, "DisableCharacterSelectByMissionObjectiveCollected");
                Assert.That(Get<bool>(hud, "m_isCollectingObjective"), Is.True);

                Call(hud, "EnableCharacterSelectByMissionObjectiveCollected", new object[] { null });
                Assert.That(Get<bool>(hud, "m_isCollectingObjective"), Is.False);
            });
        }

        // CharacterSelectActive 0x633740 is false while an objective is being collected, before it touches the character
        // (a null character here would fault on CanSwitch).
        [Test]
        public void CharacterSelectIsInactiveWhileAnObjectiveIsCollected()
        {
            WithHud((owned, hud, collectables) =>
            {
                Call(hud, "DisableCharacterSelectByMissionObjectiveCollected");

                Assert.That((bool)Call(hud, "CharacterSelectActive", new object[] { null }), Is.False);
            });
        }

        // ChangeCharacter 0x6337c0 does nothing unless the selection is both visible (0x63387a) and allowed (0x633898):
        // the managers are not even read (no CharacterManager or MissionManager is registered here).
        [Test]
        public void ChangingCharacterNeedsTheSelectionVisibleAndAllowed()
        {
            WithHud((owned, hud, collectables) =>
            {
                Assert.DoesNotThrow(() => Call(hud, "ChangeCharacter", (CharacterId)0x1234, 1));

                hud.CharacterSelectVisible.Value = true;
                Assert.DoesNotThrow(() => Call(hud, "ChangeCharacter", (CharacterId)0x1234, 1));

                Assert.That(Get<List<CharacterId>>(hud, "m_validCharacterIds"), Is.Empty, "no mission characters were read");
            });
        }

        // DetermineIfCharacterSelectIsActive 0x6328d0 shows the selection when there is no mission manager to ask
        // (TryGet false, jump to 0x632c0b). DebugUnlockLevels.AreAllLevelsUnlocked is a constant false in the player.
        [Test]
        public void WithoutAMissionManagerTheCharacterSelectionIsShown()
        {
            WithHud((owned, hud, collectables) =>
            {
                Call(hud, "DetermineIfCharacterSelectIsActive");

                Assert.That(hud.CharacterSelectVisible.Value, Is.True);
            });
        }

        // ShowCollectableViews 0x632730 sets up at once for a valid manager, and SetupCollectableViews 0x633a70 adds the
        // HUD's OnCollectableStateUpdate to the manager's event before walking the (here empty) counter array.
        [Test]
        public void ShowingCollectableViewsSubscribesToAValidManager()
        {
            WithHud((owned, hud, collectables) =>
            {
                Set(hud, "m_collectableContainers", new UIContainerCollectableCounter[0]); // serialized authored array
                Assert.That(collectables.OnCollectableStateUpdate, Is.Null);

                Call(hud, "ShowCollectableViews");

                Delegate[] handlers = collectables.OnCollectableStateUpdate.GetInvocationList();
                Assert.That(handlers.Length, Is.EqualTo(1));
                Assert.That(handlers[0].Target, Is.SameAs(hud));
                Assert.That(handlers[0].Method.Name, Is.EqualTo("OnCollectableStateUpdate"));
            });
        }

        // OnCollectableStateUpdate 0x633c20 forwards to the counters of the updated type only (cmp [counter+0x38] at
        // 0x633c60, virtual call at 0x633c75).
        [Test]
        public void ACollectableUpdateReachesOnlyTheCountersOfThatType()
        {
            WithHud((owned, hud, collectables) =>
            {
                var orbs = owned.Host("Lucid HUD orb counter", false).AddComponent<UIContainerCollectableCounter>();
                var coins = owned.Host("Lucid HUD coin counter", false).AddComponent<UIContainerCollectableCounter>();
                var moreOrbs = owned.Host("Lucid HUD second orb counter", false).AddComponent<UIContainerCollectableCounter>();
                Set(orbs, "m_type", CollectableType.Orb);
                Set(coins, "m_type", CollectableType.BlueCoin);
                Set(moreOrbs, "m_type", CollectableType.Orb);
                Set(hud, "m_collectableContainers", new[] { orbs, coins, moreOrbs });

                Call(hud, "OnCollectableStateUpdate", CollectableType.Orb, State(9, 6), default(CollectableChangeMetadata));

                Assert.That(orbs.TotalAmount.Value, Is.EqualTo(9));
                Assert.That(orbs.CollectedAmount.Value, Is.EqualTo(6));
                Assert.That(moreOrbs.TotalAmount.Value, Is.EqualTo(9));
                Assert.That(moreOrbs.CollectedAmount.Value, Is.EqualTo(6));
                Assert.That(coins.TotalAmount.Value, Is.Zero, "the other type is left alone");
                Assert.That(coins.HasValues.Value, Is.False);
            });
        }
    }
}
