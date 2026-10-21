using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The declarations of the pause menu, the sound level pages, the support and credits pages and the other small settings
    // components against the DummyDll metadata: type flags, field and method order and flags, IL2CPP options in their
    // authored order and base types. The compiler-generated members (lambdas, iterator state machines, the cached lambda
    // class) are included because their numeric suffixes are member ordinals that fix their names.
    public sealed class PauseAndSoundSettingsDeclarationTests
    {
        private const BindingFlags NestedPrivate = BindingFlags.NonPublic | BindingFlags.Public;

        // InvertCameraSetting.
        [Test]
        public void InvertCameraSettingMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(InvertCameraSetting), 0x00100001,
                new (string, int)[]
                {
                    ("m_saveManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnSaveManagerValid", 0x0081),
                    ("Action_InvertCameraSetting", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(InvertCameraSetting), 2, 1);
            Assert.That(typeof(InvertCameraSetting).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        }

        // SoundLevelsOptionsValues.
        [Test]
        public void SoundLevelsOptionsValuesMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(SoundLevelsOptionsValues), 0x00100109,
                new (string, int)[]
                {
                    ("<MasterVolume>k__BackingField", 0x0021),
                    ("<MusicVolume>k__BackingField", 0x0021),
                    ("<SfxVolume>k__BackingField", 0x0021),
                    ("<VoiceVolume>k__BackingField", 0x0021),
                },
                new (string, int)[]
                {
                    ("get_MasterVolume", 0x0886),
                    ("get_MusicVolume", 0x0886),
                    ("get_SfxVolume", 0x0886),
                    ("get_VoiceVolume", 0x0886),
                    (".ctor", 0x1886),
                    ("ApplyToSettings", 0x0086),
                });
            AssertIl2CppOptions(typeof(SoundLevelsOptionsValues));
            Assert.That(typeof(SoundLevelsOptionsValues).BaseType, Is.EqualTo(typeof(System.ValueType)));
        }

        // UIContainerAnalyticsConsent.
        [Test]
        public void UIContainerAnalyticsConsentMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerAnalyticsConsent), 0x00100101,
                new (string, int)[]
                {
                    ("ConsentState", 0x0026),
                    ("m_analyticsConsentManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x00c4),
                    ("Action_OnToggleConsent", 0x0086),
                    ("Action_Close", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerAnalyticsConsent), 1, 2);
            Assert.That(typeof(UIContainerAnalyticsConsent).BaseType, Is.EqualTo(typeof(UIContainerGameplayControl)));
        }

        // UIContainerCredits.
        [Test]
        public void UIContainerCreditsMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerCredits), 0x00100001,
                new (string, int)[]
                {
                    ("m_creditsScrollParent", 0x0001),
                    ("m_endYPoint", 0x0001),
                    ("m_scrollToPoint", 0x0001),
                    ("m_scrollSpeed", 0x0001),
                    ("m_endWaitSeconds", 0x0001),
                    ("m_onEndTrigger", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerCredits), 1, 2);
            Assert.That(typeof(UIContainerCredits).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertMembers(typeof(UIContainerCredits).GetNestedType("<Start>d__6", NestedPrivate), 0x00100103,
                new (string, int)[]
                {
                    ("<>1__state", 0x0001),
                    ("<>2__current", 0x0001),
                    ("<>4__this", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("System.IDisposable.Dispose", 0x01e1),
                    ("MoveNext", 0x01e1),
                    ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1),
                    ("System.Collections.IEnumerator.get_Current", 0x09e1),
                });
        }

        // UIContainerEmailSupport.
        [Test]
        public void UIContainerEmailSupportMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerEmailSupport), 0x00100001,
                new (string, int)[]
                {
                    ("m_closeCustomerSupportEvent", 0x0001),
                    ("BodyText", 0x0026),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081),
                    ("Action_Close", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerEmailSupport), 1, 2);
            Assert.That(typeof(UIContainerEmailSupport).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // UIContainerPause.
        [Test]
        public void UIContainerPauseMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerPause), 0x00100101,
                new (string, int)[]
                {
                    ("m_resumeEvent", 0x0001),
                    ("m_resultsContainer", 0x0001),
                    ("m_resultsSecondaryContainer", 0x0001),
                    ("m_missionRanksContainer", 0x0001),
                    ("LevelTitle", 0x0026),
                    ("LevelSubtitle", 0x0026),
                    ("DreamNumber", 0x0026),
                    ("ShowFPSWarningMessage", 0x0026),
                    ("ZoneIconAsset", 0x0026),
                    ("ActBackground", 0x0026),
                    ("ShowFullscreenBackground", 0x0026),
                    ("m_closingCoroutine", 0x0001),
                    ("m_levelManager", 0x0001),
                    ("m_missionManager", 0x0001),
                    ("m_uiManager", 0x0001),
                    ("m_zoneAccent", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x00c4),
                    ("OnLevelManagerValid", 0x0081),
                    ("OnQualityManagerValid", 0x0081),
                    ("OnMissionManagerValid", 0x0081),
                    ("Action_Restart", 0x0086),
                    ("Action_Respawn", 0x0086),
                    ("Action_Exit", 0x0086),
                    ("Restart", 0x0081),
                    ("Respawn", 0x0081),
                    ("Close", 0x0081),
                    ("FireMissionQuitAnalytics", 0x0081),
                    ("OnDestroy", 0x00c4),
                    (".ctor", 0x1886),
                    ("<Start>b__16_0", 0x0081),
                    ("<OnMissionManagerValid>b__19_0", 0x0081),
                    ("<Restart>b__23_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerPause), 2, 1);
            Assert.That(typeof(UIContainerPause).BaseType, Is.EqualTo(typeof(UIContainerGameplayControl)));
            AssertMembers(typeof(UIContainerPause).GetNestedType("<>c", NestedPrivate), 0x00102103,
                new (string, int)[]
                {
                    ("<>9", 0x0036),
                    ("<>9__22_0", 0x0016),
                },
                new (string, int)[]
                {
                    (".cctor", 0x1891),
                    (".ctor", 0x1886),
                    ("<Action_Exit>b__22_0", 0x0083),
                });
            AssertMembers(typeof(UIContainerPause).GetNestedType("<>c__DisplayClass24_0", NestedPrivate), 0x00100103,
                new (string, int)[]
                {
                    ("<>4__this", 0x0006),
                    ("characterManager", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("<Respawn>b__0", 0x0083),
                    ("<Respawn>b__1", 0x0083),
                });
            AssertMembers(typeof(UIContainerPause).GetNestedType("<Respawn>d__24", NestedPrivate), 0x00100103,
                new (string, int)[]
                {
                    ("<>1__state", 0x0001),
                    ("<>2__current", 0x0001),
                    ("<>4__this", 0x0006),
                    ("<>8__1", 0x0001),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("System.IDisposable.Dispose", 0x01e1),
                    ("MoveNext", 0x01e1),
                    ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1),
                    ("System.Collections.IEnumerator.get_Current", 0x09e1),
                });
            AssertMembers(typeof(UIContainerPause).GetNestedType("<Restart>d__23", NestedPrivate), 0x00100103,
                new (string, int)[]
                {
                    ("<>1__state", 0x0001),
                    ("<>2__current", 0x0001),
                    ("<>4__this", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("System.IDisposable.Dispose", 0x01e1),
                    ("MoveNext", 0x01e1),
                    ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1),
                    ("System.Collections.IEnumerator.get_Current", 0x09e1),
                });
        }

        // UIContainerPauseBackground.
        [Test]
        public void UIContainerPauseBackgroundMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerPauseBackground), 0x00100001,
                new (string, int)[]
                {
                    ("m_qualitySettings", 0x0001),
                    ("m_backgroundImage", 0x0001),
                    ("m_useRenderTexture", 0x0001),
                    ("m_cinemachineCameraManagerRef", 0x0021),
                    ("m_cinemachineCameraManager", 0x0001),
                    ("m_cachedCullingMask", 0x0001),
                    ("m_renderTexture", 0x0001),
                    ("m_updateBackground", 0x0001),
                    ("m_screenSize", 0x0001),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081),
                    ("Update", 0x0081),
                    ("Initialise", 0x0081),
                    ("InitialiseFromCamera", 0x0081),
                    ("RenderBackgroundImage", 0x0081),
                    ("OnDisable", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerPauseBackground), 2, 1);
            Assert.That(typeof(UIContainerPauseBackground).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertMembers(typeof(UIContainerPauseBackground.QualityBackground), 0x00102002,
                new (string, int)[]
                {
                    ("ResolutionMax", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                });
        }

        // UIContainerQRCode.
        [Test]
        public void UIContainerQRCodeMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerQRCode), 0x00100001,
                new (string, int)[]
                {
                    ("URLText", 0x0026),
                    ("QRCodeSprite", 0x0026),
                    ("m_stringToQRCodeLookup", 0x0001),
                    ("m_parameters", 0x0001),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6),
                    ("UpdateBinders", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerQRCode), 1, 2);
            Assert.That(typeof(UIContainerQRCode).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertMembers(typeof(UIContainerQRCode).GetNestedType("QRCodeSpriteLookUp", NestedPrivate), 0x0010210b,
                new (string, int)[]
                {
                    ("DefaultSprite", 0x0006),
                    ("LanguageSpriteOverride", 0x0006),
                },
                new (string, int)[0]);
            AssertMembers(typeof(UIContainerQRCode).GetNestedType("LanguageToSpritePair", NestedPrivate), 0x0010210b,
                new (string, int)[]
                {
                    ("Language", 0x0006),
                    ("Sprite", 0x0006),
                },
                new (string, int)[0]);
        }

        // UIContainerResetGameData.
        [Test]
        public void UIContainerResetGameDataMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerResetGameData), 0x00100001,
                new (string, int)[]
                {
                    ("m_onConfirmDeleteUIEvent", 0x0001),
                },
                new (string, int)[]
                {
                    ("Action_DeleteCurrentSave", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerResetGameData), 2, 1);
            Assert.That(typeof(UIContainerResetGameData).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // UIContainerSoundLevels.
        [Test]
        public void UIContainerSoundLevelsMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSoundLevels), 0x00100101,
                new (string, int)[]
                {
                    ("m_soundLevelsOptionsContainer", 0x0001),
                    ("m_saveEvent", 0x0001),
                    ("m_existingSaveData", 0x0001),
                    ("m_audioManager", 0x0001),
                    ("m_saveManager", 0x0001),
                    ("m_masterVolume", 0x0001),
                    ("m_musicVolume", 0x0001),
                    ("m_sfxVolume", 0x0001),
                    ("m_voiceVolume", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("Refresh", 0x0081),
                    ("Action_OnExit", 0x0086),
                    ("Action_ResetToDefaults", 0x0086),
                    ("OnMasterVolumeToggle", 0x0081),
                    ("OnMusicVolumeToggle", 0x0081),
                    ("OnSfxVolumeToggle", 0x0081),
                    ("OnVoiceVolumeToggle", 0x0081),
                    ("UpdateAudioSettings", 0x0081),
                    ("LoadSettingsSaveData", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSoundLevels), 1, 2);
            Assert.That(typeof(UIContainerSoundLevels).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // UIContainerSoundLevelsOptions.
        [Test]
        public void UIContainerSoundLevelsOptionsMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSoundLevelsOptions), 0x00100101,
                new (string, int)[]
                {
                    ("MasterVolume", 0x0026),
                    ("MusicVolume", 0x0026),
                    ("SfxVolume", 0x0026),
                    ("VoiceVolume", 0x0026),
                    ("m_onSaveClick", 0x0001),
                    ("m_onMasterVolumeToggle", 0x0001),
                    ("m_onMusicVolumeToggle", 0x0001),
                    ("m_onSfxVolumeToggle", 0x0001),
                    ("m_onVoiceVolumeToggle", 0x0001),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6),
                    ("GetValues", 0x0086),
                    ("Action_OnSaveClick", 0x0086),
                    ("Action_OnMasterVolumeToggle", 0x0086),
                    ("Action_OnMusicVolumeToggle", 0x0086),
                    ("Action_OnSfxVolumeToggle", 0x0086),
                    ("Action_OnVoiceVolumeToggle", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSoundLevelsOptions), 1, 2);
            Assert.That(typeof(UIContainerSoundLevelsOptions).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // UIWidgetButtonSupport.
        [Test]
        public void UIWidgetButtonSupportMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetButtonSupport), 0x00100001,
                new (string, int)[]
                {
                    ("m_debugViewContainerInEditor", 0x0001),
                    ("m_openCustomerSupportEvent", 0x0001),
                },
                new (string, int)[]
                {
                    ("Action_SendEmail", 0x0086),
                    ("OpenEmailContainer", 0x0081),
                    ("GetEmailString", 0x0091),
                    ("GetEmailBody", 0x0096),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetButtonSupport), 2, 1);
            Assert.That(typeof(UIWidgetButtonSupport).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        }

    }
}
