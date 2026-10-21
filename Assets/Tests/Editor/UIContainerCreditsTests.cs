using System;
using System.Collections;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCredits (Game.Runtime 0x020008a1; constructor 0x629FE0, Start 0x629F50 and its iterator MoveNext
    // 0x62A010) as a real component on an owned inactive host. The scroll parent and the two marker points are
    // RectTransforms on owned objects, and each case steps the iterator Start returns by hand.
    public sealed class UIContainerCreditsTests
    {
        private sealed class Credits
        {
            internal UIContainerCredits Container;
            internal RectTransform Parent, End, ScrollTo;
            internal int Triggered;
        }

        private static RectTransform Rect(Owned owned, string name) =>
            owned.Track(new GameObject(name, typeof(RectTransform))).GetComponent<RectTransform>();

        private static void With(float scrollSpeed, float endWaitSeconds, Action<Credits> body) => Owned.Run(owned =>
        {
            var credits = new Credits
            {
                Container = owned.Host("Lucid credits", false).AddComponent<UIContainerCredits>(),
                Parent = Rect(owned, "Lucid credits scroll parent"),
                End = Rect(owned, "Lucid credits end"),
                ScrollTo = Rect(owned, "Lucid credits scroll to"),
            };
            var trigger = new UnityEvent();
            trigger.AddListener(() => credits.Triggered++);
            // Serialized in the original.
            Set(credits.Container, "m_creditsScrollParent", credits.Parent);
            Set(credits.Container, "m_endYPoint", credits.End);
            Set(credits.Container, "m_scrollToPoint", credits.ScrollTo);
            Set(credits.Container, "m_scrollSpeed", scrollSpeed);
            Set(credits.Container, "m_endWaitSeconds", endWaitSeconds);
            Set(credits.Container, "m_onEndTrigger", trigger);
            body(credits);
        });

        // The constructor 0x629FE0 stores 1.0f (0x3f800000) in the scroll speed before the base constructor runs.
        [Test]
        public void ANewPageScrollsAtSpeedOne()
        {
            Owned.Run(owned =>
                Assert.That(Get<float>(owned.Host("Lucid credits", false).AddComponent<UIContainerCredits>(), "m_scrollSpeed"), Is.EqualTo(1f)));
        }

        // MoveNext 0x62A010 compares the y of the scroll-to point's position with the y of the end point's (the cmpltps result
        // is read from byte 4, 0x62A091), strictly: while the first is below the second the parent moves up by
        // Time.deltaTime * m_scrollSpeed (mulss 0x62A0B2, Translate 0x62A0C3) and one null is yielded. Each row's x says
        // the opposite of its y, so a comparison of x fails.
        [Test]
        public void WhileTheScrollToPointIsBelowTheEndPointTheParentMovesUpByTheFrameTimeTimesTheSpeedAndOneNullIsYielded()
        {
            With(7.5f, 2f, credits =>
            {
                credits.ScrollTo.position = new Vector3(100f, 0f, 0f);
                credits.End.position = new Vector3(0f, 10f, 0f);
                credits.Parent.position = new Vector3(0f, 3f, 0f);
                IEnumerator scroll = (IEnumerator)Call(credits.Container, "Start");

                // Time.deltaTime is the editor's own and may be zero in a frame; the case holds for any value, but a zero frame
                // cannot tell a missing Translate or a wrong speed factor apart (a precondition on it would depend on ambient editor state).
                float frameTime = Time.deltaTime;
                Assert.That(scroll.MoveNext(), Is.True);

                Assert.That(scroll.Current, Is.Null);
                Assert.That(credits.Parent.position.y, Is.EqualTo(3f + frameTime * 7.5f).Within(1e-4f));
                Assert.That(credits.Parent.position.x, Is.Zero);
                Assert.That(credits.Triggered, Is.Zero);
            });
        }

        // At or above the end height (the strict comparison fails for an equal y, 0x62A099) the parent stays put and the
        // iterator yields WaitForSeconds(m_endWaitSeconds) (0x62A12E); the row with the larger x and the smaller y is the
        // only one that scrolls.
        [Test]
        public void ReachingTheEndHeightStopsTheScrollAndWaitsTheConfiguredSeconds()
        {
            foreach ((Vector3 scrollTo, bool scrolls) in new[]
            {
                (new Vector3(0f, 10f, 0f), false),
                (new Vector3(-100f, 20f, 0f), false),
                (new Vector3(100f, 9.99f, 0f), true),
            })
            {
                With(2f, 3.5f, credits =>
                {
                    credits.ScrollTo.position = scrollTo;
                    credits.End.position = new Vector3(0f, 10f, 0f);
                    IEnumerator scroll = (IEnumerator)Call(credits.Container, "Start");

                    Assert.That(scroll.MoveNext(), Is.True);

                    if (scrolls)
                    {
                        Assert.That(scroll.Current, Is.Null, "scrolling at " + scrollTo);
                        return;
                    }
                    Assert.That(scroll.Current, Is.InstanceOf<WaitForSeconds>(), "stopped at " + scrollTo);
                    // Reflection: the seconds of an engine WaitForSeconds have no accessor.
                    Assert.That(Get<float>(scroll.Current, "m_Seconds"), Is.EqualTo(3.5f));
                    Assert.That(credits.Parent.position, Is.EqualTo(Vector3.zero));
                });
            }
        }

        // The loop compares again on every frame (the scrolling states 0 and 1 are the same code), so a point that reaches the
        // end between two steps ends the scroll at the next one; after the wait (state 2, 0x62A0E4) the trigger fires once and
        // the iterator ends without yielding again.
        [Test]
        public void TheScrollEndsOnTheFrameThePointReachesTheEndThenTheTriggerFiresOnceAfterTheWait()
        {
            With(1f, 0.5f, credits =>
            {
                credits.ScrollTo.position = new Vector3(0f, 0f, 0f);
                credits.End.position = new Vector3(0f, 10f, 0f);
                IEnumerator scroll = (IEnumerator)Call(credits.Container, "Start");

                Assert.That(scroll.MoveNext(), Is.True);
                Assert.That(scroll.Current, Is.Null);
                Assert.That(scroll.MoveNext(), Is.True);
                Assert.That(scroll.Current, Is.Null, "still below the end");

                credits.ScrollTo.position = new Vector3(0f, 10f, 0f);
                Assert.That(scroll.MoveNext(), Is.True);
                Assert.That(scroll.Current, Is.InstanceOf<WaitForSeconds>());
                Assert.That(credits.Triggered, Is.Zero, "the trigger waits for the wait to finish");

                Assert.That(scroll.MoveNext(), Is.False);
                Assert.That(credits.Triggered, Is.EqualTo(1));
                Assert.That(scroll.MoveNext(), Is.False);
                Assert.That(credits.Triggered, Is.EqualTo(1), "a finished iterator does not fire again");
            });
        }

        // The end trigger is tested for null (0x62A0F6) before it is invoked.
        [Test]
        public void AMissingEndTriggerIsSkipped()
        {
            With(1f, 0f, credits =>
            {
                // Reflection: a serialized event always exists in the editor; an unset one is the state under test.
                Set(credits.Container, "m_onEndTrigger", null);
                credits.ScrollTo.position = new Vector3(0f, 10f, 0f);
                credits.End.position = new Vector3(0f, 10f, 0f);
                IEnumerator scroll = (IEnumerator)Call(credits.Container, "Start");

                Assert.That(scroll.MoveNext(), Is.True);
                Assert.DoesNotThrow(() => Assert.That(scroll.MoveNext(), Is.False));
            });
        }

        // MoveNext reads all three transforms without a guard (0x62A054..0x62A07C raise the null reference exception).
        [Test]
        public void AMissingMarkerPointFaultsTheFirstStep()
        {
            With(1f, 0f, credits =>
            {
                Set(credits.Container, "m_endYPoint", null);
                IEnumerator scroll = (IEnumerator)Call(credits.Container, "Start");

                Assert.Throws<NullReferenceException>(() => scroll.MoveNext());
            });
        }
    }
}
