using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "RequirementCategoryAlwaysHidden", menuName = "HardlightProject/Requirements/RequirementCategoryAlwaysHidden")]
	public sealed class RequirementCategoryAlwaysHidden : RequirementLevelCategoryBase
	{
		// Game.Runtime 0x06001f0d: supplied player is exactly mov w0,0;ret.
		public override bool RequirementsMet(GameplayLevelCategory category)
		{
			return false;
		}
        // Game.Runtime 0x06001f0e: genuine requirement-base constructor forward.
        public RequirementCategoryAlwaysHidden() { }
	}
}
