using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIManager : MonoBehaviour, ISystem
    {
        [SerializeField] private Camera m_guiCamera;
        [SerializeField] private Transform m_freeParent;
        [SerializeField] private Transform m_linkedParent;
        [SerializeField] private Transform m_singleParent;
        [SerializeField] private Transform m_stackParent;
        [SerializeField] private Transform m_transitionParent;
        [SerializeField] private Transform m_debugParent;
        [SerializeField] private List<UIVisibilityGroupDefinition> m_visibilityGroupDefinitions;
        public Action<UIContainer> OnContainerCreated;
        public event Action<UIContainerIdentifier> OnContainerClosed = delegate { };
        private readonly Dictionary<UIContainerIdentifier, int> m_containerLastSelectedWidgetIndex = new Dictionary<UIContainerIdentifier, int>();
        private readonly Dictionary<UIContainerBehaviour, UIContainerManager> m_containerManagers = new Dictionary<UIContainerBehaviour, UIContainerManager>(HardlightEnumComparers.UIContainerBehaviourComparer);
        private const int StackableDataID = 0;
        private StackableData m_visibilityGroupsStack;
        private readonly Dictionary<string, List<UIVisibilityGroupMember>> m_uiVisibilityGroupMembers = new Dictionary<string, List<UIVisibilityGroupMember>>();
        private readonly Dictionary<string, bool> m_workingUIGroupVisibility = new Dictionary<string, bool>();

        // HLModernUI.Runtime060000f2: constructor arguments follow the original
        // six-manager insertion order. Registration occurs after base data/event setup.
        private void Awake()
        {
            m_containerManagers.Add(UIContainerBehaviour.Free, new UIFree(m_freeParent));
            m_containerManagers.Add(UIContainerBehaviour.Linked, new UILinked(m_linkedParent));
            m_containerManagers.Add(UIContainerBehaviour.Single, new UISingular(m_singleParent));
            m_containerManagers.Add(UIContainerBehaviour.Stack, new UIStacker(m_stackParent));
            m_containerManagers.Add(UIContainerBehaviour.Transition, new UITransitionContainerManager(m_transitionParent));
            m_containerManagers.Add(UIContainerBehaviour.Debug, new UIDebug(m_debugParent));
            m_uiVisibilityGroupMembers.Clear();
            var defaults = new Dictionary<string, bool>(m_visibilityGroupDefinitions.Count);
            foreach (UIVisibilityGroupDefinition definition in m_visibilityGroupDefinitions)
                defaults.Add(definition.GetGUID(), definition.InitialVisibility);
            StackableData.RegisterTypeOperations<Dictionary<string, bool>>(VisibilityGroupsStackCombiner);
            m_visibilityGroupsStack = new StackableData();
            m_visibilityGroupsStack.SetBaseValue(StackableDataID, defaults, StackableData.RetrievalOperation.LogicalAnd);
            m_visibilityGroupsStack.OnDataUpdated += OnStackableDataUpdated;
            ProcessManager.RegisterSystem(this);
        }

        //060000f3: opening still occurs when parameters are null, but the returned
        // value is null unless both the Unity object and parameters pass their guards.
        public UIContainer GetOrCreate(UIContainerIdentifier containerIdentifier, IUIContainerParameters parameters)
        {
            UIContainer container = GetOrCreate(containerIdentifier);
            if (container == null || parameters == null) return null;
            container.Setup(parameters);
            return container;
        }

        //060000f4: the identifier's original prefab lease is acquired before the
        // manager lookup. The creation callback also runs for a reused container.
        public UIContainer GetOrCreate(UIContainerIdentifier containerIdentifier)
        {
            UIContainerManager manager = null;
            UIContainer prefab = containerIdentifier.GetOrCreateContainer();
            UIContainer container = null;
            if (m_containerManagers.TryGetValue(prefab.Behaviour, out manager))
            {
                container = manager.OpenContainer(prefab);
                container.AssignCamera(m_guiCamera);
                OnContainerCreated?.Invoke(container);
            }
            return container;
        }

        //060000f5/6: registration delegates to the identifier; unregister retains
        // its original ignored-argument decrement behavior.
        public void Register(UIContainer container) { container.Identifier.RegisterContainer(container); }
        public void Unregister(UIContainer container) { container.Identifier.UnregisterContainer(container); }

        //060000f7: every manager is visited, including managers after a successful match.
        public void SetEnabled(UIContainerIdentifier identifier, bool enable)
        {
            foreach (UIContainerManager manager in m_containerManagers.Values)
                manager.SetEnabled(identifier, enable);
        }

        //060000f8: notification follows complete enumerator disposal, even when no
        // manager owned the identifier. The initialized event is invoked directly.
        public void Close(UIContainerIdentifier identifier)
        {
            foreach (UIContainerManager manager in m_containerManagers.Values) manager.Close(identifier);
            OnContainerClosed(identifier);
        }

        //060000f9/fa: bulk closing deliberately excludes Transition containers.
        public void CloseAllContainersExcept(IReadOnlyList<UIContainerIdentifier> exceptFor)
        {
            foreach (var (behaviour, manager) in m_containerManagers)
                if (behaviour != UIContainerBehaviour.Transition) manager.CloseAllExcept(exceptFor);
        }
        public void CloseAllContainers()
        {
            foreach (var (behaviour, manager) in m_containerManagers)
                if (behaviour != UIContainerBehaviour.Transition) manager.CloseAll();
        }

        //060000fb: first manager match wins; no identifier validity precheck.
        public bool IsOpen(UIContainerIdentifier identifier)
        {
            foreach (UIContainerManager manager in m_containerManagers.Values)
                if (manager.IsOpen(identifier)) return true;
            return false;
        }

        //060000fc: the matching manager need not contain only one container; only
        // other manager families' IsAnyOpen values participate in exclusivity.
        public bool IsOnlyContainerOpen(UIContainerIdentifier identifier)
        {
            bool found = false;
            foreach (UIContainerManager manager in m_containerManagers.Values)
            {
                if (manager.IsOpen(identifier)) found = true;
                else if (manager.IsAnyOpen()) return false;
            }
            return found;
        }
        //060000fd: the original is a subset check across managers, including an empty map.
        public bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers)
        {
            foreach (UIContainerManager manager in m_containerManagers.Values)
                if (!manager.AreExclusivelyOpen(identifiers)) return false;
            return true;
        }

        //060000fe: out is passed directly to each manager; null is written only
        // after all managers and their enumerator finish unsuccessfully.
        public bool TryGet(UIContainerIdentifier identifier, out UIContainer container)
        {
            foreach (UIContainerManager manager in m_containerManagers.Values)
                if (manager.TryGet(identifier, out container)) return true;
            container = null;
            return false;
        }
        //060000ff..101: preserve the separate type test and cast, including wrong-type null.
        public bool TryGetAs<T>(UIContainerIdentifier identifier, out T container) where T : UIContainer
        {
            UIContainer found = null;
            if (TryGet(identifier, out found) && found is T)
            {
                container = (T)found;
                return true;
            }
            container = null;
            return false;
        }
        public T GetOrCreate<T>(UIContainerIdentifier identifier) where T : UIContainer
        {
            UIContainer container = GetOrCreate(identifier);
            if (container is T) return (T)container;
            return null;
        }
        public T GetOrCreate<T>(UIContainerIdentifier identifier, IUIContainerParameters parameters) where T : UIContainer
        {
            UIContainer container = GetOrCreate(identifier, parameters);
            if (container is T) return (T)container;
            return null;
        }

        //06000102: missing GUID list creation precedes AddUnique; visibility is
        // indexed directly, so an absent default definition remains an error.
        public void RegisterUIVisibilityGroupMember(UIVisibilityGroupDefinition visibilityGroupDefinition, UIVisibilityGroupMember uiVisibilityGroupMember)
        {
            string guid = visibilityGroupDefinition.GetGUID();
            if (!m_uiVisibilityGroupMembers.ContainsKey(guid))
                m_uiVisibilityGroupMembers.Add(guid, new List<UIVisibilityGroupMember>());
            m_uiVisibilityGroupMembers[guid].AddUnique(uiVisibilityGroupMember);
            uiVisibilityGroupMember.OnVisibilityChanged(m_visibilityGroupsStack.Get<Dictionary<string, bool>>(StackableDataID)[guid]);
        }
        //06000103: preserve an empty member list and do not reset member visibility.
        public void UnregisterUIVisibilityGroupMember(UIVisibilityGroupDefinition visibilityGroupDefinition, UIVisibilityGroupMember uiVisibilityGroupMember)
        {
            List<UIVisibilityGroupMember> members = null;
            if (m_uiVisibilityGroupMembers.TryGetValue(visibilityGroupDefinition.GetGUID(), out members))
                members.Remove(uiVisibilityGroupMember);
        }

        //06000104: live dictionary/list enumeration intentionally exposes the
        // original mutation behavior; no snapshot, missing-member filter or catch.
        private void OnStackableDataUpdated(int id)
        {
            if (id != StackableDataID) return;
            foreach (var (guid, visible) in m_visibilityGroupsStack.Get<Dictionary<string, bool>>(StackableDataID))
            {
                List<UIVisibilityGroupMember> members = null;
                if (m_uiVisibilityGroupMembers.TryGetValue(guid, out members))
                    foreach (UIVisibilityGroupMember member in members) member.OnVisibilityChanged(visible);
            }
        }

        //06000105: first encountered GUID wins. The shared working map is cleared
        // only for an unset carrier; count equality permits early exit.
        private StackableData.OperationAction VisibilityGroupsStackCombiner(
            StackableData.StackableDataContainer<Dictionary<string, bool>> visibilityOverride,
            ref StackableData.ResultCarrier<Dictionary<string, bool>> result)
        {
            if (!result.HasValue)
            {
                m_workingUIGroupVisibility.Clear();
                result.SetValue(m_workingUIGroupVisibility);
            }
            foreach (var (guid, visible) in visibilityOverride.Value)
                m_workingUIGroupVisibility.TryAdd(guid, visible);
            return m_workingUIGroupVisibility.Count == m_visibilityGroupDefinitions.Count
                ? StackableData.OperationAction.EarlyExit : StackableData.OperationAction.Continue;
        }
        //06000106/107: direct original StackableData handle routes.
        public void RemoveVisibilityOverrides(StackableDataHandle stackableDataHandle)
        {
            m_visibilityGroupsStack.RemoveOverrides(stackableDataHandle);
        }
        public StackableDataHandle AddVisibilityOverrides(Dictionary<string, bool> overridesDictionary)
        {
            return m_visibilityGroupsStack.AddOverride(StackableDataID, overridesDictionary);
        }
        //06000108/109: stored selected indices remain keyed by the original GUID object.
        public void SetSelectedWidget(UIContainer container, int uiWidgetNavigationIndex)
        {
            m_containerLastSelectedWidgetIndex[container.Identifier] = uiWidgetNavigationIndex;
        }
        public bool TryGetSelectedWidget(UIContainer container, out int index)
        {
            return m_containerLastSelectedWidgetIndex.TryGetValue(container.Identifier, out index);
        }
        //0600010a..10d: original field initializers and natural empty event delegate.
        public UIManager() { }
    }
}
