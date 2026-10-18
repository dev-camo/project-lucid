using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200074b; bodies translated from the 1.10.1 x86-64 binary.
	// A shared asset form of TriggerCondition: the answer is cached until the character's next
	// pre-FSM update, and the character subscriptions made in OnEnable are never released.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class TriggerCondition_CachedBase : ScriptableObjectWithGuid
	{
		[SerializeField]
		private bool m_invert;

		protected static readonly SystemRef<CharacterManager> s_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);

		protected bool m_hasCache;

		protected const string AssetMenu = "HardlightProject/TriggerConditions/";

		// 060028fd (1.10.1 RVA 0x5CCEB0)
		protected virtual void OnEnable()
		{
			s_characterManagerRef.OnSystemStartup += OnCharacterManagerStartup;
			if (s_characterManagerRef.TryGet(out CharacterManager characterManager))
				characterManager.RegisterOnCharacterChange(OnCharacterChange, true);
		}

		// 060028fe (1.10.1 RVA 0x5CD020)
		private void OnCharacterManagerStartup(CharacterManager characterManager)
		{
			characterManager.RegisterOnCharacterChange(OnCharacterChange, true);
		}

		// 060028ff (1.10.1 RVA 0x5CD0A0)
		private void OnCharacterChange(Character character)
		{
			character.OnFixedUpdatePreFSM += OnCharacterPreFSM;
		}

		// 06002900 (1.10.1 RVA 0x5CD190)
		private void OnCharacterPreFSM(Character character)
		{
			m_hasCache = false;
		}

		// 06002901
		protected abstract bool DoEvaluate(Character character);

		// 06002902 (1.10.1 RVA 0x5CD1A0)
		public bool Evaluate()
		{
			return DoEvaluate() != m_invert;
		}

		// 06002903 (1.10.1 RVA 0x5CD1D0): false while no character is current.
		protected virtual bool DoEvaluate()
		{
			return TryGetCharacter(out Character character) && DoEvaluate(character);
		}

		// 06002904 (1.10.1 RVA 0x5CD350)
		public bool Evaluate(Character character)
		{
			return DoEvaluate(character) != m_invert;
		}

		// 06002905 (1.10.1 RVA 0x5CD2D0)
		private static bool TryGetCharacter(out Character character)
		{
			return s_characterManagerRef.Get().TryGetCurrentCharacter(out character);
		}
	}
}
