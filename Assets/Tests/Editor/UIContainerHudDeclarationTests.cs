using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Emission checks: the declarations of the gameplay HUD containers and the value setters against the DummyDll
    // metadata (TypeDef, field and method flags, in metadata order, and the Il2CppSetOption pairs in metadata order).
    // The behaviour of each member is covered by the other HUD container tests.
    public sealed class UIContainerHudDeclarationTests
    {
        private static Type Nested(Type type, string name) => type.GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public);

        [Test]
        public void OrbDetectorUiDeclarations()
        {
            AssertMembers(typeof(OrbDetectorUI), 0x00100001,
                new[]
                {
                    ("m_gameplayIsland", 0x0001), ("m_audioSource", 0x0001), ("m_levels", 0x0001), ("m_gameplayMusicAudioSource", 0x0001),
                    ("m_orbProximityMusicLowPassCutoff", 0x0001), ("m_orbCollectionLowPassRestore", 0x0001), ("NoLevel", 0x8051),
                    ("m_timeSinceAudio", 0x0001), ("m_currentLevel", 0x0001), ("m_lastWeightedProximity", 0x0001), ("m_audioManager", 0x0001),
                    ("m_musicLowPassFilter", 0x0001), ("m_noLowPassFilterAvailable", 0x0001), ("m_restoringMusicAfterOrbCollection", 0x0001),
                    ("m_timeSinceOrbCollection", 0x0001),
                },
                new[]
                {
                    ("Awake", 0x00c4), ("OnAudioManagerValid", 0x0081), ("OnEnable", 0x00c4), ("OnDisable", 0x00c4), ("SetMusicEffects", 0x0081),
                    ("InternalUpdate", 0x00c4), ("SetLevel", 0x0081), ("SetGameObjectsActive", 0x0081), ("PlayAudio", 0x0081),
                    ("IsCollectingOrb", 0x0081), ("InternalValidate", 0x0081), ("OnValidate", 0x00c4), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(OrbDetectorUI), 1, 2);
            Assert.That(typeof(OrbDetectorUI).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Assert.That(Field(typeof(OrbDetectorUI), "NoLevel").GetRawConstantValue(), Is.EqualTo(-1));

            Type level = typeof(OrbDetectorUI.OrbDetectorLevel);
            AssertMembers(level, 0x00102002,
                new[] { ("m_minProximity", 0x0001), ("m_enabledWhileActive", 0x0001), ("m_audioClip", 0x0001), ("m_audioRepeatFrequency", 0x0001) },
                new[] { ("get_MinProximity", 0x0886), ("get_EnabledWhileActive", 0x0886), ("get_AudioClip", 0x0886), ("get_AudioRepeatFrequency", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(level, 1, 2);
            var minimum = (UnityEngine.RangeAttribute)Field(level, "m_minProximity").GetCustomAttribute(typeof(UnityEngine.RangeAttribute));
            Assert.That((minimum.min, minimum.max), Is.EqualTo((0f, 1f)));
            var repeat = (UnityEngine.RangeAttribute)Field(level, "m_audioRepeatFrequency").GetCustomAttribute(typeof(UnityEngine.RangeAttribute));
            Assert.That((repeat.min, repeat.max), Is.EqualTo((0.1f, 10f)));
        }

        [Test]
        public void ValueSetterDeclarations()
        {
            AssertMembers(typeof(UIContainerValueSetter), 0x00100081,
                new[] { ("m_identifier", 0x0001) }, new[] { ("get_UIContainer", 0x0884), (".ctor", 0x1884) });
            AssertIl2CppOptions(typeof(UIContainerValueSetter), 1, 2);
            Assert.That(typeof(UIContainerValueSetter).BaseType, Is.EqualTo(typeof(MonoBehaviour)));

            AssertMembers(typeof(UIContainerValueProgressSetter), 0x00100001,
                new (string, int)[0], new[] { ("SetProgress", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerValueProgressSetter), 1, 2);
            Assert.That(typeof(UIContainerValueProgressSetter).BaseType, Is.EqualTo(typeof(UIContainerValueSetter)));

            AssertMembers(typeof(UIContainerSequenceProgressSetter), 0x00100001,
                new[] { ("m_sequencedEffect", 0x0001) }, new[] { ("Update", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerSequenceProgressSetter), 1, 2);
            Assert.That(typeof(UIContainerSequenceProgressSetter).BaseType, Is.EqualTo(typeof(UIContainerValueSetter)));

            AssertMembers(typeof(UIContainerConditionalTriggerSetter), 0x00100001,
                new[] { ("m_conditionalTrigger", 0x0001), ("m_progressCurrent", 0x0001), ("m_progressTotal", 0x0001) },
                new[] { ("OnEnable", 0x0081), ("Update", 0x0081), ("OnDisable", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerConditionalTriggerSetter), 1, 2);
            Assert.That(typeof(UIContainerConditionalTriggerSetter).BaseType, Is.EqualTo(typeof(UIContainerValueSetter)));
        }

        [Test]
        public void GameplayContainerDeclarations()
        {
            AssertMembers(typeof(UIContainerGameplayControl), 0x00100001,
                new[] { ("m_enableGameplayControl", 0x0001), ("m_appRef", 0x0021), ("m_previousGameplayControl", 0x0001) },
                new[] { ("Start", 0x01c4), ("OnDestroy", 0x01c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerGameplayControl), 2, 1);

            AssertMembers(typeof(UIContainerGameplayIsland), 0x00100001,
                new[] { ("IslandNameValid", 0x0026), ("IslandName", 0x0026), ("WeightedProximity", 0x0026), ("DistanceProximity", 0x0026), ("CollectionInProgress", 0x0026) },
                new[] { ("Awake", 0x0081), ("OnDestroy", 0x0081), ("OnGameplayIslandChanged", 0x0081), (".ctor", 0x1886), ("<Awake>b__5_0", 0x0081) });
            AssertIl2CppOptions(typeof(UIContainerGameplayIsland), 2, 1);

            AssertMembers(typeof(UIContainerLeaderboards), 0x00100101,
                new[] { ("m_leaderboardIdentifier", 0x0001), ("CanShowChallenge", 0x0026), ("m_timeTrialLeaderboardManagerRef", 0x0021) },
                new[]
                {
                    ("Start", 0x0081), ("OnDestroy", 0x0081), ("ControllerShowLeaderboards", 0x0081), ("ShowLeaderboards", 0x0086),
                    ("SetLeaderboard", 0x0086), ("Action_IssueChallenge", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerLeaderboards), 2, 1);
            ParameterInfo value = typeof(UIContainerLeaderboards).GetMethod("ControllerShowLeaderboards", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters().Single();
            Assert.That((value.IsOptional, value.DefaultValue), Is.EqualTo((true, (object)0f)));
            Assert.That(Tooltip(Field(typeof(UIContainerLeaderboards), "m_leaderboardIdentifier")),
                Is.EqualTo("Optional leaderboard identifier so when we show the leaderboards we open it at that one."));

            AssertMembers(typeof(UIContainerLevelDefinition), 0x00100001,
                new[] { ("LoadingImage", 0x0026), ("m_levelDefinition", 0x0001), ("m_missionDefinition", 0x0001), ("m_levelSprite", 0x0001) },
                new[] { ("Setup", 0x00c6), ("SetMissionDefinition", 0x0086), ("OnDestroy", 0x0081), (".ctor", 0x1886), ("<Setup>b__4_0", 0x0081) });
            AssertIl2CppOptions(typeof(UIContainerLevelDefinition), 1, 2);

            AssertMembers(typeof(UIContainerRewardRestriction), 0x00100001,
                new[] { ("Icon", 0x0026), ("Text", 0x0026), ("Count", 0x0026), ("m_remainingRewardCounterText", 0x0001), ("m_collectableDefinition", 0x0001) },
                new[] { ("Setup", 0x00c6), ("OnDisable", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerRewardRestriction), 2, 1);

            AssertMembers(typeof(UIContainerSkipCutscene), 0x00100001,
                new[] { ("m_holdToSkipInput", 0x0001), ("m_theatreSkipString", 0x0001), ("m_uiManagerRef", 0x0021), ("m_app", 0x0001), ("m_cutsceneManager", 0x0001) },
                new[] { ("Start", 0x0081), ("Action_OnFullSkip", 0x0086), ("Action_OnSkipOnce", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerSkipCutscene), 1, 2);

            foreach (Type type in new[] { typeof(UIContainerGameplayControl), typeof(UIContainerGameplayIsland), typeof(UIContainerLeaderboards),
                         typeof(UIContainerLevelDefinition), typeof(UIContainerRewardRestriction), typeof(UIContainerSkipCutscene),
                         typeof(UIContainerNotificationRequest), typeof(UIContainerOrbCollectableCounter) })
                Assert.That(type.BaseType, Is.EqualTo(typeof(UIContainer)), type.Name);
        }

        [Test]
        public void NotificationRequestDeclarations()
        {
            AssertMembers(typeof(UIContainerNotificationRequest), 0x00100101,
                new[] { ("m_notificationTypes", 0x0001), ("m_displayImage", 0x0001), ("m_displayString", 0x0001), ("m_notificationManager", 0x0001), ("m_notification", 0x0001) },
                new[] { ("Setup", 0x00c6), ("Action_OnAccepted", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerNotificationRequest), 1, 2);

            Type config = Nested(typeof(UIContainerNotificationRequest), "NotificationPermissionConfig");
            AssertMembers(config, 0x00102003,
                new[] { ("Notification", 0x0006), ("Sprite", 0x0006), ("String", 0x0006) }, new[] { (".ctor", 0x1886) });
            AssertIl2CppOptions(config, 1, 2);
            Assert.That(AttributeTypes(Field(config, "String")), Is.EqualTo(new[] { typeof(Hardlight.Utils.HashEnumAttribute) }));
        }

        [Test]
        public void OrbCollectableCounterDeclarations()
        {
            Type counter = typeof(UIContainerOrbCollectableCounter);
            AssertMembers(counter, 0x00100001,
                new[]
                {
                    ("m_rewardType", 0x0001), ("m_animatePendingCount", 0x0001), ("m_initialDelaySeconds", 0x0001), ("m_animateTickIntervalSeconds", 0x0001),
                    ("m_progressionManager", 0x0001), ("m_missionManager", 0x0001), ("m_animateCountRoutine", 0x0001), ("m_dataManagerRef", 0x0021),
                    ("CollectedAmount", 0x0026), ("HasValues", 0x0026), ("CollectableIcon", 0x0026),
                },
                new[]
                {
                    ("Awake", 0x0081), ("ProgressionManagerValid", 0x0081), ("MissionManagerValid", 0x0081), ("UpdateBindings", 0x0081),
                    ("AnimateCount", 0x0081), ("OnDestroy", 0x0081), ("UpdateValues", 0x0086), ("UpdateValues", 0x0086), ("UpdateValues", 0x0086),
                    ("UpdateValues", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(counter, 1, 2);
            foreach (string field in new[] { "m_initialDelaySeconds", "m_animateTickIntervalSeconds" })
            {
                CustomAttributeData showIf = Field(counter, field).GetCustomAttributesData().Single(data => data.AttributeType == typeof(ShowIfAttribute));
                Assert.That(showIf.ConstructorArguments[0].Value, Is.EqualTo("m_animatePendingCount"), field);
            }

            Type closure = Nested(counter, "<>c__DisplayClass19_0");
            AssertMembers(closure, 0x00100103, new[] { ("missionDefinition", 0x0006) }, new[] { (".ctor", 0x1886), ("<UpdateValues>b__0", 0x0083) });
            Type iterator = Nested(counter, "<AnimateCount>d__15");
            AssertMembers(iterator, 0x00100103,
                new[] { ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("<>4__this", 0x0006), ("targetCount", 0x0006) },
                new[]
                {
                    (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1), ("MoveNext", 0x01e1),
                    ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1), ("System.Collections.IEnumerator.Reset", 0x01e1),
                    ("System.Collections.IEnumerator.get_Current", 0x09e1),
                });
        }

        [Test]
        public void UtilitiesDeclarations()
        {
            AssertMembers(typeof(UIContainerUtilities), 0x00100181,
                new[] { ("s_levelManagerRef", 0x0031), ("s_missionManagerRef", 0x0031), ("s_appRef", 0x0031) },
                new[] { ("TryGetSelectedLevelAndZone", 0x0096), ("SetBackgroundGradientBinders", 0x0096), ("GetZoneAccent", 0x0096), (".cctor", 0x1891) });
            AssertIl2CppOptions(typeof(UIContainerUtilities), 1, 2);
            ParameterInfo[] accent = typeof(UIContainerUtilities).GetMethod("GetZoneAccent").GetParameters();
            Assert.That(accent.Select(parameter => (parameter.Name, parameter.IsOptional, parameter.DefaultValue)).ToArray(),
                Is.EqualTo(new[] { ("missionDefinition", true, (object)null), ("zone", true, (object)null), ("levelDefinition", true, (object)null) }));
            ParameterInfo[] selected = typeof(UIContainerUtilities).GetMethod("TryGetSelectedLevelAndZone").GetParameters();
            Assert.That(selected.Select(parameter => (parameter.Name, parameter.IsOut)).ToArray(),
                Is.EqualTo(new[] { ("levelDefinition", true), ("zone", true) }));
        }
    }
}
