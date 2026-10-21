using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerBossHealth (Game.Runtime 0200087b, bodies 0x61A4F0..0x61B1E0) against a real BossManager in
    // an owned registry row. Health reaches the manager only through Register(Boss), which needs the App that
    // BossManager.Awake takes, so its private pool fields are set directly (once, in WithRig).
    //
    // The frame time is not controllable in Edit Mode. The lerp iterators are therefore driven with settings
    // that make their result independent of it: LerpUIDown starts from a saturated counter, and LerpUIUp runs
    // with a zero lerp time, where the ratio is infinite for any positive frame time and NaN for none, and
    // both end the inner loop after one step.
    public sealed class UIContainerBossHealthTests
    {
        private sealed class Rig
        {
            public BossManager Manager;
            public UIContainerBossHealth Container;
            public GameObject[] Pips;
            public Image[] Fills;
        }

        // The manager has a health pool of three with two left, and the encounter name MISSING.
        private static void WithRig(int pipCount, Action<Owned, Rig> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<SaveManager>();
            var rig = new Rig { Manager = owned.Register(owned.Host("Lucid boss health manager", false).AddComponent<BossManager>()) };
            Set(rig.Manager, "m_sharedHealthTotal", 3);
            Set(rig.Manager, "m_sharedHealthCurrent", 2);
            Set(rig.Manager, "m_bossEncounterName", Strings.MISSING);

            rig.Container = owned.Host("Lucid boss health container", false).AddComponent<UIContainerBossHealth>();
            rig.Pips = new GameObject[pipCount];
            rig.Fills = new Image[pipCount];
            for (int i = 0; i < pipCount; i++)
            {
                rig.Pips[i] = owned.Host("Lucid boss health pip " + i, true);
                GameObject fill = owned.Host("Lucid boss health fill " + i, false);
                rig.Fills[i] = fill.AddComponent<Image>();
                rig.Fills[i].fillAmount = 0f;
            }
            Set(rig.Container, "m_healthPoolObjects", rig.Pips);
            Set(rig.Container, "m_healthFills", rig.Fills);
            body(owned, rig);
        });

        // BossManagerActive is true while the manager lists a boss; the case's boss is only listed.
        private static void ListABoss(Owned owned, Rig rig) =>
            rig.Manager.Bosses.Add(owned.Host("Lucid boss health boss", false).AddComponent<Boss>());

        private static bool[] Active(GameObject[] objects) => Array.ConvertAll(objects, value => value.activeSelf);

        private static float[] Fill(Image[] images) => Array.ConvertAll(images, image => image.fillAmount);

        private static bool Visited(Image image) => !(image.fillAmount == 0f);

        // Constructor 0x61AD70: hidden bindables, half a second of lerp, no health yet (-1).
        [Test]
        public void ConstructionStartsHiddenWithoutAHealthTotal()
        {
            WithRig(0, (owned, rig) =>
            {
                Assert.That(rig.Container.Visible.Value, Is.False);
                Assert.That((int)rig.Container.BossName.Value, Is.Zero);
                Assert.That(Get<float>(rig.Container, "m_lerpTimeInSeconds"), Is.EqualTo(0.5f));
                Assert.That(Get<bool>(rig.Container, "m_useUnscaledTime"), Is.False);
                Assert.That(Get<int>(rig.Container, "m_totalHealth"), Is.EqualTo(-1));
                Assert.That(Get<WaitForEndOfFrame>(rig.Container, "m_waitForEndOfFrame"), Is.Not.Null);
            });
        }

        // Setup 0x61A4F0 sets Visible to false (0x61A569), takes the BossManager reference (0x61A598) and
        // InvokeOnValid runs OnBossManagerValid at once for a registered manager, which sets Visible to
        // true (0x61A84A). Both bindable writes notify, so a listener sees false then true.
        [Test]
        public void SetupHidesThenShowsOnceTheManagerIsValid()
        {
            WithRig(2, (owned, rig) =>
            {
                var seen = new List<bool>();
                rig.Container.Visible.AddListener(() => seen.Add(rig.Container.Visible.Value));

                rig.Container.Setup(null);
                Assert.That(seen, Is.EqualTo(new[] { false, true }));
                Assert.That(Get<BossManager>(rig.Container, "m_bossManager"), Is.SameAs(rig.Manager));
                Assert.That(rig.Manager.OnBossHealthPoolChange.GetInvocationList().Length, Is.EqualTo(1));
                Assert.That(rig.Manager.OnBossHealthPoolChange.Target, Is.SameAs(rig.Container));
                Assert.That(rig.Manager.OnBossHealthPoolChange.Method.Name, Is.EqualTo("OnHealthChange"));
                Assert.That(rig.Manager.OnBossHealthTotalUpdate.GetInvocationList().Length, Is.EqualTo(1));
                Assert.That(rig.Manager.OnBossHealthTotalUpdate.Target, Is.SameAs(rig.Container));
                Assert.That(rig.Manager.OnBossHealthTotalUpdate.Method.Name, Is.EqualTo("OnBossHealthTotalUpdate"));
            });
        }

        // OnBossManagerValid 0x61A600 activates the container by BossManagerActive (0x61A673), copies the
        // total and current health and the boss name (0x61A691..0x61A6A6), then shows pip i while i < total
        // (0x61A6EB) and fills it while i < current (0x61A705).
        [Test]
        public void AValidManagerSizesAndFillsThePips()
        {
            WithRig(5, (owned, rig) =>
            {
                ListABoss(owned, rig);
                Call(rig.Container, "OnBossManagerValid", rig.Manager);

                Assert.That(rig.Container.gameObject.activeSelf, Is.True);
                Assert.That(Get<int>(rig.Container, "m_totalHealth"), Is.EqualTo(3));
                Assert.That(Get<int>(rig.Container, "m_currentHealthTarget"), Is.EqualTo(2));
                Assert.That(rig.Container.BossName.Value, Is.EqualTo(Strings.MISSING));
                Assert.That(Active(rig.Pips), Is.EqualTo(new[] { true, true, true, false, false }));
                Assert.That(Fill(rig.Fills), Is.EqualTo(new[] { 1f, 1f, 0f, 0f, 0f }));
                Assert.That(rig.Container.Visible.Value, Is.True);
            });
        }

        [Test]
        public void WithoutABossTheContainerDeactivatesItself()
        {
            WithRig(2, (owned, rig) =>
            {
                rig.Container.gameObject.SetActive(true);
                Call(rig.Container, "OnBossManagerValid", rig.Manager);
                Assert.That(rig.Container.gameObject.activeSelf, Is.False);
            });
        }

        // OnBossHealthTotalUpdate 0x61AAA0 stores the total, shows pip i while i < total with a full fill
        // (0x61AB02), makes the current target equal the total (0x61AB1D) and re-reads the manager's name.
        [Test]
        public void ANewTotalShowsThatManyFullPips()
        {
            WithRig(4, (owned, rig) =>
            {
                ListABoss(owned, rig);
                Call(rig.Container, "OnBossManagerValid", rig.Manager);
                Set(rig.Manager, "m_bossEncounterName", Strings.NONE);

                Call(rig.Container, "OnBossHealthTotalUpdate", 2);
                Assert.That(Get<int>(rig.Container, "m_totalHealth"), Is.EqualTo(2));
                Assert.That(Get<int>(rig.Container, "m_currentHealthTarget"), Is.EqualTo(2));
                Assert.That(Active(rig.Pips), Is.EqualTo(new[] { true, true, false, false }));
                Assert.That(Fill(rig.Fills), Is.EqualTo(new[] { 1f, 1f, 1f, 1f }));
                Assert.That(rig.Container.BossName.Value, Is.EqualTo(Strings.NONE));
            });
        }

        // OnHealthChange 0x61A860 records the new target, restarts the lerp routine and picks the rising or
        // falling lerp by comparing the old target with the new one (0x61A89F).
        [Test]
        public void AHealthChangeRecordsTheTargetAndStartsALerp()
        {
            WithRig(4, (owned, rig) =>
            {
                ListABoss(owned, rig);
                Call(rig.Container, "OnBossManagerValid", rig.Manager);
                Assert.That(Get<Coroutine>(rig.Container, "m_lerpRoutine"), Is.Null);

                rig.Manager.RestoreHealthToPool(3);
                Assert.That(Get<int>(rig.Container, "m_currentHealthTarget"), Is.EqualTo(3));
                Assert.That(Get<Coroutine>(rig.Container, "m_lerpRoutine"), Is.Not.Null, "a rise");

                rig.Manager.RestoreHealthToPool(1);
                Assert.That(Get<int>(rig.Container, "m_currentHealthTarget"), Is.EqualTo(1));
                Assert.That(Get<Coroutine>(rig.Container, "m_lerpRoutine"), Is.Not.Null, "a fall");
            });
        }

        // MoveNext 0x61B000: pip i from the old target is lerped for i < current - 1, not for the size of
        // the change (0x61B032..0x61B039), and each pass yields the shared WaitForEndOfFrame (0x61B14B).
        [Test]
        public void LerpUpFillsCurrentMinusOnePipsFromTheOldTarget()
        {
            WithRig(6, (owned, rig) =>
            {
                Set(rig.Container, "m_lerpTimeInSeconds", 0f);
                var lerp = (IEnumerator)Call(rig.Container, "LerpUIUp", 4, 2);
                var wait = Get<WaitForEndOfFrame>(rig.Container, "m_waitForEndOfFrame");

                int yields = 0;
                while (lerp.MoveNext())
                {
                    yields++;
                    Assert.That(lerp.Current, Is.SameAs(wait));
                    Assert.That(yields, Is.LessThan(10), "the lerp must end");
                }
                Assert.That(yields, Is.EqualTo(3));
                Assert.That(Array.ConvertAll(rig.Fills, Visited), Is.EqualTo(new[] { false, false, true, true, true, false }));
            });
        }

        // The pip index old + i is bounds-checked against the fills (0x61B058, with the out-of-range fault
        // at 0x61B176): old 3 and current 4 reach fill 4 on the second pip.
        [Test]
        public void LerpUpFaultsWhenThePipsRunPastTheFills()
        {
            WithRig(4, (owned, rig) =>
            {
                Set(rig.Container, "m_lerpTimeInSeconds", 0f);
                var lerp = (IEnumerator)Call(rig.Container, "LerpUIUp", 4, 3);
                Assert.That(lerp.MoveNext(), Is.True, "pip 3 is the last fill");
                Assert.Throws<IndexOutOfRangeException>(() => lerp.MoveNext());
            });
        }

        // MoveNext 0x61AE90: the fill is 1 - ratio, so a saturated counter empties the pip in one step;
        // the next step ends the iterator (0x61AEC5: the fill is not above 0).
        [Test]
        public void LerpDownEmptiesTheGivenFillAndEnds()
        {
            WithRig(2, (owned, rig) =>
            {
                Set(rig.Container, "m_lerpTimeCounter", 1e6f); // OnHealthChange zeroes it and only frames add to it
                rig.Fills[1].fillAmount = 0.7f;
                var lerp = (IEnumerator)Call(rig.Container, "LerpUIDown", rig.Fills[1]);

                Assert.That(lerp.MoveNext(), Is.True);
                Assert.That(lerp.Current, Is.SameAs(Get<WaitForEndOfFrame>(rig.Container, "m_waitForEndOfFrame")));
                Assert.That(rig.Fills[1].fillAmount, Is.Zero);
                Assert.That(lerp.MoveNext(), Is.False);
            });
        }

        // OnDestroy 0x61AB90 returns for a container that never had a manager, and otherwise removes both
        // of its subscriptions.
        [Test]
        public void DestroyingUnsubscribesFromTheManager()
        {
            WithRig(1, (owned, rig) =>
            {
                Assert.DoesNotThrow(() => Call(rig.Container, "OnDestroy"));

                Call(rig.Container, "OnBossManagerValid", rig.Manager);
                Assert.That(rig.Manager.OnBossHealthPoolChange, Is.Not.Null);
                Assert.That(rig.Manager.OnBossHealthTotalUpdate, Is.Not.Null);

                Call(rig.Container, "OnDestroy");
                Assert.That(rig.Manager.OnBossHealthPoolChange, Is.Null);
                Assert.That(rig.Manager.OnBossHealthTotalUpdate, Is.Null);
            });
        }
    }
}
