using System;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // These are owned managed records, their genuine save base, and Unity's
    // serializer. No save manager, platform service, or replacement provider runs.
    [TestFixture]
    public sealed class OriginalChallengeRewardSaveTests
    {
        private static readonly FieldInfo DirtyField = typeof(SaveDataItem).GetField(
            "m_dirty", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        private static readonly FieldInfo SavingField = typeof(SaveDataItem).GetField(
            "m_savingEnabled", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        private static bool Dirty(SaveDataChallengeReward value)
        { return (bool)DirtyField.GetValue(value); }

        private static bool SavingEnabled(SaveDataChallengeReward value)
        { return (bool)SavingField.GetValue(value); }

        [Test]
        public void OriginalRewardMergeRetainsAllFlagsAndRawDirtyStates()
        {
            foreach (bool oldFlag in new[] { false, true })
            foreach (bool newFlag in new[] { false, true })
            foreach (bool oldDirty in new[] { false, true })
            foreach (bool newDirty in new[] { false, true })
            foreach (bool oldSaving in new[] { false, true })
            foreach (bool newSaving in new[] { false, true })
            {
                string track = new string(new[] { 't', 'r', 'a', 'c', 'k' });
                string reward = new string(new[] { 'r', 'e', 'w', 'a', 'r', 'd' });
                var value = new SaveDataChallengeReward(track, reward);
                var incoming = new SaveDataChallengeReward(null, "other reward");
                value.Collected = oldFlag;
                incoming.Collected = newFlag;
                value.MarkSaved();
                incoming.MarkSaved();
                if (oldDirty) value.MarkDirty();
                if (newDirty) incoming.MarkDirty();
                if (!oldSaving) value.DisableSaving();
                if (!newSaving) incoming.DisableSaving();

                value.ResolveNewData(incoming);

                Assert.That(value.Collected, Is.EqualTo(oldFlag || newFlag), "Monotonic collected merge");
                Assert.That(incoming.Collected, Is.EqualTo(newFlag), "Incoming record remains intact");
                Assert.That(value.TrackGUID, Is.SameAs(track), "Destination track identity remains intact");
                Assert.That(value.RewardGUID, Is.SameAs(reward), "Destination reward identity remains intact");
                Assert.That(incoming.TrackGUID, Is.Null, "Unvalidated incoming track remains null");
                Assert.That(incoming.RewardGUID, Is.EqualTo("other reward"));
                Assert.That(Dirty(value), Is.EqualTo(oldDirty), "Merge does not invoke dirty setter");
                Assert.That(Dirty(incoming), Is.EqualTo(newDirty));
                Assert.That(SavingEnabled(value), Is.EqualTo(oldSaving));
                Assert.That(SavingEnabled(incoming), Is.EqualTo(newSaving));
            }
        }

        [Test]
        public void OriginalRewardSettersAndEmptyHooksKeepSaveSemantics()
        {
            var value = new SaveDataChallengeReward(null, null);
            Assert.That(value.TrackGUID, Is.Null);
            Assert.That(value.RewardGUID, Is.Null);
            Assert.That(value.Collected, Is.False);
            Assert.That(Dirty(value), Is.False);
            Assert.That(SavingEnabled(value), Is.True, "Ordinary constructor runs the genuine save base");

            value.Collected = false;
            Assert.That(Dirty(value), Is.True, "Even equal assignments mark dirty");
            value.MarkSaved();
            Assert.That(Dirty(value), Is.False);
            value.Collected = true;
            Assert.That(value.HasChangesToSave(), Is.True);
            value.MarkSaved();
            value.Collected = true;
            Assert.That(Dirty(value), Is.True, "Repeated true assignment still marks dirty");
            value.DisableSaving();
            value.OnBeforeSerialize();
            value.OnAfterDeserialize();
            value.Initialise();
            value.ResolveNewData(value);
            Assert.That(value.Collected, Is.True, "Empty hooks and self-merge retain data");
            Assert.That(Dirty(value), Is.True);
            Assert.That(SavingEnabled(value), Is.False);
            Assert.That(value.HasChangesToSave(), Is.False, "Disabled saving differs from the raw dirty flag");
            value.MarkSaved();
            Assert.That(Dirty(value), Is.True, "Disabled saving retains dirty state");

            foreach (bool flag in new[] { false, true })
            foreach (bool dirty in new[] { false, true })
            {
                var target = new SaveDataChallengeReward("retained track", "retained reward");
                target.Collected = flag;
                target.MarkSaved();
                if (dirty) target.MarkDirty();
                Assert.Throws<NullReferenceException>(() => target.ResolveNewData(null));
                Assert.That(target.Collected, Is.EqualTo(flag), "Null incoming faults before the store");
                Assert.That(Dirty(target), Is.EqualTo(dirty));
                Assert.That(target.TrackGUID, Is.EqualTo("retained track"));
                Assert.That(target.RewardGUID, Is.EqualTo("retained reward"));
                Assert.That(SavingEnabled(target), Is.True);
            }
        }

        [Test]
        public void RealUnityRewardSerializationKeepsOriginalDataAndRuntimeFlags()
        {
            var source = new SaveDataChallengeReward("track \"quoted\"", "reward \u2600");
            source.Collected = true;
            source.DisableSaving();
            string json = JsonUtility.ToJson(source);
            Assert.That(json.Contains("\"m_trackGUID\""), Is.True);
            Assert.That(json.Contains("\"m_rewardGUID\""), Is.True);
            Assert.That(json.Contains("\"m_collected\":true"), Is.True);
            Assert.That(json.Contains("m_dirty"), Is.False);
            Assert.That(json.Contains("m_savingEnabled"), Is.False);
            Assert.That(Dirty(source), Is.True, "Original serialization hooks have no side effects");
            Assert.That(SavingEnabled(source), Is.False);

            var restored = JsonUtility.FromJson<SaveDataChallengeReward>(json);
            Assert.That(restored.TrackGUID, Is.EqualTo(source.TrackGUID));
            Assert.That(restored.RewardGUID, Is.EqualTo(source.RewardGUID));
            Assert.That(restored.Collected, Is.True);
            Assert.That(Dirty(restored), Is.False);
            Assert.That(SavingEnabled(restored), Is.False, "Parameterized-only record has serializer runtime defaults");
            restored.Collected = false;
            Assert.That(Dirty(restored), Is.True);
            Assert.That(restored.HasChangesToSave(), Is.False);

            var existing = new SaveDataChallengeReward("old track", "old reward");
            existing.MarkDirty();
            JsonUtility.FromJsonOverwrite(json, existing);
            Assert.That(existing.TrackGUID, Is.EqualTo(source.TrackGUID));
            Assert.That(existing.RewardGUID, Is.EqualTo(source.RewardGUID));
            Assert.That(existing.Collected, Is.True);
            Assert.That(Dirty(existing), Is.True, "Overwrite retains existing unpersisted dirty state");
            Assert.That(SavingEnabled(existing), Is.True, "Overwrite preserves the genuine constructed base");
            existing.MarkSaved();
            Assert.That(existing.HasChangesToSave(), Is.False);

            var missing = JsonUtility.FromJson<SaveDataChallengeReward>("{\"m_collected\":true}");
            Assert.That(missing.TrackGUID, Is.Null);
            Assert.That(missing.RewardGUID, Is.Null);
            Assert.That(missing.Collected, Is.True);
            Assert.That(SavingEnabled(missing), Is.False);
            missing.ResolveNewData(new SaveDataChallengeReward("different track", "different reward"));
            Assert.That(missing.Collected, Is.True);
            Assert.That(Dirty(missing), Is.False, "Merging a restored record retains serializer dirty defaults");
        }
    }
}
