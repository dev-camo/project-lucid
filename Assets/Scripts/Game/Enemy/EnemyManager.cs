using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Complete original Game.Runtime 02000654; its 02000655 is emitted naturally
    // by the empty field-initializer lambda below.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyManager : ISystem, ITimeScaled
    {
        private const string DebugMenuPath = "Enemy Debug";
        private const string DebugMenuButton = "Toggle Display";
        private readonly List<Enemy> m_enemies = new List<Enemy>();
        private readonly List<Enemy> m_enemiesPendingInitialisation = new List<Enemy>();
        private bool m_fsmsReady;
        private CharacterManager m_characterManager;
        private readonly List<Actor> m_enemyTargets = new List<Actor>();
        private SystemRef<CharacterManager> m_characterManagerRef;
        private FSMStateLoader m_fsmStateLoader;

        public bool IsPaused { get; set; }

        public Action OnEnemyDestroyedByPlayer = () => { };

        public EnemyManager()
        {
            ProcessManager.SubscribeToAction(this, SystemAction.Initialise, Initialise);
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown, Shutdown);
        }

        public void OnUpdate(float deltaTime)
        {
            if (m_characterManager != null &&
                m_characterManager.TryGetCurrentCharacter(out Character character))
            {
                Vector3 position = character.WorldPosition;
                foreach (Enemy enemy in m_enemies)
                    enemy.SetEnabledByProximity(position);
            }
        }

        private void Initialise(object context = null)
        {
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            m_characterManagerRef.OnSystemStartup += InitialiseCharacterManager;
            ProcessManager.GetSystemRef<TimeManager>(null, true)
                .InvokeOnValid(InitialiseTimeManager);
            ProcessManager.GetSystemRef<LevelManager>(null, true)
                .InvokeOnValid(OnLevelManagerValid);
        }

        private void InitialiseTimeManager(TimeManager timeManager) =>
            timeManager.Subscribe(this, TimeCategory.EnemyMovement, UpdateOn.Update, "");

        private void OnLevelManagerValid(LevelManager levelManager) =>
            levelManager.InvokeOnLevelActivated(OnLevelActivated, true);

        private void Shutdown(object context = null)
        {
            foreach (FiniteStateMachineScriptableObject stateMachine in
                m_fsmStateLoader.LoadedStateMachines)
                stateMachine.ReleaseFSM();

            SystemRef<TimeManager> timeManagerRef =
                ProcessManager.GetSystemRef<TimeManager>(null, true);
            if (timeManagerRef.IsValid())
                timeManagerRef.Get().Unsubscribe(this, TimeCategory.EnemyMovement, UpdateOn.Update);

            m_characterManagerRef.OnSystemStartup -= InitialiseCharacterManager;
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().ReleaseOnCharacterChange(UpdateTargetsForEnemies);

            LevelManager levelManager = ProcessManager.GetSystemSafe<LevelManager>(null, true);
            if (levelManager != null)
                levelManager.RemoveLevelActivatedAction(OnLevelActivated);
        }

        private void InitialiseCharacterManager(CharacterManager characterManager)
        {
            m_characterManager = characterManager;
            m_characterManager.RegisterOnCharacterChange(UpdateTargetsForEnemies, true);
            var stateMachines = new Dictionary<string, FiniteStateMachineScriptableObject>(
                ProcessManager.GetSystem<App>(null, true).DataManager.EnemyStateMachines);
            m_fsmStateLoader = new FSMStateLoader(stateMachines.Values, OnAllFSMsLoaded);
            m_fsmStateLoader.LoadStates();
        }

        private void OnAllFSMsLoaded()
        {
            m_fsmsReady = true;
            foreach (Enemy enemy in m_enemiesPendingInitialisation)
                enemy.Initialise(enemy.Definition);
            m_enemiesPendingInitialisation.Clear();
        }

        public void Register(Enemy enemy)
        {
            m_enemies.AddUnique(enemy);
            if (m_fsmsReady)
                enemy.Initialise(enemy.Definition);
            else
                m_enemiesPendingInitialisation.Add(enemy);
        }

        public void Deregister(Enemy enemy)
        {
            m_enemies.Remove(enemy);
            m_enemiesPendingInitialisation.Remove(enemy);
        }

        private void OnLevelActivated(LevelManagerLevel level)
        {
            foreach (Enemy enemy in m_enemies)
                enemy.ResetGameplayElement();
        }

        private void UpdateTargetsForEnemies(Character character)
        {
            m_enemyTargets.Clear();
            if (character != null)
                m_enemyTargets.Add(character);
        }

        public IReadOnlyCollection<Actor> GetTargetsForEnemies() => m_enemyTargets;

        private void ToggleDebugInfo()
        {
            foreach (Enemy enemy in m_enemies)
            {
            }
        }

        public void ReportEnemyDestroyedByPlayer()
        {
            ProcessManager.GetSystem<SaveManager>(null, true).CurrentSave
                .GetOrCreatePlayerStatData(SaveDataPlayerStat.Type.EnemiesDestroyed)
                .IncrementCounter(1L);
            OnEnemyDestroyedByPlayer();
        }
    }
}

