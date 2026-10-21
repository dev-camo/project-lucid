using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008b1 and 020008b2 (closure); bodies 0x62F100..0x62F6F7 (1.10.1 x86-64).
    // Debug list that adds or removes m_changeValueBy of any registered collectable, one row per collectable type.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerDebugCollectables : UIContainer
    {
        [SerializeField]
        private UIContainerIdentifier m_debugCollectablesContainerIdentifier;
        [SerializeField]
        private Transform m_listRoot;
        [SerializeField]
        private UIContainerDebugCollectableModifier m_debugCollectableModifierPrefab;
        [SerializeField]
        private int m_changeValueBy = 5;
        private CollectableManager m_collectableManager;

        // 06003236
        private void Start()
        {
            ProcessManager.GetSystemRef<CollectableManager>(null, true).InvokeOnValid(OnCollectableManagerValid);
        }

        // 06003237
        public void Action_Close()
        {
            ProcessManager.GetSystem<UIManager>(null, true).Close(m_debugCollectablesContainerIdentifier);
        }

        // 06003238
        private void OnCollectableManagerValid(CollectableManager collectableManager)
        {
            m_collectableManager = collectableManager;
            PopulateCollectables();
        }

        // 06003239 / closure 0600323c..0600323e: a row for every collectable type that has a registered state; the
        // increase and decrease actions of a row are OnChange with the sign fixed.
        private void PopulateCollectables()
        {
            foreach (CollectableType collectableType in EnumUtilities.GetValues<CollectableType>())
            {
                if (!m_collectableManager.TryGetCollectableState(collectableType, out CollectableState state))
                    continue;
                UIContainerDebugCollectableModifier debugCollectableModifier =
                    Instantiate(m_debugCollectableModifierPrefab, m_listRoot);
                debugCollectableModifier.Setup(new UIContainerDebugCollectableModifierParameters(
                    collectableType.GetString(), state.Collected, state.Total,
                    () => OnChange(debugCollectableModifier, collectableType, true),
                    () => OnChange(debugCollectableModifier, collectableType, false)));
            }
        }

        // 0600323a: the native closures inline this body; the collected count of the row follows the manager's state.
        private void OnChange(UIContainerDebugCollectableModifier container, CollectableType collectableType, bool positive)
        {
            m_collectableManager.ChangeCollectableAmount(new CollectableChangeData
            {
                Type = collectableType,
                FixedChange = positive ? m_changeValueBy : -m_changeValueBy
            });
            if (m_collectableManager.TryGetCollectableState(collectableType, out CollectableState state))
                container.UpdateCollected(state.Collected);
        }

        // 0600323b
        public UIContainerDebugCollectables() { }
    }
}
