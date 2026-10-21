using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.UI.Binding;

namespace HardlightProject
{
	// Original Game.Runtime 02000907; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIContainerProgressionBlueCoins : UIContainer
	{
		public readonly Bindable<string> BlueCoinsAmount = new Bindable<string>();

		private MetaGameUnlockBlueCoins m_unlock;

		// 06003437 (1.10.1 RVA 0x673590)
		private void Awake()
		{
			if (UIProgressionUtilities.TryGetCurrentUnlock(out MetaGameUnlockBlueCoins unlock))
			{
				m_unlock = unlock;
				SetBindableFields();
			}
		}

		// 06003438 (1.10.1 RVA 0x673700)
		private void OnDestroy()
		{
			m_unlock?.ReleaseAssets();
		}

		// 06003439 (1.10.1 RVA 0x673670)
		private void SetBindableFields()
		{
			BlueCoinsAmount.Value = string.Format("+{0}", m_unlock.Amount);
		}

		// 0600343a (1.10.1 RVA 0x673730)
		public UIContainerProgressionBlueCoins()
		{
		}
	}
}
