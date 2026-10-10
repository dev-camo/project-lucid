using Hardlight;
using Hardlight.Enums;

namespace HardlightProject
{
	public struct UIContainerPromptChoiceParameters : IUIContainerParameters
	{
		public Strings Text;

		public int ChoiceIndex;

		public bool IsDefaultChoice;

		public GameInput GameInput;

		public GameAction GameAction;

		public GameAction GameActionCombo;

		public float MinimumTime;
	}
}
