using System;
using System.Collections;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // HardlightProject.LerpColour.SetTargetColour (0x060039a7 @0x6b1f60) on an
    // active, owned host. No shared static, registry or authored asset is
    // touched; the host is destroyed in the finally.
    public sealed class OriginalLerpColourPlayTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static FieldInfo Field(string name) => typeof(LerpColour).GetField(name, Instance);

        // 0x6b1fcf..0x6b2084: SafeStopCoroutine(ref m_lerpRoutine), then
        // d__6 is built with a fresh Colour.Value as the start (+0x28) and the
        // argument as the target (+0x38), started, stored, and only then is
        // m_valueAssigned set. The stored handle here is a test-owned stand-in
        // on the same host so a missed stop is visible as a resumed step. A
        // zero multiplier keeps the iterator at t = 0, so the colour it writes
        // is the captured start; swapping start and target writes the target.
        [UnityTest]
        public IEnumerator SetTargetColourStopsTheStoredRoutineAndLerpsFromTheCurrentValue()
        {
            var host = new GameObject("Lucid owned lerp colour host");
            try
            {
                var lerp = host.AddComponent<LerpColour>();
                Field("m_lerpSpeedMultiplier").SetValue(lerp, 0f);
                var current = new Color(0.25f, 0.5f, 0.75f, 1f);
                var target = new Color(1f, 0f, 0f, 1f);
                lerp.Colour.Value = current;

                int resumed = 0;
                Coroutine pending = lerp.StartCoroutine(Pending(() => ++resumed));
                Assert.That(resumed, Is.EqualTo(1));
                Field("m_lerpRoutine").SetValue(lerp, pending);

                lerp.SetTargetColour(target);
                object stored = Field("m_lerpRoutine").GetValue(lerp);
                Assert.That(stored, Is.Not.Null);
                Assert.That(ReferenceEquals(stored, pending), Is.False, "the new routine replaces the handle");
                Assert.That(Field("m_valueAssigned").GetValue(lerp), Is.True);
                Assert.That(lerp.Colour.Value, Is.EqualTo(current), "the first step lerps from the current value");

                yield return null;
                yield return null;
                Assert.That(resumed, Is.EqualTo(1), "the stored routine was stopped");
                Assert.That(lerp.Colour.Value, Is.EqualTo(current));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static IEnumerator Pending(Action step)
        {
            while (true)
            {
                step();
                yield return null;
            }
        }
    }
}
