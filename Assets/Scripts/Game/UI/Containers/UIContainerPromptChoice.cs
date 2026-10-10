using System;
using Hardlight;
using Hardlight.UI.Binding;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class UIContainerPromptChoice : UIContainer
	{
		public readonly Bindable<string> Text = new Bindable<string>();

		public readonly Bindable<bool> IsDefaultChoice = new Bindable<bool>();

		public readonly Bindable<GameInput> GameInput = new Bindable<GameInput>();

		public readonly Bindable<GameAction> GameAction = new Bindable<GameAction>();

		public readonly Bindable<GameAction> GameActionCombo = new Bindable<GameAction>();

		public readonly Bindable<float> MinimumTime = new Bindable<float>();

		public Action<int> OnSelected;

		private int m_choiceIndex;

		public override void Setup(IUIContainerParameters parameters)
		{
			base.Setup(parameters);
			Setup(parameters.GetAs<UIContainerPromptChoiceParameters>());
		}

		public void Setup(UIContainerPromptChoiceParameters parameters)
		{
			Text.Value = StringTable.GetString(parameters.Text);
			IsDefaultChoice.Value = parameters.IsDefaultChoice;
			GameInput.Value = parameters.GameInput;
			GameAction.Value = parameters.GameAction;
			GameActionCombo.Value = parameters.GameActionCombo;
			MinimumTime.Value = parameters.MinimumTime;
			m_choiceIndex = parameters.ChoiceIndex;
		}

		public void Action_Choose()
		{
			OnSelected?.Invoke(m_choiceIndex);
		}
	}
}
