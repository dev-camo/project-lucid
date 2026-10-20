using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000404; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("SettingHomingAttack")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterTransition_SettingHomingAttack : CharacterTransition_Setting
	{
		// 06001797 (1.10.1 RVA 0x794440)
		protected CharacterTransition_SettingHomingAttack(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06001798 (1.10.1 RVA 0x794470)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_SettingHomingAttack(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001799 (1.10.1 RVA 0x794510)
		protected override ButtonMappingType GetButtonMapping(SaveDataSettings saveDataSettings)
		{
			return saveDataSettings.HomingAttackMapping;
		}
	}
}
