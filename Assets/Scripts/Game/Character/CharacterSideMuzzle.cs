using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000346. Flips the muzzle transforms to the left or
    // right pose depending on which side of the character the camera sits.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [DisallowMultipleComponent]
    public class CharacterSideMuzzle : MonoBehaviour
    {
        private const string DefaultHeadName = "Head_M";
        private const string DefaultMouthName = "MouthMover_M";
        private const string DefaultMuzzleName = "MuzzleMid_M";
        [SerializeField]
        private Transform m_characterRoot;
        [SerializeField]
        private Transform[] m_muzzleComponents;
        [SerializeField]
        private float m_switchThreshold = 0.1f;
        private readonly List<CharacterMuzzle> m_muzzleSettings = new List<CharacterMuzzle>();
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private readonly SystemRef<CutsceneManager> m_cutsceneManagerRef =
            ProcessManager.GetSystemRef<CutsceneManager>(null, true);
        private Transform m_mainCamera;
        private bool m_isActive;

        // 0600148d
        private void Awake()
        {
            m_muzzleSettings.Clear();
            foreach (Transform muzzleComponent in m_muzzleComponents)
                m_muzzleSettings.Add(new CharacterMuzzle(muzzleComponent));
        }

        // 0600148e; each manager's startup handler also runs now when it is already up.
        private void OnEnable()
        {
            m_cameraManagerRef.OnSystemStartup += OnCameraManagerStartup;
            if (m_cameraManagerRef.IsValid()) OnCameraManagerStartup(m_cameraManagerRef.Get());
            m_cameraManagerRef.OnSystemShutdown += OnCameraManagerShutdown;
            m_cutsceneManagerRef.OnSystemStartup += OnCutsceneManagerStartup;
            if (m_cutsceneManagerRef.IsValid()) OnCutsceneManagerStartup(m_cutsceneManagerRef.Get());
            m_cutsceneManagerRef.OnSystemShutdown += OnCutsceneManagerShutdown;
        }

        // 0600148f
        private void OnCameraManagerStartup(CinemachineCameraManager manager)
        {
            manager.OnMainCameraReady += AssignCamera;
            if (manager.MainCameraIsReady) AssignCamera(manager.MainCamera);
        }

        // 06001490; an ongoing cutscene keeps the muzzle driven by its camera.
        private void OnCameraManagerShutdown(CinemachineCameraManager _)
        {
            if (m_cutsceneManagerRef.IsValid() && m_cutsceneManagerRef.Get().IsCutscenePlaying)
            {
                OnCutsceneStarted();
                return;
            }
            m_isActive = false;
        }

        // 06001491
        private void OnCutsceneManagerStartup(CutsceneManager manager)
        {
            manager.OnCutsceneStarted += OnCutsceneStarted;
            if (manager.IsCutscenePlaying) OnCutsceneStarted();
            manager.OnCutsceneFinishing += OnCutsceneFinishing;
        }

        // 06001492; comic cutscenes render from their own camera.
        private void OnCutsceneStarted()
        {
            if (m_cutsceneManagerRef.Get().TryGetCurrentComicCutscene(out ComicCutscene comicCutscene))
                AssignCamera(comicCutscene.Camera);
        }

        // 06001493; return to the gameplay camera, or stop when none is ready.
        private void OnCutsceneFinishing()
        {
            if (m_cameraManagerRef.IsValid())
            {
                CinemachineCameraManager cameraManager = m_cameraManagerRef.Get();
                if (cameraManager.MainCameraIsReady)
                {
                    m_mainCamera = cameraManager.MainCamera.transform;
                    return;
                }
            }
            StopAllCoroutines();
            m_isActive = false;
        }

        // 06001494
        private void OnCutsceneManagerShutdown(CutsceneManager _) => OnCutsceneFinishing();

        // 06001495
        private void OnDisable()
        {
            m_isActive = false;
            if (m_cameraManagerRef.IsValid())
                m_cameraManagerRef.Get().OnMainCameraReady -= AssignCamera;
            m_cameraManagerRef.OnSystemStartup -= OnCameraManagerStartup;
            m_cameraManagerRef.OnSystemShutdown -= OnCameraManagerShutdown;
            if (m_cutsceneManagerRef.IsValid())
            {
                CutsceneManager cutsceneManager = m_cutsceneManagerRef.Get();
                cutsceneManager.OnCutsceneStarted -= OnCutsceneStarted;
                cutsceneManager.OnCutsceneFinishing -= OnCutsceneFinishing;
            }
            m_cutsceneManagerRef.OnSystemStartup -= OnCutsceneManagerStartup;
            m_cutsceneManagerRef.OnSystemShutdown -= OnCutsceneManagerShutdown;
        }

        // 06001496; the original guards on this component (not the camera) being alive.
        private void AssignCamera(Camera trackedCamera)
        {
            if (this == null) return;
            m_mainCamera = trackedCamera.transform;
            UpdateMuzzleSide(0f);
            m_isActive = true;
        }

        // 06001497
        private void LateUpdate()
        {
            if (m_isActive) UpdateMuzzleSide(m_switchThreshold);
        }

        // 06001498; the sign of the camera offset against the character's forward picks the side.
        private void UpdateMuzzleSide(float switchThreshold)
        {
            float side = Vector3.Dot(-m_characterRoot.forward, m_mainCamera.position - m_characterRoot.position);
            if (!(Mathf.Abs(side) > switchThreshold)) return;
            bool isRightSide = side > 0f;
            foreach (CharacterMuzzle muzzle in m_muzzleSettings)
                muzzle.SetMuzzleSide(isRightSide);
        }

        // 06001499
        private void Reset()
        {
            m_characterRoot = GetNamedChild(DefaultHeadName);
            m_muzzleComponents = new Transform[2];
            m_muzzleComponents[0] = GetNamedChild(DefaultMouthName);
            m_muzzleComponents[1] = GetNamedChild(DefaultMuzzleName);
        }

        // 0600149a
        private Transform GetNamedChild(string gameObjectName)
        {
            foreach (Transform child in gameObject.GetComponentsInChildren<Transform>())
            {
                if (child.name != gameObjectName) continue;
                return child;
            }
            return null;
        }

        // 0600149b; the switch threshold defaults to 0.1 and both manager references
        // are resolved before MonoBehaviour's constructor.
        public CharacterSideMuzzle() { }
    }
}
