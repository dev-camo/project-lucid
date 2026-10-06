using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight.Utils;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Managed checks deliberately avoid AudioSource/MonoBehaviour engine calls.
    // Raw real-type fixtures bypass constructors; they are not valid Unity objects.
    public static class AudioSourceRuntimeVerification
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static int s_checks;
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            s_checks++;
        }
        private static object Raw(Type type)
        {
            object value = FormatterServices.GetUninitializedObject(type);
            // Coroutine's actual engine finalizer is not a managed fixture path.
            GC.SuppressFinalize(value);
            return value;
        }
        private static void Put(object target, string name, object value) => target.GetType().GetField(name, Own).SetValue(target, value);
        private static object Get(object target, string name) => target.GetType().GetField(name, Own).GetValue(target);
        private static IEnumerator Fade(HLAudioSourceData target, IEnumerator fade) =>
            (IEnumerator)typeof(HLAudioSourceData).GetMethod("PerformFade", Own).Invoke(target, new object[] { fade });
        private static IEnumerator Delay(CoroutineUtils host, Action action, float seconds) =>
            (IEnumerator)typeof(CoroutineUtils).GetMethod("DelayCoroutine", Own, null, new[] { typeof(Action), typeof(float) }, null)
                .Invoke(host, new object[] { action, seconds });
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            bool thrown = false;
            try { action(); } catch (T) { thrown = true; }
            Require(thrown, message);
        }

        public static int RunManaged()
        {
            s_checks = 0;
            var data = (HLAudioSourceData)Raw(typeof(HLAudioSourceData));
            var host = (CoroutineUtils)Raw(typeof(CoroutineUtils));
            var coroutine = (Coroutine)Raw(typeof(Coroutine));
            Type fadeType = typeof(HLAudioSourceData).GetNestedType("AudioFade", BindingFlags.NonPublic);
            object fadeState = Activator.CreateInstance(fadeType, true);
            Put(data, "m_currentFade", fadeState);

            // This actual compiled constructor guard does not call any Unity
            // engine API. The original native constructor allocates/stores its
            // AudioFade before Object::.ctor; a base-only omission must fail.
            ConstructorInfo constructor = typeof(HLAudioSourceData).GetConstructors()[0];
            byte[] il = constructor.GetMethodBody().GetILAsByteArray();
            Require(il.Length >= 17 && il[0] == 0x02 && il[1] == 0x73 && il[6] == 0x7d && il[11] == 0x02 && il[12] == 0x28 &&
                constructor.Module.ResolveMethod(BitConverter.ToInt32(il, 2)).DeclaringType == fadeType &&
                constructor.Module.ResolveField(BitConverter.ToInt32(il, 7)).Name == "m_currentFade" &&
                constructor.Module.ResolveMethod(BitConverter.ToInt32(il, 13)).DeclaringType == typeof(object),
                "exact original fade allocation/store-before-base constructor");

            Require((float)Get(fadeState, "StartingVolume") == 0 && Get(fadeState, "FadeCoroutine") == null,
                "genuine AudioFade base-only constructor defaults");
            Require(!data.IsPlayingOrLoadingOneShot(), "neither loading nor playing");
            data.SetOneShotClipLoading(true);
            Require(data.IsPlayingOrLoadingOneShot(), "loading alone is observable");
            Put(data, "m_playingOneShot", true);
            Require(data.IsPlayingOrLoadingOneShot(), "both flags are observable");
            data.SetOneShotClipLoading(false);
            Require(data.IsPlayingOrLoadingOneShot(), "playing survives loading reset");
            typeof(HLAudioSourceData).GetMethod("<PlayOneShot>b__42_0", Own).Invoke(data, null);
            Require(!data.IsPlayingOrLoadingOneShot(), "genuine one-shot callback clears playback");
            data.SetOneShotClipLoading(true);
            Put(data, "m_playingOneShot", true);
            typeof(HLAudioSourceData).GetMethod("<PlayOneShot>b__42_0", Own).Invoke(data, null);
            Require(data.IsPlayingOrLoadingOneShot() && !(bool)Get(data, "m_playingOneShot"),
                "completion retains independent loading state");

            IEnumerator nested = new List<int> { 2, 7 }.GetEnumerator();
            Put(fadeState, "FadeCoroutine", coroutine);
            IEnumerator fade = Fade(data, nested);
            Require(ReferenceEquals(Get(fadeState, "FadeCoroutine"), coroutine), "fade factory does no cleanup");
            Require(fade.MoveNext() && ReferenceEquals(fade.Current, nested), "yield authored enumerator itself");
            Require(ReferenceEquals(Get(fadeState, "FadeCoroutine"), coroutine), "yield retains handle");
            ((IDisposable)fade).Dispose();
            Require(ReferenceEquals(Get(fadeState, "FadeCoroutine"), coroutine), "Dispose has no finally cleanup");
            Require(!fade.MoveNext() && Get(fadeState, "FadeCoroutine") == null, "normal resumption clears handle");
            Require(!fade.MoveNext(), "completed fade remains terminal");
            Throws<NotSupportedException>(() => fade.Reset(), "fade Reset rejects restart");
            Put(fadeState, "FadeCoroutine", coroutine);
            IEnumerator nullFade = Fade(data, null);
            Require(nullFade.MoveNext() && nullFade.Current == null, "null nested fade still yields one frame");
            Require(!nullFade.MoveNext() && Get(fadeState, "FadeCoroutine") == null, "null fade completes cleanup");

            var secondsField = typeof(WaitForSeconds).GetField("m_Seconds", Own);
            Require(secondsField != null && secondsField.FieldType == typeof(float), "real Unity WaitForSeconds field");
            float[] durations = { -0.25f, 0f, 0.2f, float.PositiveInfinity, float.NegativeInfinity, float.NaN };
            foreach (float seconds in durations)
            {
                int calls = 0;
                IEnumerator timer = Delay(host, () => calls++, seconds);
                Require(calls == 0 && timer.Current == null, "delay factory is lazy");
                Require(timer.MoveNext() && timer.Current is WaitForSeconds && calls == 0, "all durations yield a real wait");
                float stored = (float)secondsField.GetValue(timer.Current);
                Require(BitConverter.ToInt32(BitConverter.GetBytes(stored), 0) == BitConverter.ToInt32(BitConverter.GetBytes(seconds), 0),
                    "duration bits survive without clamping");
                Require(!timer.MoveNext() && calls == 1, "callback occurs on second advance exactly once");
                Require(!timer.MoveNext() && calls == 1, "completed timer cannot repeat callback");
            }
            IEnumerator reentrant = null;
            bool reentered = true;
            reentrant = Delay(host, () => reentered = reentrant.MoveNext(), 1f);
            Require(reentrant.MoveNext(), "reentrant timer yields first");
            Require(!reentrant.MoveNext() && !reentered, "state terminal before reentrant callback");

            var expected = new InvalidOperationException("callback failure");
            IEnumerator failing = Delay(host, () => throw expected, 1f);
            Require(failing.MoveNext(), "throwing timer yields first");
            Exception observed = null;
            try { failing.MoveNext(); } catch (Exception e) { observed = e; }
            Require(ReferenceEquals(observed, expected), "callback exception escapes unchanged");
            Require(!failing.MoveNext(), "throwing timer is already terminal");

            IEnumerator missing = Delay(host, null, 1f);
            Require(missing.MoveNext(), "null callback accepted until completion");
            Throws<NullReferenceException>(() => missing.MoveNext(), "null callback is unguarded");
            Require(!missing.MoveNext(), "null callback failure still terminal");

            int disposedCalls = 0;
            IEnumerator disposed = Delay(host, () => disposedCalls++, 2f);
            Require(disposed.MoveNext(), "disposal timer first advance");
            ((IDisposable)disposed).Dispose();
            Require(disposedCalls == 0, "Dispose does not invoke user code");
            Require(!disposed.MoveNext() && disposedCalls == 1, "original empty Dispose leaves resumption active");
            Throws<NotSupportedException>(() => disposed.Reset(), "delay Reset rejects restart");

            int neverStartedCalls = 0;
            IEnumerator neverStarted = Delay(host, () => neverStartedCalls++, 2f);
            ((IDisposable)neverStarted).Dispose();
            Require(neverStarted.MoveNext() && neverStartedCalls == 0, "Dispose before first advance does not cancel");
            Require(!neverStarted.MoveNext() && neverStartedCalls == 1, "disposed factory still completes normally");

            IEnumerator first = Delay(host, () => { }, 2f), second = Delay(host, () => { }, 2f);
            Require(first.MoveNext() && second.MoveNext() && !ReferenceEquals(first.Current, second.Current),
                "float-delay allocates independent waits");
            return s_checks;
        }

        // Requires genuine Unity engine execution. This does not start playback,
        // run timers/fades, or establish original assets/audio presentation parity.
        public static int RunEngine()
        {
            s_checks = 0;
            GameObject first = null, second = null;
            AudioClip clip = null;
            try
            {
                first = new GameObject("OriginalAudioSourceDataFixture");
                AudioSource source = first.AddComponent<AudioSource>();
                AudioFadeTransitionSettings settings = JsonUtility.FromJson<AudioFadeTransitionSettings>(
                    "{\"m_fadeOutDuration\":0.3,\"m_fadeInDuration\":0.7}");
                Require(settings.FadeOutDuration == 0.3f && settings.FadeInDuration == 0.7f, "serialized fade durations");
                var id = (HLAudioSourceIdentifier)unchecked((int)0x83b4a571);
                var data = new HLAudioSourceData(id, source, true, (HLAudioSourceTransition)2, settings);
                Require(data.Identifier == id, "constructor identifier");
                Require(data.LowPassFilter == null && !data.HasLowPassFilter, "no low-pass component");
                Require(data.IsGameplayLevelSource && data.Transition == (HLAudioSourceTransition)2, "constructor category and transition");
                Require(data.FadeTransitionSettings.FadeOutDuration == 0.3f && data.FadeTransitionSettings.FadeInDuration == 0.7f,
                    "constructor retains full struct");
                Require(data.CurrentAudio == 0 && data.IsValid() && !data.IsDestroyed(), "new valid source identity");
                object fadeState = Get(data, "m_currentFade");
                Require(fadeState != null && (float)Get(fadeState, "StartingVolume") == 0 && Get(fadeState, "FadeCoroutine") == null,
                    "real constructor initializes fade before base");
                Require((float)Get(data, "m_endTime") == 0 && Get(data, "m_oneShotIsPlayingRoutine") == null,
                    "retained original unused fields");
                data.SetPan(-0.25f); Require(source.panStereo == -0.25f, "pan setter");
                data.Mute(); Require(data.IsMuted() && source.mute, "mute setter");
                data.Unmute(); Require(!data.IsMuted() && !source.mute, "unmute setter");
                data.SetVolume(0.35f); Require(source.volume == 0.35f && data.GetVolume() == 0.35f, "volume wrappers");
                data.SetLooping(true); Require(data.IsRepeating && source.loop, "loop setter");
                data.SetLooping(false); Require(!data.IsRepeating && !source.loop, "loop reset");
                data.MoveTo(new Vector3(2f, -3f, 4f)); Require(first.transform.position == new Vector3(2f, -3f, 4f), "gameObject transform route");

                clip = AudioClip.Create("OriginalAudioSourceDataClipFixture", 44100, 1, 44100, false);
                var times = new List<float> { 0.15f, 0.4f };
                data.SetAudioClip(clip, (HLAudioClipIdentifier)17, true, times);
                Require(ReferenceEquals(source.clip, clip) && data.CurrentAudio == (HLAudioClipIdentifier)17, "clip then identity");
                Require(ReferenceEquals(Get(data, "m_startTimes"), times) && (bool)Get(data, "m_startTimesSet"), "start-time alias retained");
                data.SetAudioClip(clip, (HLAudioClipIdentifier)18, false);
                Require(ReferenceEquals(Get(data, "m_startTimes"), times) && !(bool)Get(data, "m_startTimesSet"), "disabled random start retains list");
                data.SetAudioClip(clip, (HLAudioClipIdentifier)19, true);
                Require(Get(data, "m_startTimes") == null && (bool)Get(data, "m_startTimesSet"), "enabled null start list stored unchanged");
                data.StopFade(); Require(source.volume == 0.35f, "no active fade leaves volume unchanged");

                second = new GameObject("OriginalAudioLowPassFixture");
                AudioSource filteredSource = second.AddComponent<AudioSource>();
                AudioLowPassFilter filter = second.AddComponent<AudioLowPassFilter>();
                var filtered = new HLAudioSourceData((HLAudioSourceIdentifier)2, filteredSource, false,
                    (HLAudioSourceTransition)0, default);
                Require(ReferenceEquals(filtered.LowPassFilter, filter) && filtered.HasLowPassFilter && !filtered.IsGameplayLevelSource,
                    "genuine GetComponent low-pass route");
                UnityEngine.Object.DestroyImmediate(second); second = null;
                Require(filtered.IsDestroyed() && !filtered.IsValid() && !filtered.IsAtEnd(), "destroyed source uses Unity null");
                filtered.Destroy(); Require(filtered.IsDestroyed(), "destroy already destroyed source is safe");
                return s_checks;
            }
            finally
            {
                try { if (second != null) UnityEngine.Object.DestroyImmediate(second); }
                finally
                {
                    try { if (first != null) UnityEngine.Object.DestroyImmediate(first); }
                    finally { if (clip != null) UnityEngine.Object.DestroyImmediate(clip); }
                }
            }
        }
    }
}
