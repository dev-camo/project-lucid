using System;
using System.Collections.Generic;
using System.Globalization;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetToggleSaveData (Game.Runtime 0x020009db, Setup 0x6A5380, Select 0x6A5620, constructor 0x6A5640) as a real
    // component beside its real UIToggle on an owned inactive host, with the real save, progress and string table the
    // original reads. The play time string comes from an owned string table.
    public sealed class UIWidgetToggleSaveDataTests
    {
        private const Strings PlayTimeString = Strings.ACT_1;

        // 1500000000000 ms is 2017-07-14 02:40:00 UTC. The player statistic holds 1 h 2 min 3 s. Three ornaments are saved.
        internal const string SaveJson = "{\"m_lastLevelVisitedGUID\":\"lucid-level\",\"m_lastSavedTimestampMs\":1500000000000," +
            "\"m_ornaments\":[{\"m_guid\":\"orn-1\"},{\"m_guid\":\"orn-2\"},{\"m_guid\":\"orn-3\"}]," +
            "\"m_playerStats\":[{\"m_playerStatType\":0,\"m_playerStatCounter\":3723000}]}";

        // A widget with the toggle reference the editor Reset fills.
        internal static UIWidgetToggleSaveData NewSaveToggle(Owned owned, Strings playTimeString = PlayTimeString)
        {
            var widget = owned.Host("Lucid save data toggle", false).AddComponent<UIWidgetToggleSaveData>();
            Call(widget, "Reset"); // Reflection: the editor's Reset is private and Edit Mode does not send it
            Set(widget, "m_playTimeStringId", playTimeString); // serialized in the original
            return widget;
        }

        // Progress holding the given orb and ring counts. The totals of a cached save are only filled from authored
        // mission collectables, so the case writes them into the private dictionary.
        internal static ProgressionCollectableSave NewProgress(int orbsCollected, int orbsTotal, int ringsCollected)
        {
            var progress = new ProgressionCollectableSave();
            var totals = Get<Dictionary<CollectableType, ProgressionCollectable>>(progress, "m_total");
            totals[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, orbsCollected, orbsTotal);
            totals[CollectableType.Ring] = new ProgressionCollectable(CollectableType.Ring, ringsCollected, ringsCollected + 1);
            return progress;
        }

        // The play time string and the current culture are put back after the case.
        private static void Run(string playTimeFormat, Action<Owned> body)
        {
            CultureInfo priorCulture = CultureInfo.CurrentCulture;
            Owned.Run(owned =>
            {
                owned.Lease(new ReleaseAction(() => CultureInfo.CurrentCulture = priorCulture));
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                OwnedChallengeServices.Register(owned); // a ProgressionCollectableSave takes the challenge and dream power store managers
                OwnedStringTable.Run(new Dictionary<Strings, string> { { PlayTimeString, playTimeFormat } }, () => body(owned));
            });
        }

        private static void SetSentinels(UIWidgetToggleSaveData widget)
        {
            widget.NumberOfOrbs.Value = 5;
            widget.NumberOfOrnaments.Value = 6;
            widget.LastedUpdatedTimestamp.Value = "stamp";
            widget.PlayTimeTimestamp.Value = "play";
        }

        private static void AssertSentinels(UIWidgetToggleSaveData widget)
        {
            Assert.That(widget.NumberOfOrbs.Value, Is.EqualTo(5));
            Assert.That(widget.NumberOfOrnaments.Value, Is.EqualTo(6));
            Assert.That(widget.LastedUpdatedTimestamp.Value, Is.EqualTo("stamp"));
            Assert.That(widget.PlayTimeTimestamp.Value, Is.EqualTo("play"));
        }

        // The constructor (0x6A5640) builds the five binders in declaration order, each its own object.
        [Test]
        public void AFreshWidgetHasFiveSeparateEmptyBinders()
        {
            Owned.Run(owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);

                object[] binders = { widget.NumberOfOrbs, widget.NumberOfOrnaments, widget.LastedUpdatedTimestamp, widget.PlayTimeTimestamp, widget.SlotEmpty };
                Assert.That(binders, Is.All.Not.Null);
                Assert.That(binders, Is.Unique);
                Assert.That(widget.NumberOfOrbs.Value, Is.EqualTo(0));
                Assert.That(widget.NumberOfOrnaments.Value, Is.EqualTo(0));
                Assert.That(widget.LastedUpdatedTimestamp.Value, Is.Null);
                Assert.That(widget.PlayTimeTimestamp.Value, Is.Null);
                Assert.That(widget.SlotEmpty.Value, Is.False);
            });
        }

        // 0x6A5405 runs the base setup first (label and callback), then reads the parameters (+0x20 save, +0x28 progress).
        // A slot without a save (0x6A542B) only sets SlotEmpty (0x6A543E): the other binders keep what they held.
        [Test]
        public void ASlotWithoutASaveIsOnlyMarkedEmpty()
        {
            Run("Played {0}", owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);
                SetSentinels(widget);
                var received = new List<bool>();

                widget.Setup(new UIContainerSaveDataViewParameters(null, NewProgress(7, 9, 20), "Slot label", received.Add));

                Assert.That(widget.SlotEmpty.Value, Is.True);
                AssertSentinels(widget);
                Assert.That(widget.Label.Value, Is.EqualTo("Slot label"));
                widget.Action_OnValueChanged(true);
                Assert.That(received, Is.EqualTo(new[] { true }));
            });
        }

        // IsSaveEmpty (0x5E7E90) is a blank-or-null check of the last visited level (+0x70): a save that never visited a
        // level is empty even when it holds statistics.
        [TestCase("{\"m_lastSavedTimestampMs\":1500000000000}", TestName = "ASaveWithoutAVisitedLevelIsEmpty")]
        [TestCase("{\"m_lastLevelVisitedGUID\":\"  \",\"m_lastSavedTimestampMs\":1500000000000}", TestName = "ASaveWhoseVisitedLevelIsBlankIsEmpty")]
        public void ASaveWithoutAVisitedLevelIsMarkedEmpty(string json)
        {
            Run("Played {0}", owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);
                SetSentinels(widget);

                widget.Setup(new UIContainerSaveDataViewParameters(JsonUtility.FromJson<SaveDataGame>(json), NewProgress(7, 9, 20), "label", null));

                Assert.That(widget.SlotEmpty.Value, Is.True);
                AssertSentinels(widget);
            });
        }

        // 0x6A5476 clears SlotEmpty, 0x6A548F..0x6A54B0 publishes the Orb entry's collected count (the second field of the
        // GetValueOrDefault result, shr 0x20) and 0x6A54BF the saved ornament count.
        [Test]
        public void ASavedSlotPublishesTheCollectedOrbsAndTheOrnamentCount()
        {
            Run("Played {0}", owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);
                widget.SlotEmpty.Value = true;

                widget.Setup(new UIContainerSaveDataViewParameters(JsonUtility.FromJson<SaveDataGame>(SaveJson), NewProgress(7, 9, 20), "label", null));

                Assert.That(widget.SlotEmpty.Value, Is.False);
                Assert.That(widget.NumberOfOrbs.Value, Is.EqualTo(7), "collected, not the total of 9 or the 20 rings");
                Assert.That(widget.NumberOfOrnaments.Value, Is.EqualTo(3));
            });
        }

        // GetValueOrDefault (0x6A5497) gives a zero entry when the progress holds no Orb total.
        [Test]
        public void ProgressWithoutAnOrbTotalShowsNoOrbs()
        {
            Run("Played {0}", owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);
                widget.NumberOfOrbs.Value = 5;
                ProgressionCollectableSave progress = NewProgress(7, 9, 20);
                Get<Dictionary<CollectableType, ProgressionCollectable>>(progress, "m_total").Remove(CollectableType.Orb);

                widget.Setup(new UIContainerSaveDataViewParameters(JsonUtility.FromJson<SaveDataGame>(SaveJson), progress, "label", null));

                Assert.That(widget.NumberOfOrbs.Value, Is.EqualTo(0));
            });
        }

        // 0x6A54D9..0x6A5559: the stored value (+0xB0) is read as Unix milliseconds (TimeUtils.FromUnixTime), converted to
        // local time and formatted with the current culture. The wall-clock text of the invariant culture is read back, so
        // the case does not depend on the machine's time zone.
        [Test]
        public void TheSaveTimeIsShownInLocalTimeFromUnixMilliseconds()
        {
            Run("Played {0}", owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);

                widget.Setup(new UIContainerSaveDataViewParameters(JsonUtility.FromJson<SaveDataGame>(SaveJson), NewProgress(1, 1, 1), "label", null));

                DateTime shown = DateTime.Parse(widget.LastedUpdatedTimestamp.Value, CultureInfo.InvariantCulture);
                Assert.That(shown.Ticks, Is.EqualTo(new DateTime(2017, 7, 14, 2, 40, 0, DateTimeKind.Utc).ToLocalTime().Ticks));
            });
        }

        // 0x6A5574..0x6A55F6: the table string of the serialized id is formatted with the play time statistic (type 0, the
        // counter at +0x18 in milliseconds) written as hh:mm:ss.
        [Test]
        public void ThePlayTimeFillsTheLocalisedPlayTimeString()
        {
            Run("Played {0} so far", owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);

                widget.Setup(new UIContainerSaveDataViewParameters(JsonUtility.FromJson<SaveDataGame>(SaveJson), NewProgress(1, 1, 1), "label", null));

                Assert.That(widget.PlayTimeTimestamp.Value, Is.EqualTo("Played 01:02:03 so far"));
            });
        }

        // The format is the literal "hh\:mm\:ss" (0x2bfc688): the hours are those of the day, so 25 hours show as 01.
        [Test]
        public void ThePlayTimeShowsTheHoursOfTheDayOnly()
        {
            Run("{0}", owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);
                string json = "{\"m_lastLevelVisitedGUID\":\"lucid-level\",\"m_playerStats\":[{\"m_playerStatType\":0,\"m_playerStatCounter\":90061000}]}";

                widget.Setup(new UIContainerSaveDataViewParameters(JsonUtility.FromJson<SaveDataGame>(json), NewProgress(1, 1, 1), "label", null));

                Assert.That(widget.PlayTimeTimestamp.Value, Is.EqualTo("01:01:01"));
            });
        }

        // Only the total play time statistic (type 0) counts; another statistic holding more time is not used.
        [Test]
        public void OnlyTheTotalPlayTimeStatisticIsUsed()
        {
            Run("{0}", owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);
                string json = "{\"m_lastLevelVisitedGUID\":\"lucid-level\",\"m_playerStats\":[" +
                    "{\"m_playerStatType\":2,\"m_playerStatCounter\":7200000},{\"m_playerStatType\":0,\"m_playerStatCounter\":61000}]}";

                widget.Setup(new UIContainerSaveDataViewParameters(JsonUtility.FromJson<SaveDataGame>(json), NewProgress(1, 1, 1), "label", null));

                Assert.That(widget.PlayTimeTimestamp.Value, Is.EqualTo("00:01:01"));
            });
        }

        // Select 0x6A5620 turns the toggle on (UIToggle.set_IsOn, +0x20); turning it on again changes nothing.
        [Test]
        public void SelectingTurnsTheToggleOnOnce()
        {
            Owned.Run(owned =>
            {
                UIWidgetToggleSaveData widget = NewSaveToggle(owned);
                var toggle = widget.GetComponent<UIToggle>();
                var changes = new List<bool>();
                toggle.OnValueChanged.AddListener(changes.Add);
                Assert.That(widget.IsSelected(), Is.False);

                widget.Select();
                widget.Select();

                Assert.That(widget.IsSelected(), Is.True);
                Assert.That(changes, Is.EqualTo(new[] { true }));
            });
        }
    }
}
