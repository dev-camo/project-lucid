using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000860. Preserve all three original fields, including
    // the unused level manager, and the camera reference created before base setup.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class UIContainerCharacterAbility : UIContainer
    {
        private UIContainerCharacterAbilityParameters m_parameters;
        private readonly SystemRef<CinemachineCameraManager> m_cinemachineCameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private LevelManager m_levelManager;

        // 06003087: base setup precedes parameter conversion and callback registration.
        // A conversion or base failure leaves the original partial initialization.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIContainerCharacterAbilityParameters>();
            m_cinemachineCameraManagerRef.InvokeOnValid(OnCinemachineCameraManagerValid);
        }

        // 06003088: use the existing main camera immediately, otherwise append the
        // named callback to the original public delegate. Repeated setup may append it.
        private void OnCinemachineCameraManagerValid(CinemachineCameraManager cinemachineCameraManager)
        {
            if (cinemachineCameraManager.MainCameraIsReady)
                SetCamera(cinemachineCameraManager.MainCamera);
            else
                cinemachineCameraManager.OnMainCameraReady += SetCamera;
        }

        // 06003089: remove one callback occurrence from a currently valid manager
        // before assigning the canvas camera. The camera and canvas have no null gate.
        private void SetCamera(Camera customCamera)
        {
            if (m_cinemachineCameraManagerRef.IsValid())
                m_cinemachineCameraManagerRef.Get().OnMainCameraReady -= SetCamera;
            m_canvas.worldCamera = customCamera;
        }

        // 0600308a: resolve the manager once after the original IsNull test. The
        // forward vector points from the camera to the UI transform; use camera up.
        protected void OrientateToCamera(Transform localTransform)
        {
            if (m_cinemachineCameraManagerRef.IsNull())
                return;
            CinemachineCameraManager manager = m_cinemachineCameraManagerRef.Get();
            if (!manager.MainCameraIsReady)
                return;
            Transform cameraTransform = manager.MainCamera.transform;
            Vector3 forward = (localTransform.position - cameraTransform.position).normalized;
            localTransform.rotation = Quaternion.LookRotation(forward, cameraTransform.up);
        }

        // 0600308b: publish the parameter's character first, then use Unity lifetime
        // inequality. Calling before parameters are assigned retains the original fault.
        protected bool TryGetCharacter(out Character character)
        {
            character = m_parameters.Character;
            return character != null;
        }

        // 0600308c: the native generic body performs a type-test and a plain reference
        // comparison. The object cast deliberately avoids Unity's destroyed-object test.
        protected bool TryGetAbility<TAbility>(out TAbility ability) where TAbility : CharacterAbility
        {
            ability = m_parameters.Ability as TAbility;
            return (object)ability != null;
        }

        // 0600308d: only the readonly SystemRef initializer precedes UIContainer.
        protected UIContainerCharacterAbility() { }
    }
}
