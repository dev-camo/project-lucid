using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLModernUI.Runtime 0200001b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[DisallowMultipleComponent]
	public sealed class UIWidgetNavigationHotButton : MonoBehaviour
	{
		[SerializeField]
		private GameInput m_input = GameInput.None;

		[SerializeField]
		private Transform m_rootGlyph;

		[SerializeField]
		private UIWidgetButton m_button;

		[SerializeField]
		[Tooltip("Would you like this hot button to be part of an exclusive list of game inputs?")]
		private bool m_exclusive;

		private readonly SystemRef<UINavigationManager> m_navigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>();

		// 06000072 (1.10.1 RVA 0x1A3D080)
		private void OnEnable()
		{
			ControlMapping.Subscribe(m_input, TriggerButton, InputTrigger.Down);
			if (m_exclusive)
				ControlMapping.AddExclusiveInput(m_input);
			m_navigationManagerRef.InvokeOnValid(OnNavigationManagerValid);
		}

		// 06000073 (1.10.1 RVA 0x1A3D1C0)
		private void OnNavigationManagerValid(UINavigationManager navigationManager)
		{
			navigationManager.RegisterOnActiveChangedEvent(OnNavigationActiveChanged, true);
		}

		// 06000074 (1.10.1 RVA 0x1A3D300)
		private void OnNavigationActiveChanged(bool value)
		{
			if (m_rootGlyph == null)
				return;
			m_rootGlyph.gameObject.SetActive(value);
		}

		// 06000075 (1.10.1 RVA 0x1A3D390): the exclusive input is removed even when m_exclusive
		// is false.
		private void OnDisable()
		{
			ControlMapping.Unsubscribe(m_input, TriggerButton);
			ControlMapping.RemoveExclusiveInput(m_input);
			if (m_navigationManagerRef.IsValid())
				m_navigationManagerRef.Get().UnregisterOnActiveChangedEvent(OnNavigationActiveChanged);
		}

		// 06000076 (1.10.1 RVA 0x1A3D580)
		private void TriggerButton(float value)
		{
			if (m_button.IsInteractable)
				m_button.OnButtonPress(true);
		}

		// 06000077 (1.10.1 RVA 0x1A3D680): the retail body is empty; any editor-only content was
		// stripped from the player build and cannot be recovered.
		private void OnValidate()
		{
		}

		// 06000078 (1.10.1 RVA 0x1A3D690)
		private void Reset()
		{
			m_button = GetComponent<UIWidgetButton>();
		}

		// 06000079 (1.10.1 RVA 0x1A3D6E0)
		public UIWidgetNavigationHotButton()
		{
		}
	}
}
