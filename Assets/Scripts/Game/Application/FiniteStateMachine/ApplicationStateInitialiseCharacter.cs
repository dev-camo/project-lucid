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
	// Original Game.Runtime 020000d7; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStateInitialiseCharacter : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 0600067d (1.10.1 RVA 0x6473F0)
			public JSONCtorArgs()
			{
			}
		}

		[CompilerGenerated]
		private sealed class _003C_003Ec__DisplayClass8_0
		{
			public MissionState missionState;

			// 0600067e (1.10.1 RVA 0x647400)
			public _003C_003Ec__DisplayClass8_0()
			{
			}

			// 0600067f (1.10.1 RVA 0x647410)
			internal bool _003CInitialiseMissions_003Eb__0()
			{
				return missionState.SetUpComplete;
			}
		}

		[CompilerGenerated]
		private sealed class _003CInitialiseMissions_003Ed__8 : IEnumerator<object>, IEnumerator, IDisposable
		{
			private int _003C_003E1__state;

			private object _003C_003E2__current;

			public IGraphUser app;

			public LevelManagerLevel loadedLevel;

			public ApplicationStateInitialiseCharacter _003C_003E4__this;

			private MissionManager _003CmissionManager_003E5__2;

			private IMissionContext _003CactiveMissionContext_003E5__3;

			private IEnumerator<KeyValuePair<string, MissionState>> _003C_003E7__wrap3;

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

			// 06000680 (1.10.1 RVA 0x6470E0)
			[DebuggerHidden]
			public _003CInitialiseMissions_003Ed__8(int _003C_003E1__state)
			{
				this._003C_003E1__state = _003C_003E1__state;
			}

			// 06000681 (1.10.1 RVA 0x647430)
			[DebuggerHidden]
			void IDisposable.Dispose()
			{
				if (_003C_003E1__state == 1 || _003C_003E1__state == -3)
				{
					_003C_003Em__Finally1();
				}
			}

			// 06000682 (1.10.1 RVA 0x6474F0)
			private bool MoveNext()
			{
				if (_003C_003E1__state == 0)
				{
					_003C_003E1__state = -1;
					_003CmissionManager_003E5__2 = ProcessManager.GetSystem<MissionManager>();
					app.Storage.GetValue<bool>(AppFSMKeys.IsFastLoading, false, true);
					_003CactiveMissionContext_003E5__3 = _003CmissionManager_003E5__2.ActiveMissionContext;
					if (_003CactiveMissionContext_003E5__3 != null)
					{
						bool isReload = app.Storage.GetValue<bool>(AppFSMKeys.RestartInProgress, false, true);
						_003CmissionManager_003E5__2.PurgeMissionStates(isReload);
						_003CmissionManager_003E5__2.LoadMissionsFromLevel(loadedLevel, isReload);
						_003CmissionManager_003E5__2.LoadMission(_003CactiveMissionContext_003E5__3.MissionDefinition, _003CactiveMissionContext_003E5__3.LevelDefinition);
						_003CmissionManager_003E5__2.CachePreLevelMissionSaveData(_003CactiveMissionContext_003E5__3.LevelDefinition.GetGUID());
					}
					if (ProcessManager.GetSystemRef<GameplayIslandManager>().TryGet(out GameplayIslandManager islandManager))
					{
						islandManager.SanitisePendingGameplayElements();
					}
					_003C_003E7__wrap3 = _003CmissionManager_003E5__2.MissionStates.GetEnumerator();
					_003C_003E1__state = -3;
				}
				if (_003C_003E1__state == 1)
				{
					_003C_003E1__state = -3;
				}
				if (_003C_003E1__state == -3)
				{
					while (_003C_003E7__wrap3.MoveNext())
					{
						_003C_003Ec__DisplayClass8_0 locals = new _003C_003Ec__DisplayClass8_0();
						_003C_003E7__wrap3.Current.Deconstruct(out string _, out locals.missionState);
						MissionDefinition definition = locals.missionState.Definition;
						if (definition.SubLevelDefinition != null)
						{
							continue;
						}
						bool isActiveMission = _003CactiveMissionContext_003E5__3 != null && _003CactiveMissionContext_003E5__3.MissionDefinition == definition;
						if (!isActiveMission && !loadedLevel.LevelDefinition.LoadAllMissions && locals.missionState.SetUpComplete)
						{
							continue;
						}
						_003CmissionManager_003E5__2.SetUpMissionTracker(locals.missionState);
						_003C_003E2__current = new WaitUntil(locals._003CInitialiseMissions_003Eb__0);
						_003C_003E1__state = 1;
						return true;
					}
					_003C_003Em__Finally1();
					_003C_003E7__wrap3 = null;
					_003C_003E2__current = _003C_003E4__this.ReplaceCharacter(app);
					_003C_003E1__state = 2;
					return true;
				}
				if (_003C_003E1__state == 2)
				{
					_003C_003E1__state = -1;
				}
				return false;
			}

			bool IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			// 06000683 (1.10.1 RVA 0x648200)
			private void _003C_003Em__Finally1()
			{
				_003C_003E1__state = -1;
				_003C_003E7__wrap3?.Dispose();
			}

			// 06000685 (1.10.1 RVA 0x6482C0)
			[DebuggerHidden]
			void IEnumerator.Reset()
			{
				throw new NotSupportedException();
			}
		}

		[CompilerGenerated]
		private sealed class _003CReplaceCharacter_003Ed__9 : IEnumerator<object>, IEnumerator, IDisposable
		{
			private int _003C_003E1__state;

			private object _003C_003E2__current;

			public IGraphUser app;

			public ApplicationStateInitialiseCharacter _003C_003E4__this;

			private CharacterId _003CcharacterId_003E5__2;

			private CharacterManager _003CcharacterManager_003E5__3;

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

			// 06000687 (1.10.1 RVA 0x647180)
			[DebuggerHidden]
			public _003CReplaceCharacter_003Ed__9(int _003C_003E1__state)
			{
				this._003C_003E1__state = _003C_003E1__state;
			}

			// 06000688 (1.10.1 RVA 0x648310)
			[DebuggerHidden]
			void IDisposable.Dispose()
			{
			}

			// 06000689 (1.10.1 RVA 0x648320)
			private bool MoveNext()
			{
				if (_003C_003E1__state == 0)
				{
					_003C_003E1__state = -1;
					_003CcharacterId_003E5__2 = app.Storage.GetValue<CharacterId>(AppFSMKeys.CurrentCharacterId, default, true);
					CharacterId missionCharacterId = app.Storage.GetValue<CharacterId>(AppFSMKeys.MissionCharacterId, default, true);
					if (missionCharacterId != CharacterId.None && _003CcharacterId_003E5__2 != missionCharacterId)
					{
						_003CcharacterId_003E5__2 = missionCharacterId;
					}
					_003C_003E2__current = new WaitUntil(_003C_003E4__this._003CReplaceCharacter_003Eb__9_0);
					_003C_003E1__state = 1;
					return true;
				}
				if (_003C_003E1__state == 1)
				{
					_003C_003E1__state = -1;
					_003CcharacterManager_003E5__3 = _003C_003E4__this.m_characterManagerRef.Get();
					_003C_003E2__current = _003CcharacterManager_003E5__3.LoadAllCharacters();
					_003C_003E1__state = 2;
					return true;
				}
				if (_003C_003E1__state == 2)
				{
					_003C_003E1__state = -1;
					_003CcharacterManager_003E5__3.RegisterOnCharacterChange(_003C_003E4__this.InitialiseCharacter, false);
					_003CcharacterManager_003E5__3.SetCharacter(_003CcharacterId_003E5__2, false);
				}
				return false;
			}

			bool IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			// 0600068b (1.10.1 RVA 0x6487D0)
			[DebuggerHidden]
			void IEnumerator.Reset()
			{
				throw new NotSupportedException();
			}
		}

		private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

		private readonly SystemRef<TrackManager> m_trackManagerRef = ProcessManager.GetSystemRef<TrackManager>();

		private readonly ApplicationStateEvent m_stateEvent;

		// 06000674 (1.10.1 RVA 0x646D00)
		private ApplicationStateInitialiseCharacter(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
		}

		// 06000675 (1.10.1 RVA 0x646DF0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateInitialiseCharacter(fsm, stateId, ApplicationStateEvent.FindByName(args.EventId));
		}

		// 06000676 (1.10.1 RVA 0x646E90)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			InitialiseLevel(user);
		}

		// 06000677 (1.10.1 RVA 0x646EC0)
		private void InitialiseLevel(IGraphUser app)
		{
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			levelManager.TryGetCurrentLevel(out LevelManagerLevel loadedLevel);
			CameraYAxisProxyManager cameraProxy = ProcessManager.GetSystemSafe<CameraYAxisProxyManager>();
			if (cameraProxy != null)
			{
				cameraProxy.enabled = true;
			}
			CoroutineUtils.RunCoroutine(InitialiseMissions(app, loadedLevel));
		}

		// 06000678 (1.10.1 RVA 0x647040)
		[IteratorStateMachine(typeof(_003CInitialiseMissions_003Ed__8))]
		private IEnumerator InitialiseMissions(IGraphUser app, LevelManagerLevel loadedLevel)
		{
			return new _003CInitialiseMissions_003Ed__8(0) { _003C_003E4__this = this, app = app, loadedLevel = loadedLevel };
		}

		// 06000679 (1.10.1 RVA 0x647100)
		[IteratorStateMachine(typeof(_003CReplaceCharacter_003Ed__9))]
		private IEnumerator ReplaceCharacter(IGraphUser app)
		{
			return new _003CReplaceCharacter_003Ed__9(0) { _003C_003E4__this = this, app = app };
		}

		// 0600067a (1.10.1 RVA 0x6471A0)
		private void InitialiseCharacter(Character character)
		{
			m_characterManagerRef.Get().ReleaseOnCharacterChange(InitialiseCharacter);
			PublishEvent();
		}

		// 0600067b (1.10.1 RVA 0x647240)
		private void PublishEvent()
		{
			ProcessManager.GetSystem<App>().Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}

		// 0600067c (1.10.1 RVA 0x6473B0)
		[CompilerGenerated]
		private bool _003CReplaceCharacter_003Eb__9_0()
		{
			return m_trackManagerRef.IsValid();
		}
	}
}
