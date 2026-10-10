using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    public sealed class OriginalMissionScorerComboStagePreservationTests
    {
        private const string FirstValuesJson = "{\"m_drainPerSecond\":-0.625,\"m_maxCombo\":1.125,\"m_comboTimeLimit\":-3.5,\"m_comboMultiplier\":7.25}";
        private const string SecondValuesJson = "{\"m_comboMultiplier\":-2.25,\"m_comboTimeLimit\":9.5,\"m_drainPerSecond\":4.75,\"m_maxCombo\":0.5}";
        private const string UpdatedFirstValuesJson = "{\"m_maxCombo\":32,\"m_drainPerSecond\":0.125,\"m_comboMultiplier\":0,\"m_comboTimeLimit\":0.25}";

        [Test]
        public void OrdinaryConstructorPreservesAllFourDefaults()
        {
            ProjectLucid.Editor.MissionScorerComboStageCurrentFacts.RequireWholeCurrentEmission();
            var stage = new MissionScorerComboStage();
            Assert.That(ReadInOriginalGetterOrder(stage), Is.EqualTo(new[] { 5f, 1f, 2f, 0f }));
        }

        [Test]
        public void GenuineUnitySerializationPopulatesPrivateFieldsAndRoundTripsTheirOrder()
        {
            ProjectLucid.Editor.MissionScorerComboStageCurrentFacts.RequireWholeCurrentEmission();
            var stage = new MissionScorerComboStage();
            JsonUtility.FromJsonOverwrite(FirstValuesJson, stage);
            Assert.That(ReadInOriginalGetterOrder(stage), Is.EqualTo(new[] { -3.5f, 7.25f, 1.125f, -0.625f }));

            var restored = JsonUtility.FromJson<MissionScorerComboStage>(JsonUtility.ToJson(stage));
            Assert.That(restored, Is.Not.SameAs(stage));
            Assert.That(ReadInOriginalGetterOrder(restored), Is.EqualTo(new[] { -3.5f, 7.25f, 1.125f, -0.625f }));
        }

        [Test]
        public void IndependentlyConstructedInstancesKeepTheirOwnSerializedState()
        {
            ProjectLucid.Editor.MissionScorerComboStageCurrentFacts.RequireWholeCurrentEmission();
            var first = new MissionScorerComboStage();
            var second = new MissionScorerComboStage();
            JsonUtility.FromJsonOverwrite(FirstValuesJson, first);
            JsonUtility.FromJsonOverwrite(SecondValuesJson, second);
            Assert.That(ReadInOriginalGetterOrder(first), Is.EqualTo(new[] { -3.5f, 7.25f, 1.125f, -0.625f }));
            Assert.That(ReadInOriginalGetterOrder(second), Is.EqualTo(new[] { 9.5f, -2.25f, 0.5f, 4.75f }));

            JsonUtility.FromJsonOverwrite(UpdatedFirstValuesJson, first);
            Assert.That(ReadInOriginalGetterOrder(first), Is.EqualTo(new[] { 0.25f, 0f, 32f, 0.125f }));
            Assert.That(ReadInOriginalGetterOrder(second), Is.EqualTo(new[] { 9.5f, -2.25f, 0.5f, 4.75f }));
        }

        private static float[] ReadInOriginalGetterOrder(MissionScorerComboStage stage)
        {
            return new[] { stage.ComboTimeLimit, stage.ComboMultiplier, stage.MaxCombo, stage.DrainPerSecond };
        }
    }
}
