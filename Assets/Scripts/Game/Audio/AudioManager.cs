using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete genuine AudioManager; source closure is private and unresolved at
    // the original DataManager/SaveManager/Addressable and audio handler graph.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AudioManager : MonoBehaviour, ISystem
    {
        [SerializeField] private HLAudioSourceRegisterComponent m_uiAudioSource;
        [SerializeField] private HLAudioSourceRegisterComponent m_uiBackgroundAudioSource;
        [SerializeField] private HLAudioSourceRegisterComponent m_jukeboxAudioSource;
        [SerializeField] private HLAudioSourceHandler m_audioSourceHandler;
        private HLAudioClipHandler m_audioClipHandler;
        private HLAudioMixerHandler m_audioMixerHandler;
        private InGameAudioListener m_activeAudioListener;
        private SaveManager m_saveManager;

        private void Awake()
        {
            ProcessManager.GetSystemRef<DataManager>(null, true).InvokeOnValid(DataManagerValid);
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(SaveManagerValid);
        }

        private void DataManagerValid(DataManager dataManager)
        {
            GameObject parent = new GameObject("AudioManager_ManagedObjects");
            UnityEngine.Object.DontDestroyOnLoad(parent);
            m_audioSourceHandler.Initialise(parent.transform);
            m_audioClipHandler = new HLAudioClipHandler(dataManager);
            m_audioMixerHandler = new HLAudioMixerHandler(dataManager);
            ProcessManager.RegisterSystem(this, null, false, false);
            LoadAudioSource(m_uiAudioSource);
            LoadAudioSource(m_uiBackgroundAudioSource);
            LoadAudioSource(m_jukeboxAudioSource);
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
        }

        private void SaveManagerValid(SaveManager manager)
        {
            m_saveManager = manager;
            m_saveManager.OnLoadCompleted += ApplyAudioSettings;
        }

        private void ApplyAudioSettings()
        {
            SaveDataSettings settings = m_saveManager.GetSaveDataSettings();
            HLAudioMixerDefinition mixer = m_audioMixerHandler.Get(HLAudioMixerIdentifier.Main);
            mixer.SetMasterVolume(settings.MasterVolume);
            mixer.SetMusicVolume(settings.MusicVolume);
            mixer.SetSfxVolume(settings.SfxVolume);
            mixer.SetVoiceVolume(settings.VoiceVolume);
        }

        private void Shutdown(object context = null)
        {
            ReleaseAudioClips(false);
            m_audioSourceHandler.Clear(false);
            if (m_saveManager != null) m_saveManager.OnLoadCompleted -= ApplyAudioSettings;
        }

        public void ReleaseAllHandles()
        {
            ReleaseAudioClips(false);
            m_audioSourceHandler.Clear(false);
        }

        public void ReleaseAudioSources(bool gameplayLevelSourcesOnly = false) => m_audioSourceHandler.Clear(gameplayLevelSourcesOnly);

        public void ReleaseAudioClips(bool keepPlayingClips)
        {
            List<HLAudioClipIdentifier> exclude = new List<HLAudioClipIdentifier>();
            if (keepPlayingClips)
                foreach (HLAudioSourceData source in m_audioSourceHandler.GetAllAudioSources())
                    if (source.IsPlaying()) exclude.Add(source.CurrentAudio);
            m_audioClipHandler.ReleaseAudioClips(exclude);
        }

        public void LoadAudioSource(HLAudioSourceRegisterComponent backgroundAudioSourcePrefab)
        {
            if (backgroundAudioSourcePrefab == null) return;
            m_audioSourceHandler.InstantiateSource(backgroundAudioSourcePrefab);
        }

        public void InstantiateSource(HLAudioSourceRegisterComponent audioSourcePrefab) => m_audioSourceHandler.InstantiateSource(audioSourcePrefab);
        public void AddSource(HLAudioSourceData audioSourceData) => m_audioSourceHandler.Add(audioSourceData);
        public bool HasSource(HLAudioSourceIdentifier identifier) => m_audioSourceHandler.HasSource(identifier);
        public void GetClip(HLAudioClipIdentifier identifier, Action<HLAudioClipHandler.LoadedAudioParameters> onComplete) => m_audioClipHandler.Get(identifier, onComplete);
        public HLAudioMixerDefinition GetMixer(HLAudioMixerIdentifier identifier) => m_audioMixerHandler.Get(identifier);

        public void PlayClipAtSource(HLAudioClipIdentifier audioClipIdentifier, HLAudioSourceIdentifier audioSourceIdentifier, bool startAtRandomTime = false, HLAudioSourceTransition? customTransition = null)
        {
            if (audioClipIdentifier != 0)
                m_audioClipHandler.Get(audioClipIdentifier, loadedAudioParameters =>
                {
                    if (loadedAudioParameters.Clip == null) return;
                    if (m_audioSourceHandler.IsPlayingAtSource(audioSourceIdentifier, loadedAudioParameters.Clip))
                        m_audioSourceHandler.CancelAnyActiveFade(audioSourceIdentifier);
                    else
                        m_audioSourceHandler.Play(audioSourceIdentifier, loadedAudioParameters.Clip, audioClipIdentifier, startAtRandomTime, loadedAudioParameters.StartTimes, customTransition);
                });
            else
                // The original zero-clip branch drops the supplied transition.
                m_audioSourceHandler.Play(audioSourceIdentifier, null, 0, startAtRandomTime, null, null);
        }

        public void PlayOneShotAtAvailableSource(HLAudioClipIdentifier audioClipIdentifier, HLAudioSourceIdentifier[] audioSourceIdentifier, float pan = 0f, float desiredVolumeScale = 1f)
        {
            foreach (HLAudioSourceIdentifier identifier in audioSourceIdentifier)
                if (!m_audioSourceHandler.IsPlayingOneShot(identifier))
                {
                    // A free zero identifier ends the search without playing.
                    if (identifier != 0) PlayOneShotAtSource(audioClipIdentifier, identifier, pan, desiredVolumeScale);
                    return;
                }
        }

        public void PlayOneShotAtSource(HLAudioClipIdentifier audioClipIdentifier, HLAudioSourceIdentifier audioSourceIdentifier, float pan = 0f, float desiredVolumeScale = 1f)
        {
            if (audioClipIdentifier != 0)
            {
                m_audioSourceHandler.LoadingOneShotClipForSource(audioSourceIdentifier);
                m_audioClipHandler.Get(audioClipIdentifier, loadedAudioParameters =>
                {
                    if (loadedAudioParameters.Clip == null) return;
                    m_audioSourceHandler.PlayOneShot(audioSourceIdentifier, loadedAudioParameters.Clip, loadedAudioParameters.DampenRepeatedOneShotVolume, loadedAudioParameters.Dampening, pan, desiredVolumeScale);
                });
            }
            else
                HLOutput.LogError("Will not play the given audio clip at source " + audioSourceIdentifier.GetString() + ", no clip was specified.", null);
        }

        public void StopAudioSource(HLAudioSourceIdentifier audioSourceIdentifier)
        {
            if (m_audioSourceHandler.TryGetAudioSourceData(audioSourceIdentifier, out HLAudioSourceData source)) source.Stop();
        }
        public bool TryGetAudioSourceData(HLAudioSourceIdentifier audioSourceIdentifier, out HLAudioSourceData audioSourceData) => m_audioSourceHandler.TryGetAudioSourceData(audioSourceIdentifier, out audioSourceData);
        public void MoveAudioSource(HLAudioSourceIdentifier identifier, Vector3 position) => m_audioSourceHandler.MoveTo(identifier, position);
        public void RegisterAudioListener(InGameAudioListener inGameAudioListener) => m_activeAudioListener = inGameAudioListener;
        public void RemoveAudioListener(InGameAudioListener inGameAudioListener)
        {
            if (inGameAudioListener != m_activeAudioListener) return;
            m_activeAudioListener = null;
        }
        public void ActivateLowPass(InGameAudioListener.LowFilterType filterType) => m_activeAudioListener.ActivateLowPass(filterType);
        public void RemoveLowPassFilter(InGameAudioListener.LowFilterType filterType)
        {
            if (m_activeAudioListener == null) return;
            m_activeAudioListener.RemoveLowPassFilter(filterType);
        }
        public void ListenerSetShouldFollowPlayer(bool shouldFollow) => m_activeAudioListener.SetShouldFollow(shouldFollow);
        public IReadOnlyCollection<HLAudioSourceData> GetAllAudioSources() => m_audioSourceHandler.GetAllAudioSources();
        public void FadeOutSource(HLAudioSourceIdentifier identifier)
        {
            m_audioSourceHandler.CancelAnyActiveFade(identifier);
            m_audioSourceHandler.FadeOut(identifier);
        }
        public void FadeInSource(HLAudioSourceIdentifier identifier, float restoreVolume)
        {
            m_audioSourceHandler.CancelAnyActiveFade(identifier);
            m_audioSourceHandler.FadeIn(identifier, restoreVolume);
        }
        public AudioManager() { }
    }
}
