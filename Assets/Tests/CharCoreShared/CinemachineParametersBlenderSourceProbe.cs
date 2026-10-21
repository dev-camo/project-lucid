using HardlightProject;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only concrete CinemachineParametersBlenderSource (an abstract MonoBehaviour) exposing the
    // protected UpdateBlendValue and the inherited character manager reference.
    public sealed class CinemachineParametersBlenderSourceProbe : CinemachineParametersBlenderSource
    {
        public void RunUpdateBlendValue(float targetValue, float deltaTime) => UpdateBlendValue(targetValue, deltaTime);

        public void RunInternalUpdate(float deltaTime) => InternalUpdate(deltaTime);

        public CharacterManager Manager => m_characterManager;
    }
}
