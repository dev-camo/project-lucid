using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class LevelManagerSystems
    {
        private readonly Transform m_parent;
        private readonly Dictionary<Type, ISystem> m_managedSystems = new Dictionary<Type, ISystem>();
        private readonly Dictionary<Type, GameObject> m_managedGameObjects = new Dictionary<Type, GameObject>();

        // Original 0x06002499. The dictionary field initializers precede the
        // constructor's parent assignment, including its allocation failure order.
        public LevelManagerSystems(Transform parent)
        {
            m_parent = parent;
        }

        // Original 0x0600249a. This route creates an unparented object. Awake
        // runs during AddComponent, before either ownership dictionary is updated.
        public T AddMonoBehaviour<T>() where T : Component, ISystem
        {
            var gameObject = new GameObject(typeof(T).ToString());
            T system = gameObject.AddComponent<T>();
            m_managedGameObjects.Add(typeof(T), gameObject);
            AddManagedSystem(system);
            return system;
        }

        // Original 0x0600249b. Prefab Awake owns registry registration; this
        // method dispatches Initialise and records ownership without registering.
        public void ReplacePrefab<T>(T prefab) where T : Component, ISystem
        {
            Type type = typeof(T);
            RemoveManagedGameObject<T>();
            T system = UnityEngine.Object.Instantiate(prefab, m_parent);
            m_managedGameObjects.Add(type, system.gameObject);
            system.ProcessSystemAction(SystemAction.Initialise);
            m_managedSystems[type] = system;
        }

        // Original 0x0600249c. The valid registered system is removed only
        // when replacement is requested. Creation follows the shutdown callback.
        public void AddManagedSystem<T>(bool canReplace = false) where T : class, ISystem, new()
        {
            SystemRef<T> systemRef = ProcessManager.GetSystemRef<T>();
            if (systemRef != null && systemRef.IsValid())
            {
                if (!canReplace) return;
                RemoveManagedSystem(systemRef.Get());
            }
            AddManagedSystem(new T());
        }

        // Original 0x0600249d. Registry replacement and Initialise happen
        // before publication in the ownership dictionary; failures remain visible.
        public void AddManagedSystem<T>(T system, bool canReplace = false) where T : class, ISystem
        {
            ProcessManager.RegisterSystem(system, canReplace: canReplace);
            system.ProcessSystemAction(SystemAction.Initialise);
            m_managedSystems[typeof(T)] = system;
        }

        // Original 0x0600249e. Retail removes the supplied system, rather than
        // looking up the previous owned instance. Preserve that distinction.
        public void ReplaceManagedSystem<T>(T system) where T : class, ISystem
        {
            RemoveManagedSystem(system);
            AddManagedSystem(system);
        }

        // Original 0x0600249f. The observed generic call is ISystem, so per-
        // instance removal targets the ISystem key while enumerating live Values.
        // Clear follows the entire loop; a callback fault aborts subsequent cleanup.
        public void Shutdown()
        {
            foreach (ISystem system in m_managedSystems.Values)
                RemoveManagedSystem(system);
            m_managedSystems.Clear();
            foreach (GameObject gameObject in m_managedGameObjects.Values)
                UnityEngine.Object.Destroy(gameObject);
            m_managedGameObjects.Clear();
        }

        // Original 0x060024a0. Schedule destruction and remove the object row
        // before dispatching the matching system's shutdown callback.
        private void RemoveManagedGameObject<T>() where T : class, ISystem
        {
            Type type = typeof(T);
            if (m_managedGameObjects.TryGetValue(type, out GameObject gameObject))
            {
                UnityEngine.Object.Destroy(gameObject);
                m_managedGameObjects.Remove(type);
            }
            if (m_managedSystems.TryGetValue(type, out ISystem system))
                RemoveManagedSystem(system);
        }

        // Original 0x060024a1. A plain reference comparison is intentional:
        // Unity's destroyed-object equality is not used for this generic system.
        public void RemoveManagedSystem<T>(T system) where T : class, ISystem
        {
            if (system != null)
            {
                system.ProcessSystemAction(SystemAction.Shutdown);
                ProcessManager.UnregisterSystem(system);
            }
            m_managedSystems.Remove(typeof(T));
        }

        // Original 0x060024a2. Presence of the row determines the result even
        // when its value is null or no longer a valid registered system.
        public bool HasSystemOfType<T>() where T : class, ISystem
        {
            return m_managedSystems.TryGetValue(typeof(T), out ISystem system);
        }
    }
}
