using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000bd; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateAdjustAudioSource : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		private class JSONCtorArgs
		{
			[GraphEnumPopup(typeof(HLAudioSourceIdentifier))]
			public string AudioSource;

			public float Volume;

			// 06000636 (1.10.1 RVA 0x53BE80)
			public JSONCtorArgs()
			{;}
		}

		private readonly HLAudioSourceIdentifier m_audioSource;

		private readonly float m_volume;

		// 06000633 (1.10.1 RVA 0x53BC50)
		private ApplicationStateAdjustAudioSource(FiniteStateMachine fsm, string stateName, HLAudioSourceIdentifier audioMixer, float volume) : base(fsm, stateName)
		{
			m_audioSource = audioMixer;
			m_volume = volume;
		}

		// 06000634 (1.10.1 RVA 0x53BCA0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			Enum.TryParse(args.AudioSource, out HLAudioSourceIdentifier audioSource);
			return new ApplicationStateAdjustAudioSource(fsm, stateId, audioSource, args.Volume);
		}

		// 06000635 (1.10.1 RVA 0x53BDC0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			HLAudioMixerDefinition mixer = ProcessManager.GetSystem<AudioManager>().GetMixer((HLAudioMixerIdentifier)0x7642df2f);
			if (m_audioSource == HLAudioSourceIdentifier.Sfx)
			{
				mixer.SetSfxVolume(m_volume);
			}
			else if (m_audioSource == HLAudioSourceIdentifier.Ambient)
			{
				mixer.SetAmbientVolume(m_volume);
			}
			else
			{
				mixer.SetMusicVolume(m_volume);
			}
		}
	}
}
