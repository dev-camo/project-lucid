using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002e9; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE63D80..0xE64330). A modifier ability applies its effect to each character ability
	// its definition lists, or once with no target when the list is empty.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class CharacterAbility_Modifier<T> : ActorAbility where T : CharacterAbilityDefinition_Modifier
	{
		protected Character m_character;

		// 060011c8 (1.10.1 RVA 0xE63D80), 060011c9 (1.10.1 RVA 0xE63D90)
		protected T Definition { get; private set; }

		// 060011ca (1.10.1 RVA 0xE63DB0)
		public override AbilityDefinition DefinitionBase
		{
			get
			{
				return Definition;
			}
		}

		// 060011cb (1.10.1 RVA 0xE63DC0)
		private bool ModifiesAbilities
		{
			get
			{
				return Definition.ModifyAbilities.Count > 0;
			}
		}

		// 060011cc (1.10.1 RVA 0xE63E00); native code compares Definition with null through
		// UnityEngine.Object.op_Inequality and discards the result.
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			m_character = actor as Character;
			Definition = definition as T;
			if (Definition != null)
			{
			}
		}

		// 060011cd (1.10.1 RVA 0xE63F80)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			AddModifiers();
		}

		// 060011ce (1.10.1 RVA 0xE63FB0)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			RemoveModifiers();
		}

		// 060011cf (1.10.1 RVA 0xE63FE0); modifying listed abilities waits for the next fixed update,
		// the lambda being <AddModifiers>b__12_0 (0xE64330).
		private void AddModifiers()
		{
			if (ModifiesAbilities)
			{
				CoroutineUtils.OnFixedUpdate(() => ForEachModifyAbility(AddModifier));
			}
			else
			{
				AddModifier(null);
			}
		}

		// 060011d0 (1.10.1 RVA 0xE640C0)
		private void RemoveModifiers()
		{
			if (ModifiesAbilities)
			{
				ForEachModifyAbility(RemoveModifier);
			}
			else
			{
				RemoveModifier(null);
			}
		}

		// 060011d1 (1.10.1 RVA 0xE64180)
		private void ForEachModifyAbility(Action<CharacterAbility> callback)
		{
			foreach (ActorAbilityType abilityType in Definition.ModifyAbilities)
			{
				if (m_character.TryGetAbilityFromType(abilityType, out ActorAbility ability) && ability is CharacterAbility characterAbility)
				{
					callback(characterAbility);
				}
			}
		}

		// 060011d2
		protected abstract void AddModifier(CharacterAbility modifyAbility);

		// 060011d3
		protected abstract void RemoveModifier(CharacterAbility modifyAbility);

		// 060011d4 (1.10.1 RVA 0xE64320)
		protected CharacterAbility_Modifier()
		{
		}
	}
}
