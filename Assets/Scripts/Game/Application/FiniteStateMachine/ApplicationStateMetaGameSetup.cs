using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200014a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/MetaGame/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStateMetaGameSetup : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		private class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string Event;

			// 060007de (1.10.1 RVA 0x65E510)
			public JSONCtorArgs()
			{
			}
		}

		[CompilerGenerated]
		private sealed class _003CDestroyCharacter_003Ed__10 : IEnumerator<object>, IEnumerator, IDisposable
		{
			private int _003C_003E1__state;

			private object _003C_003E2__current;

			public ApplicationStateMetaGameSetup _003C_003E4__this;

			public IGraphUser user;

			public MissionDefinition missionDefinition;

			private CharacterManager _003CcharacterManager_003E5__2;

			object IEnumerator<object>.Current
			{
				[DebuggerHidden]
				get
				{return _003C_003E2__current;}
			}

			object IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{return _003C_003E2__current;}
			}

			// 060007df (1.10.1 RVA 0x65E270)
			[DebuggerHidden]
			public _003CDestroyCharacter_003Ed__10(int _003C_003E1__state)
			{
				this._003C_003E1__state = _003C_003E1__state;
			}

			// 060007e0 (1.10.1 RVA 0x65E520)
			[DebuggerHidden]
			void IDisposable.Dispose()
			{
			}

			// 060007e1 (1.10.1 RVA 0x65E530)
			private bool MoveNext()
			{
				if (_003C_003E1__state == 0)
				{
					_003C_003E1__state = -1;
					_003CcharacterManager_003E5__2 = _003C_003E4__this.m_characterManagerRef.Get();
					if (!_003CcharacterManager_003E5__2.TryGetCurrentCharacter(out Character character))
					{
						_003C_003E4__this.UpdateUnlocks(user, missionDefinition);
						return false;
					}
					user.Storage.SetValue(AppFSMKeys.CharacterLastUsed, character.Definition.Id);
					_003C_003E2__current = character.ExitTriggerVolumes();
					_003C_003E1__state = 1;
					return true;
				}
				if (_003C_003E1__state == 1)
				{
					_003C_003E1__state = -1;
					_003CcharacterManager_003E5__2.DestroyCharacters();
					ParticleEffectManager particleManager = ProcessManager.GetSystemSafe<ParticleEffectManager>();
					if (particleManager != null) particleManager.CleanUpAfterLevel();
					_003C_003E4__this.UpdateUnlocks(user, missionDefinition);
				}
				return false;
			}

			bool IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			// 060007e3 (1.10.1 RVA 0x65E890)
			[DebuggerHidden]
			void IEnumerator.Reset()
			{
				throw new NotSupportedException();
			}
		}

		private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

		private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();

		private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();

		private readonly ApplicationStateEvent m_stateEvent;

		// 060007d6 (1.10.1 RVA 0x65C9B0)
		private ApplicationStateMetaGameSetup(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
		}

		// 060007d7 (1.10.1 RVA 0x65CAD0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateMetaGameSetup(fsm, stateId, ApplicationStateEvent.FindByName(args.Event));
		}

		// 060007d8 (1.10.1 RVA 0x65CB70)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			App app = user.GetAs<App>();
			app.SetHasGameplayControl(false, false);
			user.Storage.SetValue(AppFSMKeys.MetaGameActive, true);
			user.Storage.SetValue(AppFSMKeys.GameplayActive, false);
			user.Storage.RemoveValue<CharacterId>(AppFSMKeys.MissionCharacterId);
			user.Storage.RemoveValue<bool>(AppFSMKeys.IsFastLoading);
			LevelManager levelManager = m_levelManagerRef.Get();
			GameplayLevelDefinition levelDefinition = null;
			if (levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
			{
				levelDefinition = level.LevelDefinition;
				level.Data.UnregisterOutroSequence(user.Storage);
			}
			MissionDefinition missionDefinition = null;
			if (m_missionManagerRef.TryGet(out MissionManager missionManager))
			{
				if (missionManager.TryGetActiveMissionState(out MissionState missionState, false))
				{
					missionDefinition = missionState.Definition;
					missionState.Tracker.ResetMission();
				}
				else if (user.Storage.TryGetValue(AppFSMKeys.MetaGameState, out MetaGameState metaGameState))
				{
					levelDefinition = metaGameState.LevelDefinition;
					missionDefinition = metaGameState.MissionDefinition;
					user.Storage.RemoveValue<MetaGameState>(AppFSMKeys.MetaGameState);
				}
				missionManager.SetActiveMissionContext(null);
				missionManager.ClearPendingRewards();
			}
			UpdateMetaGameState(user, app.DataManager, levelDefinition, missionDefinition);
			ProcessManager.GetSystem<OrnamentManager>().CheckAllAchievementUnlocks();
			SaveManager saveManager = ProcessManager.GetSystem<SaveManager>();
			foreach (GameplayLevelDefinition unlockedLevel in levelManager.GetUnlockedLevels())
			{
				if (!(unlockedLevel == null))
				{
					UpdateNewFlagsForLevel(unlockedLevel, saveManager);
				}
			}
			CoroutineUtils.RunCoroutine(DestroyCharacter(user, missionDefinition));
		}

		// 060007d9 (1.10.1 RVA 0x65D6A0)
		private static void UpdateMetaGameState(IGraphUser user, DataManager dataManager, GameplayLevelDefinition levelDefinition, MissionDefinition missionDefinition)
		{
			if (levelDefinition == null)
			{
				return;
			}
			if (missionDefinition == null)
			{
				user.Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(levelDefinition, null));
				return;
			}
			if (missionDefinition.SceneOverride == levelDefinition)
			{
				GameplayLevelDefinition missionLevel = dataManager.MissionDefinitionLevelLookup[missionDefinition];
				user.Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(missionLevel, missionDefinition));
			}
			else
			{
				user.Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(levelDefinition, missionDefinition));
			}
		}

		// 060007da (1.10.1 RVA 0x65DB70)
		private void UpdateNewFlagsForLevel(GameplayLevelDefinition levelDefinition, SaveManager saveManager)
		{
			SaveDataLevel levelData = saveManager.CurrentSave.GetOrCreateLevelData(levelDefinition.GetGUID());
			MissionManager missionManager = m_missionManagerRef.Get();
			bool changed = false;
			foreach (MissionGroup group in levelDefinition.MissionList.Groups)
			{
				if (!group.MeetsAllRequirements(levelDefinition))
				{
					continue;
				}
				foreach (MissionDefinition mission in group.Definitions)
				{
					SaveDataLevelMission missionData = missionManager.GetSaveDataForMission(mission.GetGUID(), levelDefinition.GetGUID(), levelData);
					if (!mission.ShowInMissionList || mission.IsPersistentTracker)
					{
						continue;
					}
					if (missionData.IsNewState == MissionIsNewState.IsNew && missionData.Complete)
					{
						changed = true;
						missionData.IsNewState = MissionIsNewState.Seen;
					}
					else if (!missionData.TimeTrialUnlockSeen && !missionData.Complete && missionData.IsNewState == MissionIsNewState.None
						&& mission.ArchetypeRequirementMet(saveManager.CurrentSave))
					{
						changed = true;
						missionData.IsNewState = MissionIsNewState.IsNew;
					}
				}
			}
			if (changed)
			{
				saveManager.RequestSave();
			}
		}

		// 060007db (1.10.1 RVA 0x65E1D0)
		[IteratorStateMachine(typeof(_003CDestroyCharacter_003Ed__10))]
		private IEnumerator DestroyCharacter(IGraphUser user, MissionDefinition missionDefinition)
		{
			return new _003CDestroyCharacter_003Ed__10(0) { _003C_003E4__this = this, user = user, missionDefinition = missionDefinition };
		}

		// 060007dc (1.10.1 RVA 0x65E290)
		private void UpdateUnlocks(IGraphUser user, MissionDefinition missionDefinition)
		{
			if (!(missionDefinition == null))
			{
				CharacterManager characterManager = m_characterManagerRef.Get();
				characterManager.UnlockCharacterArchetype(missionDefinition);
				characterManager.UpdateUnlocks();
				m_levelManagerRef.Get().UpdateUnlocks();
				m_missionManagerRef.Get().UpdateUnlocks();
			}
			PublishMessage(user);
		}

		// 060007dd (1.10.1 RVA 0x65E370)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
