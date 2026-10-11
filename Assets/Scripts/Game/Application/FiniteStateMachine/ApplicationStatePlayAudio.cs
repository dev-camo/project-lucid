// Preserved Sonic Dream Team 1.10.1, Game.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// HardlightProject.ApplicationStatePlayAudio+JSONCtorArgs 0x02000102
// 0x060006fc .ctor 0x628e04, 0x64f430
// HardlightProject.ApplicationStatePlayAudio 0x02000101
// 0x060006f9 .ctor 0x628840, 0x64ef00
// 0x060006fa ConstructInstance 0x6288b0, 0x64ef60
// 0x060006fb DoOnEnter 0x6289e0, 0x64f060
// Original Game.Runtime whole four-method family including private JSONCtorArgs.
// JSON Replay starts true; clip parsing precedes source parsing and FSM identifier conversion.
// Absent source suppresses only zero clip. Existing source plus zero clip still reaches the original stop/error provider branch.
// Replay false short-circuits TryGet then IsPlayingClip; audio provider receives pan 0/volume 1 or false/null.
// Native source-handler accesses inline the genuine public AudioManager wrappers; no new private API is exposed.
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    public class ApplicationStatePlayAudio : FSMState
    {
        [System.Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class JSONCtorArgs
        {
            [GraphEnumPopup(typeof(HLAudioClipIdentifier))] public string Clip;
            [GraphEnumPopup(typeof(HLAudioSourceIdentifier))] public string Source;
            public bool OneShot;
            public bool Replay = true;
        }
        private readonly HLAudioClipIdentifier m_clip;
        private readonly HLAudioSourceIdentifier m_source;
        private readonly bool m_oneShot;
        private readonly bool m_replay;
        private ApplicationStatePlayAudio(FiniteStateMachine fsm, string stateName, HLAudioClipIdentifier clip,
            HLAudioSourceIdentifier source, bool oneShot, bool replay) : base(fsm, stateName)
        {
            m_clip = clip;
            m_source = source;
            m_oneShot = oneShot;
            m_replay = replay;
        }
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            HLAudioClipIdentifier clip = EnumUtilities.Parse<HLAudioClipIdentifier>(args.Clip);
            HLAudioSourceIdentifier source = EnumUtilities.Parse<HLAudioSourceIdentifier>(args.Source);
            return new ApplicationStatePlayAudio(fsm, stateId, clip, source, args.OneShot, args.Replay);
        }
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            AudioManager audioManager = ProcessManager.GetSystem<AudioManager>();
            if (!audioManager.HasSource(m_source) && m_clip == 0) return;
            if (!m_replay && audioManager.TryGetAudioSourceData(m_source, out HLAudioSourceData source)
                && source.IsPlayingClip(m_clip)) return;
            if (m_oneShot) audioManager.PlayOneShotAtSource(m_clip, m_source, 0f, 1f);
            else audioManager.PlayClipAtSource(m_clip, m_source, false, null);
        }
    }
}
