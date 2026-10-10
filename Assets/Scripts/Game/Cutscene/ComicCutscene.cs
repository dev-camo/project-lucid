using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace HardlightProject
{
    // Complete original Game.Runtime 02000429, 0600187a..06001887. The panel
    // dictionary is live: authored callbacks can leave partial changes on fault.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ComicCutscene : Cutscene
    {
        [SerializeField] private Transform m_panelContainerTransform;
        [SerializeField] private Camera m_comicCamera;
        private readonly Dictionary<string, Dictionary<int, ComicPanel>> m_activePanels = new Dictionary<string, Dictionary<int, ComicPanel>>();
        private SystemRef<CinemachineCameraManager> m_cameraManagerRef;

        public Camera Camera => m_comicCamera;

        public void AddTrack(string thisTrackHandle)
        {
            if (m_activePanels.ContainsKey(thisTrackHandle))
                return;
            m_activePanels.Add(thisTrackHandle, new Dictionary<int, ComicPanel>());
        }

        public void SpawnPanel(string thisTrackHandle, int i, ComicPanelBehaviour comicPanelBehaviour)
        {
            Quaternion rotation = Quaternion.Euler(comicPanelBehaviour.Rotation);
            ComicPanel panel = Object.Instantiate(comicPanelBehaviour.PanelTemplate, m_panelContainerTransform);
            Transform panelTransform = panel.transform;
            panelTransform.SetLocalPositionAndRotation(comicPanelBehaviour.ScreenPosition, rotation);
            // Vector2's original implicit conversion retains z=0 for both pose and
            // scale. Do not replace this with Vector3.one's z component.
            panelTransform.localScale = comicPanelBehaviour.Scale;
            m_activePanels[thisTrackHandle].Add(i, panel);
            panel.Initialise(comicPanelBehaviour, this);
            m_comicCamera.targetTexture = comicPanelBehaviour.RenderTexture;
        }

        public void DespawnPanel(string thisTrackHandle, int i)
        {
            if (Application.isPlaying)
                Object.Destroy(m_activePanels[thisTrackHandle][i].gameObject);
            else
                Object.DestroyImmediate(m_activePanels[thisTrackHandle][i].gameObject);
            m_activePanels[thisTrackHandle].Remove(i);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void ClearAllPanels()
        {
            foreach (string track in m_activePanels.Keys)
                foreach (var pair in m_activePanels[track])
                {
                    if (Application.isPlaying)
                        Object.Destroy(pair.Value.gameObject);
                    else
                        Object.DestroyImmediate(pair.Value.gameObject);
                }
            // Both enumerators dispose before the outer dictionary clears. A
            // callback/lookup fault leaves the original dictionary contents.
            m_activePanels.Clear();
        }

        public bool PanelIsActive(string thisTrackHandle, int i)
        {
            if (!Application.isPlaying)
                AddTrack(thisTrackHandle);
            return m_activePanels[thisTrackHandle].ContainsKey(i);
        }

        public void DriveAnimation(string thisTrackHandle, int i, float inputWeight) =>
            m_activePanels[thisTrackHandle][i].EnterExit(inputWeight);

        public void SetPanelLooping(string thisTrackHandle, int i) =>
            m_activePanels[thisTrackHandle][i].SetLooping();

        public bool SelectCharacterOptionA(ComicPanel comicPanel)
        {
            PlayState state = TimelineDirector.state;
            if (state != PlayState.Playing)
                SelectCharacterOption(Definition.CharacterChoice[0], comicPanel);
            return state != PlayState.Playing;
        }

        public bool SelectCharacterOptionB(ComicPanel comicPanel)
        {
            PlayState state = TimelineDirector.state;
            if (state != PlayState.Playing)
                SelectCharacterOption(Definition.CharacterChoice[1], comicPanel);
            return state != PlayState.Playing;
        }

        public void SelectCharacterOption(CharacterId characterId, ComicPanel comicPanel)
        {
            App app = ProcessManager.GetSystem<App>(null, true);
            app.Storage.SetValue(AppFSMKeys.CurrentCharacterId, characterId);
            app.Storage.SetValue(AppFSMKeys.MissionCharacterId, characterId);
            SetAllOtherPanelsDeselected(comicPanel);
            TimelineDirector.Resume();
        }

        public void SetAllOtherPanelsDeselected(ComicPanel comicPanel)
        {
            foreach (string track in m_activePanels.Keys)
                foreach (var pair in m_activePanels[track])
                    if (pair.Value != comicPanel)
                        pair.Value.SetDeselected();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void EditorUpdatePanelLive(string thisTrackHandle, int i, ComicPanelBehaviour comicPanelBehaviour)
        {
            Transform panelTransform = m_activePanels[thisTrackHandle][i].transform;
            Quaternion rotation = Quaternion.Euler(comicPanelBehaviour.Rotation);
            panelTransform.SetLocalPositionAndRotation(comicPanelBehaviour.ScreenPosition, rotation);
            panelTransform.localScale = comicPanelBehaviour.Scale;
        }

        public ComicCutscene() { }
    }
}
