using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200084d; bodies translated from the 1.10.1 x86-64 binary.
	// Pairs a level definition with the level widget that shows it.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public sealed class UIWidgetForGameplayLevelDefinition
	{
		[SerializeField]
		private GameplayLevelDefinition m_gameplayLevelDefinition;

		[SerializeField]
		private UIWidgetSDTLevel m_widgetSDTLevel;

		// 0x06003017 (0x60D360)
		public GameplayLevelDefinition LevelDefinition => m_gameplayLevelDefinition;

		// 0x06003018 (0x60D370)
		public UIWidgetSDTLevel Widget => m_widgetSDTLevel;

		// 0x06003019
		public UIWidgetForGameplayLevelDefinition() { }
	}
}
