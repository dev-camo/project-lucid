using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioMixerHandler
    {
        private readonly DataManager m_dataManager;
        public HLAudioMixerHandler(DataManager dataManager) { m_dataManager = dataManager; }
        public HLAudioMixerDefinition Get(HLAudioMixerIdentifier identifier)
        {
            m_dataManager.AudioMixers.TryGetValue(identifier, out HLAudioMixerDefinition definition);
            return definition;
        }
        public IEnumerable<HLAudioMixerDefinition> GetAll() => m_dataManager.AudioMixers.Values;
    }
}
