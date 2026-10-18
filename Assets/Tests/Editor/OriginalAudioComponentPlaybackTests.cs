using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.OwnedAudioServices;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // Game.Runtime audio components (TypeDefs 0x02000050, 0x020001a4..0x020001c0, 0x0200082f,
    // 0x02000839) run against the owned AudioManager of OwnedAudioServices. What the manager does
    // with a request is read from its own diagnostics: clip 0 reports "Will not play the given
    // audio clip at source <source>, no clip was specified." (AudioManager.PlayOneShotAtSource),
    // and a declared clip sent to a source that was never added reports the missing source twice,
    // once while marking it loading and once naming the clip (HLAudioSourceHandler.PlayOneShot).
    // Addresses are the 1.10.1 x86-64 bodies.
    public sealed class OriginalAudioComponentPlaybackTests
    {
        private static readonly HLAudioClipIdentifier ClipA = (HLAudioClipIdentifier)0x1A01;
        private static readonly HLAudioClipIdentifier ClipB = (HLAudioClipIdentifier)0x1A02;
        private static readonly HLAudioClipIdentifier ClipC = (HLAudioClipIdentifier)0x1A03;
        private static readonly HLAudioClipIdentifier Unknown = (HLAudioClipIdentifier)0x1A0F;
        private const string NoClipAtSfx = "Will not play the given audio clip at source Sfx, no clip was specified.:0";
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private static string[] OneShotAtMissingSfx(string clipName) => new[]
        {
            "Cannot load clip, the source Sfx does not exist.:0",
            "Cannot play one shot of audio clip " + clipName + ", the source Sfx does not exist.:0",
        };

        private static Type[] Args(params Type[] types) => types;

        private static int Draw(int seed, int count)
        {
            Random.InitState(seed);
            return Random.Range(0, count);
        }

        private static Random.State AfterIntDraw(int seed, int count)
        {
            Random.InitState(seed);
            Random.Range(0, count);
            return Random.state;
        }

        // Runs a case that seeds UnityEngine.Random and checks the generator is
        // left as the case found it. OwnedAudioServices restores the state it
        // saw when constructed, so every seed (oracle draws included) is taken
        // inside its using block.
        private static void KeepingRandomState(Action body)
        {
            Random.State before = Random.state;
            try
            {
                body();
            }
            finally
            {
                Random.State after = Random.state;
                Random.state = before;
                Assert.That(after, Is.EqualTo(before), "UnityEngine.Random.state is put back");
            }
        }

        // HLAudioSfxBase.PlayOneShotAtSource 0x6ED2E0. With m_useCooldown (+0x24) set,
        // elapsed = unscaledTime - m_lastTriggeredTimeSeconds (+0x2C, subss 0x6ED316) is compared
        // with ucomiss m_cooldownSeconds, elapsed and jae to the return (0x6ED323): the request is
        // dropped when cooldown >= elapsed, ordered. After the request (0x6ED34E) the time is
        // stamped only with the cooldown on (0x6ED353..0x6ED360). Infinite operands make elapsed
        // exact: -inf against -inf separates the inclusive drop from a strict one, NaN separates
        // the ordered compare from !(elapsed > cooldown), and with no cooldown nothing is stamped.
        [Test]
        public void SfxCooldownDropsUpToTheWindowAndLetsNaNThrough()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                HLAudioSfxBase sfx = owned.Add<HLAudioSfxBase>();
                Set(sfx, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                var rows = new (bool useCooldown, float last, float cooldown, bool plays, bool stamps)[]
                {
                    (false, float.PositiveInfinity, float.NegativeInfinity, true, false),
                    (true, float.PositiveInfinity, float.NegativeInfinity, false, false),
                    (true, float.PositiveInfinity, float.NaN, true, true),
                    (true, float.NegativeInfinity, float.MaxValue, true, true),
                    (true, float.NegativeInfinity, float.PositiveInfinity, false, false),
                };
                foreach (var row in rows)
                {
                    string label = row.useCooldown + "/" + row.last + "/" + row.cooldown;
                    owned.Log.Errors.Clear();
                    Set(sfx, "m_useCooldown", row.useCooldown);
                    Set(sfx, "m_lastTriggeredTimeSeconds", row.last);
                    Set(sfx, "m_cooldownSeconds", row.cooldown);
                    float before = Time.unscaledTime;
                    sfx.PlayOneShotAtSource((HLAudioClipIdentifier)0);
                    float after = Time.unscaledTime;
                    Assert.That(owned.Log.Errors, Is.EqualTo(row.plays ? new[] { NoClipAtSfx } : new string[0]), label);
                    var last = (float)Get(sfx, "m_lastTriggeredTimeSeconds");
                    if (row.stamps) Assert.That(last, Is.InRange(before, after), label);
                    else Assert.That(last, Is.EqualTo(row.last), label);
                }
            }
        }

        // HLAudioSfxBase.StopAudioSource 0x6ED370 and IsPlaying 0x6ED3C0 both Get the manager
        // first (0x6ED3A3, 0x6ED3FB), so an unregistered manager faults. IsPlaying is false for a
        // source the manager does not hold (je 0x6ED415) and otherwise reads the source's
        // AudioSource.isPlaying (0x6ED421); StopAudioSource stops a held source.
        [Test]
        public void SfxStopAndIsPlayingReadTheSourceThroughTheManager()
        {
            using (var owned = new OwnedAudioServices())
            {
                HLAudioSfxBase sfx = owned.Add<HLAudioSfxBase>();
                Set(sfx, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                var stopFault = Assert.Throws<NullReferenceException>(sfx.StopAudioSource);
                Assert.That(stopFault.Message, Is.EqualTo("System 'HardlightProject.AudioManager' is null"));
                var playingFault = Assert.Throws<NullReferenceException>(() => sfx.IsPlaying());
                Assert.That(playingFault.Message, Is.EqualTo("System 'HardlightProject.AudioManager' is null"));

                owned.RegisterAudioManager();
                Assert.That(sfx.IsPlaying(), Is.False, "no Sfx source");
                Assert.DoesNotThrow(sfx.StopAudioSource, "a missing source is not stopped");

                AudioSource source = owned.AddSource(HLAudioSourceIdentifier.Sfx);
                Assert.That(sfx.IsPlaying(), Is.EqualTo(source.isPlaying));
                sfx.StopAudioSource();
                Assert.That(source.isPlaying, Is.False);
                Assert.That(sfx.IsPlaying(), Is.False);
                Assert.That(owned.Log.Errors, Is.Empty);
            }
        }

        // HLAudioSfxComponent.PlayOneShotAtSource 0x6ED4D0 forwards m_audioClipIdentifier (+0x38)
        // to the base request; SetClip 0x6ED4E0 only stores the identifier.
        [Test]
        public void SfxComponentRequestsItsClipAndSetClipReplacesIt()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                owned.Clip(ClipA, "lucid-sfx-a");
                owned.Clip(ClipB, "lucid-sfx-b");
                HLAudioSfxComponent sfx = owned.Add<HLAudioSfxComponent>();
                Set(sfx, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                Set(sfx, "m_audioClipIdentifier", ClipA);
                sfx.PlayOneShotAtSource();
                Assert.That(owned.Log.Errors, Is.EqualTo(OneShotAtMissingSfx("lucid-sfx-a")));

                owned.Log.Errors.Clear();
                sfx.SetClip(ClipB);
                Assert.That(owned.Log.Errors, Is.Empty, "SetClip requests nothing");
                sfx.PlayOneShotAtSource();
                Assert.That(owned.Log.Errors, Is.EqualTo(OneShotAtMissingSfx("lucid-sfx-b")));
            }
        }

        // HLAudioSfxComponentButton: OnEnable 0x6ED570 adds PlayOneShotAtSource() to
        // m_button.onClick (Button+0x100) and OnDisable 0x6ED5F0 removes it; Reset 0x6ED670 takes
        // GetComponent<Button>.
        [Test]
        public void ButtonClicksRequestTheClipOnlyWhileEnabled()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                HLAudioSfxComponentButton sfx = owned.Add<HLAudioSfxComponentButton>();
                Button button = sfx.GetComponent<Button>();
                Assert.That(button, Is.Not.Null, "RequireComponent adds the button");
                Set(sfx, "m_button", null);
                Call(typeof(HLAudioSfxComponentButton), sfx, "Reset");
                Assert.That(Get(sfx, "m_button"), Is.SameAs(button));
                Set(sfx, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);

                button.onClick.Invoke();
                Assert.That(owned.Log.Errors, Is.Empty, "no listener before OnEnable");
                Call(typeof(HLAudioSfxComponentButton), sfx, "OnEnable");
                button.onClick.Invoke();
                Assert.That(owned.Log.Errors, Is.EqualTo(new[] { NoClipAtSfx }));
                Call(typeof(HLAudioSfxComponentButton), sfx, "OnDisable");
                button.onClick.Invoke();
                Assert.That(owned.Log.Errors, Is.EqualTo(new[] { NoClipAtSfx }), "OnDisable removed the listener");
            }
        }

        // HLAudioSfxComponentToggle.PlayOneShotAtSource(bool) 0x6ED880: true sends
        // m_audioClipIdentifier (+0x38); false sends m_offAudioClipIdentifier (+0x48) only when it
        // is not zero (test/je 0x6ED894). OnEnable 0x6ED740 and OnDisable 0x6ED7E0 add and remove
        // it on UIToggle.OnValueChanged (+0x58).
        [Test]
        public void ToggleOnRequestsTheClipAndOffOnlyASetOffClip()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                owned.Clip(ClipA, "lucid-toggle-on");
                owned.Clip(ClipB, "lucid-toggle-off");
                HLAudioSfxComponentToggle sfx = owned.Add<HLAudioSfxComponentToggle>();
                UIToggle toggle = sfx.GetComponent<UIToggle>();
                Assert.That(toggle, Is.Not.Null, "RequireComponent adds the toggle");
                Set(sfx, "m_toggle", null);
                Call(typeof(HLAudioSfxComponentToggle), sfx, "Reset");
                Assert.That(Get(sfx, "m_toggle"), Is.SameAs(toggle));
                Set(sfx, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                Set(sfx, "m_audioClipIdentifier", ClipA);
                Call(typeof(HLAudioSfxComponentToggle), sfx, "OnEnable");

                toggle.OnValueChanged.Invoke(true);
                Assert.That(owned.Log.Errors, Is.EqualTo(OneShotAtMissingSfx("lucid-toggle-on")));
                owned.Log.Errors.Clear();
                toggle.OnValueChanged.Invoke(false);
                Assert.That(owned.Log.Errors, Is.Empty, "off without an off clip is silent");
                Set(sfx, "m_offAudioClipIdentifier", ClipB);
                toggle.OnValueChanged.Invoke(false);
                Assert.That(owned.Log.Errors, Is.EqualTo(OneShotAtMissingSfx("lucid-toggle-off")));

                owned.Log.Errors.Clear();
                Call(typeof(HLAudioSfxComponentToggle), sfx, "OnDisable");
                toggle.OnValueChanged.Invoke(true);
                Assert.That(owned.Log.Errors, Is.Empty, "OnDisable removed the listener");
            }
        }

        // HLAudioSfxRandomisedComponent.PlayOneShotAtSource 0x6ED970 sends
        // m_randomAudioClipIdentifiers[Random.Range(0, Length)] (0x6ED986). The randomised toggle
        // 0x6EDB60 returns at once for false (test/je 0x6EDB62) and otherwise inlines that pick.
        [Test]
        public void RandomisedComponentsPickOneIdentifierPerRequest() => KeepingRandomState(() =>
        {
            const int seed = 0x5EED;
            string[] names = { "lucid-random-a", "lucid-random-b", "lucid-random-c" };
            using (var owned = new OwnedAudioServices())
            {
                int index = Draw(seed, 3);
                Random.State afterDraw = AfterIntDraw(seed, 3);
                owned.RegisterAudioManager();
                owned.Clip(ClipA, names[0]);
                owned.Clip(ClipB, names[1]);
                owned.Clip(ClipC, names[2]);
                HLAudioSfxRandomisedComponent sfx = owned.Add<HLAudioSfxRandomisedComponent>();
                Set(sfx, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                Set(sfx, "m_randomAudioClipIdentifiers", new[] { ClipA, ClipB, ClipC });
                Random.InitState(seed);
                sfx.PlayOneShotAtSource();
                Assert.That(owned.Log.Errors, Is.EqualTo(OneShotAtMissingSfx(names[index])));
                Assert.That(Random.state, Is.EqualTo(afterDraw), "one Random.Range(0, 3)");

                owned.Log.Errors.Clear();
                HLAudioSfxRandomisedComponentToggle toggled = owned.Add<HLAudioSfxRandomisedComponentToggle>();
                UIToggle toggle = owned.Add<UIToggle>();
                Set(toggled, "m_toggle", toggle);
                Set(toggled, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                Set(toggled, "m_randomAudioClipIdentifiers", new[] { ClipA, ClipB, ClipC });
                Call(typeof(HLAudioSfxRandomisedComponentToggle), toggled, "OnEnable");
                Random.InitState(seed);
                Random.State seeded = Random.state;
                toggle.OnValueChanged.Invoke(false);
                Assert.That(owned.Log.Errors, Is.Empty);
                Assert.That(Random.state, Is.EqualTo(seeded), "false draws nothing");
                toggle.OnValueChanged.Invoke(true);
                Assert.That(owned.Log.Errors, Is.EqualTo(OneShotAtMissingSfx(names[index])));
                Assert.That(Random.state, Is.EqualTo(afterDraw));
                Call(typeof(HLAudioSfxRandomisedComponentToggle), toggled, "OnDisable");
                owned.Log.Errors.Clear();
                toggle.OnValueChanged.Invoke(true);
                Assert.That(owned.Log.Errors, Is.Empty, "OnDisable removed the listener");
            }
        });

        // HLAudio3DComponent: Start 0x6688B0 stores GetSystemRef<AudioManager>. PlayOneShotAtSource
        // 0x668930 gets the manager, moves the source through the inlined MoveAudioSource
        // (HLAudioSourceHandler.MoveTo 0x668990) to transform.position, then requests the one-shot
        // (0x6689B3) with pan 0 and volume 1. A zero clip still moves the source first; a declared
        // source is marked loading for a clip the handler cannot supply.
        [Test]
        public void SpatialComponentMovesTheSourceBeforeRequestingTheOneShot()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                AudioSource source = owned.AddSource(HLAudioSourceIdentifier.Sfx);
                HLAudio3DComponent spatial = owned.Add<HLAudio3DComponent>();
                Set(spatial, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                Assert.That(Get(spatial, "m_audioManagerRef"), Is.Null, "acquired in Start");
                Call(typeof(HLAudio3DComponent), spatial, "Start");
                Assert.That(Get(spatial, "m_audioManagerRef"), Is.SameAs(owned.TypedReferences<AudioManager>()[typeof(AudioManager).ToString()]));

                spatial.transform.position = new Vector3(3f, 4f, 5f);
                spatial.PlayOneShotAtSource();
                Assert.That(source.transform.position, Is.EqualTo(new Vector3(3f, 4f, 5f)));
                Assert.That(owned.Log.Errors, Is.EqualTo(new[] { NoClipAtSfx }));
                Assert.That(owned.Source(HLAudioSourceIdentifier.Sfx).IsPlayingOrLoadingOneShot(), Is.False);

                owned.Log.Errors.Clear();
                spatial.transform.position = new Vector3(-1f, 0f, 2f);
                Set(spatial, "m_audioClipIdentifier", Unknown);
                spatial.PlayOneShotAtSource();
                Assert.That(source.transform.position, Is.EqualTo(new Vector3(-1f, 0f, 2f)));
                Assert.That(owned.Log.Errors, Is.Empty);
                Assert.That(owned.Source(HLAudioSourceIdentifier.Sfx).IsPlayingOrLoadingOneShot(), Is.True, "the request marked the source loading");
            }
        }

        // HLAudioAmbientComponent.Start 0x6EA240 returns for clip 0 (cmp/je 0x6EA29D) before
        // GetSystemRef; otherwise it waits on the reference with OnAudioManagerReady 0x6EA340,
        // whose lambda <OnAudioManagerReady>b__7_0 0x6EA560 ignores a null clip (op_Equality
        // 0x6EA5AC), stores it in m_clip (+0x40) and, with m_playOnAwake (+0x31), plays it. Play
        // 0x6EA3D0 assigns the clip and, only with m_startPlaybackAtRandomSeekTime (+0x30), draws
        // Random.Range(0, length) (0x6EA45D).
        [Test]
        public void AmbientLoadsItsClipAndPlaysItOnAwakeOnly()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                AudioClip clip = owned.Clip(ClipA, "lucid-ambient");

                HLAudioAmbientComponent none = owned.Add<HLAudioAmbientComponent>();
                Call(typeof(HLAudioAmbientComponent), none, "Start");
                Assert.That(Get(none, "m_audioManagerRef"), Is.Null, "clip 0 never acquires the reference");

                HLAudioAmbientComponent ambient = owned.Add<HLAudioAmbientComponent>();
                AudioSource source = ambient.GetComponent<AudioSource>();
                Set(ambient, "m_audioSource", source);
                Set(ambient, "m_audioClipIdentifier", ClipA);
                Random.InitState(7);
                Random.State seeded = Random.state;
                Call(typeof(HLAudioAmbientComponent), ambient, "Start");
                Assert.That(Get(ambient, "m_audioManagerRef"), Is.SameAs(owned.TypedReferences<AudioManager>()[typeof(AudioManager).ToString()]));
                Assert.That(Get(ambient, "m_clip"), Is.SameAs(clip));
                Assert.That(source.clip, Is.SameAs(clip), "played on awake");
                Assert.That(Random.state, Is.EqualTo(seeded), "no seek without the random start");

                HLAudioAmbientComponent waiting = owned.Add<HLAudioAmbientComponent>();
                AudioSource waitingSource = waiting.GetComponent<AudioSource>();
                Set(waiting, "m_audioSource", waitingSource);
                Set(waiting, "m_audioClipIdentifier", ClipA);
                Set(waiting, "m_playOnAwake", false);
                Call(typeof(HLAudioAmbientComponent), waiting, "Start");
                Assert.That(Get(waiting, "m_clip"), Is.SameAs(clip));
                Assert.That(waitingSource.clip, Is.Null, "not played without m_playOnAwake");
                waiting.PlayAudio();
                Assert.That(waitingSource.clip, Is.SameAs(clip), "PlayAudio plays the held clip");

                AudioClip held = owned.Clip(ClipB, "lucid-ambient-held");
                Set(waiting, "m_clip", held);
                Set(waiting, "m_audioClipIdentifier", Unknown);
                Set(waiting, "m_playOnAwake", true);
                Call(typeof(HLAudioAmbientComponent), waiting, "OnAudioManagerReady", Args(typeof(AudioManager)), owned.Audio);
                Assert.That(Get(waiting, "m_clip"), Is.SameAs(held), "a null clip keeps the held one");
                Assert.That(waitingSource.clip, Is.SameAs(clip), "and plays nothing");
            }
        }

        // Play 0x6EA3D0 with m_startPlaybackAtRandomSeekTime draws exactly one
        // Random.Range(0f, clip.length); Play and StopAudio 0x6EA490 return when the source is
        // Unity-null (op_Equality 0x6EA41F, 0x6EA4D9), so a destroyed source is skipped rather
        // than dereferenced; Reset 0x6EA500 takes GetComponent<AudioSource>.
        [Test]
        public void AmbientSeekDrawsOnceAndADestroyedSourceIsSkipped()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                AudioClip clip = owned.Clip(ClipA, "lucid-ambient-seek");
                HLAudioAmbientComponent ambient = owned.Add<HLAudioAmbientComponent>();
                AudioSource source = ambient.GetComponent<AudioSource>();
                Set(ambient, "m_audioSource", null);
                Call(typeof(HLAudioAmbientComponent), ambient, "Reset");
                Assert.That(Get(ambient, "m_audioSource"), Is.SameAs(source));

                Set(ambient, "m_clip", clip);
                Set(ambient, "m_startPlaybackAtRandomSeekTime", true);
                Random.InitState(11);
                Random.Range(0f, clip.length);
                Random.State afterSeek = Random.state;
                Random.InitState(11);
                ambient.PlayAudio();
                Assert.That(source.clip, Is.SameAs(clip));
                Assert.That(Random.state, Is.EqualTo(afterSeek), "one Random.Range(0, length)");

                AudioSource destroyed = owned.Add<AudioSource>();
                Object.DestroyImmediate(destroyed);
                Set(ambient, "m_audioSource", destroyed);
                Assert.DoesNotThrow(ambient.PlayAudio);
                Assert.DoesNotThrow(ambient.StopAudio);
                Set(ambient, "m_audioSource", null);
                Assert.DoesNotThrow(ambient.PlayAudio);
                Assert.DoesNotThrow(ambient.StopAudio);
            }
        }

        // HLAudioAssignClipToSource.Awake 0x6EA5E0 waits on the readonly reference with
        // OnAudioManagerValid 0x6EA670; its lambda <OnAudioManagerValid>b__5_0 0x6EA7C0 assigns
        // the loaded clip with no null check (set_clip 0x6EA7D3). A wait started before the
        // manager is registered completes on registration.
        [Test]
        public void AssignClipWaitsForTheManagerAndAssignsEvenANullClip()
        {
            using (var owned = new OwnedAudioServices())
            {
                HLAudioAssignClipToSource assign = owned.Add<HLAudioAssignClipToSource>();
                AudioSource source = owned.Add<AudioSource>(assign.gameObject);
                Set(assign, "m_audioSource", source);
                Set(assign, "m_identifier", ClipA);
                Call(typeof(HLAudioAssignClipToSource), assign, "Awake");
                Assert.That(source.clip, Is.Null, "no manager yet");

                AudioClip clip = owned.Clip(ClipA, "lucid-assign");
                owned.RegisterAudioManager();
                Assert.That(source.clip, Is.SameAs(clip), "registration completes the wait");

                Set(assign, "m_identifier", Unknown);
                Call(typeof(HLAudioAssignClipToSource), assign, "Awake");
                Assert.That(source.clip, Is.Null, "a null clip replaces the assigned one");
                Call(typeof(HLAudioAssignClipToSource), assign, "Reset");
                Assert.That(Get(assign, "m_audioSource"), Is.SameAs(source));
            }
        }

        // HLAudioChangeMusic.Awake 0x6EA800 waits for the manager into m_audioManager (+0x38,
        // OnAudioManagerValid 0x6EA890). Action_PlayClip 0x6EA8B0 and PlayClip 0x6EA960 both call
        // PlayClipAtSource(m_clipID, m_sourceID, false, transition) with a Nullable built from
        // m_audioSourceTransition (+0x2C) only when m_useCustomTransition (+0x28) is set and null
        // otherwise (xor r8d 0x6EA942). The handler fades on (transition ?? source) == Fade, so the
        // four rows separate a null from an explicit None (flag clear over a fading source still
        // fades) and from an always-passed field (flag clear over a plain source swaps at once).
        // A fade starts through CoroutineUtils.RunCoroutine, which the leased inactive host
        // refuses, so a fading row leaves the clip unassigned; startAtRandomTime false leaves
        // Random alone.
        [Test]
        public void ChangeMusicUsesItsTransitionOnlyWhenTheCustomFlagIsSet()
        {
            using (var owned = new OwnedAudioServices())
            {
                HLAudioChangeMusic music = owned.Add<HLAudioChangeMusic>();
                Call(typeof(HLAudioChangeMusic), music, "Awake");
                Assert.That(Get(music, "m_audioManager"), Is.Null, "no manager yet");
                AudioClip clip = owned.Clip(ClipA, "lucid-music");
                AudioManager audio = owned.RegisterAudioManager();
                Assert.That(Get(music, "m_audioManager"), Is.SameAs(audio));
                owned.RefusingCoroutineHost();

                AudioSource plain = owned.AddSource(HLAudioSourceIdentifier.Background, HLAudioSourceTransition.None);
                AudioSource fading = owned.AddSource(HLAudioSourceIdentifier.Jukebox, HLAudioSourceTransition.Fade);
                Set(music, "m_clipID", ClipA);
                Random.InitState(3);
                Random.State seeded = Random.state;

                var rows = new (HLAudioSourceIdentifier source, bool custom, HLAudioSourceTransition transition, bool fades)[]
                {
                    (HLAudioSourceIdentifier.Background, false, HLAudioSourceTransition.Fade, false),
                    (HLAudioSourceIdentifier.Jukebox, false, HLAudioSourceTransition.None, true),
                    (HLAudioSourceIdentifier.Jukebox, true, HLAudioSourceTransition.None, false),
                    (HLAudioSourceIdentifier.Background, true, HLAudioSourceTransition.Fade, true),
                };
                var plays = new (string name, Action play)[]
                {
                    ("Action_PlayClip", music.Action_PlayClip),
                    ("PlayClip", () => Call(typeof(HLAudioChangeMusic), music, "PlayClip")),
                };
                foreach (var play in plays)
                foreach (var row in rows)
                {
                    string label = play.name + " " + row.source + " custom=" + row.custom + " " + row.transition;
                    AudioSource target = row.source == HLAudioSourceIdentifier.Background ? plain : fading;
                    plain.clip = null;
                    fading.clip = null;
                    Set(music, "m_sourceID", row.source);
                    Set(music, "m_useCustomTransition", row.custom);
                    Set(music, "m_audioSourceTransition", row.transition);
                    if (row.fades) LogAssert.Expect(LogType.Error, RefusedCoroutine);
                    play.play();
                    Assert.That(target.clip, row.fades ? Is.Null : Is.SameAs(clip), label);
                }
                Assert.That(owned.Source(HLAudioSourceIdentifier.Background).CurrentAudio, Is.EqualTo(ClipA));
                Assert.That(owned.Source(HLAudioSourceIdentifier.Jukebox).CurrentAudio, Is.EqualTo(ClipA));
                Assert.That(Random.state, Is.EqualTo(seeded));
                Assert.That(owned.Log.Errors, Is.Empty);
            }
        }

        // HLAudioChangeMusicOnCharacterTrigger.Awake 0x6EAA90 subscribes OnCharacterTrigger to
        // m_characterEventTrigger.OnTriggerEnter (add_OnTriggerEnter 0x6EAAFA) before the inlined
        // base wait (0x6EAB33), so a missing trigger fails before the manager is taken. The
        // handler 0x6EAB80 is the inlined PlayClip.
        [Test]
        public void CharacterTriggerSubscribesBeforeWaitingAndPlaysOnEntry()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.ReadRow<CharacterManager>();
                AudioManager audio = owned.RegisterAudioManager();
                AudioClip clip = owned.Clip(ClipA, "lucid-trigger-music");
                AudioSource source = owned.AddSource(HLAudioSourceIdentifier.Background);

                HLAudioChangeMusicOnCharacterTrigger missing = owned.Add<HLAudioChangeMusicOnCharacterTrigger>();
                Assert.Throws<NullReferenceException>(() => Call(typeof(HLAudioChangeMusicOnCharacterTrigger), missing, "Awake"));
                Assert.That(Get(missing, "m_audioManager"), Is.Null, "the subscription comes first");

                GameObject host = owned.Host("character trigger");
                host.AddComponent<BoxCollider>();
                CharacterEventTrigger trigger = host.AddComponent<CharacterEventTrigger>();
                HLAudioChangeMusicOnCharacterTrigger music = owned.Add<HLAudioChangeMusicOnCharacterTrigger>();
                Set(music, "m_characterEventTrigger", trigger);
                Set(music, "m_clipID", ClipA);
                Set(music, "m_sourceID", HLAudioSourceIdentifier.Background);
                Call(typeof(HLAudioChangeMusicOnCharacterTrigger), music, "Awake");
                Assert.That(Get(music, "m_audioManager"), Is.SameAs(audio));
                Assert.That(source.clip, Is.Null);

                Call(typeof(CharacterEventTrigger), trigger, "DoTriggerEnter", Args(typeof(Character)), new object[] { null });
                Assert.That(source.clip, Is.SameAs(clip), "entry plays the clip");
                Assert.That(owned.Log.Errors, Is.Empty);
            }
        }

        // HLAudioOneShotOnSource.LoadClips 0x6EC120 returns once m_loadClipsCalled (+0x48) is set;
        // otherwise it gets the manager once (0x6EC17A), requests every identifier with the
        // <LoadClips>b__10_0 lambda 0x6EC6A0 (which appends the clip unchecked, null included) and
        // sets the flag (0x6EC1DB). Awake 0x6EC100 loads only with m_preloadAudio (+0x34).
        [Test]
        public void OneShotOnSourcePreloadsEveryIdentifierOnce()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.RegisterAudioManager();
                AudioClip clip = owned.Clip(ClipA, "lucid-on-source");
                HLAudioOneShotOnSource idle = owned.Add<HLAudioOneShotOnSource>();
                Set(idle, "m_oneShotIds", new[] { ClipA });
                Set(idle, "m_preloadAudio", false);
                Call(typeof(HLAudioOneShotOnSource), idle, "Awake");
                Assert.That((ICollection)Get(idle, "m_loadedClips"), Is.Empty, "no preload");
                Assert.That(Get(idle, "m_loadClipsCalled"), Is.False);

                HLAudioOneShotOnSource preload = owned.Add<HLAudioOneShotOnSource>();
                Set(preload, "m_oneShotIds", new[] { ClipA, Unknown, ClipA });
                Call(typeof(HLAudioOneShotOnSource), preload, "Awake");
                Assert.That((IEnumerable)Get(preload, "m_loadedClips"), Is.EqualTo(new[] { clip, null, clip }));
                Assert.That(Get(preload, "m_loadClipsCalled"), Is.True);
                Call(typeof(HLAudioOneShotOnSource), preload, "LoadClips");
                Assert.That(((ICollection)Get(preload, "m_loadedClips")).Count, Is.EqualTo(3), "a second load is skipped");
            }
        }

        // PlayOneShots 0x6EC1F0 plays at once (inlined ClipsAreLoaded, Count == Length 0x6EC236)
        // through Internal_PlayOneShots 0x6EC320: PlayMode.All plays each loaded clip, Random
        // plays m_loadedClips[Random.Range(0, Count)] (0x6EC495), and any other mode raises
        // ArgumentOutOfRangeException (0x6EC434). The loaded clips here are null, so each
        // AudioSource.PlayOneShot is counted by its null-clip warning.
        [Test]
        public void OneShotOnSourcePlaysEveryOrOneLoadedClip() => KeepingRandomState(() =>
        {
            const int seed = 0x0A0;
            using (var owned = new OwnedAudioServices())
            using (var log = new UnityLogCounter())
            {
                Random.State afterDraw = AfterIntDraw(seed, 3);
                owned.RegisterAudioManager();
                HLAudioOneShotOnSource oneShot = owned.Add<HLAudioOneShotOnSource>();
                Set(oneShot, "m_audioSource", oneShot.gameObject.AddComponent<AudioSource>());
                Set(oneShot, "m_oneShotIds", new[] { ClipA, ClipB, ClipC });
                var loaded = (List<AudioClip>)Get(oneShot, "m_loadedClips");
                loaded.AddRange(new AudioClip[] { null, null, null });
                Type mode = typeof(HLAudioOneShotOnSource).GetNestedType("PlayMode", Instance);

                Set(oneShot, "m_playMode", Enum.ToObject(mode, 0));
                oneShot.PlayOneShots();
                Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(3), "All");

                log.Lines.Clear();
                Set(oneShot, "m_playMode", Enum.ToObject(mode, 1));
                Random.InitState(seed);
                oneShot.PlayOneShots();
                Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(1), "Random");
                Assert.That(Random.state, Is.EqualTo(afterDraw));

                log.Lines.Clear();
                Set(oneShot, "m_playMode", Enum.ToObject(mode, 2));
                Assert.Throws<ArgumentOutOfRangeException>(oneShot.PlayOneShots);
                Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(0));
                Assert.That(Get(oneShot, "m_waitForLoadRoutine"), Is.Null);
                Assert.That(owned.Log.Errors, Is.Empty);
            }
        });

        // With clips still missing, PlayOneShots loads (only when m_preloadAudio is clear,
        // 0x6EC24D) and then starts <WaitForClipsToLoadThenPlay>d__12 (StartCoroutine 0x6EC2B7),
        // which an inactive owner refuses. Its MoveNext 0x6EC740 first yields a WaitUntil over
        // ClipsAreLoaded, then plays and clears m_waitForLoadRoutine (+0x50).
        [Test]
        public void OneShotOnSourceWaitsForMissingClipsBeforePlaying()
        {
            using (var owned = new OwnedAudioServices())
            using (var log = new UnityLogCounter())
            {
                owned.RegisterAudioManager();
                owned.Clip(ClipA, "lucid-on-source-wait");
                HLAudioOneShotOnSource preloaded = owned.Add<HLAudioOneShotOnSource>();
                Set(preloaded, "m_oneShotIds", new[] { ClipA });
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                preloaded.PlayOneShots();
                Assert.That(Get(preloaded, "m_loadClipsCalled"), Is.False, "a preloading owner does not load here");

                HLAudioOneShotOnSource lazy = owned.Add<HLAudioOneShotOnSource>();
                Set(lazy, "m_oneShotIds", new[] { ClipA });
                Set(lazy, "m_preloadAudio", false);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                lazy.PlayOneShots();
                Assert.That(Get(lazy, "m_loadClipsCalled"), Is.True, "a lazy owner loads before waiting");
                Assert.That(((ICollection)Get(lazy, "m_loadedClips")).Count, Is.EqualTo(1));

                Set(preloaded, "m_audioSource", preloaded.gameObject.AddComponent<AudioSource>());
                var wait = (IEnumerator)Call(typeof(HLAudioOneShotOnSource), preloaded, "WaitForClipsToLoadThenPlay");
                Assert.That(wait.MoveNext(), Is.True);
                Assert.That(wait.Current, Is.TypeOf<WaitUntil>());
                var until = (WaitUntil)wait.Current;
                Assert.That(until.keepWaiting, Is.True, "nothing loaded yet");
                ((List<AudioClip>)Get(preloaded, "m_loadedClips")).Add(null);
                Assert.That(until.keepWaiting, Is.False);
                Assert.That(wait.MoveNext(), Is.False);
                Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(1), "played once loaded");
                Assert.That(Get(preloaded, "m_waitForLoadRoutine"), Is.Null);
            }
        }

        // HLAudioRandomLoop.OnEnable 0x6ED180 stores GetSystemRef<AudioManager>, draws the
        // identifier (Random.Range 0x6ED224) before Get (0x6ED23E) and loads it with
        // <OnEnable>b__3_0 0x6ED2A0, which sets loop, then clip, then plays.
        [Test]
        public void RandomLoopPicksBeforeGettingTheManagerAndLoopsTheClip() => KeepingRandomState(() =>
        {
            const int seed = 0x1007;
            using (var owned = new OwnedAudioServices())
            {
                int index = Draw(seed, 3);
                Random.State afterDraw = AfterIntDraw(seed, 3);
                HLAudioRandomLoop early = owned.Add<HLAudioRandomLoop>();
                Set(early, "m_randomAudioClipIdentifiers", new[] { ClipA, ClipB, ClipC });
                Random.InitState(seed);
                var missing = Assert.Throws<NullReferenceException>(() => Call(typeof(HLAudioRandomLoop), early, "OnEnable"));
                Assert.That(missing.Message, Is.EqualTo("System 'HardlightProject.AudioManager' is null"));
                Assert.That(Random.state, Is.EqualTo(afterDraw), "the pick precedes Get");

                owned.RegisterAudioManager();
                AudioClip[] clips = { owned.Clip(ClipA, "lucid-loop-a"), owned.Clip(ClipB, "lucid-loop-b"), owned.Clip(ClipC, "lucid-loop-c") };
                HLAudioRandomLoop loop = owned.Add<HLAudioRandomLoop>();
                AudioSource source = owned.Add<AudioSource>(loop.gameObject);
                Set(loop, "m_audioSource", source);
                Set(loop, "m_randomAudioClipIdentifiers", new[] { ClipA, ClipB, ClipC });
                Random.InitState(seed);
                Call(typeof(HLAudioRandomLoop), loop, "OnEnable");
                Assert.That(source.loop, Is.True);
                Assert.That(source.clip, Is.SameAs(clips[index]));
                Assert.That(Get(loop, "m_audioManagerRef"), Is.SameAs(owned.TypedReferences<AudioManager>()[typeof(AudioManager).ToString()]));
            }
        });

        // PitchRepeatedAudio.Play 0x608720: pitch = exp2f(m_intervals[m_counter % Count] / 12.0)
        // (idiv 0x60875A, divss by 12.0 at 0x29A7AA4, exp2f 0x60877D), then PlayOneShot(clip, 1.0)
        // (0x60879E) and m_counter++ (0x6087A3); ResetCounter 0x6087B0 zeroes the counter. The
        // offsets are the twelve float bit patterns the constructor 0x6087C0 stores into its list
        // (0x608857, 0x6088AF, 0x60890B, ... 0x608C3D).
        [Test]
        public void PitchStepsThroughTheOffsetsAndWraps()
        {
            int[] storedBits =
            {
                0x00000000, 0x40000000, 0x40800000, 0x40A00000, 0x40E00000, 0x41100000,
                0x41300000, 0x41400000, 0x41600000, 0x41800000, 0x41880000, 0x41980000,
            };
            float[] offsets = Array.ConvertAll(storedBits, BitConverter.Int32BitsToSingle);
            using (var owned = new OwnedAudioServices())
            using (var log = new UnityLogCounter())
            {
                PitchRepeatedAudio pitch = owned.Add<PitchRepeatedAudio>();
                AudioSource source = owned.Add<AudioSource>(pitch.gameObject);
                Set(pitch, "m_audioSource", source);
                for (int i = 0; i <= offsets.Length; i++)
                {
                    pitch.Play();
                    float expected = (float)Math.Pow(2.0, offsets[i % offsets.Length] / 12.0);
                    Assert.That(source.pitch, Is.EqualTo(expected).Within(1e-6f), "play " + i);
                    Assert.That(Get(pitch, "m_counter"), Is.EqualTo(i + 1));
                }
                Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(offsets.Length + 1));

                pitch.ResetCounter();
                Assert.That(Get(pitch, "m_counter"), Is.EqualTo(0));
                Set(pitch, "m_intervals", new List<float> { 7f, -12f });
                pitch.Play();
                pitch.Play();
                Assert.That(source.pitch, Is.EqualTo(0.5f).Within(1e-6f));
                pitch.Play();
                Assert.That(source.pitch, Is.EqualTo((float)Math.Pow(2.0, 7.0 / 12.0)).Within(1e-6f), "counter 2 wraps to the first offset");
            }
        }

        // AudioSourceMixerAssigner.Awake 0x6687A0: GetSystem<DataManager>, AudioMixers (+0x110)
        // TryGetValue, then MixerGroups (+0x28) TryGetValue, and only when both hit sets the
        // source's outputAudioMixerGroup (0x668877). With no source assigned, reaching the setter
        // faults, so the fault marks exactly the both-hit row. Reset 0x668750 takes the source.
        [Test]
        public void MixerAssignerSetsTheGroupOnlyWhenMixerAndGroupAreFound()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.Row<DataManager>();
                AudioSourceMixerAssigner assigner = owned.Add<AudioSourceMixerAssigner>();
                AudioSource source = owned.Add<AudioSource>(assigner.gameObject);
                var unregistered = Assert.Throws<NullReferenceException>(() => Call(typeof(AudioSourceMixerAssigner), assigner, "Awake"));
                Assert.That(unregistered.Message, Is.EqualTo("System 'HardlightProject.DataManager' is null"));

                DataManager data = owned.Add<DataManager>();
                owned.Register(data);
                HLAudioMixerDefinition mixer = ScriptableObject.CreateInstance<HLAudioMixerDefinition>();
                owned.Own(mixer);
                var group = (HLAudioMixerGroupIdentifier)19;
                Set(assigner, "m_audioSource", null);
                Set(assigner, "m_groupIdentifier", group);

                Assert.DoesNotThrow(() => Call(typeof(AudioSourceMixerAssigner), assigner, "Awake"), "no mixer");
                data.AudioMixers[HLAudioMixerIdentifier.Main] = mixer;
                Assert.DoesNotThrow(() => Call(typeof(AudioSourceMixerAssigner), assigner, "Awake"), "no group");
                mixer.MixerGroups.Add(group, null);
                Set(assigner, "m_mixerIdentifier", (HLAudioMixerIdentifier)5);
                Assert.DoesNotThrow(() => Call(typeof(AudioSourceMixerAssigner), assigner, "Awake"), "another mixer");
                Set(assigner, "m_mixerIdentifier", HLAudioMixerIdentifier.Main);
                Assert.Throws<NullReferenceException>(() => Call(typeof(AudioSourceMixerAssigner), assigner, "Awake"), "both found");

                Call(typeof(AudioSourceMixerAssigner), assigner, "Reset");
                Assert.That(Get(assigner, "m_audioSource"), Is.SameAs(source));
                Assert.DoesNotThrow(() => Call(typeof(AudioSourceMixerAssigner), assigner, "Awake"));
                Assert.That(source.outputAudioMixerGroup, Is.Null);
            }
        }

        // AudioLowPassActivator.Awake 0x606DC0 stores GetSystemSafe<AudioManager>, which is null
        // without a system rather than a fault. The actions 0x606E40/0x606E60 tail-call
        // ActivateLowPass/RemoveLowPassFilter with m_filter (+0x20). ActivateLowPass has no
        // listener guard; InGameAudioListener returns from ActivateLowPass for an active type
        // and only looks a type up (and so faults for an unconfigured one) when it is not active,
        // while RemoveLowPassFilter looks it up only when it is active.
        [Test]
        public void LowPassActivatorForwardsItsFilterToTheListener()
        {
            const InGameAudioListener.LowFilterType filter = InGameAudioListener.LowFilterType.Transporter;
            const InGameAudioListener.LowFilterType other = InGameAudioListener.LowFilterType.ScoreAttack;
            using (var owned = new OwnedAudioServices())
            {
                AudioLowPassActivator early = owned.Add<AudioLowPassActivator>();
                Call(typeof(AudioLowPassActivator), early, "Awake");
                Assert.That(Get(early, "m_audioManager"), Is.Null, "GetSystemSafe does not fault");

                AudioManager audio = owned.RegisterAudioManager();
                AudioLowPassActivator activator = owned.Add<AudioLowPassActivator>();
                Set(activator, "m_filter", filter);
                Call(typeof(AudioLowPassActivator), activator, "Awake");
                Assert.That(Get(activator, "m_audioManager"), Is.SameAs(audio));
                Assert.Throws<NullReferenceException>(activator.Action_ActivateLowPass, "no listener");
                Assert.DoesNotThrow(activator.Action_DeactivateLowPass, "removal is guarded");

                InGameAudioListener listener = owned.Add<InGameAudioListener>();
                audio.RegisterAudioListener(listener);
                var active = (IList)Get(listener, "m_listOfSettings");
                Type settingsType = typeof(InGameAudioListener).GetNestedType("LowPassFilterSettings", Instance);
                object Settings(InGameAudioListener.LowFilterType type)
                {
                    object settings = Activator.CreateInstance(settingsType, true);
                    settingsType.GetField("Type").SetValue(settings, type);
                    return settings;
                }

                active.Add(Settings(filter));
                Assert.DoesNotThrow(activator.Action_ActivateLowPass, "the filter is already active");
                Assert.Throws<KeyNotFoundException>(activator.Action_DeactivateLowPass, "an active filter is looked up");
                active.Clear();
                active.Add(Settings(other));
                Assert.Throws<KeyNotFoundException>(activator.Action_ActivateLowPass, "an inactive filter is looked up");
                Assert.DoesNotThrow(activator.Action_DeactivateLowPass, "an inactive filter is not removed");
            }
        }

        // HLAudioOneShotShuffledLoop.Awake 0x6EC8B0 zeroes m_lastPlayedIndex, shuffles the
        // identifiers in place (ListExtensions.Shuffle 0x6EC922), caches the squared range
        // (mulss 0x6EC92C) and, only with m_disableDuringIntroSequence (+0x3C), gets the
        // LevelManager once and adds both intro handlers (0x6EC98E, 0x6EC9ED). The handlers
        // clear and set m_levelIntroSequenceHasPlayed; OnDestroy 0x6ECA80 removes them only while
        // the reference is valid; OnValidate 0x6ECA60 recaches the range.
        [Test]
        public void ShuffledLoopFollowsTheIntroSequenceOnlyWhenAskedTo() => KeepingRandomState(() =>
        {
            const int seed = 0x5AF;
            HLAudioClipIdentifier[] ids = { ClipA, ClipB, ClipC, Unknown };
            using (var owned = new OwnedAudioServices())
            {
                var shuffled = new List<HLAudioClipIdentifier>(ids);
                Random.InitState(seed);
                shuffled.Shuffle();
                Random.State afterShuffle = Random.state;
                owned.ReadRow<TimeManager>();
                owned.ReadRow<CharacterManager>();
                owned.ReadRow<MissionManager>();
                owned.ReadRow<SaveManager>();
                owned.Row<LevelManager>();

                HLAudioOneShotShuffledLoop free = owned.Add<HLAudioOneShotShuffledLoop>();
                Set(free, "m_oneShotIds", (HLAudioClipIdentifier[])ids.Clone());
                Set(free, "m_disableDuringIntroSequence", false);
                Assert.DoesNotThrow(() => Call(typeof(HLAudioOneShotShuffledLoop), free, "Awake"), "no LevelManager is needed");

                LevelManager level = owned.Add<LevelManager>();
                owned.Register(level);
                HLAudioOneShotShuffledLoop loop = owned.Add<HLAudioOneShotShuffledLoop>();
                var order = (HLAudioClipIdentifier[])ids.Clone();
                Set(loop, "m_oneShotIds", order);
                Set(loop, "m_lastPlayedIndex", 3);
                Set(loop, "m_scriptActivationRangeMeters", 3f);
                Random.InitState(seed);
                Call(typeof(HLAudioOneShotShuffledLoop), loop, "Awake");
                Assert.That(Get(loop, "m_oneShotIds"), Is.SameAs(order), "shuffled in place");
                Assert.That(order, Is.EqualTo(shuffled.ToArray()));
                Assert.That(Random.state, Is.EqualTo(afterShuffle), "one shuffle");
                Assert.That(Get(loop, "m_lastPlayedIndex"), Is.EqualTo(0));
                Assert.That(Get(loop, "m_scriptActivationRangeSq"), Is.EqualTo(9f));

                level.OnIntroSequenceComplete();
                Assert.That(Get(loop, "m_levelIntroSequenceHasPlayed"), Is.True);
                level.OnIntroSequenceBegin();
                Assert.That(Get(loop, "m_levelIntroSequenceHasPlayed"), Is.False);

                Set(loop, "m_scriptActivationRangeMeters", 0.5f);
                Call(typeof(HLAudioOneShotShuffledLoop), loop, "OnValidate");
                Assert.That(Get(loop, "m_scriptActivationRangeSq"), Is.EqualTo(0.25f));

                Call(typeof(HLAudioOneShotShuffledLoop), loop, "OnDestroy");
                Assert.That(level.OnIntroSequenceBegin, Is.Null);
                Assert.That(level.OnIntroSequenceComplete, Is.Null);
                Assert.DoesNotThrow(() => Call(typeof(HLAudioOneShotShuffledLoop), free, "OnDestroy"));
            }
        });

        // Update 0x6ECC20 returns while m_disableDuringIntroSequence (+0x3C) is set and the intro
        // has not played (+0x70, je 0x6ECC36); every other row reaches the inlined
        // CharacterInRange, whose CharacterManager reference Get (0x6ECC6E) faults on the owned
        // empty row. OnGetClip 0x6ECF20 plays the loaded clip on m_audioSource.
        [Test]
        public void ShuffledLoopUpdateIsGatedByTheIntroSequence()
        {
            using (var owned = new OwnedAudioServices())
            using (var log = new UnityLogCounter())
            {
                owned.ReadRow<TimeManager>();
                owned.ReadRow<CharacterManager>();
                owned.ReadRow<LevelManager>();
                HLAudioOneShotShuffledLoop loop = owned.Add<HLAudioOneShotShuffledLoop>();
                var rows = new (bool disable, bool played, bool reaches)[]
                {
                    (true, false, false), (true, true, true), (false, false, true), (false, true, true),
                };
                foreach (var row in rows)
                {
                    Set(loop, "m_disableDuringIntroSequence", row.disable);
                    Set(loop, "m_levelIntroSequenceHasPlayed", row.played);
                    if (!row.reaches)
                    {
                        Assert.DoesNotThrow(loop.Update, row.disable + "/" + row.played);
                        continue;
                    }
                    var reached = Assert.Throws<NullReferenceException>(loop.Update, row.disable + "/" + row.played);
                    Assert.That(reached.Message, Is.EqualTo("System 'HardlightProject.CharacterManager' is null"));
                }

                Set(loop, "m_audioSource", owned.Add<AudioSource>(loop.gameObject));
                Call(typeof(HLAudioOneShotShuffledLoop), loop, "OnGetClip", Args(typeof(HLAudioClipHandler.LoadedAudioParameters)),
                    default(HLAudioClipHandler.LoadedAudioParameters));
                Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(1));
            }
        }

        // HLAudioOneShotAmbient.Awake 0x6EBEA0 takes GetSystem<AudioManager> into +0x48 before
        // GetSystem<TimeManager> (0x6EBF2C), so an empty TimeManager row faults after the manager
        // is stored. ActorAudioHelper.Start 0x51D860 stores GetSystem<CharacterManager>.
        [Test]
        public void SystemLookupsFaultOnTheirEmptyRows()
        {
            using (var owned = new OwnedAudioServices())
            {
                owned.ReadRow<TimeManager>();
                owned.ReadRow<CharacterManager>();
                AudioManager audio = owned.RegisterAudioManager();
                HLAudioOneShotAmbient ambient = owned.Add<HLAudioOneShotAmbient>();
                var time = Assert.Throws<NullReferenceException>(() => Call(typeof(HLAudioOneShotAmbient), ambient, "Awake"));
                Assert.That(time.Message, Is.EqualTo("System 'Hardlight.TimeManager' is null"));
                Assert.That(Get(ambient, "m_audioManager"), Is.SameAs(audio));

                ActorAudioHelper helper = owned.Add<ActorAudioHelper>();
                var character = Assert.Throws<NullReferenceException>(() => Call(typeof(ActorAudioHelper), helper, "Start"));
                Assert.That(character.Message, Is.EqualTo("System 'HardlightProject.CharacterManager' is null"));
            }
        }

        // Cooldown rows shared by both TryPlayOneShot bodies. Each adds the PlayerMovement delta
        // (finite and non-negative) to the timer and returns only when cooldown > timer, ordered
        // (ucomiss cooldown, timer, then jbe to the play at 0x6EBF92 and ja to the return at
        // 0x6ECE6C). Infinite and NaN operands make the timer exact whatever the delta is:
        // +inf against +inf plays (a <= return would not), a NaN timer or cooldown plays (a
        // !(timer >= cooldown) return would not).
        private static readonly (float timer, float cooldown, bool plays)[] CooldownRows =
        {
            (0f, float.PositiveInfinity, false),
            (float.PositiveInfinity, float.PositiveInfinity, true),
            (float.NaN, 0f, true),
            (0f, float.NaN, true),
            (0f, float.NegativeInfinity, true),
        };

        // HLAudioOneShotAmbient.TryPlayOneShot 0x6EBF60 over a PlayerMovement TimeManager. A play
        // zeroes the timer (0x6EBF9E) and draws the cooldown Random.Range(min, max) (0x6EBFB1);
        // with pan and volume off no other float is drawn. The pick loop 0x6EC020..0x6EC039 draws
        // Random.Range(0, Length) and draws again only while the pick equals m_lastPlayed (+0x60)
        // and Length > 1, then stores it. With no source identifiers the request plays nowhere.
        [Test]
        public void AmbientCooldownIsOrderedAndRepicksOnlyWithAChoice() => KeepingRandomState(() =>
        {
            const int seed = 0xA3B;
            using (var owned = new OwnedAudioServices())
            {
                AudioManager audio = owned.RegisterAudioManager();
                HLAudioOneShotAmbient ambient = owned.Add<HLAudioOneShotAmbient>();
                Set(ambient, "m_audioManager", audio);
                Set(ambient, "m_timeManager", owned.PlayerMovementTime());
                Set(ambient, "m_audioSourceIdentifiers", new HLAudioSourceIdentifier[0]);
                Set(ambient, "m_randomPan", false);
                Set(ambient, "m_randomVolume", false);
                Set(ambient, "m_minWaitNewSound", 2f);
                Set(ambient, "m_maxWaitNewSound", 2f);
                Set(ambient, "m_oneShotIds", new[] { ClipA });

                Random.InitState(seed);
                Random.Range(2f, 2f);
                Random.Range(0, 1);
                Random.State afterSinglePick = Random.state;
                foreach (var row in CooldownRows)
                {
                    string label = row.timer + " against " + row.cooldown;
                    Set(ambient, "m_timeSinceLastPlay", row.timer);
                    Set(ambient, "m_randomCooldown", row.cooldown);
                    Set(ambient, "m_lastPlayed", 0);
                    Random.InitState(seed);
                    Random.State seeded = Random.state;
                    Call(typeof(HLAudioOneShotAmbient), ambient, "TryPlayOneShot");
                    if (!row.plays)
                    {
                        Assert.That(Get(ambient, "m_randomCooldown"), Is.EqualTo(float.PositiveInfinity), label);
                        Assert.That(Random.state, Is.EqualTo(seeded), label + " draws nothing");
                        continue;
                    }
                    Assert.That(Get(ambient, "m_timeSinceLastPlay"), Is.EqualTo(0f), label);
                    Assert.That(Get(ambient, "m_randomCooldown"), Is.EqualTo(2f), label);
                    // One clip: the pick equals m_lastPlayed, but Length 1 ends the loop.
                    Assert.That(Random.state, Is.EqualTo(afterSinglePick), label + " picks once");
                    Assert.That(Get(ambient, "m_lastPlayed"), Is.EqualTo(0), label);
                }

                Set(ambient, "m_oneShotIds", new[] { ClipA, ClipB, ClipC });
                Random.InitState(seed);
                Random.Range(2f, 2f);
                var picks = new List<int>();
                for (int i = 0; i < 32; i++) picks.Add(Random.Range(0, 3));
                int repeated = picks[0];
                int differing = picks.FindIndex(pick => pick != repeated);
                Assert.That(differing, Is.GreaterThan(0), "the seed yields a differing pick");
                foreach (var start in new[] { (last: repeated, draws: differing + 1, picked: picks[differing]), (last: (repeated + 1) % 3, draws: 1, picked: repeated) })
                {
                    Random.InitState(seed);
                    Random.Range(2f, 2f);
                    for (int i = 0; i < start.draws; i++) Random.Range(0, 3);
                    Random.State expected = Random.state;
                    Set(ambient, "m_timeSinceLastPlay", 0f);
                    Set(ambient, "m_randomCooldown", float.NegativeInfinity);
                    Set(ambient, "m_lastPlayed", start.last);
                    Random.InitState(seed);
                    Call(typeof(HLAudioOneShotAmbient), ambient, "TryPlayOneShot");
                    Assert.That(Get(ambient, "m_lastPlayed"), Is.EqualTo(start.picked), "last " + start.last);
                    Assert.That(Random.state, Is.EqualTo(expected), start.draws + " picks after last " + start.last);
                }
                Assert.That(owned.Log.Errors, Is.Empty);
            }
        });

        // HLAudioOneShotShuffledLoop.TryPlayOneShot 0x6ECDD0 over a PlayerMovement TimeManager.
        // After the cooldown gate it reshuffles in place and restarts at 0 only when
        // m_lastPlayedIndex (+0x68) equals Length (cmp/jne 0x6ECE79, Shuffle 0x6ECE8F), zeroes the
        // timer, draws the cooldown (0x6ECEA7), requests m_oneShotIds[m_lastPlayedIndex] with
        // OnGetClip (GetClip 0x6ECF08) and increments the index (0x6ECF0D). The identifiers are
        // undeclared, so each request plays a null clip and is counted by its warning.
        // CharacterInRange (setae over ucomiss rangeSq, distanceSq at 0x6ECDB9) is not run here:
        // it needs a current Character.
        [Test]
        public void ShuffledLoopCooldownIsOrderedAndReshufflesAtLength() => KeepingRandomState(() =>
        {
            const int seed = 0x5B1;
            using (var owned = new OwnedAudioServices())
            using (var log = new UnityLogCounter())
            {
                owned.ReadRow<TimeManager>();
                owned.ReadRow<CharacterManager>();
                owned.ReadRow<LevelManager>();
                owned.RegisterAudioManager();
                HLAudioOneShotShuffledLoop loop = owned.Add<HLAudioOneShotShuffledLoop>();
                Set(loop, "m_timeManagerRef", new SystemRef<TimeManager>(ProcessManager.GetDefaultName<TimeManager>(), owned.PlayerMovementTime()));
                Set(loop, "m_audioSource", owned.Add<AudioSource>(loop.gameObject));
                Set(loop, "m_minWaitNewSound", 2f);
                Set(loop, "m_maxWaitNewSound", 2f);
                HLAudioClipIdentifier[] order = { ClipA, ClipB, ClipC };
                Set(loop, "m_oneShotIds", order);

                foreach (var row in CooldownRows)
                {
                    string label = row.timer + " against " + row.cooldown;
                    log.Lines.Clear();
                    Set(loop, "m_timeSinceLastPlay", row.timer);
                    Set(loop, "m_randomCooldown", row.cooldown);
                    Set(loop, "m_lastPlayedIndex", 0);
                    Call(typeof(HLAudioOneShotShuffledLoop), loop, "TryPlayOneShot");
                    Assert.That(Get(loop, "m_lastPlayedIndex"), Is.EqualTo(row.plays ? 1 : 0), label);
                    Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(row.plays ? 1 : 0), label);
                    Assert.That(Get(loop, "m_randomCooldown"), Is.EqualTo(row.plays ? 2f : float.PositiveInfinity), label);
                    if (row.plays) Assert.That(Get(loop, "m_timeSinceLastPlay"), Is.EqualTo(0f), label);
                }

                foreach (int index in new[] { order.Length - 1, order.Length })
                {
                    bool reshuffles = index == order.Length;
                    var expectedOrder = new List<HLAudioClipIdentifier>(order);
                    Random.InitState(seed);
                    if (reshuffles) expectedOrder.Shuffle();
                    Random.Range(2f, 2f);
                    Random.State expected = Random.state;
                    log.Lines.Clear();
                    Set(loop, "m_timeSinceLastPlay", 0f);
                    Set(loop, "m_randomCooldown", float.NegativeInfinity);
                    Set(loop, "m_lastPlayedIndex", index);
                    Random.InitState(seed);
                    Call(typeof(HLAudioOneShotShuffledLoop), loop, "TryPlayOneShot");
                    Assert.That(Get(loop, "m_oneShotIds"), Is.SameAs(order), "index " + index);
                    Assert.That(order, Is.EqualTo(expectedOrder.ToArray()), "index " + index);
                    Assert.That(Random.state, Is.EqualTo(expected), "index " + index);
                    Assert.That(Get(loop, "m_lastPlayedIndex"), Is.EqualTo(reshuffles ? 1 : index + 1), "index " + index);
                    Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(1), "index " + index);
                }
            }
        });
    }
}
