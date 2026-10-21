using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200090b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class UIContainerProgressionMusicTrack : UIContainer
	{
		public readonly Bindable<Texture> TrackImage = new Bindable<Texture>();

		public readonly Bindable<int> TrackNumber = new Bindable<int>();

		public readonly Bindable<Strings> TrackName = new Bindable<Strings>();

		public readonly Bindable<Strings> TrackComposer = new Bindable<Strings>();

		private MetaGameUnlockMusicTrack m_unlock;

		// 06003448 (1.10.1 RVA 0x673FE0)
		private void Awake()
		{
			if (UIProgressionUtilities.TryGetCurrentUnlock(out MetaGameUnlockMusicTrack unlock))
			{
				m_unlock = unlock;
				SetBindableFields();
			}
		}

		// 06003449 (1.10.1 RVA 0x674160)
		private void OnDestroy()
		{
			m_unlock?.ReleaseAssets();
		}

		// 0600344a (1.10.1 RVA 0x674050): the image is loaded synchronously here; the unlock's own asynchronous loader is not used.
		private void SetBindableFields()
		{
			MusicTrackDefinition musicTrackDefinition = m_unlock.MusicTrackDefinition;
			TrackImage.Value = musicTrackDefinition.ImageAssetTexture.Load();
			TrackName.Value = musicTrackDefinition.Name;
			TrackComposer.Value = musicTrackDefinition.Composer;
			TrackNumber.Value = ProcessManager.GetSystem<MusicTrackManager>().GetTrackIndex(musicTrackDefinition);
		}

		// 0600344b (1.10.1 RVA 0x674190)
		public UIContainerProgressionMusicTrack()
		{
		}
	}
}
