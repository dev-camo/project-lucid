using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Full original clip-loader graph. This private source remains unaccepted
    // until the genuine DataManager and AddressableManager dependencies close.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioClipHandler
    {
        private readonly DataManager m_dataManager;
        private readonly SystemRef<AddressableManager> m_addressableManagerRef;
        private readonly Dictionary<HLAudioClipIdentifier, AudioClipData> m_loadedAudioClips =
            new Dictionary<HLAudioClipIdentifier, AudioClipData>(HardlightEnumComparers.HLAudioClipIdentifierComparer);

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private abstract class AudioClipData
        {
            public AsyncOperationHandle<AudioClip> ClipHandle;
            protected AudioClipData(AsyncOperationHandle<AudioClip> clipHandle)
            {
                ClipHandle = clipHandle;
            }
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class MusicData : AudioClipData
        {
            public IReadOnlyList<float> StartTimes;
            public MusicData(AsyncOperationHandle<AudioClip> clipHandle, IReadOnlyList<float> startTimes) : base(clipHandle)
            {
                StartTimes = startTimes;
            }
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class SfxData : AudioClipData
        {
            public bool DampenRepeatedOneShotVolume;
            public IReadOnlyList<float> Dampening;
            public SfxData(AsyncOperationHandle<AudioClip> clipHandle, bool dampen, IReadOnlyList<float> dampening) : base(clipHandle)
            {
                DampenRepeatedOneShotVolume = dampen;
                Dampening = dampening;
            }
        }

        public struct LoadedAudioParameters
        {
            public AudioClip Clip;
            public IReadOnlyList<float> StartTimes;
            public bool DampenRepeatedOneShotVolume;
            public IReadOnlyList<float> Dampening;
        }

        public HLAudioClipHandler(DataManager dataManager)
        {
            m_dataManager = dataManager;
            m_addressableManagerRef = ProcessManager.GetSystemRef<AddressableManager>(null, true);
        }

        public void ReleaseAudioClips(IReadOnlyList<HLAudioClipIdentifier> exclude)
        {
            AddressableManager manager = m_addressableManagerRef.Get();
            List<HLAudioClipIdentifier> identifiers = new List<HLAudioClipIdentifier>();
            foreach (KeyValuePair<HLAudioClipIdentifier, AudioClipData> pair in m_loadedAudioClips)
            {
                pair.Deconstruct(out HLAudioClipIdentifier identifier, out AudioClipData data);
                // Exclusion precedes handle inspection. In-flight handles stay
                // cached; release is restricted to completed operations.
                if (exclude.Contains(identifier)) continue;
                if (data.ClipHandle.IsDone) identifiers.Add(identifier);
            }
            foreach (HLAudioClipIdentifier identifier in identifiers)
            {
                manager.ReleaseHandle(m_loadedAudioClips[identifier].ClipHandle);
                // Reload the dictionary after the release callback boundary.
                m_loadedAudioClips.Remove(identifier);
            }
        }

        public void Get(HLAudioClipIdentifier identifier, Action<LoadedAudioParameters> onComplete)
        {
            LoadedAudioParameters onCompleteParameters = default;
            if (m_loadedAudioClips.TryGetValue(identifier, out AudioClipData data))
            {
                if (data is MusicData music)
                    onCompleteParameters.StartTimes = music.StartTimes;
                else if (data is SfxData sfx)
                {
                    onCompleteParameters.DampenRepeatedOneShotVolume = sfx.DampenRepeatedOneShotVolume;
                    onCompleteParameters.Dampening = sfx.Dampening;
                }
                if (data.ClipHandle.IsDone)
                {
                    onCompleteParameters.Clip = data.ClipHandle.Result;
                    // The original synchronous path requires a callback.
                    onComplete(onCompleteParameters);
                }
                else
                    data.ClipHandle.Completed += handle =>
                    {
                        onCompleteParameters.Clip = handle.Result;
                        onComplete?.Invoke(onCompleteParameters);
                    };
            }
            else if (m_dataManager.AudioClips.TryGetValue(identifier, out HLAudioClipDefinition definition))
            {
                if (definition is HLMusicClipDefinition music)
                    LoadAudioClip(music, onComplete);
                else if (definition is HLSfxClipDefinition sfx)
                    LoadAudioClip(sfx, onComplete);
                else
                    onComplete(onCompleteParameters);
            }
            else
                onComplete(onCompleteParameters);
        }

        private void LoadAudioClip(HLMusicClipDefinition audioClipDefinition, Action<LoadedAudioParameters> onComplete)
        {
            AsyncOperationHandle<AudioClip> handle = m_addressableManagerRef.Get().LoadAsset(audioClipDefinition.AudioClipReference, audioClip =>
            {
                // Read definition data even for a missing callback, and read it
                // at completion rather than taking a pre-load snapshot.
                LoadedAudioParameters parameters = new LoadedAudioParameters
                {
                    Clip = audioClip,
                    StartTimes = audioClipDefinition.StartTimes,
                    DampenRepeatedOneShotVolume = false,
                    Dampening = null
                };
                onComplete?.Invoke(parameters);
            }, null);
            MusicData data = new MusicData(handle, audioClipDefinition.StartTimes);
            // Native order publishes after LoadAsset returns. Synchronous
            // reentry therefore retains its original duplicate-add failure.
            m_loadedAudioClips.Add(audioClipDefinition.Identifier, data);
        }

        private void LoadAudioClip(HLSfxClipDefinition audioClipDefinition, Action<LoadedAudioParameters> onComplete)
        {
            AsyncOperationHandle<AudioClip> handle = m_addressableManagerRef.Get().LoadAsset(audioClipDefinition.AudioClipReference, audioClip =>
            {
                LoadedAudioParameters parameters = new LoadedAudioParameters
                {
                    Clip = audioClip,
                    StartTimes = null,
                    DampenRepeatedOneShotVolume = audioClipDefinition.DampenRepeatedOneShotVolume,
                    Dampening = audioClipDefinition.Dampening
                };
                onComplete?.Invoke(parameters);
            }, null);
            SfxData data = new SfxData(handle, audioClipDefinition.DampenRepeatedOneShotVolume, audioClipDefinition.Dampening);
            m_loadedAudioClips.Add(audioClipDefinition.Identifier, data);
        }
    }
}
