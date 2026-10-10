using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class RequirementLevelCategoryBase : ScriptableObject
	{
		protected const string DefinitionsMenu = "HardlightProject/Requirements/";

		public abstract bool RequirementsMet(GameplayLevelCategory category);
        // Game.Runtime 0x06001f18, ARM64 0x52eb58..0x52eb60: original protected ScriptableObject constructor forward.
        protected RequirementLevelCategoryBase() { }
	}
}
