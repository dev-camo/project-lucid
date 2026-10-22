using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using RTLTMPro;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerLevelSelect (Game.Runtime 0x020008c8; OnZoneSelected 0x638350, UpdateZoneState 0x6383F0, UpdateOrnamentBinders
    // 0x638720, Action_Continue 0x638280, constructor 0x638C40). Not covered: Start (it sets up the zones container, follows the
    // ornament manager and refreshes the challenge manager's reward tracks), UpdateLevelState (it needs the App's storage that
    // Start caches), Continue (a level manager that writes the App's storage, a save request and the exit event) and
    // Action_Continue, which only calls Continue.
    public sealed class UIContainerLevelSelectTests
    {
        private static UIContainerLevelSelect NewContainer(Owned owned) => owned.Host("Lucid level select", false).AddComponent<UIContainerLevelSelect>();

        // dump.cs TypeDefIndex 2247 and the DummyDll rows (fields 0x04002317..0x04002326).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerLevelSelect), 0x00100101,
                new (string, int)[]
                {
                    ("m_exitEvent", 0x0001), ("m_zonesContainer", 0x0001), ("m_ornamentsAlertBadge", 0x0001), ("SelectedZoneTitle", 0x0026),
                    ("SelectedZoneSubtitle", 0x0026), ("SelectedLevelName", 0x0026), ("HasNewOrnaments", 0x0026), ("CollectedOrnaments", 0x0026),
                    ("TotalOrnaments", 0x0026), ("ZoneIconAsset", 0x0026), ("ZoneBackgroundStartColour", 0x0026), ("ZoneBackgroundEndColour", 0x0026),
                    ("RewardsAvailable", 0x0026), ("m_app", 0x0001), ("m_selectedLevelDefinition", 0x0001), ("m_zoneIconAssetHandle", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("Action_Continue", 0x0086), ("OnZoneSelected", 0x0081), ("UpdateZoneState", 0x0081),
                    ("UpdateLevelState", 0x0081), ("Continue", 0x0081), ("UpdateOrnamentBinders", 0x0081), (".ctor", 0x1886),
                });
        }

        // OnZoneSelected 0x638350 and UpdateZoneState 0x6383F0 run the same code: the zone's title, subtitle, icon and the two
        // background colours are copied into their bindings (the loads at +0x20, +0x24, +0x40, +0x48..+0x57 and +0x58..+0x67).
        [Test]
        public void ASelectedZoneFillsItsTitleSubtitleIconAndColours()
        {
            foreach (string method in new[] { "OnZoneSelected", "UpdateZoneState" })
            {
                Owned.Run(owned =>
                {
                    owned.LeaseRow<LevelManager>(); // GameplayLevelCategory's field initialiser creates the row
                    var zone = Create<GameplayLevelCategory>(owned,
                        "{\"m_title\":1234,\"m_subtitle\":5678,\"m_backgroundStartColour\":{\"r\":0.25,\"g\":0.5,\"b\":0.75,\"a\":1},\"m_backgroundEndColour\":{\"r\":1,\"g\":0.5,\"b\":0.25,\"a\":0.5}}");
                    var icon = new UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite("lucid-zone-icon");
                    Set(zone, "m_iconAsset", icon); // Reflection: serialized reference
                    UIContainerLevelSelect container = NewContainer(owned);

                    Call(container, method, zone);

                    Assert.That((int)container.SelectedZoneTitle.Value, Is.EqualTo(1234), method);
                    Assert.That((int)container.SelectedZoneSubtitle.Value, Is.EqualTo(5678), method);
                    Assert.That(container.ZoneIconAsset.Value, Is.SameAs(icon), method);
                    Assert.That(container.ZoneBackgroundStartColour.Value, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)), method);
                    Assert.That(container.ZoneBackgroundEndColour.Value, Is.EqualTo(new Color(1f, 0.5f, 0.25f, 0.5f)), method);
                });
            }
        }

        private sealed class OrnamentRig
        {
            internal Owned Owned;
            internal OrnamentManager Manager;
            internal UIContainerLevelSelect Container;
            internal GameObject BadgeTarget;
            internal RTLTextMeshPro BadgeText;
        }

        private static void WithOrnaments(Action<OrnamentRig> body) => Owned.Run(owned =>
        {
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown); // the manager's constructor subscribes both
            owned.LeaseRow<MissionManager>(); // the manager's field initialiser
            var rig = new OrnamentRig { Owned = owned, Manager = new OrnamentManager() };
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(rig.Manager)));
            rig.Container = NewContainer(owned);
            var badgeHost = owned.Track(new GameObject("Lucid ornament badge", typeof(RectTransform)));
            badgeHost.SetActive(false);
            rig.BadgeTarget = owned.Host("Lucid ornament badge target", false, badgeHost.transform);
            rig.BadgeText = badgeHost.AddComponent<RTLTextMeshPro>();
            var badge = badgeHost.AddComponent<UIWidgetSDTAlertBadge>();
            Set(badge, "m_text", rig.BadgeText); // Reflection: serialized references
            Set(badge, "m_enableObjectWhileActive", rig.BadgeTarget);
            Set(rig.Container, "m_ornamentsAlertBadge", badge);
            body(rig);
        });

        private static void AddOrnament(OrnamentRig rig, OrnamentCategory category, OrnamentIdentifier id, bool unlocked, bool seen, bool collected)
        {
            var collections = Get<Dictionary<OrnamentCategory, Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus>>>(rig.Manager, "m_ornamentCollections");
            if (!collections.TryGetValue(category, out Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus> collection))
                collections[category] = collection = new Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus>();
            collection[id] = new OrnamentManager.OrnamentStatus(unlocked, seen, collected);
        }

        // UpdateOrnamentBinders 0x638720: the collected and total counts are copied, and every ornament that is unlocked, not yet
        // seen and collected (the masked compare at 0x638A47..0x638A59, bytes 0, 1, 2 of the status) is counted across all
        // collections. Any other combination of the three flags is not.
        [Test]
        public void OnlyUnlockedUnseenCollectedOrnamentsAreCountedAcrossAllCollections()
        {
            WithOrnaments(rig =>
            {
                Set(rig.Manager, "m_totalCollected", 6); // Reflection: totals BuildMasterList computes from the save
                Set(rig.Manager, "m_totalOrnaments", 11);
                OrnamentIdentifier[] ids = (OrnamentIdentifier[])Enum.GetValues(typeof(OrnamentIdentifier));
                int next = 1;
                // Every combination of (unlocked, seen, collected) in the story collection.
                for (int bits = 0; bits < 8; bits++)
                    AddOrnament(rig, OrnamentCategory.Story, ids[next++], (bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0);
                // One more match in another collection and one that only lacks the collection.
                AddOrnament(rig, OrnamentCategory.Challenge, ids[next++], true, false, true);
                AddOrnament(rig, OrnamentCategory.Challenge, ids[next++], false, false, true);

                Call(rig.Container, "UpdateOrnamentBinders", rig.Manager);

                Assert.That(rig.Container.CollectedOrnaments.Value, Is.EqualTo(6));
                Assert.That(rig.Container.TotalOrnaments.Value, Is.EqualTo(11));
                Assert.That(rig.Container.HasNewOrnaments.Value, Is.True);
                Assert.That(rig.BadgeTarget.activeSelf, Is.True);
                Assert.That(rig.BadgeText.text, Is.EqualTo("2"), "the story match (1, 0, 1) and the challenge match; shown as a count, not as an alert");
            });
        }

        // With nothing new the flag is cleared, the badge object is hidden and no text is written.
        [Test]
        public void WithNothingNewTheBadgeIsHidden()
        {
            WithOrnaments(rig =>
            {
                AddOrnament(rig, OrnamentCategory.Story, OrnamentIdentifier.OrnamentAmyAction, true, true, true);
                rig.Container.HasNewOrnaments.Value = true;
                rig.BadgeTarget.SetActive(true);
                rig.BadgeText.text = "Lucid sentinel"; // a text that a write would replace

                Call(rig.Container, "UpdateOrnamentBinders", rig.Manager);

                Assert.That(rig.Container.HasNewOrnaments.Value, Is.False);
                Assert.That(rig.BadgeTarget.activeSelf, Is.False);
                Assert.That(rig.BadgeText.text, Is.EqualTo("Lucid sentinel"), "no text is written");
            });
        }

        // The badge is optional (op_Equality at 0x638B90): without one the counts are still bound.
        [Test]
        public void WithoutABadgeTheNewOrnamentsAreStillBound()
        {
            WithOrnaments(rig =>
            {
                Set(rig.Container, "m_ornamentsAlertBadge", null); // Reflection: serialized reference
                AddOrnament(rig, OrnamentCategory.Story, OrnamentIdentifier.OrnamentAmyAction, true, false, true);

                Call(rig.Container, "UpdateOrnamentBinders", rig.Manager);

                Assert.That(rig.Container.HasNewOrnaments.Value, Is.True);
            });
        }
    }
}
