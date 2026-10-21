using System;
using System.Collections.Generic;
using System.Linq;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The proximity and proximity-time score evaluators (MissionScorerProximityEvaluator.Evaluate 0x5C0A70,
    // MissionScorerProximityTimeEvaluator.Evaluate 0x5C0D60) and the scorer prefab instance that drives an objective
    // with them (0x5D6A90, 0x5D6AC0). Expected scores are read off the native comparisons, never recomputed.
    public sealed class MissionScorerEvaluatorTests
    {
        private static MissionScorerProximityEvaluator.TargetDistanceDefinition Band(float distance, float score, ActorAnimationDefinition animation = null)
        {
            var band = new MissionScorerProximityEvaluator.TargetDistanceDefinition();
            Set(band, "m_distance", distance); // Reflection: authored serialized fields of the band
            Set(band, "m_score", score);
            Set(band, "m_animationDefinition", animation);
            return band;
        }

        private static T NewEvaluator<T>(Owned owned, params MissionScorerProximityEvaluator.TargetDistanceDefinition[] bands)
            where T : MissionScorerProximityEvaluator
        {
            var evaluator = owned.Host("Lucid proximity evaluator", false).AddComponent<T>();
            Set(evaluator, "m_targetDistanceDefinitions", bands.ToList()); // Reflection: authored band list
            return evaluator;
        }

        private static Transform NewOther(Owned owned, Vector3 position)
        {
            Transform other = owned.Host("Lucid proximity other", false).transform;
            other.position = position;
            return other;
        }

        // The exact floats here are local distances along z of an unrotated, unscaled evaluator at the origin.
        [Test]
        public void TheLastBandWhoseDistanceIsStrictlyExceededScores()
        {
            Owned.Run(owned =>
            {
                MissionScorerProximityEvaluator evaluator = NewEvaluator<MissionScorerProximityEvaluator>(owned,
                    Band(0f, 10f), Band(2f, 20f), Band(5f, 30f));
                // 0x5C0C19 ucomiss then jbe: a band is taken only when the distance is greater than its own, so a
                // distance equal to a band's keeps the band before it; the first band is the fallback at any distance.
                foreach ((float distance, float expected) in new[] { (0f, 10f), (1f, 10f), (2f, 10f), (3f, 20f), (5f, 20f), (7f, 30f) })
                {
                    float score = evaluator.Evaluate(null, NewOther(owned, new Vector3(0f, 0f, distance)), out ActorAnimationDefinition animation);
                    Assert.That(score, Is.EqualTo(expected), "distance " + distance);
                    Assert.That(animation, Is.Null);
                }
            });
        }

        // 0x5C0C19..0x5C0C28: the search stops at the first band whose distance is not exceeded, so a later band
        // with a smaller distance is never reached.
        [Test]
        public void TheSearchStopsAtTheFirstBandThatIsNotExceededEvenWhenALaterBandIsSmaller()
        {
            Owned.Run(owned =>
            {
                MissionScorerProximityEvaluator evaluator = NewEvaluator<MissionScorerProximityEvaluator>(owned,
                    Band(0f, 10f), Band(5f, 20f), Band(2f, 30f));
                Assert.That(evaluator.Evaluate(null, NewOther(owned, new Vector3(0f, 0f, 3f)), out _), Is.EqualTo(10f));
            });
        }

        [Test]
        public void TheSelectedBandSuppliesTheAnimationAlongWithTheScore()
        {
            Owned.Run(owned =>
            {
                var first = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                var second = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                MissionScorerProximityEvaluator evaluator = NewEvaluator<MissionScorerProximityEvaluator>(owned,
                    Band(0f, 10f, first), Band(2f, 20f, second));
                evaluator.Evaluate(null, NewOther(owned, new Vector3(0f, 0f, 1f)), out ActorAnimationDefinition near);
                evaluator.Evaluate(null, NewOther(owned, new Vector3(0f, 0f, 3f)), out ActorAnimationDefinition far);
                Assert.That(near, Is.SameAs(first));
                Assert.That(far, Is.SameAs(second));
            });
        }

        // 0x5C0B12..0x5C0C6F: each unchecked axis is zeroed in the local offset before its length is measured.
        [Test]
        public void OnlyTheEnabledLocalAxesContributeToTheDistance()
        {
            Owned.Run(owned =>
            {
                MissionScorerProximityEvaluator evaluator = NewEvaluator<MissionScorerProximityEvaluator>(owned,
                    Band(0f, 10f), Band(1.5f, 20f), Band(3f, 30f), Band(4.5f, 40f), Band(9f, 50f));
                Transform other = NewOther(owned, new Vector3(1f, 4f, 2f));
                // Local offsets: x 1, y 4, z 2; the lengths 1, 4, 2, ~4.12, ~2.24, ~4.47, ~4.58 and 0.
                var expected = new (bool x, bool y, bool z, float score)[]
                {
                    (true, false, false, 10f), (false, true, false, 30f), (false, false, true, 20f),
                    (true, true, false, 30f), (true, false, true, 20f), (false, true, true, 30f),
                    (true, true, true, 40f), (false, false, false, 10f),
                };
                foreach ((bool x, bool y, bool z, float score) in expected)
                {
                    Set(evaluator, "m_useXAxis", x); // Reflection: authored axis toggles
                    Set(evaluator, "m_useYAxis", y);
                    Set(evaluator, "m_useZAxis", z);
                    Assert.That(evaluator.Evaluate(null, other, out _), Is.EqualTo(score), $"axes x={x} y={y} z={z}");
                }
            });
        }

        [Test]
        public void AllThreeAxesAreEnabledByDefault()
        {
            Owned.Run(owned =>
            {
                var evaluator = owned.Host("Lucid proximity defaults", false).AddComponent<MissionScorerProximityEvaluator>();
                var timed = owned.Host("Lucid proximity time defaults", false).AddComponent<MissionScorerProximityTimeEvaluator>();
                foreach (MissionScorerProximityEvaluator each in new[] { evaluator, timed })
                    Assert.That(new[] { Get<bool>(each, "m_useXAxis"), Get<bool>(each, "m_useYAxis"), Get<bool>(each, "m_useZAxis") },
                        Is.EqualTo(new[] { true, true, true }), each.GetType().Name);
            });
        }

        // 0x5C0ABF..0x5C0AFE: a live override replaces the component's own transform, and the other point is taken
        // into its local space, so rotation and position of the origin both matter.
        [Test]
        public void TheOverrideTransformIsTheOriginAndItsRotationSetsTheLocalAxes()
        {
            Owned.Run(owned =>
            {
                MissionScorerProximityEvaluator evaluator = NewEvaluator<MissionScorerProximityEvaluator>(owned,
                    Band(0f, 10f), Band(0.5f, 20f));
                evaluator.transform.position = new Vector3(100f, 0f, 0f);
                Transform other = NewOther(owned, new Vector3(0f, 0f, 3f));
                Assert.That(evaluator.Evaluate(null, other, out _), Is.EqualTo(20f), "the component's own transform is far away");

                // The override sits at the other point's position turned a quarter about y, so the offset is zero.
                Transform origin = owned.Host("Lucid proximity origin", false).transform;
                origin.position = new Vector3(0f, 0f, 3f);
                origin.rotation = Quaternion.Euler(0f, 90f, 0f);
                Set(evaluator, "m_optionalTransformOverride", origin); // Reflection: authored override
                Assert.That(evaluator.Evaluate(null, other, out _), Is.EqualTo(10f), "measured from the override");

                // From the same rotated frame a point 3 along world z lies on the local x axis, and none on local z.
                origin.position = Vector3.zero;
                Set(evaluator, "m_useXAxis", true);
                Set(evaluator, "m_useYAxis", false);
                Set(evaluator, "m_useZAxis", false);
                Assert.That(evaluator.Evaluate(null, other, out _), Is.EqualTo(20f), "local x carries the world z offset");
                Set(evaluator, "m_useXAxis", false);
                Set(evaluator, "m_useZAxis", true);
                Assert.That(evaluator.Evaluate(null, other, out _), Is.EqualTo(10f), "local z carries none of it");
            });
        }

        // 0x5C0B96..0x5C0BAA read the count and the first band; the fallback is band zero for a single entry and for
        // a null entry met in the search.
        [Test]
        public void ASingleBandAndANullEntryBothLeaveTheFirstBandSelected()
        {
            Owned.Run(owned =>
            {
                Transform other = NewOther(owned, new Vector3(0f, 0f, 8f));
                Assert.That(NewEvaluator<MissionScorerProximityEvaluator>(owned, Band(5f, 7f)).Evaluate(null, other, out _), Is.EqualTo(7f));

                var withNull = NewEvaluator<MissionScorerProximityEvaluator>(owned, Band(0f, 1f), null, Band(2f, 3f));
                Assert.That(withNull.Evaluate(null, other, out _), Is.EqualTo(1f), "the null entry ends the search before the third band");
            });
        }

        [Test]
        public void AnEmptyBandListFaultsReadingTheFirstBand()
        {
            Owned.Run(owned =>
            {
                MissionScorerProximityEvaluator evaluator = NewEvaluator<MissionScorerProximityEvaluator>(owned);
                Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.Evaluate(null, NewOther(owned, Vector3.zero), out _));
            });
        }

        [Test]
        public void TargetDistanceDefinitionExposesItsThreeAuthoredFields()
        {
            Owned.Run(owned =>
            {
                var animation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                MissionScorerProximityEvaluator.TargetDistanceDefinition band = Band(1.25f, -3.5f, animation);
                Assert.That(band.Distance, Is.EqualTo(1.25f));
                Assert.That(band.Score, Is.EqualTo(-3.5f));
                Assert.That(band.AnimationDefinition, Is.SameAs(animation));
            });
        }

        // 0x5C0D7F..0x5C0D94: the objective's remaining seconds are rounded up (roundss mode 0xA) before the curve
        // lookup, and the curve value multiplies the proximity score.
        [Test]
        public void TheCurveIsReadAtTheRoundedUpSecondsAndMultipliesTheProximityScore()
        {
            WithObjective((owned, objective) =>
            {
                MissionScorerProximityTimeEvaluator evaluator = NewEvaluator<MissionScorerProximityTimeEvaluator>(owned,
                    Band(0f, 5f), Band(2f, 6f));
                Set(evaluator, "m_timeRemainingMultiplierCurve", AnimationCurve.Linear(0f, 0f, 10f, 100f)); // Reflection: authored curve
                Transform other = NewOther(owned, new Vector3(0f, 0f, 1f)); // the first band, score 5
                // 2.3 s lies between 2 and 3: a rounded-up lookup reads 30, a floor or a round would read 20.
                foreach ((float seconds, float expected) in new[] { (2.3f, 150f), (2.0f, 100f), (2.7f, 150f), (0.2f, 50f) })
                {
                    StartTimer(objective, seconds);
                    Assert.That(evaluator.Evaluate(objective, other, out _), Is.EqualTo(expected).Within(1e-3f), "remaining " + seconds);
                }
            });
        }

        [Test]
        public void TheTimeEvaluatorReturnsTheBandAnimationAndMultipliesTheSelectedBandScore()
        {
            WithObjective((owned, objective) =>
            {
                var animation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                MissionScorerProximityTimeEvaluator evaluator = NewEvaluator<MissionScorerProximityTimeEvaluator>(owned,
                    Band(0f, 5f), Band(2f, 6f, animation));
                Set(evaluator, "m_timeRemainingMultiplierCurve", AnimationCurve.Constant(0f, 10f, 2f)); // Reflection: authored curve
                StartTimer(objective, 4f);
                float score = evaluator.Evaluate(objective, NewOther(owned, new Vector3(0f, 0f, 3f)), out ActorAnimationDefinition result);
                Assert.That(score, Is.EqualTo(12f).Within(1e-4f));
                Assert.That(result, Is.SameAs(animation));
            });
        }

        // 0x5D6A90: the instance evaluates its objective against the other transform, then completes it.
        [Test]
        public void EnteringTheProximityTargetScoresTheObjectiveAndCompletesIt()
        {
            WithObjective((owned, objective) =>
            {
                MissionScorerProximityEvaluator evaluator = NewEvaluator<MissionScorerProximityEvaluator>(owned,
                    Band(0f, 10f), Band(2f, 20f));
                objective.ScorerEvaluator = evaluator;
                MissionScorerInstance instance = owned.Host("Lucid scorer instance", false).AddComponent<MissionScorerInstance>();
                instance.Objective = objective;
                CountingEvent completed = Author(objective, "m_onComplete");

                instance.Action_OnProximityTargetEnter(NewOther(owned, new Vector3(0f, 0f, 3f)));

                Assert.That(objective.ScoreToAdd, Is.EqualTo(20f));
                Assert.That(objective.IsComplete, Is.True);
                Assert.That(completed.Count, Is.EqualTo(1));
            });
        }

        // 0x5D6AC0: the authored speed-check score replaces evaluation; a second entry inside the objective's
        // cooldown keeps the first score (SetScoreToAdd 0x5C0880) yet still completes the objective again.
        [Test]
        public void EnteringTheSpeedCheckStoresTheScoreAndCompletesTheObjectiveEvenDuringItsCooldown()
        {
            WithObjective((owned, objective) =>
            {
                MissionScorerInstance instance = owned.Host("Lucid scorer instance", false).AddComponent<MissionScorerInstance>();
                instance.Objective = objective;
                CountingEvent completed = Author(objective, "m_onComplete");

                instance.Action_OnSpeedCheckEnter(42.5f);
                Assert.That(objective.ScoreToAdd, Is.EqualTo(42.5f));
                Assert.That(objective.IsComplete, Is.True);

                instance.Action_OnSpeedCheckEnter(7f);
                Assert.That(objective.ScoreToAdd, Is.EqualTo(42.5f), "the cooldown ignores the second score");
                Assert.That(completed.Count, Is.EqualTo(2));
            });
        }

        [Test]
        public void TheInstanceExposesItsEvaluatorAndTheObjectiveItIsGiven()
        {
            WithObjective((owned, objective) =>
            {
                MissionScorerInstance instance = owned.Host("Lucid scorer instance", false).AddComponent<MissionScorerInstance>();
                MissionScorerProximityEvaluator evaluator = NewEvaluator<MissionScorerProximityEvaluator>(owned, Band(0f, 1f));
                Assert.That(instance.Evaluator, Is.Null);
                Assert.That(instance.Objective, Is.Null);
                Set(instance, "m_evaluator", evaluator); // Reflection: serialized prefab reference
                instance.Objective = objective;
                Assert.That(instance.Evaluator, Is.SameAs(evaluator));
                Assert.That(instance.Objective, Is.SameAs(objective));
            });
        }

        private static void WithObjective(Action<Owned, MissionScorerObjective> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<CharacterManager>();
            GameObject host = owned.Host("Lucid scorer objective", false);
            MissionScorerObjective objective = EnemyAbilityTestSupport.AddTimeScaled<MissionScorerObjective>(host);
            foreach (string name in new[] { "m_onActivated", "m_onPreviouslyCompleted", "m_onReset", "m_onComplete" })
                Author(objective, name); // Reflection: authored events, which the constructor leaves null
            body(owned, objective);
        });

        // The objective's own activation path (TryStartTimer) leaves exactly the authored limit as the remaining time;
        // a reset first lets a second activation start the timer again.
        private static void StartTimer(MissionObjective objective, float seconds)
        {
            objective.OnObjectiveReset();
            Set(objective, "m_timeLimitSeconds", seconds); // Reflection: authored serialized limit
            objective.OnObjectiveActivated();
        }
    }
}
