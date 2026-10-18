using System;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLModernUI.Runtime 0200004a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[RequireComponent(typeof(UIToggle))]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIWidgetToggle : UIWidget
	{
		[SerializeField]
		[InspectorReadOnly]
		protected UIToggle m_toggle;

		[SerializeField]
		[Tooltip("Toggle is blocked from being set to 'on' while it is not interactable.")]
		private bool m_mustBeInteractableToSelect;

		[SerializeField]
		private UIWidgetNavigation m_uiWidgetNavigation;

		public readonly Bindable<string> Label = new Bindable<string>();

		private Action<bool> m_callback;

		// 060001d1 (1.10.1 RVA 0x1A49CB0)
		public UIWidgetNavigation WidgetNavigation => m_uiWidgetNavigation;

		// 060001d2 (1.10.1 RVA 0x1A49CC0)
		public override void Setup(IUIWidgetParameters parameters)
		{
			UIWidgetToggleParameters data = parameters.GetAs<UIWidgetToggleParameters>();
			Label.Value = data.Label;
			m_callback = data.Callback;
		}

		// 060001d3 (1.10.1 RVA 0x1A49D30)
		public virtual void Action_OnValueChanged(bool value)
		{
			m_callback?.Invoke(value);
		}

		// 060001d4 (1.10.1 RVA 0x1A49D60)
		public void Action_PublishUIMessage(UIModernEvent eventId)
		{
			var exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
			var message = new UIModernMessage(UIModernEventType.OnClick);
			exchange.PublishMessage(in message, in eventId, MessageBroadcastType.Both);
		}

		// 060001d5 (1.10.1 RVA 0x1A49E00): only turning the toggle on is blocked; the
		// nonvirtual canvas-group-aware IsInteractable is read, not the virtual property.
		public void SetSelected(bool selected)
		{
			if (m_mustBeInteractableToSelect && !m_toggle.IsInteractable() && selected)
				return;
			m_toggle.IsOn = selected;
		}

		// 060001d6 (1.10.1 RVA 0x1A49E40)
		public bool IsSelected()
		{
			return m_toggle.IsOn;
		}

		// 060001d7 (1.10.1 RVA 0x1A49E50): bypasses the interactable guard of SetSelected.
		public void SetSelectedWithoutNotify(bool selected)
		{
			m_toggle.SetIsOnWithoutNotify(selected);
		}

		// 060001d8 (1.10.1 RVA 0x1A49E60)
		public void SetToggleGroup(UIToggleGroup toggleGroup)
		{
			m_toggle.GroupElement.Group = toggleGroup;
		}

		// 060001d9 (1.10.1 RVA 0x1A49E80)
		public void SetInteractable(bool interactable)
		{
			m_toggle.Interactable = interactable;
		}

		// 060001da (1.10.1 RVA 0x1A49EA0)
		public void ToggleIsOn()
		{
			m_toggle.IsOn = !m_toggle.IsOn;
		}

		// 060001db (1.10.1 RVA 0x1A49EC0)
		private void Reset()
		{
			m_toggle = GetComponent<UIToggle>();
		}

		// 060001dc (1.10.1 RVA 0x1A49F10)
		public UIWidgetToggle()
		{
		}
	}
}
