using System;
using System.Reflection;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Declarations of the restored Game.Runtime subtitle and time machine timeline types against the original DummyDll metadata:
    // type and member flags, member order, field attribute order and Il2Cpp options.
    public sealed class TimelineTrackAndClipDeclarationTests
    {
        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }


        // SubtitleClip 0x0200044a.
        [Test]
        public void SubtitleClipKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SubtitleClip), 0x00100001, typeof(UnityEngine.Playables.PlayableAsset), new[] { 1, 2 },
                new[]
                {
                    ("m_subtitleText", 0x0001), ("m_subtitleTextEditor", 0x0001),
                    ("m_backgroundColour", 0x0001), ("m_dataManagerRef", 0x0021)
                },
                new[]
                {
                    ("CreatePlayable", 0x00c6), (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SubtitleClip), "m_subtitleText")), Is.EqualTo(new[] { typeof(Hardlight.Utils.HashEnumAttribute), typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SubtitleClip), "m_subtitleTextEditor")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField), typeof(UnityEngine.TooltipAttribute) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SubtitleClip), "m_backgroundColour")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
        }

        // SubtitleMixer 0x0200044b.
        [Test]
        public void SubtitleMixerKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SubtitleMixer), 0x00100001, typeof(UnityEngine.Playables.PlayableBehaviour), new[] { 2, 1 },
                new[]
                {
                    ("m_currentText", 0x0001), ("m_currentAlpha", 0x0001),
                    ("m_backgroundColour", 0x0001), ("m_inputCount", 0x0001),
                    ("m_backingPanel", 0x0001), ("m_anyTextShowing", 0x0001),
                    ("m_subtitlesEnabled", 0x0001)
                },
                new[]
                {
                    ("OnGraphStart", 0x00c6), ("ProcessFrame", 0x00c6),
                    (".ctor", 0x1886)
                });
        }

        // SubtitleTrack 0x0200044c; its Track attributes are checked by the time machine track test.
        [Test]
        public void SubtitleTrackKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SubtitleTrack), 0x00100001, typeof(UnityEngine.Timeline.TrackAsset), new[] { 2, 1 },
                new (string, int)[0],
                new[]
                {
                    ("CreateTrackMixer", 0x00c6), (".ctor", 0x1886)
                });
        }

        // TimeMachineBehaviour 0x02000805 with its nested enums 0x02000806 and 0x02000807.
        [Test]
        public void TimeMachineBehaviourKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.TimeMachineBehaviour), 0x00102001, typeof(UnityEngine.Playables.PlayableBehaviour), new[] { 1, 2 },
                new[]
                {
                    ("m_action", 0x0004), ("m_markerLabel", 0x0004),
                    ("m_condition", 0x0004), ("m_conditionInput", 0x0004),
                    ("m_markerToJumpTo", 0x0004), ("m_timeToJumpTo", 0x0004),
                    ("m_endSoftenTime", 0x0004), ("ClipExecuted", 0x0006),
                    ("m_inputPressOccurred", 0x0004), ("m_inputHoldOccurred", 0x0004)
                },
                new[]
                {
                    ("get_Action", 0x0886), ("get_MarkerLabel", 0x0886),
                    ("get_Condition", 0x0886), ("get_ConditionInput", 0x0886),
                    ("get_MarkerToJumpTo", 0x0886), ("get_TimeToJumpTo", 0x0886),
                    ("get_EndSoftenTime", 0x0886), ("OnGraphStart", 0x00c6),
                    ("InputPressed", 0x0081), ("ConditionMet", 0x0086),
                    (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineBehaviour), "m_action")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineBehaviour), "m_markerLabel")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineBehaviour), "m_condition")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineBehaviour), "m_conditionInput")), Is.EqualTo(new[] { typeof(Hardlight.HideIfAttribute), typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineBehaviour), "m_markerToJumpTo")), Is.EqualTo(new[] { typeof(Hardlight.ShowIfAttribute), typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineBehaviour), "m_timeToJumpTo")), Is.EqualTo(new[] { typeof(Hardlight.ShowIfAttribute), typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineBehaviour), "m_endSoftenTime")), Is.EqualTo(new[] { typeof(Hardlight.ShowIfAttribute), typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineBehaviour), "ClipExecuted")), Is.EqualTo(new[] { typeof(UnityEngine.HideInInspector) }));
            AssertType(typeof(HardlightProject.TimeMachineBehaviour).GetNestedType("TimeMachineCondition", BindingFlags.Public), 0x00000102, typeof(System.Enum), new int[0],
                new[]
                {
                    ("value__", 0x0606), ("Always", 0x8056),
                    ("Input", 0x8056), ("InputHeld", 0x8056)
                },
                new (string, int)[0]);
            AssertType(typeof(HardlightProject.TimeMachineBehaviour).GetNestedType("TimeMachineAction", BindingFlags.Public), 0x00000102, typeof(System.Enum), new int[0],
                new[]
                {
                    ("value__", 0x0606), ("Marker", 0x8056),
                    ("JumpToTime", 0x8056), ("JumpToMarker", 0x8056),
                    ("JumpToEnd", 0x8056), ("Pause", 0x8056),
                    ("DoNothing", 0x8056)
                },
                new (string, int)[0]);
        }

        // TimeMachineClip 0x02000808.
        [Test]
        public void TimeMachineClipKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.TimeMachineClip), 0x00102001, typeof(UnityEngine.Playables.PlayableAsset), new[] { 1, 2 },
                new[]
                {
                    ("m_timeMachinePlayableAsset", 0x0001)
                },
                new[]
                {
                    ("get_Behaviour", 0x0886), ("get_clipCaps", 0x09e6),
                    ("CreatePlayable", 0x00c6), (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineClip), "m_timeMachinePlayableAsset")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
        }

        // TimeMachineMixerBehaviour 0x02000809.
        [Test]
        public void TimeMachineMixerBehaviourKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.TimeMachineMixerBehaviour), 0x00100001, typeof(UnityEngine.Playables.PlayableBehaviour), new[] { 1, 2 },
                new[]
                {
                    ("m_markerClips", 0x0001)
                },
                new[]
                {
                    ("get_MarkerClips", 0x0886), ("set_MarkerClips", 0x0886),
                    ("ProcessFrame", 0x00c6), (".ctor", 0x1886)
                });
        }

        // TimeMachineSkipBehaviour 0x0200080a.
        [Test]
        public void TimeMachineSkipBehaviourKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.TimeMachineSkipBehaviour), 0x00102001, typeof(HardlightProject.TimeMachineBehaviour), new[] { 2, 1 },
                new[]
                {
                    ("SkipEnabledState", 0x0006), ("m_cutsceneManager", 0x0001)
                },
                new[]
                {
                    (".ctor", 0x1886), (".ctor", 0x1886),
                    ("OnBehaviourPlay", 0x00c6), ("ProcessFrame", 0x00c6)
                });
        }

        // TimeMachineSkipClip 0x0200080b.
        [Test]
        public void TimeMachineSkipClipKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.TimeMachineSkipClip), 0x00102001, typeof(HardlightProject.TimeMachineClip), new[] { 1, 2 },
                new[]
                {
                    ("m_skipEnabledState", 0x0001)
                },
                new[]
                {
                    ("CreatePlayable", 0x00c6), (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.TimeMachineSkipClip), "m_skipEnabledState")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
        }

        // TimeMachineTrack 0x0200080c.
        [Test]
        public void TimeMachineTrackKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.TimeMachineTrack), 0x00100001, typeof(UnityEngine.Timeline.TrackAsset), new[] { 2, 1 },
                new (string, int)[0],
                new[]
                {
                    ("CreateTrackMixer", 0x00c6), (".ctor", 0x1886)
                });
        }

    }
}
