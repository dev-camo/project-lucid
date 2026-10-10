using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a8c, complete nine-method declaration.
    // Its real effects, camera, island, and level providers remain required.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class OutroSequence : MonoBehaviour
    {
        [SerializeField] private EffectSequence m_effectSequence;
        [SerializeField] private MaterialManipulator m_manipulator;
        [SerializeField] private EffectSequence m_outroAnimationSequence;
        [SerializeField] private Vector3 m_offset;
        private LevelManager m_levelManager;
        private readonly SystemRef<CinemachineCameraManager> m_cinemachineCameraManager
            = ProcessManager.GetSystemRef<CinemachineCameraManager>();

        // 0x06003cb2; ARM 0x6a9274. The LevelManager reference is a local;
        // only the valid callback publishes the manager field.
        private void Start() => ProcessManager.GetSystemRef<LevelManager>().InvokeOnValid(OnLevelManagerValid);

        // 0x06003cb3; ARM 0x6a9364. Restore materials before removing the
        // loaded callback; neither provider receives an additional null guard.
        private void OnDestroy()
        {
            m_manipulator.Action_Restore();
            m_levelManager.RemoveLevelLoadedAction(OnLevelLoaded);
        }

        // 0x06003cb4; ARM 0x6a93fc. Fetch and transform the camera point
        // before reading this transform, then read camera rotation and assign both.
        private void Update()
        {
            if (m_cinemachineCameraManager.IsNull()) return;
            Transform cameraTransform = m_cinemachineCameraManager.Get().MainCamera.transform;
            Vector3 position = cameraTransform.TransformPoint(m_offset);
            Transform ownTransform = transform;
            Quaternion rotation = cameraTransform.rotation;
            ownTransform.SetPositionAndRotation(position, rotation);
        }

        // 0x06003cb5; ARM 0x6a9520. Store the manager before querying its
        // current level. A current level registers directly without a scene test.
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            m_levelManager = levelManager;
            if (m_levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                level.Data.RegisterOutroSequence(this);
            else m_levelManager.InvokeOnLevelLoaded(OnLevelLoaded);
        }

        // 0x06003cb6; ARM 0x6a9610. Distinct ordinary scene-name strings leave
        // the callback pending. Registration precedes callback removal on a match.
        private void OnLevelLoaded(LevelManagerLevel level)
        {
            if (level.Data.gameObject.scene.name != gameObject.scene.name) return;
            level.Data.RegisterOutroSequence(this);
            m_levelManager.RemoveLevelLoadedAction(OnLevelLoaded);
        }

        // 0x06003cb7; ARM 0x6a971c. Both effects receive the same readonly
        // interface local, with the animation sequence called before the main one.
        public void Play(TargetedEffect targetedEffect)
        {
            IEffectData data = targetedEffect;
            m_outroAnimationSequence.BeginSequence(in data, null);
            m_effectSequence.BeginSequence(in data, null);
        }

        // 0x06003cb8; ARM 0x6a976c. The supplied instance is used directly;
        // no ownership transfer, guard, or completion callback is added.
        public void BeginSequence(IEffectData targetedEffect, EffectSequence dreamOrbInstance)
        {
            dreamOrbInstance.BeginSequence(in targetedEffect, null);
            m_effectSequence.BeginSequence(in targetedEffect, null);
        }

        // 0x06003cb9; ARM 0x6a97bc.
        public void DeactivateIslands() => ProcessManager.GetSystem<GameplayIslandManager>().DeactivateAllIslands();

        // 0x06003cba; ARM 0x6a9834. The genuine SystemRef initializer runs
        // before the MonoBehaviour base constructor; other fields remain default.
        public OutroSequence() { }
    }
}
