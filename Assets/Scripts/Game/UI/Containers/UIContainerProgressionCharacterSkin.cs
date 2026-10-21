using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000908; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIContainerProgressionCharacterSkin : UIContainer
	{
		public readonly Bindable<Strings> Name = new Bindable<Strings>();

		public readonly Bindable<Strings> Description = new Bindable<Strings>();

		public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();

		private MetaGameUnlockCharacterSkin m_unlock;

		// 0600343b (1.10.1 RVA 0x6737B0): the name is the skin's own; the description is the unlock's body text.
		private void Awake()
		{
			if (UIProgressionUtilities.TryGetCurrentUnlock(out MetaGameUnlockCharacterSkin unlock))
			{
				m_unlock = unlock;
				Name.Value = m_unlock.CharacterSkinDefinition.Name;
				Description.Value = m_unlock.GetBody();
				m_unlock.GetCustomSprite(sprite => Icon.Value = sprite);
			}
		}

		// 0600343c (1.10.1 RVA 0x6738C0)
		private void OnDestroy()
		{
			m_unlock?.ReleaseAssets();
		}

		// 0600343d (1.10.1 RVA 0x6738F0)
		public UIContainerProgressionCharacterSkin()
		{
		}
	}
}
