using System.Collections.Generic;
using Hardlight;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008b6 and 020008b7 (closure); bodies 0x62FFB0..0x630A8A (1.10.1 x86-64).
    // Debug level list: one button per level shown in level select, plus a Back button once a list has been shown.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerFastLevelSelect : UIContainer
    {
        [SerializeField]
        private GameObject m_listRoot;
        [SerializeField]
        private UIWidgetButton m_levelSelectPrefab;
        private readonly Stack<GameplayLevelCategory> m_categoryStack = new Stack<GameplayLevelCategory>();
        private readonly List<UIWidgetButton> m_buttons = new List<UIWidgetButton>();
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);

        // 06003248
        private void Start()
        {
            ShowSelectableLevels(null);
        }

        // 06003249
        private void ClearList()
        {
            for (int i = 0; i < m_buttons.Count; i++)
                Destroy(m_buttons[i].gameObject);
            m_buttons.Clear();
        }

        // 0600324a / closure 0600324f..06003250: the list is always every level of the game (the parameterless
        // GetLevels); the category is only remembered on the stack, for the Back button.
        private void ShowSelectableLevels(GameplayLevelCategory category)
        {
            ClearList();
            foreach (GameplayLevelDefinition levelDefinition in m_levelManagerRef.Get().GameLevels.GetLevels())
            {
                if (!levelDefinition.ShowInLevelSelect)
                    continue;
                string label = StringTable.GetString(levelDefinition.DisplayName);
                UIWidgetButton button = Instantiate(m_levelSelectPrefab, m_listRoot.transform);
                button.Setup(new UIWidgetButtonParameters(label, () => OnUISelection(levelDefinition)));
                m_buttons.Add(button);
            }
            AddBackButton();
            m_categoryStack.Push(category);
        }

        // 0600324b
        private void AddBackButton()
        {
            if (m_categoryStack.Count == 0)
                return;
            UIWidgetButton button = Instantiate(m_levelSelectPrefab, m_listRoot.transform);
            button.Setup(new UIWidgetButtonParameters("Back", OnBackButton));
            m_buttons.Add(button);
        }

        // 0600324c: a level starts a fast load; a category shows its list (nothing in the retail list produces one).
        private void OnUISelection(object uiData)
        {
            if (uiData is GameplayLevelDefinition levelDefinition)
            {
                LevelManager levelManager = m_levelManagerRef.Get();
                ProcessManager.GetSystem<App>(null, true).FastLoad(levelDefinition);
                levelManager.LoadGame();
            }
            else if (uiData is GameplayLevelCategory category)
            {
                ShowSelectableLevels(category);
            }
        }

        // 0600324d: the first pop removes the list on show, the second the one before it, which is shown again.
        private void OnBackButton()
        {
            if (m_categoryStack.TryPop(out _))
            {
                m_categoryStack.TryPop(out GameplayLevelCategory previous);
                ShowSelectableLevels(previous);
            }
        }

        // 0600324e
        public UIContainerFastLevelSelect() { }
    }
}
