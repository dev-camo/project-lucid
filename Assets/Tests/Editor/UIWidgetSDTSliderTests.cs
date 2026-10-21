using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTSlider (Game.Runtime 0x0200084f, x86-64 0x60D3D0..0x60DAB9, iterator MoveNext 0x60D6F0) as a real
    // component on an owned inactive host, driving a real Slider and instantiating real UIWidgetSliderPip widgets.
    // The Start coroutine is stepped by hand: nothing here runs the engine's coroutine scheduler.
    public sealed class UIWidgetSDTSliderTests
    {
        private static readonly Color EmptyColour = new Color(0.1f, 0.2f, 0.3f, 1f);
        private static readonly Color FillColour = new Color(0.9f, 0.8f, 0.7f, 1f);

        private sealed class Rig
        {
            internal UIWidgetSDTSlider Widget;
            internal Slider Slider;
            internal RectTransform PipParent;
            internal UIWidgetSliderPip Prefab;
        }

        // A slider from 0 to the given maximum, a 200 wide pip parent (pivot at its centre, so its rect starts at -100)
        // and a pip template, wired as the authored widget is.
        private static Rig NewRig(Owned owned, float maximum, bool wholeNumbers)
        {
            var rig = new Rig();
            rig.Widget = owned.Host("Lucid sdt slider", false).AddComponent<UIWidgetSDTSlider>();
            rig.Slider = rig.Widget.gameObject.AddComponent<Slider>();
            rig.Slider.minValue = 0f;
            rig.Slider.maxValue = maximum;
            rig.Slider.wholeNumbers = wholeNumbers;
            GameObject parent = owned.Host("Lucid sdt slider pips", false, rig.Widget.transform);
            rig.PipParent = parent.AddComponent<RectTransform>();
            rig.PipParent.sizeDelta = new Vector2(200f, 20f);
            GameObject template = owned.Host("Lucid sdt slider pip template", false);
            template.AddComponent<RectTransform>();
            rig.Prefab = template.AddComponent<UIWidgetSliderPip>();
            Set(rig.Widget, "m_slider", rig.Slider); // serialized in the original
            Set(rig.Widget, "m_prefabToSpawn", rig.Prefab); // serialized in the original
            Set(rig.Widget, "m_pipParent", rig.PipParent); // serialized in the original
            Set(rig.Widget, "m_emptyPipColour", EmptyColour); // serialized in the original
            Set(rig.Widget, "m_fillPipColour", FillColour); // serialized in the original
            return rig;
        }

        private static List<UIWidgetSliderPip> Pips(Rig rig) => Get<List<UIWidgetSliderPip>>(rig.Widget, "m_sliderPips");

        private static IEnumerator Start(Rig rig) => (IEnumerator)Call(rig.Widget, "Start");

        private static void RunStart(Rig rig)
        {
            IEnumerator start = Start(rig);
            Assert.That(start.MoveNext(), Is.True);
            Assert.That(start.MoveNext(), Is.True);
            Assert.That(start.MoveNext(), Is.False);
        }

        // The constructor 0x60D650 loads 1,1,1,1 (0x29A70B0) into both pip colours and builds the pip list.
        [Test]
        public void AFreshWidgetHoldsWhitePipColoursAndAnEmptyPipList()
        {
            Owned.Run(owned =>
            {
                var widget = owned.Host("Lucid sdt slider", false).AddComponent<UIWidgetSDTSlider>();

                Assert.That(Get<Color>(widget, "m_emptyPipColour"), Is.EqualTo(new Color(1f, 1f, 1f, 1f)));
                Assert.That(Get<Color>(widget, "m_fillPipColour"), Is.EqualTo(new Color(1f, 1f, 1f, 1f)));
                Assert.That(Get<List<UIWidgetSliderPip>>(widget, "m_sliderPips"), Is.Empty);
            });
        }

        // Start (state 0, 0x60D7B4) ends at once for a slider that does not use whole numbers: no wait, no listener,
        // no pips.
        [Test]
        public void StartDoesNothingForASliderWithoutWholeNumbers()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 4f, false);

                Assert.That(Start(rig).MoveNext(), Is.False);

                Assert.That(Pips(rig), Is.Empty);
                rig.Slider.value = 2f;
                Assert.That(rig.PipParent.childCount, Is.EqualTo(0));
            });
        }

        // Start waits for the end of the frame twice with the same WaitForEndOfFrame (states 0 and 1 both yield
        // <waitForFrame>5__2, 0x60d7fe and 0x60da1f), and does its work in the third step.
        [Test]
        public void StartYieldsTheSameEndOfFrameWaitTwiceBeforeBuildingThePips()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 4f, true);
                IEnumerator start = Start(rig);

                Assert.That(start.MoveNext(), Is.True);
                object first = start.Current;
                Assert.That(first, Is.TypeOf<WaitForEndOfFrame>());
                Assert.That(Pips(rig), Is.Empty);
                Assert.That(start.MoveNext(), Is.True);
                Assert.That(start.Current, Is.SameAs(first));
                Assert.That(Pips(rig), Is.Empty);
                Assert.That(start.MoveNext(), Is.False);
                Assert.That(Pips(rig), Is.Not.Empty);
            });
        }

        // The loop (0x60d8db) runs while the step index is below maximum + 1: a maximum of 2.5 gives pips 0..3. They are
        // instantiated under the pip parent (Instantiate with a parent) and placed at index * (width / maximum) plus
        // the parent rect's x, in single precision, with y and z zero: 200 / 2.5 = 80, from -100.
        [Test]
        public void StartSpawnsOnePipPerStepSpacedAcrossTheParentWidth()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 2.5f, true);

                RunStart(rig);

                List<UIWidgetSliderPip> pips = Pips(rig);
                Assert.That(pips.Count, Is.EqualTo(4));
                Assert.That(rig.PipParent.childCount, Is.EqualTo(4));
                for (int i = 0; i < pips.Count; i++)
                {
                    Assert.That(pips[i], Is.Not.SameAs(rig.Prefab));
                    Assert.That(pips[i].transform.parent, Is.EqualTo(rig.PipParent));
                    Assert.That(pips[i].transform.GetSiblingIndex(), Is.EqualTo(i));
                    // The engine stores the position of a RectTransform as an anchored position, so allow its rounding.
                    Vector3 position = pips[i].transform.localPosition;
                    Assert.That(position.x, Is.EqualTo(-100f + 80f * i).Within(1e-3f), "pip " + i);
                    Assert.That(position.y, Is.EqualTo(0f).Within(1e-3f));
                    Assert.That(position.z, Is.EqualTo(0f).Within(1e-3f));
                }
            });
        }

        // The last step of Start (0x60da2b) paints the pips from the slider's value, so at 0 only the first pip is
        // filled; later value changes reach the pips through the listener Start added.
        [Test]
        public void StartPaintsThePipsAndFollowsTheSlider()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 2.5f, true);

                RunStart(rig);

                Assert.That(Pips(rig).Select(pip => pip.Colour.Value),
                    Is.EqualTo(new[] { FillColour, EmptyColour, EmptyColour, EmptyColour }));

                rig.Slider.value = 2f;
                Assert.That(Pips(rig).Select(pip => pip.Colour.Value),
                    Is.EqualTo(new[] { FillColour, FillColour, FillColour, EmptyColour }));
            });
        }

        // OnSliderValueChanged 0x60D550 reads the slider's own value for every pip; its argument is not used.
        [Test]
        public void TheValueArgumentOfThePaintIsIgnored()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 2.5f, true);
                RunStart(rig);
                rig.Slider.SetValueWithoutNotify(1f);

                Call(rig.Widget, "OnSliderValueChanged", 99f);

                Assert.That(Pips(rig).Select(pip => pip.Colour.Value),
                    Is.EqualTo(new[] { FillColour, FillColour, EmptyColour, EmptyColour }));
            });
        }

        // SetValue 0x60D500 sets the slider's value (virtual setter, slot 47), notifying its listeners.
        [Test]
        public void SettingTheValueMovesTheSliderAndRepaintsThroughTheListener()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 2.5f, true);
                RunStart(rig);

                rig.Widget.SetValue(1f);

                Assert.That(rig.Slider.value, Is.EqualTo(1f));
                Assert.That(Pips(rig).Select(pip => pip.Colour.Value),
                    Is.EqualTo(new[] { FillColour, FillColour, EmptyColour, EmptyColour }));
            });
        }

        // SetValueWithoutNotify 0x60D520 calls the slider's silent setter (slot 48) and then repaints itself, so the
        // pips change although the slider notified nobody.
        [Test]
        public void SettingTheValueWithoutNotifyingStillRepaintsThePips()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 2.5f, true);
                RunStart(rig);
                var notified = new List<float>();
                rig.Slider.onValueChanged.AddListener(notified.Add);

                rig.Widget.SetValueWithoutNotify(2f);

                Assert.That(notified, Is.Empty);
                Assert.That(rig.Slider.value, Is.EqualTo(2f));
                Assert.That(Pips(rig).Select(pip => pip.Colour.Value),
                    Is.EqualTo(new[] { FillColour, FillColour, FillColour, EmptyColour }));
            });
        }

        // OnDestroy 0x60D460 removes the listener Start added: the slider no longer repaints the pips.
        [Test]
        public void DestroyingStopsTheSliderFromRepaintingThePips()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 2.5f, true);
                RunStart(rig);

                Call(rig.Widget, "OnDestroy");
                rig.Slider.value = 2f;

                Assert.That(Pips(rig).Select(pip => pip.Colour.Value),
                    Is.EqualTo(new[] { FillColour, EmptyColour, EmptyColour, EmptyColour }));
            });
        }
    }
}
