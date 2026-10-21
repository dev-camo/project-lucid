using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using static ProjectLucid.Tests.TimelineTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SubtitleClip (TypeDef 0x0200044a, x86-64 0x7A5E60..0x7A60FF), SubtitleMixer (0x0200044b,
    // 0x7A6100..0x7A64DF) and SubtitleTrack (0x0200044c, 0x7A64E0..0x7A659F). Playables are built on a real
    // PlayableGraph and the mixer's ProcessFrame is called directly (no graph output evaluates it in Edit Mode)
    // with a real TextMeshProUGUI under an inactive root, so the backing-panel toggles never wake the text. Not
    // covered: the play-mode branch of SubtitleClip.CreatePlayable (needs a DataManager with subtitle character
    // definitions) and SubtitleMixer.OnGraphStart (needs a SaveManager). Expectations for those, to verify natively
    // when such fixtures exist: the play-mode branch (0x7A5F69) takes
    // SystemRef<DataManager>.Get().SubtitleDefGroup.GetFormattedString(m_subtitleText) as the behaviour text; OnGraphStart
    // (0x7A6163) copies the SaveManager settings' SubtitlesEnabled flag (+0x65) into the mixer, then calls the base method.
    public sealed class SubtitleTimelineTests
    {
        // The mixer's SubtitleBehaviour inputs, one per (text, weight, background).
        private sealed class MixerScene
        {
            public SubtitleMixer Mixer;
            public Playable Playable;
            public ScriptPlayable<SubtitleMixer> Typed;
            public TextMeshProUGUI Text;
            public GameObject Panel;
        }

        private static MixerScene NewScene(Owned owned, params (string text, float weight, Color background)[] inputs)
        {
            PlayableGraph graph = NewGraph(owned);
            ScriptPlayable<SubtitleMixer> mixer = ScriptPlayable<SubtitleMixer>.Create(graph, inputs.Length);
            for (int i = 0; i < inputs.Length; i++)
            {
                var template = new SubtitleBehaviour { SubtitleText = inputs[i].text, BackgroundColour = inputs[i].background };
                ScriptPlayable<SubtitleBehaviour> input = ScriptPlayable<SubtitleBehaviour>.Create(graph, template, 0);
                graph.Connect(input, 0, mixer, i);
                mixer.SetInputWeight(i, inputs[i].weight);
            }
            GameObject root = owned.Host("Lucid subtitle root", false);
            GameObject panel = owned.Host("Lucid subtitle panel", true, root.transform);
            var textObject = owned.Track(new GameObject("Lucid subtitle text", typeof(RectTransform)));
            textObject.transform.SetParent(panel.transform, false);
            return new MixerScene
            {
                Mixer = mixer.GetBehaviour(),
                Playable = mixer,
                Typed = mixer,
                Text = textObject.AddComponent<TextMeshProUGUI>(),
                Panel = panel
            };
        }

        private static void Process(MixerScene scene, object playerData) =>
            scene.Mixer.ProcessFrame(scene.Playable, default(FrameData), playerData);

        // The constructor 0x7A6480: empty text, translucent black background (0x29a7dd0 = 0,0,0,0.7) and subtitles on.
        [Test]
        public void MixerStartsWithEmptyTextAndSubtitlesEnabled()
        {
            Owned.Run(owned =>
            {
                MixerScene scene = NewScene(owned);
                Assert.That(Get<string>(scene.Mixer, "m_currentText"), Is.EqualTo(string.Empty));
                Assert.That(Get<Color>(scene.Mixer, "m_backgroundColour"), Is.EqualTo(new Color(0f, 0f, 0f, 0.7f)));
                Assert.That(Get<bool>(scene.Mixer, "m_subtitlesEnabled"), Is.True);
            });
        }

        // ProcessFrame 0x7A6190: every input with a positive weight overwrites the text, alpha and background in order,
        // so the last such input wins; the text takes colour (1,1,1,alpha), and the text's parent (found once,
        // 0x7a6436..0x7a644f) is the backing panel, switched on while any clip is active.
        [Test]
        public void TheLastClipWithAPositiveWeightSuppliesTheTextAlphaAndBackground()
        {
            Owned.Run(owned =>
            {
                MixerScene scene = NewScene(owned,
                    ("first", 0.3f, Color.red), ("second", 0.6f, Color.green), ("third", 0f, Color.blue));
                Process(scene, scene.Text);
                Assert.That(scene.Text.text, Is.EqualTo("second"));
                Assert.That(scene.Text.color.r, Is.EqualTo(1f));
                Assert.That(scene.Text.color.g, Is.EqualTo(1f));
                Assert.That(scene.Text.color.b, Is.EqualTo(1f));
                Assert.That(scene.Text.color.a, Is.EqualTo(0.6f).Within(1e-6f));
                Assert.That(Get<Color>(scene.Mixer, "m_backgroundColour"), Is.EqualTo(Color.green));
                Assert.That(Get<int>(scene.Mixer, "m_inputCount"), Is.EqualTo(3));
                Assert.That(Get<GameObject>(scene.Mixer, "m_backingPanel"), Is.SameAs(scene.Panel));
                Assert.That(scene.Panel.activeSelf, Is.True);
            });
        }

        // With no positive weight the panel is hidden (SetActive(false) 0x7a6468) but the text and alpha of the
        // last frame that had a clip are written again unchanged: nothing resets them.
        [Test]
        public void WithoutAnActiveClipThePanelHidesAndTheLastTextAndAlphaAreKept()
        {
            Owned.Run(owned =>
            {
                MixerScene scene = NewScene(owned, ("line", 0.5f, Color.red));
                Process(scene, scene.Text);
                Assert.That(scene.Panel.activeSelf, Is.True);
                scene.Typed.SetInputWeight(0, 0f);
                Process(scene, scene.Text);
                Assert.That(scene.Panel.activeSelf, Is.False);
                Assert.That(scene.Text.text, Is.EqualTo("line"));
                Assert.That(scene.Text.color.a, Is.EqualTo(0.5f).Within(1e-6f));
            });
            Owned.Run(owned =>
            {
                MixerScene scene = NewScene(owned, ("never shown", 0f, Color.red));
                Process(scene, scene.Text);
                Assert.That(scene.Text.text, Is.EqualTo(string.Empty), "the initial empty text");
                Assert.That(scene.Text.color.a, Is.EqualTo(0f));
                Assert.That(scene.Panel.activeSelf, Is.False);
            });
        }

        // The weight test (ucomiss 0 against the weight, jae 0x7a6371) skips only weights that are ordered at or below
        // zero: a negative weight is skipped, but a NaN weight is not (unordered leaves the carry flag set), so its
        // clip supplies the text with a NaN alpha. A plain "weight > 0" would skip it.
        [Test]
        public void ANegativeWeightIsSkippedButANaNWeightIsNot()
        {
            Owned.Run(owned =>
            {
                MixerScene negative = NewScene(owned, ("negative", -0.5f, Color.red));
                Process(negative, negative.Text);
                Assert.That(negative.Text.text, Is.EqualTo(string.Empty));
                Assert.That(negative.Panel.activeSelf, Is.False);

                MixerScene unordered = NewScene(owned, ("unordered", float.NaN, Color.red));
                Process(unordered, unordered.Text);
                Assert.That(unordered.Text.text, Is.EqualTo("unordered"));
                Assert.That(float.IsNaN(unordered.Text.color.a), Is.True);
                Assert.That(unordered.Panel.activeSelf, Is.True);
            });
        }

        // 0x7a628d: with subtitles disabled (the player's saved preference, copied by OnGraphStart from the SaveManager,
        // which this fixture does not run, so the field is written directly) the text, colour and panel are left alone.
        [Test]
        public void DisabledSubtitlesLeaveTheTextAndPanelUntouched()
        {
            Owned.Run(owned =>
            {
                MixerScene scene = NewScene(owned, ("hidden by setting", 1f, Color.red));
                Set(scene.Mixer, "m_subtitlesEnabled", false);
                scene.Text.text = "untouched";
                scene.Text.color = Color.magenta;
                Process(scene, scene.Text);
                Assert.That(scene.Text.text, Is.EqualTo("untouched"));
                Assert.That(scene.Text.color, Is.EqualTo(Color.magenta));
                Assert.That(scene.Panel.activeSelf, Is.True, "the panel was never looked up");
                Assert.That(Get<GameObject>(scene.Mixer, "m_backingPanel"), Is.Null);
            });
        }

        // m_anyTextShowing is cleared at 0x7a625c before the binding and subtitlesEnabled guards, so a frame that ends at
        // a guard leaves it false even after a frame that set it.
        [Test]
        public void ADisabledFrameClearsTheAnyTextShowingFlag()
        {
            Owned.Run(owned =>
            {
                MixerScene scene = NewScene(owned, ("line", 1f, Color.red));
                Process(scene, scene.Text);
                Assert.That(Get<bool>(scene.Mixer, "m_anyTextShowing"), Is.True, "precondition");
                Set(scene.Mixer, "m_subtitlesEnabled", false);
                Process(scene, scene.Text);
                Assert.That(Get<bool>(scene.Mixer, "m_anyTextShowing"), Is.False);
                Set(scene.Mixer, "m_subtitlesEnabled", true);
                Process(scene, scene.Text);
                Assert.That(Get<bool>(scene.Mixer, "m_anyTextShowing"), Is.True, "precondition for the missing binding");
                Process(scene, null);
                Assert.That(Get<bool>(scene.Mixer, "m_anyTextShowing"), Is.False, "a missing binding clears it too");
            });
        }

        // The backing panel is looked up only while it is Unity-null (op_Equality 0x7a6424, parent 0x7a6436..0x7a644f):
        // once found it is kept, so a text moved under another panel does not change which panel is toggled.
        [Test]
        public void TheBackingPanelIsFoundOnlyOnce()
        {
            Owned.Run(owned =>
            {
                MixerScene scene = NewScene(owned, ("line", 1f, Color.red));
                Process(scene, scene.Text);
                Assert.That(Get<GameObject>(scene.Mixer, "m_backingPanel"), Is.SameAs(scene.Panel), "precondition");
                GameObject otherPanel = owned.Host("Lucid other subtitle panel", true, scene.Panel.transform.parent);
                scene.Text.transform.SetParent(otherPanel.transform, false);
                scene.Typed.SetInputWeight(0, 0f);
                Process(scene, scene.Text);
                Assert.That(scene.Panel.activeSelf, Is.False, "the panel found first is toggled");
                Assert.That(otherPanel.activeSelf, Is.True, "the text's new parent is never looked up");
                Assert.That(Get<GameObject>(scene.Mixer, "m_backingPanel"), Is.SameAs(scene.Panel));
            });
        }

        // 0x7a6229..0x7a6258: the binding is used only when it is a TextMeshProUGUI (isinst); anything else, or nothing,
        // is a Unity-null text and the frame ends before any input is read.
        [Test]
        public void ADifferentOrMissingBindingIsIgnored()
        {
            Owned.Run(owned =>
            {
                MixerScene scene = NewScene(owned, ("line", 1f, Color.red));
                Assert.DoesNotThrow(() => Process(scene, null));
                Assert.DoesNotThrow(() => Process(scene, "not a text component"));
                Assert.That(Get<int>(scene.Mixer, "m_inputCount"), Is.EqualTo(0), "no input was read");
                Assert.That(Get<string>(scene.Mixer, "m_currentText"), Is.EqualTo(string.Empty));
                Assert.That(scene.Panel.activeSelf, Is.True);
            });
        }

        // SubtitleClip.CreatePlayable 0x7A5E60 outside play mode: the localised string is the plain table entry or
        // the empty string (StringExists 0x1a36e10 is false without a table), the editor text and background
        // colour are copied, and the playable holds a cloned SubtitleBehaviour.
        [Test]
        public void ClipCreatesABehaviourWithTheEditModeTextAndAuthoredBackground()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<DataManager>();
                SubtitleClip clip = owned.Track(ScriptableObject.CreateInstance<SubtitleClip>());
                Assert.That(Get<Color>(clip, "m_backgroundColour"), Is.EqualTo(new Color(0f, 0f, 0f, 0.7f)), "the constructor default");
                Author(clip, serialized =>
                {
                    serialized.FindProperty("m_subtitleTextEditor").stringValue = "preview line";
                    serialized.FindProperty("m_backgroundColour").colorValue = new Color(0.1f, 0.2f, 0.3f, 0.4f);
                });
                Playable playable = clip.CreatePlayable(NewGraph(owned), null);
                Assert.That(playable.GetPlayableType(), Is.EqualTo(typeof(SubtitleBehaviour)));
                SubtitleBehaviour behaviour = ((ScriptPlayable<SubtitleBehaviour>)playable).GetBehaviour();
                Assert.That(behaviour.SubtitleText, Is.EqualTo(string.Empty));
                Assert.That(behaviour.SubtitleTextEditor, Is.EqualTo("preview line"));
                Assert.That(behaviour.BackgroundColour.r, Is.EqualTo(0.1f).Within(1e-6f));
                Assert.That(behaviour.BackgroundColour.g, Is.EqualTo(0.2f).Within(1e-6f));
                Assert.That(behaviour.BackgroundColour.b, Is.EqualTo(0.3f).Within(1e-6f));
                Assert.That(behaviour.BackgroundColour.a, Is.EqualTo(0.4f).Within(1e-6f));
            });
        }

        // SubtitleTrack.CreateTrackMixer 0x7A64E0: a ScriptPlayable<SubtitleMixer> with the supplied input count.
        [Test]
        public void TrackCreatesASubtitleMixerWithTheRequestedInputCount()
        {
            Owned.Run(owned =>
            {
                SubtitleTrack track = owned.Track(ScriptableObject.CreateInstance<SubtitleTrack>());
                Playable playable = track.CreateTrackMixer(NewGraph(owned), null, 3);
                Assert.That(playable.GetPlayableType(), Is.EqualTo(typeof(SubtitleMixer)));
                Assert.That(playable.GetInputCount(), Is.EqualTo(3));
            });
        }

        // The track attributes in metadata order: Il2Cpp ArrayBoundsChecks, TrackBindingType(TextMeshProUGUI),
        // TrackClipType(SubtitleClip), Il2Cpp NullChecks.
        [Test]
        public void TrackKeepsItsBindingAndClipTypes()
        {
            Assert.That(typeof(SubtitleTrack).GetCustomAttributesData().Select(data => data.AttributeType.Name).ToArray(),
                Is.EqualTo(new[] { "Il2CppSetOptionAttribute", "TrackBindingTypeAttribute", "TrackClipTypeAttribute", "Il2CppSetOptionAttribute" }));
            Assert.That(typeof(SubtitleTrack).GetCustomAttribute<TrackBindingTypeAttribute>().type, Is.EqualTo(typeof(TextMeshProUGUI)));
            Assert.That(typeof(SubtitleTrack).GetCustomAttribute<TrackClipTypeAttribute>().inspectedType, Is.EqualTo(typeof(SubtitleClip)));
        }
    }
}
