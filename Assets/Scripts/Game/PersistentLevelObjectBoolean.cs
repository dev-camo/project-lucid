using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 02000a17; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class PersistentLevelObjectBoolean : PersistentLevelObjectType<bool>
	{
		[SerializeField]
		private UnityEvent m_onStateTrue;
		[SerializeField]
		private UnityEvent m_onStateFalse;
		[SerializeField]
		[Tooltip("This is the state that will be set if Challenges are active, regardless of the saved state. State changes are not saved during Challenges.")]
		private bool m_stateWhenChallenge = true;

		// 060039ca (1.10.1 RVA 0x6B3080)
		protected override SaveDataLevelPersistentObject GetPersistentObjectFromSaveDataLevel(SaveDataLevel saveDataLevel, PersistentObjectIdentifierType id, bool defaultValue)
		{
			return saveDataLevel.GetOrCreatePersistentObjectData(id, defaultValue);
		}

		// 060039cb (1.10.1 RVA 0x6B30A0)
		protected override bool GetSavedValue(SaveDataLevelPersistentObject saveDataPersistentObject)
		{
			return saveDataPersistentObject.GetBooleanValue();
		}

		// 060039cc (1.10.1 RVA 0x6B30B0)
		public override bool GetCachedValue()
		{
			return m_currentValue;
		}

		// 060039cd (1.10.1 RVA 0x6B30C0)
		protected override bool GetDefaultDuringChallenge()
		{
			return m_stateWhenChallenge;
		}

		// 060039ce (1.10.1 RVA 0x6B30D0)
		protected override void SetSavedValue(SaveDataLevelPersistentObject saveDataPersistentObject, bool newValue)
		{
			saveDataPersistentObject.Set(newValue);
		}

		// 060039cf (1.10.1 RVA 0x6B30F0): the base event runs first, then the matching
		// UnityEvent when it is assigned.
		protected override void OnStateChanged(bool newValue)
		{
			base.OnStateChanged(newValue);
			if (newValue)
				m_onStateTrue?.Invoke();
			else
				m_onStateFalse?.Invoke();
		}
	}
}
