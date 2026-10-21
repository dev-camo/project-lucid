using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000909; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIContainerProgressionDreamPower : UIContainer
	{
		public readonly Bindable<Strings> Name = new Bindable<Strings>();

		public readonly Bindable<Strings> Description = new Bindable<Strings>();

		public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();

		private MetaGameUnlockDreamPowerStoreItem m_unlock;

		// 0600343f (1.10.1 RVA 0x673A00)
		private void Awake()
		{
			if (UIProgressionUtilities.TryGetCurrentUnlock(out MetaGameUnlockDreamPowerStoreItem unlock))
			{
				m_unlock = unlock;
				Name.Value = m_unlock.GetTitle();
				Description.Value = m_unlock.GetBody();
				m_unlock.GetCustomSprite(sprite => Icon.Value = sprite);
			}
		}

		// 06003440 (1.10.1 RVA 0x673B10)
		private void OnDestroy()
		{
			m_unlock?.ReleaseAssets();
		}

		// 06003441 (1.10.1 RVA 0x673B30)
		public UIContainerProgressionDreamPower()
		{
		}
	}
}
