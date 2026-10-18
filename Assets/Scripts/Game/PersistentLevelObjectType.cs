using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000a18; bodies translated from the 1.10.1 x86-64 <bool>
	// instantiation, checked against the fully shared one. The identifier enum has no zero
	// member, so an unset id (0) keeps the object out of the save data.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class PersistentLevelObjectType<T> : PersistentLevelObject
	{
		[SerializeField]
		private PersistentObjectIdentifierType m_id;
		[SerializeField]
		private T m_defaultValue;
		private SaveDataLevelPersistentObject m_saveData;
		protected T m_currentValue;
		private LevelManager m_levelManager;
		private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();
		public FastAction OnStateChangedEvent = new FastAction();

		// 060039d1 (<bool> 0x18CAAE0, shared 0x18CB1D0): an unset id announces the default
		// at once; otherwise the value waits for the level manager.
		private void Awake()
		{
			m_currentValue = m_defaultValue;
			if (m_id != 0)
				ProcessManager.GetSystemRef<LevelManager>().InvokeOnValid(OnLevelManagerValid);
			else
				OnStateChanged(m_currentValue);
		}

		// 060039d2 (<bool> 0x18CABE0, shared 0x18CB3E0)
		private void OnDestroy()
		{
			if (m_levelManager != null)
				m_levelManager.RemoveLevelActivatedAction(OnLevelActivated);
		}

		// 060039d3 (<bool> 0x18CACA0, shared 0x18CB4E0): a current level is handled here and
		// then subscribed with triggerImmediately, so it is handled a second time.
		private void OnLevelManagerValid(LevelManager levelManager)
		{
			m_levelManager = levelManager;
			if (levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
				OnLevelActivated(level);
			levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
		}

		// 060039d4 (<bool> 0x18CAD70, shared 0x18CB5E0)
		private void UpdateCurrentValueFromSave(LevelManagerLevel level)
		{
			SaveDataLevel saveDataLevel = ProcessManager.GetSystem<SaveManager>().CurrentSave.GetOrCreateLevelData(level.LevelDefinition.GetGUID());
			m_saveData = GetPersistentObjectFromSaveDataLevel(saveDataLevel, m_id, m_defaultValue);
			m_currentValue = GetSavedValue(m_saveData);
		}

		// 060039d5 (<bool> 0x18CAE40, shared 0x18CB830): a challenge never reads the save.
		private void OnLevelActivated(LevelManagerLevel level)
		{
			if (ChallengeModeActive())
				m_currentValue = GetDefaultDuringChallenge();
			else
				UpdateCurrentValueFromSave(level);
			OnStateChanged(m_currentValue);
		}

		// 060039d6 (<bool> 0x18CAF10, shared 0x18CB980): during a challenge the state changes
		// without being saved. Otherwise the save record is written before the state change
		// and the save is requested after it.
		public void Action_SetPersistentState(T newValue)
		{
			if (m_id == 0) return;
			m_currentValue = newValue;
			if (ChallengeModeActive())
			{
				OnStateChanged(newValue);
				return;
			}
			SetSavedValue(m_saveData, newValue);
			OnStateChanged(newValue);
			ProcessManager.GetSystem<SaveManager>().RequestSave();
		}

		// 060039d7 (<bool> 0x18CB060, shared 0x18CBC30)
		protected virtual void OnStateChanged(T newValue)
		{
			OnStateChangedEvent?.Invoke();
		}

		// 060039d8 (<bool> 0x18CB080, shared 0x18CBC70)
		private bool ChallengeModeActive()
		{
			return m_missionManagerRef.Get()?.ActiveMissionContext is MissionContextChallenge;
		}

		// 060039d9..060039dd: slots 5 to 9.
		protected abstract SaveDataLevelPersistentObject GetPersistentObjectFromSaveDataLevel(SaveDataLevel saveDataLevel, PersistentObjectIdentifierType id, T defaultValue);

		protected abstract T GetSavedValue(SaveDataLevelPersistentObject saveDataPersistentObject);

		protected abstract void SetSavedValue(SaveDataLevelPersistentObject saveDataPersistentObject, T newValue);

		public abstract T GetCachedValue();

		protected abstract T GetDefaultDuringChallenge();

		// 060039de (<bool> 0x18CB110, shared 0x18CBD20)
		protected PersistentLevelObjectType()
		{
		}
	}
}
