using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SequencedCharacterAnimation (TypeDef 0x02000613, x86-64 0x568630..0x568760) and its
    // private TriggerType enum (0x02000614). The CharacterManager is real and registered before the effect is built,
    // the character is a real component with a real Animator controller (CharacterTriggerTestSupport), and the effect
    // runs through its public PlayEffect.
    public sealed class SequencedCharacterAnimationTests
    {
        private const string Parameter = "LucidSequencedParameter";
        private static readonly string[] Triggers = { Parameter };

        private static SequencedCharacterAnimation NewEffect(Owned owned, int triggerType, string parameter = Parameter)
        {
            var effect = owned.Host("Lucid owned sequenced character animation", false).AddComponent<SequencedCharacterAnimation>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("m_animationParameter.m_parameterName").stringValue = parameter;
            serialized.FindProperty("m_animationTriggerType").intValue = triggerType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        private static void Play(SequencedCharacterAnimation effect, Action onComplete)
        {
            IEffectData data = new TargetedEffect();
            effect.PlayEffect(in data, onComplete);
        }

        // Enum members 0x02000614: BoolTrue 0, BoolFalse 1, Trigger 2.
        [Test]
        public void TheTriggerTypeEnumKeepsItsOriginalMembers()
        {
            Type type = typeof(SequencedCharacterAnimation).GetNestedType("TriggerType", BindingFlags.NonPublic);
            Assert.That(Enum.GetNames(type), Is.EqualTo(new[] { "BoolTrue", "BoolFalse", "Trigger" }));
            Assert.That(Enum.GetValues(type).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        // The constructor (0x568760) stores only the manager reference and Effects; the trigger type stays BoolTrue.
        [Test]
        public void ConstructionLeavesTheTriggerTypeAtBoolTrue()
        {
            WithManager((owned, manager, character) =>
            {
                var effect = owned.Host("Lucid owned default character animation", false).AddComponent<SequencedCharacterAnimation>();
                Assert.That(new SerializedObject(effect).FindProperty("m_animationTriggerType").intValue, Is.EqualTo(0));
                Assert.That((int)Get<TimeCategory>(effect, "m_timeCategory"), Is.EqualTo(-2001650353));
                Assert.That(Get<SystemRef<CharacterManager>>(effect, "m_characterManagerRef").GetSafe(), Is.SameAs(manager));
            });
        }

        // 0x5686b0..0x5686f2 (type 0): SetAnimation(hash, true) on the current character, then the stored callback
        // (0x5686f7..0x568708).
        [Test]
        public void BoolTrueSetsTheParameterAndCompletes()
        {
            WithManager((owned, manager, character) =>
            {
                SetCurrent(manager, character, true);
                Animator animator = AttachAnimator(owned, character, new string[0], Triggers);
                SequencedCharacterAnimation effect = NewEffect(owned, 0);
                int completed = 0;
                Play(effect, () => completed++);
                Assert.That(animator.GetBool(Parameter), Is.True);
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // 0x5686e3..0x5686f0 (type 1): the same call with edx=0.
        [Test]
        public void BoolFalseClearsTheParameterAndCompletes()
        {
            WithManager((owned, manager, character) =>
            {
                SetCurrent(manager, character, true);
                Animator animator = AttachAnimator(owned, character, new string[0], Triggers);
                animator.SetBool(Parameter, true);
                SequencedCharacterAnimation effect = NewEffect(owned, 1);
                int completed = 0;
                Play(effect, () => completed++);
                Assert.That(animator.GetBool(Parameter), Is.False);
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // 0x5686c6..0x5686d3 (type 2): SetAnimationTrigger(hash).
        [Test]
        public void TriggerFiresTheTriggerParameterAndCompletes()
        {
            WithManager((owned, manager, character) =>
            {
                SetCurrent(manager, character, true);
                Animator animator = AttachAnimator(owned, character, Triggers, new string[0]);
                SequencedCharacterAnimation effect = NewEffect(owned, 2);
                int completed = 0;
                Play(effect, () => completed++);
                Assert.That(animator.GetBool(Parameter), Is.True, "a fired trigger reads as set");
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // 0x568678 and 0x5686a1: an unresolvable hash (empty name) or no valid current character ends the call
        // (0x56870b) before any animation is set and without the stored callback.
        [Test]
        public void ANameLessParameterOrNoCurrentCharacterDoesNothingAndNeverCompletes()
        {
            WithManager((owned, manager, character) =>
            {
                SetCurrent(manager, character, true);
                Animator animator = AttachAnimator(owned, character, new string[0], Triggers);
                int completed = 0;
                Play(NewEffect(owned, 0, parameter: ""), () => completed++);
                Assert.That(animator.GetBool(Parameter), Is.False);
                Assert.That(completed, Is.EqualTo(0), "unnamed parameter");

                SetCurrent(manager, character, false);
                Play(NewEffect(owned, 0), () => completed++);
                Assert.That(animator.GetBool(Parameter), Is.False);
                Assert.That(completed, Is.EqualTo(0), "invalid current character");
            });
        }

        // 0x568712: any value outside 0..2 throws ArgumentOutOfRangeException (default constructor) after the
        // lookups and before anything is set or completed.
        [Test]
        public void AnUnknownTriggerTypeThrowsWithoutSettingOrCompleting()
        {
            WithManager((owned, manager, character) =>
            {
                SetCurrent(manager, character, true);
                Animator animator = AttachAnimator(owned, character, new string[0], Triggers);
                SequencedCharacterAnimation effect = NewEffect(owned, 3);
                int completed = 0;
                Assert.Throws<ArgumentOutOfRangeException>(() => Play(effect, () => completed++));
                Assert.That(animator.GetBool(Parameter), Is.False);
                Assert.That(completed, Is.EqualTo(0));
            });
        }
    }
}
