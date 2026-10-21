using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200043f; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class CutsceneTheatre
    {
        public bool IsPlaying { get; private set; }
        public CutsceneDefinition Cutscene { get; private set; }
        public HLAudioClipIdentifier PreviousMusic { get; private set; }

        private readonly DataManager m_dataManager = ProcessManager.GetSystem<DataManager>();
        private readonly App m_app = ProcessManager.GetSystem<App>();
        private readonly SystemRef<CutsceneManager> m_cutsceneManagerRef = ProcessManager.GetSystemRef<CutsceneManager>();
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private readonly Queue<CutsceneDefinition> m_queue = new Queue<CutsceneDefinition>();

        // 0x06001927 (0x7A3E30): the finish handler is attached as soon as the manager exists.
        public CutsceneTheatre()
        {
            m_cutsceneManagerRef.InvokeOnValid(OnCutsceneManagerValid);
        }

        // 0x06001928 (0x7A4040)
        private void OnCutsceneManagerValid(CutsceneManager cutsceneManager)
        {
            cutsceneManager.OnCutsceneFinished += OnCutsceneFinished;
        }

        // 0x06001929 (0x7A4120)
        public void Cleanup()
        {
            if (m_cutsceneManagerRef.TryGet(out CutsceneManager cutsceneManager))
                cutsceneManager.OnCutsceneFinished -= OnCutsceneFinished;
            IsPlaying = false;
            Cutscene = null;
            m_queue.Clear();
        }

        // 0x0600192a (0x7A4280)
        public bool IsEmpty()
        {
            return m_queue.Count <= 0;
        }

        // 0x0600192b (0x7A42C0). Only an empty queue outside an active theatre session
        // accepts the cutscene; it also claims the session key and remembers the music.
        public void Enqueue(CutsceneDefinition cutsceneDefinition)
        {
            if (m_queue.Count > 0)
                return;
            if (m_app.Storage.HasValue(AppFSMKeys.CutsceneTheatre))
                return;
            StorePreviousUiBackgroundAudio();
            m_queue.Enqueue(cutsceneDefinition);
            m_app.Storage.SetValue(AppFSMKeys.CutsceneTheatre, this);
        }

        // 0x0600192c (0x7A4560). Starts a session with every already seen cutscene that
        // is valid in the theatre, under the same preconditions as Enqueue.
        public void EnqueueAll()
        {
            if (!m_saveManagerRef.TryGet(out SaveManager saveManager) || !saveManager.IsAnySaveOpen)
                return;
            if (m_queue.Count > 0)
                return;
            if (m_app.Storage.HasValue(AppFSMKeys.CutsceneTheatre))
                return;
            StorePreviousUiBackgroundAudio();
            m_queue.Clear();
            foreach (CutsceneDefinition cutsceneDefinition in m_dataManager.CutsceneDefinitions.Values)
            {
                if (cutsceneDefinition.IsValidInTheatre() && saveManager.CurrentSave.SeenCutsceneGuids.Contains(cutsceneDefinition.GetGUID()))
                    m_queue.Enqueue(cutsceneDefinition);
            }
            m_app.Storage.SetValue(AppFSMKeys.CutsceneTheatre, this);
        }

        // 0x0600192d (0x7A4940): the next cutscene is only taken while none is playing.
        public void DequeueNext()
        {
            if (m_cutsceneManagerRef.TryGet(out CutsceneManager cutsceneManager)
                && !cutsceneManager.IsCutscenePlaying
                && m_queue.Count > 0)
                Cutscene = m_queue.Dequeue();
        }

        // 0x0600192e (0x7A4A30)
        public void Play()
        {
            if (m_cutsceneManagerRef.TryGet(out CutsceneManager cutsceneManager) && !(Cutscene == null))
            {
                IsPlaying = true;
                cutsceneManager.LoadCutscene(Cutscene, null, true);
            }
        }

        // 0x0600192f (0x7A4AB0)
        public CutsceneDefinition Peek()
        {
            if (m_queue.Count > 0)
                return m_queue.Peek();
            return null;
        }

        // 0x06001930 (0x7A4B30): skipping a cutscene abandons the rest of the queue.
        private void OnCutsceneFinished()
        {
            IsPlaying = false;
            if (m_cutsceneManagerRef.Get().WasCutsceneSkipped)
                m_queue.Clear();
        }

        // 0x06001931 (0x7A44E0): remembers what the UI background source is playing.
        private void StorePreviousUiBackgroundAudio()
        {
            if (m_audioManagerRef.TryGet(out AudioManager audioManager)
                && audioManager.TryGetAudioSourceData(HLAudioSourceIdentifier.UiBackground, out HLAudioSourceData audioSourceData))
                PreviousMusic = audioSourceData.CurrentAudio;
        }
    }
}
