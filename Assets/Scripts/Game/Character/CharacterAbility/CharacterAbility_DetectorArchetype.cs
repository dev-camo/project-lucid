using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002f3; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_DetectorArchetype : CharacterAbility_Detector<CharacterAbilityDefinition_DetectorArchetype>
	{
		private DetectorArchetypeUpdateMessage m_archetypeUpdateMessage;

		private readonly SystemRef<MessageManager> m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>();

		private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();

		// 060011fb (1.10.1 RVA 0x74C810)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			m_levelManagerRef.InvokeOnValid(levelManager => levelManager.InvokeOnLevelActivated(OnLevelActivated, true));
		}

		// 060011fc (1.10.1 RVA 0x74C8D0); empty in the shipped binary.
		protected override void DoOnDetect()
		{
		}

		// 060011fd (1.10.1 RVA 0x74C8E0)
		private void OnLevelActivated(LevelManagerLevel level)
		{
			PublishDetectorMessage(m_character.Definition.Archetype == Definition.Archetype);
		}

		// 060011fe (1.10.1 RVA 0x74C940)
		private void PublishDetectorMessage(bool isValid)
		{
			m_archetypeUpdateMessage.Archetype = Definition.Archetype;
			m_archetypeUpdateMessage.IsValid = isValid;
			MessageManager manager = m_messageManagerRef.GetSafe();
			if (manager != null)
			{
				Component sender = m_appRef.Get();
				manager.ComponentMessagesWithCompletion.PublishMessage(in sender, in m_archetypeUpdateMessage, () => { }, -1);
			}
		}

		// 060011ff (1.10.1 RVA 0x74CAE0)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
		}

		// 06001200 (1.10.1 RVA 0x74CB90)
		public CharacterAbility_DetectorArchetype()
		{
		}
	}
}
