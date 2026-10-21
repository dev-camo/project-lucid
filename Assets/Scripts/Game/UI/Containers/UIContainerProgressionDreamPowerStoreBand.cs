using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200090a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIContainerProgressionDreamPowerStoreBand : UIContainer
	{
		public readonly Bindable<Strings> Title = new Bindable<Strings>();

		public readonly Bindable<Strings> Body = new Bindable<Strings>();

		public readonly Bindable<Sprite> Image = new Bindable<Sprite>();

		public readonly Bindable<string> TierIndex = new Bindable<string>();

		private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();

		private MetaGameUnlockDreamPowerStoreBand m_unlock;

		// 06003443 (1.10.1 RVA 0x673C40)
		private void Awake()
		{
			if (UIProgressionUtilities.TryGetCurrentUnlock(out MetaGameUnlockDreamPowerStoreBand unlock))
			{
				m_unlock = unlock;
				SetBindableFields();
			}
		}

		// 06003444 (1.10.1 RVA 0x673DF0)
		private void OnDestroy()
		{
			m_unlock?.ReleaseAssets();
		}

		// 06003445 (1.10.1 RVA 0x673CB0): the title is the fixed collections title; the tier is the band's one-based index.
		private void SetBindableFields()
		{
			Title.Value = Strings.MENU_DREAM_POWER_COLLECTIONS_TITLE;
			Body.Value = m_unlock.GetBody();
			int tierIndex = m_dataManagerRef.Get().GetGroup<DreamPowerStoreBandDefinitionGroup>().GetIndex(m_unlock.Definition) + 1;
			TierIndex.Value = tierIndex.ToString();
			m_unlock.GetCustomSprite(sprite => Image.Value = sprite);
		}

		// 06003446 (1.10.1 RVA 0x673E20)
		public UIContainerProgressionDreamPowerStoreBand()
		{
		}
	}
}
