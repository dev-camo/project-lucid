using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CollectableManager : ISystem
    {
        public Action<CollectableType, CollectableState, CollectableChangeMetadata> OnCollectableStateUpdate;
        public Action<CollectableType> OnCollectableForceDeactivate;
        // Original 06003b1d installs this genuine empty default delegate. Public
        // replacement with null or a throwing callback retains direct invocation.
        public Action OnAnyCollectableAmountChanged = () => { };
        private readonly Dictionary<CollectableType, CollectableState> m_collectableStates =
            new Dictionary<CollectableType, CollectableState>(HardlightEnumComparers.CollectableTypeComparer);
        private readonly Dictionary<CollectableType, CollectableTier> m_collectableTiers =
            new Dictionary<CollectableType, CollectableTier>(HardlightEnumComparers.CollectableTypeComparer);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();

        public void RegisterCollectable(CollectableType collectableType, int amount = 1)
        {
            m_collectableStates.TryGetOrNew(collectableType).AddCollectable(amount);
        }

        private void UpdateCollectableState(CollectableType collectableType,
            CollectableState collectableState, CollectableChangeMetadata metadata = default)
        {
            OnCollectableStateUpdate?.Invoke(collectableType, collectableState, metadata);
        }

        public bool TryGetCollectableState(CollectableType collectableType, out CollectableState collectableState)
        {
            return m_collectableStates.TryGetValue(collectableType, out collectableState);
        }

        public CollectableDefinition GetCollectableDefinition(CollectableType collectableType)
        {
            ProcessManager.GetSystem<DataManager>().CollectableDefinitions.TryGetValue(collectableType,
                out CollectableDefinition definition);
            return definition;
        }

        public int ChangeCollectableAmount(CollectableType collectableType, int amount,
            CollectableChangeMetadata metadata)
        {
            // Original 06003b11 resolves the authored definition even when no
            // registered state exists; the overloaded definition equality is
            // evaluated only after a successful state lookup.
            CollectableDefinition definition = GetCollectableDefinition(collectableType);
            if (!m_collectableStates.TryGetValue(collectableType, out CollectableState state) || definition == null)
            {
                HLOutput.LogError("No collectable state found for type: " + collectableType.GetString());
                return 0;
            }

            int applied = state.AdjustCollected(amount, definition.HasTotal);
            if (applied != 0)
            {
                // The native stat increments by the requested positive amount,
                // not the possibly capped applied difference. Save operations
                // precede notification, and their failures are not swallowed.
                if (collectableType == CollectableType.Ring && amount > 0)
                    m_saveManagerRef.Get().CurrentSave.GetOrCreatePlayerStatData(SaveDataPlayerStat.Type.RingsCollected)
                        .IncrementCounter(amount);
                OnAnyCollectableAmountChanged();
                UpdateCollectableState(collectableType, state, metadata);
            }
            return applied;
        }

        public int ChangeCollectableAmount(CollectableChangeData changeData)
        {
            if (!m_collectableStates.TryGetValue(changeData.Type, out CollectableState state)) return 0;
            // Original 06003b12 multiplies the current Collected count, converts
            // toward zero, then wraps the addition. Invalid float conversion
            // domains differ between original native slices and stay unapproved.
            int amount = unchecked((int)(changeData.FractionChange * state.Collected) + changeData.FixedChange);
            return ChangeCollectableAmount(changeData.Type, amount, changeData.Metadata);
        }

        public void CollectOrb(CollectableType collectableType, CollectableChangeMetadata metadata)
        {
            if (!m_collectableStates.TryGetValue(collectableType, out CollectableState state)) return;
            // Original 06003b13 emits both notifications without changing count.
            OnAnyCollectableAmountChanged();
            UpdateCollectableState(collectableType, state, metadata);
        }

        public void ResetTemporaryCollectables()
        {
            // Original 06003b14 disposes its dictionary enumerator; no callbacks,
            // force-deactivation, tier reset or collection-registration removal.
            foreach (var (type, state) in m_collectableStates) state.Reset();
        }

        public void SetCollectableTierIndex(CollectableType collectableType, int tierIndex)
        {
            m_collectableTiers.TryGetOrNew(collectableType).Index = tierIndex;
        }

        public int GetCollectableTierIndex(CollectableType collectableType)
        {
            return m_collectableTiers.TryGetValue(collectableType, out CollectableTier tier) ? tier.Index : 0;
        }

        public void SetCollectableTierMaxDamage(CollectableType collectableType, int maxDamage)
        {
            m_collectableTiers.TryGetOrNew(collectableType).MaxDamage = maxDamage;
        }

        public void GetCollectableTierMaxDamage(ref CollectableChangeData changeData)
        {
            if (!m_collectableTiers.TryGetValue(changeData.Type, out CollectableTier tier)) return;
            int maxDamage = tier.MaxDamage;
            if (maxDamage == 0) return;
            int negativeLimit = unchecked(-maxDamage);
            changeData.FixedChange = changeData.FixedChange > negativeLimit ? changeData.FixedChange : negativeLimit;
            changeData.FractionChange = 0f;
        }

        public void SetCollectableTierStartingCount(CollectableType collectableType, int startingCount)
        {
            m_collectableTiers.TryGetOrNew(collectableType).StartingCount = startingCount;
        }

        public int GetCollectableTierStartingCount(CollectableType collectableType)
        {
            return m_collectableTiers.TryGetValue(collectableType, out CollectableTier tier) ? tier.StartingCount : 0;
        }

        public void SetCollectableTierReviveMultiplier(CollectableType collectableType, float reviveMultiplier)
        {
            m_collectableTiers.TryGetOrNew(collectableType).ReviveMultiplier = reviveMultiplier;
        }

        public void GetCollectableTierReviveMultiplier(ref CollectableChangeData changeData)
        {
            if (!m_collectableTiers.TryGetValue(changeData.Type, out CollectableTier tier)) return;
            float reviveMultiplier = tier.ReviveMultiplier;
            if (reviveMultiplier == 0f) return;
            changeData.FixedChange = 0;
            float fraction = changeData.FractionChange;
            float negativeLimit = -reviveMultiplier;
            // Ordered greater-than chooses the original fraction. NaN therefore
            // selects the second operand, matching the native FCSEL predicate.
            changeData.FractionChange = fraction > negativeLimit ? fraction : negativeLimit;
        }

        // Original initializer order is default action, state dictionary, tier
        // dictionary, SaveManager reference, then Object's constructor.
        public CollectableManager() { }
    }
}
