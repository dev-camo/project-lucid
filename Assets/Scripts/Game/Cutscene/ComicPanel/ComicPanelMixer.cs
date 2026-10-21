using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
    // Original Game.Runtime 0200042f; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ComicPanelMixer : PlayableBehaviour
    {
        private int m_inputCount;
        private CutsceneManager m_cutsceneManager;
        private ComicCutscene m_comicCutscene;
        private string m_thisTrackHandle;

        // 0x0600189a (0x79E2D0)
        public void SetUniqueTrackHandle(string trackName)
        {
            m_thisTrackHandle = trackName;
        }

        // 0x0600189b (0x79E2F0). At runtime the track registers with the current comic
        // cutscene. The editor-only else branch (EditModeCreateComicCutscene) is
        // compiled out of the retail build.
        public override void OnGraphStart(Playable playable)
        {
            base.OnGraphStart(playable);
            m_inputCount = playable.GetInputCount();
            if (Application.isPlaying)
            {
                m_cutsceneManager = ProcessManager.GetSystem<CutsceneManager>();
                if (m_cutsceneManager.TryGetCurrentComicCutscene(out m_comicCutscene))
                    m_comicCutscene.AddTrack(m_thisTrackHandle);
                else
                    HLOutput.LogError("You have a timeline playing ComicPanelTrack Clips but the CutsceneManager is not aware of it.");
            }
        }

        // 0x0600189c (0x79E590). A positive weight spawns the panel once and then
        // drives its enter/exit animation until the weight reaches one, which starts
        // the loop; a zero weight despawns an active panel.
        public override void ProcessFrame(Playable playable, FrameData _, object playerData)
        {
            // The retail build keeps this null test but drops the editor-only call it guarded.
            if (!Application.isPlaying && m_comicCutscene == null)
                EditModeCreateComicCutscene();
            for (int i = 0; i < m_inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                ScriptPlayable<ComicPanelBehaviour> inputPlayable = (ScriptPlayable<ComicPanelBehaviour>)playable.GetInput(i);
                if (inputWeight > 0f)
                {
                    ComicPanelBehaviour behaviour = inputPlayable.GetBehaviour();
                    if (!m_comicCutscene.PanelIsActive(m_thisTrackHandle, i))
                        m_comicCutscene.SpawnPanel(m_thisTrackHandle, i, behaviour);
                    else if (inputWeight < 1f)
                        m_comicCutscene.DriveAnimation(m_thisTrackHandle, i, inputWeight);
                    else
                        // The retail build also reads Application.isPlaying here, for an editor-only call it dropped.
                        m_comicCutscene.SetPanelLooping(m_thisTrackHandle, i);
                }
                else if (m_comicCutscene.PanelIsActive(m_thisTrackHandle, i))
                    m_comicCutscene.DespawnPanel(m_thisTrackHandle, i);
            }
        }

        // 0x0600189d (0x79E8C0): finds the scene's comic cutscene for edit-mode previews.
        [Conditional("UNITY_EDITOR")]
        private void EditModeCreateComicCutscene()
        {
            foreach (GameObject rootGameObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                ComicCutscene comicCutscene = rootGameObject.GetComponent<ComicCutscene>();
                if (comicCutscene != null)
                {
                    m_comicCutscene = comicCutscene;
                    break;
                }
            }
            if (m_comicCutscene == null)
                HLOutput.LogError("No ComicCutscene component could be found in any of the root objects in the scene.");
            else
                m_comicCutscene.AddTrack(m_thisTrackHandle);
        }
    }
}
