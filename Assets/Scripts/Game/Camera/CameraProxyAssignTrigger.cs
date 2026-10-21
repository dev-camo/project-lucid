using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200025c. Hands the camera manager's proxy target
    // transform (rather than the colliding one) to the assign events, and always clears it on exit.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CameraProxyAssignTrigger : TransformAssignEventTrigger
    {
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);

        // 0x06000ce5
        protected override bool SetNullOnExit => true;

        // 0x06000ce6; x86_64 0x713460. The manager reference is read unguarded.
        protected override Transform GetTargetTransform(Collider other)
        {
            return m_cameraManagerRef.Get().ProxyTarget.transform;
        }

        // 0x06000ce7; the manager reference is resolved before the base constructor.
        public CameraProxyAssignTrigger() { }
    }
}
