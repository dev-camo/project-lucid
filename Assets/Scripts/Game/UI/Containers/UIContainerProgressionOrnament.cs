using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200090c; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class UIContainerProgressionOrnament : UIContainer
	{
		[SerializeField]
		private OrnamentViewer m_ornamentViewer;

		[SerializeField]
		private Animation m_ornamentAnimation;

		[SerializeField]
		private TimeCategoryObject m_timeCategoryObject;

		[SerializeField]
		private Sprite m_achievementSprite;

		[SerializeField]
		private Sprite m_challengeSprite;

		[SerializeField]
		private Sprite m_missionSprite;

		public readonly Bindable<Strings> Name = new Bindable<Strings>();

		public readonly Bindable<Strings> Description = new Bindable<Strings>();

		public readonly Bindable<Strings> Category = new Bindable<Strings>();

		public readonly Bindable<Sprite> CategoryImage = new Bindable<Sprite>();

		public readonly Bindable<bool> ShowNextButton = new Bindable<bool>();

		private MetaGameUnlockOrnament m_unlock;

		private GameObject m_ornamentViewerInstance;

		// 0600344c (1.10.1 RVA 0x6742D0): without an ornament unlock the next button is shown at once.
		private void Awake()
		{
			if (UIProgressionUtilities.TryGetCurrentUnlock(out MetaGameUnlockOrnament unlock))
			{
				m_unlock = unlock;
				SetBindableFields();
				m_ornamentViewer.ShowOrnament(m_unlock.OrnamentDefinition.Ornament, OnOrnamentLoaded);
			}
			else
			{
				ShowNextButton.Value = true;
			}
		}

		// 0600344d (1.10.1 RVA 0x674510): the viewer instance is destroyed without a null test.
		private void OnDestroy()
		{
			UIProgressionUtilities.RemoveUnlock(m_unlock);
			Destroy(m_ornamentViewerInstance);
		}

		// 0600344e (1.10.1 RVA 0x674580)
		private void OnOrnamentLoaded()
		{
			StartCoroutine(UIProgressionUtilities.WaitForAnimation(m_ornamentAnimation, m_timeCategoryObject, OnAnimationComplete));
		}

		// 0600344f (1.10.1 RVA 0x6743D0)
		private void OnAnimationComplete()
		{
			ShowNextButton.Value = true;
		}

		// 06003450 (1.10.1 RVA 0x674400): an unknown category keeps the current category text and shows no icon.
		private void SetBindableFields()
		{
			Name.Value = m_unlock.OrnamentDefinition.LocalisedName;
			Description.Value = m_unlock.OrnamentDefinition.LocalisedUnlockedLore;
			Category.Value = m_unlock.OrnamentDefinition.OrnamentCategory switch
			{
				OrnamentCategory.Story => Strings.ORNAMENT_CAT_STORY,
				OrnamentCategory.Achievement => Strings.ORNAMENT_CAT_ACHIEVER,
				OrnamentCategory.Challenge => Strings.ORNAMENT_CAT_CHALLENGES,
				_ => Category.Value,
			};
			CategoryImage.Value = m_unlock.OrnamentDefinition.OrnamentCategory switch
			{
				OrnamentCategory.Story => m_missionSprite,
				OrnamentCategory.Challenge => m_challengeSprite,
				OrnamentCategory.Achievement => m_achievementSprite,
				_ => null,
			};
		}

		// 06003451 (1.10.1 RVA 0x674610)
		public UIContainerProgressionOrnament()
		{
		}
	}
}
