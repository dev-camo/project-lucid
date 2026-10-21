using System.Collections.Generic;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTLanguageSelect (Game.Runtime 0x020009c3, Action_SetLanguage 0x69FC90, SetCachedLanguage 0x69FCC0,
    // constructor 0x69FCF0) as a real component on an owned inactive host.
    public sealed class UIWidgetSDTLanguageSelectTests
    {
        private static UIWidgetSDTLanguageSelect NewSelect(Owned owned, Languages language)
        {
            var widget = owned.Host("Lucid language widget", false).AddComponent<UIWidgetSDTLanguageSelect>();
            Set(widget, "m_language", language); // serialized in the original
            return widget;
        }

        // The constructor builds the chosen-flag binder (0x69FCF0); a fresh widget holds none of the other state.
        [Test]
        public void AFreshWidgetHasAnUnchosenFlagAndNoListener()
        {
            Owned.Run(owned =>
            {
                var widget = NewSelect(owned, Languages.French);

                Assert.That(widget.m_languageIsChosen, Is.Not.Null);
                Assert.That(widget.m_languageIsChosen.Value, Is.False);
                Assert.That(widget.SelectionEvent, Is.Null);
            });
        }

        // Action_SetLanguage 0x69FC90 invokes the selection event with the widget's language (+0x28), or does nothing
        // when nothing listens.
        [Test]
        public void SelectingAnnouncesTheWidgetsLanguage()
        {
            Owned.Run(owned =>
            {
                var widget = NewSelect(owned, Languages.Japanese);
                Assert.DoesNotThrow(widget.Action_SetLanguage);

                var announced = new List<Languages>();
                widget.SelectionEvent = announced.Add;
                widget.Action_SetLanguage();
                widget.Action_SetLanguage();

                Assert.That(announced, Is.EqualTo(new[] { Languages.Japanese, Languages.Japanese }));
            });
        }

        // SetCachedLanguage 0x69FCC0 publishes whether the widget's language equals the cached one.
        [Test]
        public void TheChosenFlagFollowsTheCachedLanguage()
        {
            Owned.Run(owned =>
            {
                var widget = NewSelect(owned, Languages.German);

                widget.SetCachedLanguage(Languages.German);
                Assert.That(widget.m_languageIsChosen.Value, Is.True);

                widget.SetCachedLanguage(Languages.Korean);
                Assert.That(widget.m_languageIsChosen.Value, Is.False);

                widget.SetCachedLanguage(Languages.German);
                Assert.That(widget.m_languageIsChosen.Value, Is.True);
            });
        }
    }
}
