using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008b8 and 020008b9 (closure); bodies 0x630A90..0x63171E (1.10.1 x86-64).
    // Debug fast load: a button per mission of the level the app is loading, each continuing straight into that mission.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerFastLoad : UIContainer
    {
        [SerializeField]
        private RectTransform m_rootTransform;
        [SerializeField]
        private UIWidgetButton m_buttonPrefab;
        [SerializeField]
        private ApplicationStateEvent m_continueEvent;
        [SerializeField]
        private GameplayLevelCategory m_zone5;
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);

        // 06003251
        private void Start()
        {
            PopulateMissions(FindLevelDefinition());
        }

        // 06003252
        private GameplayLevelDefinition FindLevelDefinition()
        {
            return m_appRef.Get().Storage.GetValue<LevelManagerLoadLevelParameters>(
                AppFSMKeys.LevelLoadParameters, null, true).LevelDefinition;
        }

        // 06003253: a level without a mission list continues at once, without a mission.
        private void PopulateMissions(GameplayLevelDefinition levelDefinition)
        {
            if (levelDefinition.MissionList == null)
            {
                Continue(levelDefinition);
                return;
            }
            foreach (MissionDefinition missionDefinition in levelDefinition.MissionList.GetMissions(true))
                CreateButton(levelDefinition, missionDefinition);
        }

        // 06003254 / closure 06003257..06003258: the button is labelled with the mission asset's name.
        private void CreateButton(GameplayLevelDefinition levelDefinition, MissionDefinition missionDefinition)
        {
            UIWidgetButton button = Instantiate(m_buttonPrefab, m_rootTransform);
            button.Setup(new UIWidgetButtonParameters(missionDefinition.name, () => Continue(levelDefinition, missionDefinition)));
        }

        // 06003255: store the meta game state, load the mission, set the regular mission context (null without a mission),
        // store the first mission character as current and mission character, set BonusZoneActive for zone-5 levels, then
        // queue the continue event.
        private void Continue(GameplayLevelDefinition levelDefinition, MissionDefinition missionDefinition = null)
        {
            MissionManager missionManager = m_missionManagerRef.Get();
            App app = m_appRef.Get();
            app.Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(levelDefinition, missionDefinition));
            missionManager.LoadMission(missionDefinition, levelDefinition);
            MissionContextRegular missionContext = null;
            if (missionDefinition != null)
                missionContext = new MissionContextRegular(missionDefinition, levelDefinition, false);
            missionManager.SetActiveMissionContext(missionContext);
            List<CharacterId> characters = new List<CharacterId>();
            missionManager.GetCharactersForActiveMission(ref characters);
            CharacterId characterId = characters[0];
            app.Storage.SetValue(AppFSMKeys.CurrentCharacterId, characterId);
            app.Storage.SetValue(AppFSMKeys.MissionCharacterId, characterId);
            if (m_zone5.GameplayEntries.Contains(levelDefinition))
                app.Storage.SetValue(AppFSMKeys.BonusZoneActive, true);
            app.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_continueEvent);
        }

        // 06003256
        public UIContainerFastLoad() { }
    }
}
