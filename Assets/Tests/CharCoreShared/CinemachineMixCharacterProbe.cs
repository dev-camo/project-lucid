using HardlightProject;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only concrete CinemachineMix_CharacterBase (an abstract MonoBehaviour) exposing the protected
    // blend update, the liveness test and the inherited character manager reference. It lives in this runtime
    // test assembly because Editor-only assemblies cannot supply attachable components.
    public sealed class CinemachineMixCharacterProbe : CinemachineMix_CharacterBase
    {
        public void RunInternalUpdate(float deltaTime) => InternalUpdate(deltaTime);

        public void RunUpdateBlendWeightings(float targetValue, float deltaTime) => UpdateBlendWeightings(targetValue, deltaTime);

        public bool RunIsLive() => IsLive();

        public CharacterManager Manager => m_characterManager;
    }
}
