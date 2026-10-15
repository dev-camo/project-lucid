using System;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // An owned inactive AudioSource; nothing is played or loaded.
    public sealed class OriginalAudioSourceDataGapTests
    {
        // HLAudioSourceData.IsAtEnd 0x06000968, x86-64 0x6ee650: only the Unity
        // null test 0x6ee69f guards; then get_time 0x6ee6ae, get_clip 0x6ee6be
        // and AudioClip.get_length 0x6ee6c8 on the unchecked clip. A present
        // source without a clip faults instead of reporting false; adding a
        // clip null guard (or treating it as at the end) fails here.
        [Test]
        public void PresentSourceWithoutAClipFaultsOnTheClipLength()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("Lucid owned clipless audio source");
                owner.SetActive(false);
                AudioSource source = owner.AddComponent<AudioSource>();
                var settings = JsonUtility.FromJson<AudioFadeTransitionSettings>("{}");
                var data = new HLAudioSourceData((HLAudioSourceIdentifier)7, source, false, (HLAudioSourceTransition)0, settings);
                Assert.That(source.clip == null, Is.True);
                Assert.That(data.IsDestroyed(), Is.False);
                Assert.Throws<NullReferenceException>(() => data.IsAtEnd());

                Object.DestroyImmediate(owner);
                Assert.That(data.IsDestroyed(), Is.True);
                Assert.That(data.IsAtEnd(), Is.False, "A destroyed source returns before the clip is read.");
            }
            finally
            {
                if (owner != null) Object.DestroyImmediate(owner);
            }
        }
    }
}
