using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000653; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class EnemyDebug : ActorDebug
	{
		private GameObject m_debugUIContainer;

		// 06002206 (1.10.1 RVA 0x576580)
		protected override string ContainerIdentifierName => "uicontaineridentifier_enemy_debug_info";

		// 06002207 (1.10.1 RVA 0x5765B0): the toggle is bound to the boss debug input, as in the retail build.
		public override void Init(Actor actor)
		{
			base.Init(actor);
			m_debugUIContainer = transform.Find("uicontainer_enemy_debug_info").gameObject;
			ControlMapping.Subscribe(GameInput.BossDebugInfo, new Action<float>(OnEnemyInfo), InputTrigger.Down, -1);
		}

		// 06002208 (1.10.1 RVA 0x5766B0)
		private void OnEnemyInfo(float amount)
		{
			ToggleDebugInfo();
		}

		// 06002209 (1.10.1 RVA 0x5766D0): IsEnabled takes the container's state from before the toggle.
		public override void ToggleDebugInfo()
		{
			IsEnabled = m_debugUIContainer.activeInHierarchy;
			m_debugUIContainer.SetActive(!IsEnabled);
		}

		// 0600220a (1.10.1 RVA 0x576700)
		private void OnDestroy()
		{
			ControlMapping.Unsubscribe(GameInput.BossDebugInfo, new Action<float>(OnEnemyInfo), -1);
		}

		// 0600220b (1.10.1 RVA 0x5767A0)
		public EnemyDebug()
		{
		}
	}
}
