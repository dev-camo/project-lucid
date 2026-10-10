using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class RequirementGameplayLevelBase : ScriptableObject
	{
		protected const string DefinitionsMenu = "HardlightProject/Requirements/";

		public abstract bool RequirementsMet(GameplayLevelDefinition levelDefinition);
        // Game.Runtime 0x06001efa, ARM64 0x52da2c..0x52da34: original protected ScriptableObject constructor forward.
        protected RequirementGameplayLevelBase() { }
	}
}
