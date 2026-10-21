using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000904; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public sealed class UIContainerPlayerProgression : UIContainer
	{
		[SerializeField]
		private Transform m_widgetRoot;

		[SerializeField]
		private UIModernEvent m_exitEvent;

		[SerializeField]
		private UIModernEvent m_exitToMenuEvent;

		[SerializeField]
		private UIModernEvent m_alternativeExitToMenuEvent;

		[SerializeField]
		private HLAudioSfxComponent m_audioSfx;

		[SerializeField]
		private GameplayLevelDefinition m_firstLevel;

		[SerializeField]
		private int m_maxOrnamentsToShow;

		[SerializeField]
		private UIWidgetAddressableSprite m_zoneAddressableSprite;

		public readonly Bindable<string> Title = new Bindable<string>();

		public readonly Bindable<string> Body = new Bindable<string>();

		public readonly Bindable<bool> ShowAriemImage = new Bindable<bool>();

		public readonly Bindable<bool> ShowZone = new Bindable<bool>();

		public readonly Bindable<Strings> ZoneTitle = new Bindable<Strings>();

		public readonly Bindable<bool> ShowMultipleContinueOptions = new Bindable<bool>();

		private App m_app;

		private SaveManager m_saveManager;

		private LevelManager m_levelManager;

		private Queue<IMetaGameUnlock> m_progressionsQueue = new Queue<IMetaGameUnlock>();

		private IMetaGameUnlock m_previousProgression;

		private IMetaGameUnlock m_currentProgression;

		private UIWidgetProgression m_currentWidget;

		private readonly HashSet<PlayerProgressionTypes> m_unlockTypes = new HashSet<PlayerProgressionTypes>();

		private int m_ornamentUnlocksShown;

		// 06003428 (1.10.1 RVA 0x672170)
		public override void Setup(IUIContainerParameters parameters)
		{
			base.Setup(parameters);
			UIContainerPlayerProgressionParameters progressionParameters = parameters.GetAs<UIContainerPlayerProgressionParameters>();
			m_unlockTypes.Clear();
			m_app = ProcessManager.GetSystem<App>();
			m_saveManager = ProcessManager.GetSystem<SaveManager>();
			m_levelManager = ProcessManager.GetSystem<LevelManager>();
			m_progressionsQueue = new Queue<IMetaGameUnlock>(progressionParameters.Unlocks);
			ShowAriemImage.Value = m_firstLevel.MissionList.HasAnyCompleteMissions(m_firstLevel);
			m_ornamentUnlocksShown = 0;
			PlayNextProgression();
		}

		// 06003429 (1.10.1 RVA 0x672350): skipped unlocks are saved and released, then the next one is shown.
		private void PlayNextProgression()
		{
			if (m_progressionsQueue.Count <= 0)
			{
				Exit(m_exitEvent);
				return;
			}
			if (m_currentWidget != null)
			{
				Destroy(m_currentWidget.gameObject);
				m_currentWidget = null;
			}
			m_previousProgression = m_currentProgression;
			m_currentProgression = m_progressionsQueue.Dequeue();
			PlayerProgressionTypes progressionType = m_currentProgression.GetProgressionType();
			bool skip = false;
			if (DebugUnlockLevels.AreAllLevelsUnlocked() && progressionType == PlayerProgressionTypes.Level)
			{
				skip = m_unlockTypes.Contains(progressionType);
			}
			else if (progressionType == PlayerProgressionTypes.Ornament)
			{
				m_ornamentUnlocksShown++;
				skip = m_ornamentUnlocksShown > m_maxOrnamentsToShow;
			}
			else if (progressionType == PlayerProgressionTypes.MissionGroup)
			{
				skip = m_unlockTypes.Contains(progressionType);
			}
			if (skip)
			{
				SaveProgression(m_currentProgression);
				PlayNextProgression();
				return;
			}
			UIWidgetProgression widget = m_currentProgression.GetWidget();
			if (widget == null)
			{
				PlayNextProgression();
				return;
			}
			m_unlockTypes.Add(progressionType);
			m_currentWidget = Instantiate(widget, m_widgetRoot);
			m_currentProgression.GetCustomTexture(texture =>
			{
				if (texture != null)
				{
					m_currentWidget.SetMaskedImage(texture);
				}
			});
			if (m_currentProgression is MetaGameUnlockMissionGroup missionGroup)
			{
				ShowZone.Value = true;
				if (m_levelManager.GameLevels.TryGetZoneForLevel(missionGroup.GetLevel(), out GameplayLevelCategory zone))
				{
					ZoneTitle.Value = zone.Title;
					m_zoneAddressableSprite.SetSpriteReference(zone.IconAsset);
				}
			}
			else
			{
				ShowZone.Value = false;
			}
			m_currentWidget.gameObject.SetActive(false);
			m_currentWidget.AnimateIn();
			CoroutineUtils.OnNextFrame(() => m_currentWidget.gameObject.SetActive(true));
			UpdateProgressionStringBindable(m_currentProgression);
			ShowMultipleContinueOptions.Value = m_currentProgression.CanExitToMenu();
			// A still-playing jingle of the same unlock type is kept instead of restarted.
			if (m_previousProgression?.GetProgressionType() == progressionType && m_audioSfx.IsPlaying())
			{
				return;
			}
			m_audioSfx.StopAudioSource();
			m_audioSfx.PlayOneShotAtSource(m_currentProgression.GetAudio());
		}

		// 0600342a (1.10.1 RVA 0x672C70)
		private void UpdateProgressionStringBindable(IMetaGameUnlock progression)
		{
			Title.Value = StringTable.GetString(progression.GetTitle());
			Body.Value = StringTable.GetString(progression.GetBody());
		}

		// 0600342b (1.10.1 RVA 0x672DF0)
		public void Action_AnimateOut()
		{
			if (m_currentWidget == null)
			{
				Exit(m_exitEvent);
				return;
			}
			m_currentWidget.AnimateOutComplete += Continue;
			m_currentWidget.AnimateOut();
		}

		// 0600342c (1.10.1 RVA 0x672F40): the lambda is 06003433 (RVA 0x673420).
		public void Action_AnimateToMenu()
		{
			m_currentWidget.AnimateOutComplete += _ =>
			{
				if (m_currentWidget != null)
				{
					SaveProgression(m_currentProgression);
				}
				m_progressionsQueue.Clear();
				Exit(m_currentProgression.UsesAlternativeExitToMenuEvent() ? m_alternativeExitToMenuEvent : m_exitToMenuEvent);
			};
			m_currentWidget.AnimateOut();
		}

		// 0600342d (1.10.1 RVA 0x673030)
		private void Continue(UIWidgetProgression widget)
		{
			widget.AnimateOutComplete -= Continue;
			SaveProgression(m_currentProgression);
			PlayNextProgression();
		}

		// 0600342e (1.10.1 RVA 0x672B60)
		private void SaveProgression(IMetaGameUnlock progression)
		{
			progression.Save(m_saveManager);
			progression.ReleaseAssets();
		}

		// 0600342f (1.10.1 RVA 0x672B40)
		// The retail binary inlines this helper at every call site.
		private void Exit(UIModernEvent uiModernEvent)
		{
			m_app.AddUniqueUIModernEvent(uiModernEvent);
		}

		// 06003430 (1.10.1 RVA 0x673130)
		public UIContainerPlayerProgression()
		{
		}
	}
}
