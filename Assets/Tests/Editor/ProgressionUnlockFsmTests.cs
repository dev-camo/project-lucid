using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime UIProgressionUtilities (TypeDef 0x02000a39, x86-64 0x6B83F0..0x6B8E2B and the shared
    // TryGetCurrentUnlock<T> 0x9DC040) and ApplicationStateRemoveProgressionUnlock (0x02000118, 0x652F30..0x6534B5). The
    // unlock list lives in the real App's storage (the App registered by OwnedCharacterManagers), and the unlocks are
    // plain IMetaGameUnlock implementations defined here, as the interface is the only contract the code reads. The
    // animation wait is covered only for a missing animation; playing one needs a live Animation and TimeManager.
    public sealed class ProgressionUnlockFsmTests : OwnedGraphNamesFixture
    {
        public ProgressionUnlockFsmTests() : base("Lucid-unlock-node-") { }


        private class Unlock : IMetaGameUnlock
        {
            private readonly string m_id;
            private readonly PlayerProgressionTypes m_type;
            public int ProgressionTypeReads;

            public Unlock(string id, PlayerProgressionTypes type)
            {
                m_id = id;
                m_type = type;
            }

            public string GetId() => m_id;
            public PlayerProgressionTypes GetProgressionType()
            {
                ProgressionTypeReads++;
                return m_type;
            }
            public UIWidgetProgression GetWidget() => null;
            public Strings GetTitle() => default;
            public Strings GetBody() => default;
            public HLAudioClipIdentifier GetAudio() => default;
            public void Save(SaveManager saveManager) { }
            public void GetCustomTexture(Action<Texture> onLoaded) { }
            public void GetCustomSprite(Action<Sprite> onLoaded) { }
            public void ReleaseAssets() { }
            public bool CanExitToMenu() => false;
            public bool UsesAlternativeExitToMenuEvent() => false;
        }

        private sealed class SpecialUnlock : Unlock
        {
            public SpecialUnlock(string id, PlayerProgressionTypes type) : base(id, type) { }
        }

        private static void WithApp(Action<App, List<IMetaGameUnlock>> body, params IMetaGameUnlock[] unlocks) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the App, DataManager and SaveManager rows.
            owned.Lease(new OwnedCharacterManagers());
            App app = ProcessManager.GetSystem<App>();
            var list = new List<IMetaGameUnlock>(unlocks);
            app.Storage.SetValue(AppFSMKeys.MetaGameUnlocks, list);
            body(app, list);
        });

        // TryGetCurrentUnlock 0x6B83F0: the out value is cleared first (0x6b84a8), the list is read from the App's storage
        // (0x6b851f), and each element has GetProgressionType (slot 1, 0x6b869d) and GetId (slot 0, 0x6b86f9) called before
        // HashSet.Add (0x6b870b). The set starts empty, so the first unlock is always the current one.
        [Test]
        public void TheCurrentUnlockIsTheHeadOfTheStoredList()
        {
            var first = new Unlock("lucid-a", PlayerProgressionTypes.MusicTrack);
            var second = new Unlock("lucid-a", PlayerProgressionTypes.Level);
            WithApp((app, list) =>
            {
                Assert.That(UIProgressionUtilities.TryGetCurrentUnlock(out IMetaGameUnlock current), Is.True);
                Assert.That(current, Is.SameAs(first));
                Assert.That(first.ProgressionTypeReads, Is.EqualTo(1), "the progression type is read although unused");
                Assert.That(second.ProgressionTypeReads, Is.EqualTo(0), "the walk stops at the head");
                Assert.That(list.Count, Is.EqualTo(2), "nothing is removed");
            }, first, second);
        }

        [Test]
        public void NoCurrentUnlockForAnEmptyListClearsTheOutValue()
        {
            WithApp((app, list) =>
            {
                Assert.That(UIProgressionUtilities.TryGetCurrentUnlock(out IMetaGameUnlock current), Is.False);
                Assert.That(current, Is.Null);
            });
        }

        // TryGetCurrentUnlock<T> 0x9DC040: the id is added to the seen set (HashSet.Add, 0x9dc36b) before the IsInst test for
        // T (0x9dc39b), so an unlock that is not a T still uses up its id, and a later T with the same id is skipped.
        [Test]
        public void TheTypedCurrentUnlockSkipsNonMatchesAndAlreadySeenIds()
        {
            var plain = new Unlock("lucid-a", PlayerProgressionTypes.Level);
            var shadowed = new SpecialUnlock("lucid-a", PlayerProgressionTypes.Level);
            var special = new SpecialUnlock("lucid-b", PlayerProgressionTypes.Level);
            WithApp((app, list) =>
            {
                Assert.That(UIProgressionUtilities.TryGetCurrentUnlock(out SpecialUnlock current), Is.True);
                Assert.That(current, Is.SameAs(special), "a plain unlock and a shadowed id are skipped");
            }, plain, shadowed, special);
            WithApp((app, list) =>
            {
                Assert.That(UIProgressionUtilities.TryGetCurrentUnlock(out SpecialUnlock current), Is.False);
                Assert.That(current, Is.Null);
            }, plain, shadowed);
            WithApp((app, list) =>
            {
                Assert.That(UIProgressionUtilities.TryGetCurrentUnlock(out SpecialUnlock current), Is.True);
                Assert.That(current, Is.SameAs(shadowed));
            }, shadowed, special);
        }

        // RemoveUnlock 0x6B88B0: the first matching element is removed from the stored list (List.Remove, 0x6b8a25), which is
        // then written back under the same key (SetValue, 0x6b8adb).
        [Test]
        public void RemovingAnUnlockTakesItOutOfTheStoredList()
        {
            var first = new Unlock("lucid-a", PlayerProgressionTypes.Level);
            var second = new Unlock("lucid-b", PlayerProgressionTypes.Level);
            WithApp((app, list) =>
            {
                UIProgressionUtilities.RemoveUnlock(second);
                Assert.That(app.Storage.GetValueOnly<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks), Is.SameAs(list));
                Assert.That(list, Is.EqualTo(new IMetaGameUnlock[] { first }));
                UIProgressionUtilities.RemoveUnlock(second);
                Assert.That(list, Is.EqualTo(new IMetaGameUnlock[] { first }), "an absent unlock changes nothing");
            }, first, second);
        }

        // RemoveUnlocksOfType 0x6B8AE0 walks the list from the last index down (0x6b8c74..0x6b8cab), asks each unlock for
        // its progression type and removes it when the type list contains it (RemoveAt, 0x6b8d56), then stores the list.
        [Test]
        public void RemovingByTypeKeepsTheOtherUnlocksInOrder()
        {
            var level = new Unlock("lucid-level", PlayerProgressionTypes.Level);
            var music = new Unlock("lucid-music", PlayerProgressionTypes.MusicTrack);
            var ornament = new Unlock("lucid-ornament", PlayerProgressionTypes.Ornament);
            var music2 = new Unlock("lucid-music-2", PlayerProgressionTypes.MusicTrack);
            WithApp((app, list) =>
            {
                UIProgressionUtilities.RemoveUnlocksOfType(new List<PlayerProgressionTypes> { PlayerProgressionTypes.MusicTrack, PlayerProgressionTypes.Ornament });
                Assert.That(list, Is.EqualTo(new IMetaGameUnlock[] { level }));
                Assert.That(app.Storage.GetValueOnly<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks), Is.SameAs(list));
                UIProgressionUtilities.RemoveUnlocksOfType(new List<PlayerProgressionTypes>());
                Assert.That(list, Is.EqualTo(new IMetaGameUnlock[] { level }));
            }, level, music, ornament, music2);
        }

        // WaitForAnimation: the first step tests the Animation (UnityEngine.Object.op_Equality, 0x6b8ecb) and, when it is
        // missing, jumps straight to invoking onComplete (0x6b90bd..0x6b90ce) and ends without reaching the TimeManager
        // lookup (0x6b8f01). A missing completion callback faults in the same step.
        [Test]
        public void WaitingForAMissingAnimationCompletesAtOnce()
        {
            int completed = 0;
            IEnumerator wait = UIProgressionUtilities.WaitForAnimation(null, null, () => completed++);
            Assert.That(completed, Is.EqualTo(0), "nothing runs before the first step");
            Assert.That(wait.MoveNext(), Is.False);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(wait.MoveNext(), Is.False);
            Assert.That(completed, Is.EqualTo(1));

            IEnumerator withoutCallback = UIProgressionUtilities.WaitForAnimation(null, null, null);
            Assert.Throws<NullReferenceException>(() => withoutCallback.MoveNext());
        }

        private static IFSMState NewState(int maxDisplayedUnlocks, params PlayerProgressionTypes[] cleared)
        {
            string types = string.Join(",", Array.ConvertAll(cleared, type => "\"" + type + "\""));
            return ApplicationStateRemoveProgressionUnlock.ConstructInstance(
                new FiniteStateMachine("Lucid-unlock-node-" + Guid.NewGuid().ToString("N"), skipAddToManager: true),
                "Lucid-unlock-node-" + Guid.NewGuid().ToString("N"),
                "{\"MaxDisplayedUnlocks\":" + maxDisplayedUnlocks + ",\"ClearedProgressionTypes\":[" + types + "]}");
        }

        // RemoveProgressionUnlock DoOnEnter 0x653240 (the base entry is not called): the current unlock is removed from the
        // App's list (0x6533a5, SetValue 0x653459) and the shown counter is incremented through AdjustValue (0x65348e); once
        // the counter reaches the limit (cmp eax, [+0x24]; jl, 0x653497) the configured types are removed too. The limit
        // itself triggers the clear (>=), and the counter keeps counting afterwards.
        [Test]
        public void EnteringShowsOneUnlockAndClearsTheConfiguredTypesAtTheLimit()
        {
            var first = new Unlock("lucid-1", PlayerProgressionTypes.Level);
            var second = new Unlock("lucid-2", PlayerProgressionTypes.MusicTrack);
            var third = new Unlock("lucid-3", PlayerProgressionTypes.MusicTrack);
            var fourth = new Unlock("lucid-4", PlayerProgressionTypes.Level);
            WithApp((app, list) =>
            {
                IFSMState state = NewState(2, PlayerProgressionTypes.MusicTrack);

                state.OnEnter(app, null);
                Assert.That(list, Is.EqualTo(new IMetaGameUnlock[] { second, third, fourth }), "below the limit only the head goes");
                Assert.That(app.Storage.GetValueOnly(AppFSMKeys.MetaGameUnlocksShown, 0), Is.EqualTo(1));

                state.OnEnter(app, null);
                Assert.That(list, Is.EqualTo(new IMetaGameUnlock[] { fourth }), "at the limit the head and the configured types go");
                Assert.That(app.Storage.GetValueOnly(AppFSMKeys.MetaGameUnlocksShown, 0), Is.EqualTo(2));

                state.OnEnter(app, null);
                Assert.That(list, Is.Empty);
                Assert.That(app.Storage.GetValueOnly(AppFSMKeys.MetaGameUnlocksShown, 0), Is.EqualTo(3));

                state.OnEnter(app, null);
                Assert.That(app.Storage.GetValueOnly(AppFSMKeys.MetaGameUnlocksShown, 0), Is.EqualTo(3), "no unlock, no count");
            }, first, second, third, fourth);
        }
    }
}
