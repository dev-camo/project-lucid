using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200019f; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "AppRatingRequesterBoss", menuName = "HardlightProject/DefinitionData/Definitions/AppRatingRequesterBoss")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public sealed class AppRatingRequesterBoss : AppRatingRequesterBase
	{
		[SerializeField]
		private GameplayLevelDefinition m_bossDefinition;

		private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);

		// 060008b3 (1.10.1 RVA 0x667A90): a rating may be requested once the base cooldown allows it, a mission manager
		// exists and any mission of the boss level has been completed.
		protected override bool Validate()
		{
			return base.Validate() && !m_missionManagerRef.IsNull() && m_missionManagerRef.Get().AnyMissionCompleted(m_bossDefinition);
		}

		// 060008b4 (1.10.1 RVA 0x667B20): the field initialiser precedes the base constructor, which sets the seven day cooldown.
		public AppRatingRequesterBoss()
		{
		}
	}
}
