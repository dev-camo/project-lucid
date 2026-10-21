using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettingsLanguage (Game.Runtime 0x02000938, Start 0x67D3D0, CacheLanguageChoice 0x67D520, OnDestroy 0x67D570,
    // constructor 0x67D870) and its cached lambda (0x02000939, 0x67D970) as a real component on an owned inactive host, with
    // three real language buttons. The App is the real one OwnedCharacterManagers registers. The Language singleton is an
    // owned component on an inactive host, installed as the singleton (its Awake would register a system row) and removed
    // afterwards; it starts as US English, the default.
    public sealed class UIContainerSettingsLanguageTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly MethodInfo SetLanguageInstance = typeof(MonoSingleton<Language>).GetProperty("Instance").GetSetMethod(true);

        private sealed class Rig
        {
            public UIContainerSettingsLanguage Container;
            public UIWidgetSDTLanguageSelect[] Buttons;
            public Language Language;
            public App App;
        }

        private static void Run(Action<Owned, Rig> body) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: the rows of the App, DataManager and SaveManager.
            owned.Lease(new OwnedCharacterManagers());
            if (Language.Instance != null) throw new InvalidOperationException("Fixture requires no live Language instance");
            var rig = new Rig { App = ProcessManager.GetSystem<App>() };
            rig.Language = owned.Host("Lucid language", false).AddComponent<Language>();
            SetLanguageInstance.Invoke(null, new object[] { rig.Language });
            owned.Lease(new ReleaseAction(() => SetLanguageInstance.Invoke(null, new object[] { null })));
            rig.Buttons = new[] { Button(owned, Languages.EnglishUS), Button(owned, Languages.French), Button(owned, Languages.German) };
            rig.Container = owned.Host("Lucid language page", false).AddComponent<UIContainerSettingsLanguage>();
            Set(rig.Container, "m_languageSelectionButtons", rig.Buttons); // serialized in the original
            body(owned, rig);
        });

        private static UIWidgetSDTLanguageSelect Button(Owned owned, Languages language)
        {
            var button = owned.Host("Lucid language button", false).AddComponent<UIWidgetSDTLanguageSelect>();
            Set(button, "m_language", language); // serialized in the original
            return button;
        }

        private static bool[] Chosen(Rig rig) => Array.ConvertAll(rig.Buttons, button => button.m_languageIsChosen.Value);

        // Start 0x67D3D0: the current language (Language.GetLanguage 0x1A342E0) is cached, and every button gets the page's
        // handler before it is told the cached language.
        [Test]
        public void StartMarksTheCurrentLanguageAsChosenOnEveryButton()
        {
            Run((owned, rig) =>
            {
                Language.OverrideLanguage(Languages.German);

                Call(rig.Container, "Start");

                Assert.That(Chosen(rig), Is.EqualTo(new[] { false, false, true }));
            });
        }

        [Test]
        public void StartCachesTheDefaultLanguageWhenNothingWasChosen()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "Start");

                Assert.That(Chosen(rig), Is.EqualTo(new[] { true, false, false }));
            });
        }

        // The handler added to each button's selection event is CacheLanguageChoice: selecting a button chooses its
        // language on all of them.
        [Test]
        public void SelectingAButtonChoosesItsLanguageOnEveryButton()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "Start");

                rig.Buttons[1].Action_SetLanguage();
                Assert.That(Chosen(rig), Is.EqualTo(new[] { false, true, false }));

                rig.Buttons[2].Action_SetLanguage();
                Assert.That(Chosen(rig), Is.EqualTo(new[] { false, false, true }));
            });
        }

        // CacheLanguageChoice 0x67D520 stores the language (+0x48) and passes it to every button.
        [Test]
        public void CachingALanguageTellsEveryButton()
        {
            Run((owned, rig) =>
            {
                rig.Container.CacheLanguageChoice(Languages.French);

                Assert.That(Chosen(rig), Is.EqualTo(new[] { false, true, false }));
            });
        }

        // OnDestroy 0x67D570 removes the handler from every button, marks the fonts as not ready for the new language
        // (FontsChangeLanguageReady false, 0x67D79B) and applies the chosen language. A changed language returns there:
        // the font loading it starts reports the fonts ready again.
        [Test]
        public void ClosingWithANewLanguageAppliesItAndLeavesTheFontsNotReady()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "Start");
                rig.Buttons[1].Action_SetLanguage();
                rig.App.Storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, true);

                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Language.CurrentLanguage, Is.EqualTo(Languages.French));
                Assert.That(rig.App.Storage.GetValue<bool>(AppFSMKeys.FontsChangeLanguageReady), Is.False);
                foreach (UIWidgetSDTLanguageSelect button in rig.Buttons)
                    Assert.That(button.SelectionEvent, Is.Null, "the page's handler is removed");
            });
        }

        // An unchanged language (OverrideLanguage 0x1A345A0 answers false) leaves nothing to reload, so the page reports the
        // fonts ready on the next frame (CoroutineUtils.OnNextFrame 0x1B31780 with the cached lambda). The refused coroutine
        // is the fixture's inactive host; the cached lambda is run by hand.
        [Test]
        public void ClosingWithTheSameLanguageReportsTheFontsReadyOnTheNextFrame()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "Start");
                rig.App.Storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, true);
                Type cache = typeof(UIContainerSettingsLanguage).GetNestedType("<>c", BindingFlags.NonPublic);
                FieldInfo lambda = cache.GetField("<>9__5_0", BindingFlags.Static | BindingFlags.Public);
                // The compiler's lazily filled cache of the static lambda; the case leaves it empty again.
                owned.Lease(new ReleaseAction(() => lambda.SetValue(null, null)));
                lambda.SetValue(null, null);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Language.CurrentLanguage, Is.EqualTo(Languages.EnglishUS));
                Assert.That(rig.App.Storage.GetValue<bool>(AppFSMKeys.FontsChangeLanguageReady), Is.False, "not ready until the next frame");
                var scheduled = (Action)lambda.GetValue(null);
                Assert.That(scheduled, Is.Not.Null, "the lambda the next frame runs");
                scheduled();
                Assert.That(rig.App.Storage.GetValue<bool>(AppFSMKeys.FontsChangeLanguageReady), Is.True);
            });
        }
    }
}
