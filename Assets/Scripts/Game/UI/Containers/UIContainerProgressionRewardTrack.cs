using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200090d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class UIContainerProgressionRewardTrack : UIContainer
	{
		public readonly Bindable<Strings> Name = new Bindable<Strings>();

		public readonly Bindable<Strings> Description = new Bindable<Strings>();

		public readonly Bindable<Strings> Overview = new Bindable<Strings>();

		public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();

		public readonly Bindable<Sprite> Thumbnail = new Bindable<Sprite>();

		public readonly Bindable<Color> BackgroundGradientStart = new Bindable<Color>();

		public readonly Bindable<Color> BackgroundGradientEnd = new Bindable<Color>();

		public readonly Bindable<Color> ShineColor = new Bindable<Color>();

		private MetaGameUnlockRewardTrack m_unlock;

		// 06003452 (1.10.1 RVA 0x674780)
		private void Awake()
		{
			if (UIProgressionUtilities.TryGetCurrentUnlock(out MetaGameUnlockRewardTrack unlock))
			{
				m_unlock = unlock;
				Name.Value = m_unlock.RewardTrackDefinition.TrackName;
				Description.Value = m_unlock.RewardTrackDefinition.TrackDescription;
				Overview.Value = m_unlock.RewardTrackDefinition.TrackOverview;
				m_unlock.GetCustomSprite(sprite => Icon.Value = sprite);
				Thumbnail.Value = m_unlock.RewardTrackDefinition.ThumbnailAsset.Load();
				BackgroundGradientStart.Value = m_unlock.RewardTrackDefinition.BackgroundGradientStart;
				BackgroundGradientEnd.Value = m_unlock.RewardTrackDefinition.BackgroundGradientEnd;
				ShineColor.Value = m_unlock.RewardTrackDefinition.ShineColour;
			}
		}

		// 06003453 (1.10.1 RVA 0x674960): the thumbnail is released here, the icon through the unlock.
		private void OnDestroy()
		{
			m_unlock?.ReleaseAssets();
			m_unlock?.RewardTrackDefinition.ThumbnailAsset.Unload();
		}

		// 06003454 (1.10.1 RVA 0x6749D0)
		public UIContainerProgressionRewardTrack()
		{
		}
	}
}
