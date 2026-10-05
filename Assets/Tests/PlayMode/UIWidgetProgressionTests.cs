using System.Collections;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class UIWidgetProgressionTests
    {
        [UnityTest]
        public IEnumerator OriginalAnimationRoutesUseConfiguredClipNamesAndSameLayerStop()
        {
            var go = new GameObject("Project Lucid original progression animation proof");
            AnimationClip incoming = null, outgoing = null;
            try
            {
                UIWidgetProgression widget = go.AddComponent<UIWidgetProgression>();
                Animation animation = go.AddComponent<Animation>();
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                incoming = new AnimationClip { name = "ProjectLucidIncoming", legacy = true };
                outgoing = new AnimationClip { name = "ProjectLucidOutgoing", legacy = true };
                incoming.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
                outgoing.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 1, 1, 0));
                outgoing.AddEvent(new AnimationEvent { time = 0.05f, functionName = "Action_AnimateOutComplete" });
                int completed = 0;
                UIWidgetProgression receiver = null;
                widget.AnimateOutComplete = value => { completed++; receiver = value; };
                animation.AddClip(incoming, incoming.name);
                animation.AddClip(outgoing, outgoing.name);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(UIWidgetProgression).GetField("m_animation", fields).SetValue(widget, animation);
                typeof(UIWidgetProgression).GetField("m_animateInClip", fields).SetValue(widget, incoming);
                typeof(UIWidgetProgression).GetField("m_animateOutClip", fields).SetValue(widget, outgoing);
                yield return null;
                widget.AnimateIn();
                Assert.That(animation.IsPlaying(incoming.name), Is.True);
                Assert.That(animation.IsPlaying(outgoing.name), Is.False);
                yield return null;
                widget.AnimateOut();
                Assert.That(animation.IsPlaying(outgoing.name), Is.True);
                Assert.That(animation.IsPlaying(incoming.name), Is.False);
                // A batch Editor can process hundreds of frames before 0.05 seconds
                // elapse. Wait for the real engine event using a bounded wall clock.
                float deadline = Time.realtimeSinceStartup + 2f;
                while (completed == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(completed, Is.EqualTo(1), "genuine legacy Animation event reaches the original method");
                Assert.That(receiver, Is.SameAs(widget), "event callback retains the original widget argument");
            }
            finally
            {
                Object.Destroy(go);
                if (incoming != null) Object.Destroy(incoming);
                if (outgoing != null) Object.Destroy(outgoing);
            }
            // These genuine engine routes do not establish authored UI parity.
        }
    }
}
