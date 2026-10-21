using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008a6; bodies translated from the 1.10.1 x86-64 binary.
    // The cutscene theatre: pages of seen comic cutscenes that can be replayed one at a time or all in a row.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerCutsceneTheatre : UIContainer
    {
        [SerializeField] private List<UIWidgetTogglePagination> m_paginationWidgets;
        [SerializeField] private List<UIWidgetCutscene> m_cutsceneWidgets;
        private DataManager m_dataManager;
        private SaveManager m_saveManager;
        private UIManager m_uiManager;
        private readonly List<CutsceneDefinition> m_cutsceneDefinitions = new List<CutsceneDefinition>();
        private readonly HashSet<string> m_seenCutsceneGUIDs = new HashSet<string>();

        // 0x060031ed (0x62A8A0)
        private void Start()
        {
            m_dataManager = ProcessManager.GetSystem<DataManager>(null, true);
            m_saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            m_uiManager = ProcessManager.GetSystem<UIManager>(null, true);
            CacheDefinitions();
            SetupPagination();
        }

        // 0x060031ee (0x62A990): the theatre lists the comic cutscenes; a cutscene counts as seen when the save
        // holds it globally or on any level.
        private void CacheDefinitions()
        {
            foreach (CutsceneDefinition cutsceneDefinition in m_dataManager.CutsceneDefinitions.Values)
            {
                if (cutsceneDefinition.IsValidInTheatre())
                    m_cutsceneDefinitions.Add(cutsceneDefinition);
            }
            m_seenCutsceneGUIDs.AddRange(m_saveManager.CurrentSave.SeenCutsceneGuids);
            foreach (SaveDataLevel level in m_saveManager.CurrentSave.Levels)
                m_seenCutsceneGUIDs.AddRange(level.SeenCutsceneOnLevelGuids);
        }

        // 0x060031ef (0x62AE40): every page tab is visible and unlocked; selecting a tab shows its page.
        private void SetupPagination()
        {
            for (int i = 0; i < m_paginationWidgets.Count; i++)
            {
                UIWidgetTogglePagination paginationWidget = m_paginationWidgets[i];
                int localIndex = i;
                paginationWidget.Setup(new UIWidgetTogglePaginationParameters(selected => SelectPage(selected, localIndex), false, false, true, true));
            }
        }

        // 0x060031f0 (0x62AFB0)
        private void SelectPage(bool selected, int page)
        {
            if (selected)
                SetupWidgets(GetCutscenesStartingFrom(page));
        }

        // 0x060031f1 (0x62AFE0)
        private void LoadPage(int page)
        {
            SetupWidgets(GetCutscenesStartingFrom(page));
        }

        // 0x060031f2 (0x62B000): a page holds as many cutscenes as there are widgets, so page n starts at widgets * n.
        private IReadOnlyList<CutsceneDefinition> GetCutscenesStartingFrom(int startFrom)
        {
            int widgetCount = m_cutsceneWidgets.Count;
            List<CutsceneDefinition> cutscenes = new List<CutsceneDefinition>();
            int index = widgetCount * startFrom;
            while (cutscenes.Count != m_cutsceneWidgets.Count && index < m_cutsceneDefinitions.Count)
            {
                cutscenes.Add(m_cutsceneDefinitions[index]);
                index++;
            }
            return cutscenes;
        }

        // 0x060031f3 (0x62B150): widgets beyond the page keep their previous setup; a cutscene is locked until seen
        // and new until it has been played from the theatre.
        private void SetupWidgets(IReadOnlyList<CutsceneDefinition> cutsceneDefinitions)
        {
            if (m_cutsceneWidgets.Count <= 0)
                return;
            IReadOnlyList<string> playedGuids = m_saveManager.CurrentSave.CutsceneIsNewGuids;
            for (int i = 0; i < m_cutsceneWidgets.Count; i++)
            {
                if (i >= cutsceneDefinitions.Count)
                    continue;
                UIWidgetCutscene widgetCutscene = m_cutsceneWidgets[i];
                CutsceneDefinition cutsceneDefinition = cutsceneDefinitions[i];
                string guid = cutsceneDefinition.GetGUID();
                bool locked = !m_seenCutsceneGUIDs.Contains(guid);
                bool isNew = !playedGuids.Contains(guid);
                widgetCutscene.Setup(new UIWidgetCutsceneParameters(cutsceneDefinition.name, () => PlayCutscene(widgetCutscene, cutsceneDefinition), locked, cutsceneDefinition.TheatreImage, isNew));
            }
        }

        // 0x060031f4 (0x62B4A0): only while this is the one open container; the cutscene stops being new.
        private void PlayCutscene(UIWidgetCutscene widgetCutscene, CutsceneDefinition cutsceneDefinition)
        {
            if (!m_uiManager.IsOnlyContainerOpen(m_identifier))
                return;
            m_saveManager.CurrentSave.AddIsNewCutsceneGuid(cutsceneDefinition.GetGUID());
            m_saveManager.RequestSave();
            widgetCutscene.SetIsNew(false);
            new CutsceneTheatre().Enqueue(cutsceneDefinition);
        }

        // 0x060031f5 (0x62B550)
        public void Action_PlayAll()
        {
            if (m_uiManager.IsOnlyContainerOpen(m_identifier))
                new CutsceneTheatre().EnqueueAll();
        }

        // 0x060031f6 (0x62B5C0)
        public UIContainerCutsceneTheatre() { }
    }
}
