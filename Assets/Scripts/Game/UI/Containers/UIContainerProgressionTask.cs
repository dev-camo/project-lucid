using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200090e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIContainerProgressionTask : UIContainer
	{
		public readonly Bindable<Strings> TaskType = new Bindable<Strings>();

		public readonly Bindable<string> TaskName = new Bindable<string>();

		public readonly Bindable<bool> ShowMissionData = new Bindable<bool>();

		public readonly Bindable<Strings> MissionZone = new Bindable<Strings>();

		public readonly Bindable<Strings> MissionAct = new Bindable<Strings>();

		public readonly Bindable<bool> ShowChallengeData = new Bindable<bool>();

		public readonly Bindable<int> ChallengeXP = new Bindable<int>();

		public readonly Bindable<Sprite> TaskImage = new Bindable<Sprite>();

		public readonly Bindable<Texture> UnlockImage = new Bindable<Texture>();

		public readonly Bindable<Sprite> UnlockSprite = new Bindable<Sprite>();

		public readonly Bindable<Sprite> DreamPowerImage = new Bindable<Sprite>();

		public readonly Bindable<bool> ShowNextButton = new Bindable<bool>();

		public readonly Bindable<Color> RewardGlowColour = new Bindable<Color>();

		[SerializeField]
		private Animation m_animationComponent;

		[SerializeField]
		private AnimationClip m_revealAnimation;

		[SerializeField]
		private AnimationClip m_pressedAnimation;

		[SerializeField]
		private Sprite m_achievementSprite;

		[SerializeField]
		private Sprite m_challengeSprite;

		[SerializeField]
		private Sprite m_missionSprite;

		[SerializeField]
		private Sprite m_dreamPowerSprite;

		[SerializeField]
		private TimeCategoryObject m_timeCategoryObject;

		[SerializeField]
		private UIModernEvent m_onRevealComplete;

		[SerializeField]
		private Color m_defaultRewardGlowColour;

		private IMetaGameUnlock m_unlock;

		private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();

		private readonly SystemRef<CinemachineCameraManager> m_cinemachineManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();

		private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();

		// 06003456 (1.10.1 RVA 0x674BD0): with a current unlock the level is unloaded when one is valid and the gameplay camera is switched off.
		private void Awake()
		{
			if (!UIProgressionUtilities.TryGetCurrentUnlock(out m_unlock))
			{
				return;
			}
			ShowNextButton.Value = true;
			RewardGlowColour.Value = m_defaultRewardGlowColour;
			switch (m_unlock)
			{
				case MetaGameUnlockOrnament ornamentUnlock:
					ShowCompletedTaskDetails(ornamentUnlock);
					break;
				case MetaGameUnlockMusicTrack musicUnlock:
					ShowCompletedTaskDetails(musicUnlock);
					break;
				case MetaGameUnlockDreamPowerStoreBand storeBandUnlock:
					ShowCompletedTaskDetails(storeBandUnlock);
					break;
				case MetaGameUnlockBlueCoins coinUnlock:
					ShowCompletedTaskDetails(coinUnlock);
					break;
				case MetaGameUnlockDreamPowerStoreItem dreamPowerUnlock:
					ShowCompletedTaskDetails(dreamPowerUnlock);
					break;
				case MetaGameUnlockRewardTrack rewardTrackUnlock:
					ShowCompletedTaskDetails(rewardTrackUnlock);
					break;
				case MetaGameUnlockCharacterSkin characterSkinUnlock:
					ShowCompletedTaskDetails(characterSkinUnlock);
					break;
			}
			if (m_levelManagerRef.TryGet(out LevelManager levelManager) && levelManager.CurrentLevelValid)
			{
				CoroutineUtils.RunCoroutine(levelManager.UnloadLevel());
			}
			if (m_cinemachineManagerRef.TryGet(out CinemachineCameraManager cinemachineCameraManager))
			{
				cinemachineCameraManager.ToggleGameplayCameraActive(false);
			}
		}

		// 06003457 (1.10.1 RVA 0x675660): no null test on the unlock.
		private void OnDestroy()
		{
			m_unlock.ReleaseAssets();
		}

		// 06003458 (1.10.1 RVA 0x674E40): authentic empty body.
		private void OnRevealAnimationComplete()
		{
		}

		// 06003459 (1.10.1 RVA 0x675710)
		public void OnButtonPressed()
		{
			ShowNextButton.Value = false;
			StartCoroutine(UIProgressionUtilities.WaitForAnimation(m_animationComponent, m_timeCategoryObject, OnShownAnimationComplete, m_pressedAnimation));
		}

		// 0600345a (1.10.1 RVA 0x6757D0)
		private void OnShownAnimationComplete()
		{
			ProcessManager.GetSystem<App>().AddUniqueUIModernEvent(m_onRevealComplete);
		}

		// 0600345b (1.10.1 RVA 0x674E50): a mission ornament names its mission, level and zone, an achievement ornament its
		// achievement, and any other ornament with challenge XP its reward track.
		private void ShowCompletedTaskDetails(MetaGameUnlockOrnament ornamentUnlock)
		{
			DataManager dataManager = m_dataManagerRef.Get();
			MissionDefinition missionDefinition = ornamentUnlock.MissionDefinition;
			ornamentUnlock.GetCustomTexture(texture => UnlockImage.Value = texture);
			if (missionDefinition != null)
			{
				TaskType.Value = Strings.MISSIONS;
				string missionName = StringTable.GetString(missionDefinition.MissionName);
				if (missionDefinition.IsInstancedTracker)
				{
					missionName = string.Format(missionName, "", missionDefinition.TotalObjectiveCount.ToString());
				}
				TaskName.Value = missionName;
				GameplayLevelDefinition levelDefinition = dataManager.MissionDefinitionLevelLookup[missionDefinition];
				MissionAct.Value = levelDefinition.ActName;
				if (m_levelManagerRef.Get().GameLevels.TryGetZoneForLevel(levelDefinition, out GameplayLevelCategory zone))
				{
					MissionZone.Value = zone.Title;
					TaskImage.Value = m_missionSprite;
					ShowMissionData.Value = true;
				}
			}
			else if (ornamentUnlock.AchievementIdentifier != AchievementIdentifier.None)
			{
				TaskType.Value = Strings.SETTINGS_BUTTON_LABEL_ACHIEVEMENTS;
				TaskName.Value = StringTable.GetString(dataManager.AchievementDefinitions[ornamentUnlock.AchievementIdentifier].Title);
				TaskImage.Value = m_achievementSprite;
			}
			else if (ornamentUnlock.ChallengeXP > 0)
			{
				ChallengeXP.Value = ornamentUnlock.ChallengeXP;
				SetChallengeRewardDetails(ornamentUnlock.RewardTrackType);
			}
		}

		// 0600345c (1.10.1 RVA 0x6751B0): the track image falls back to the sprite's texture when the texture asset is not valid.
		private void ShowCompletedTaskDetails(MetaGameUnlockMusicTrack musicUnlock)
		{
			MusicTrackDefinition musicTrackDefinition = musicUnlock.MusicTrackDefinition;
			UnlockImage.Value = musicTrackDefinition.ImageAssetTexture.IsValid()
				? musicTrackDefinition.ImageAssetTexture.Load()
				: musicTrackDefinition.ImageAsset.Load().texture;
			ChallengeXP.Value = musicUnlock.ChallengeXP;
			SetChallengeRewardDetails(musicUnlock.RewardTrackType);
		}

		// 0600345d (1.10.1 RVA 0x675290)
		private void ShowCompletedTaskDetails(MetaGameUnlockDreamPowerStoreBand storeBandUnlock)
		{
			storeBandUnlock.GetCustomSprite(sprite => UnlockSprite.Value = sprite);
			ChallengeXP.Value = storeBandUnlock.ChallengeXP;
			SetChallengeRewardDetails(storeBandUnlock.RewardTrackType);
		}

		// 0600345e (1.10.1 RVA 0x675340)
		private void ShowCompletedTaskDetails(MetaGameUnlockBlueCoins coinUnlock)
		{
			coinUnlock.GetCustomSprite(texture => UnlockSprite.Value = texture);
			ChallengeXP.Value = coinUnlock.ChallengeXP;
			SetChallengeRewardDetails(coinUnlock.RewardTrackType);
		}

		// 0600345f (1.10.1 RVA 0x6753F0)
		private void ShowCompletedTaskDetails(MetaGameUnlockDreamPowerStoreItem dreamPowerUnlock)
		{
			TaskType.Value = Strings.MENU_DREAM_POWERS_STORE_INFO_HEADER;
			TaskName.Value = StringTable.GetString(dreamPowerUnlock.GetTitle());
			dreamPowerUnlock.GetCustomSprite(sprite => DreamPowerImage.Value = sprite);
			TaskImage.Value = m_dreamPowerSprite;
		}

		// 06003460 (1.10.1 RVA 0x675500)
		private void ShowCompletedTaskDetails(MetaGameUnlockRewardTrack rewardTrackUnlock)
		{
			rewardTrackUnlock.GetCustomSprite(sprite => UnlockSprite.Value = sprite);
			ChallengeXP.Value = rewardTrackUnlock.ChallengeXP;
			SetChallengeRewardDetails(rewardTrackUnlock.RewardTrackType);
		}

		// 06003461 (1.10.1 RVA 0x6755B0)
		private void ShowCompletedTaskDetails(MetaGameUnlockCharacterSkin characterSkinUnlock)
		{
			characterSkinUnlock.GetCustomSprite(sprite => UnlockSprite.Value = sprite);
			ChallengeXP.Value = characterSkinUnlock.ChallengeXP;
			SetChallengeRewardDetails(characterSkinUnlock.RewardTrackType);
		}

		// 06003462 (1.10.1 RVA 0x675850)
		private void SetChallengeRewardDetails(RewardTrackType rewardTrackType)
		{
			RewardTrackDefinition rewardTrackDefinition = m_dataManagerRef.Get().RewardTrackDefinitions[rewardTrackType];
			TaskType.Value = rewardTrackDefinition.TrackName;
			TaskName.Value = StringTable.GetString(Strings.CHALLENGES_LABEL_XP);
			TaskImage.Value = rewardTrackDefinition.IconAsset.Load();
			RewardGlowColour.Value = rewardTrackDefinition.RewardGlowColour;
			ShowChallengeData.Value = true;
		}

		// 06003463 (1.10.1 RVA 0x6759A0)
		public UIContainerProgressionTask()
		{
		}
	}
}
