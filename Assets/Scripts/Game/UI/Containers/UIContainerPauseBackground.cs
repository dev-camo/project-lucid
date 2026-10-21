using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000902; bodies translated from the 1.10.1 x86-64 binary.
    // The blurred-looking backdrop of the pause menu: a one-off render of the gameplay camera into a texture.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerPauseBackground : UIContainer
    {
        [SerializeField] private PerformanceFeature<QualityBackground> m_qualitySettings;
        [SerializeField] private RawImage m_backgroundImage;
        [SerializeField] private bool m_useRenderTexture;
        private readonly SystemRef<CinemachineCameraManager> m_cinemachineCameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private CinemachineCameraManager m_cinemachineCameraManager;
        private int m_cachedCullingMask;
        private RenderTexture m_renderTexture;
        private bool m_updateBackground;
        private Vector2Int m_screenSize;

        // 0x06003420 (0x671B80): without a render texture the component switches itself off.
        private void OnEnable()
        {
            if (!m_useRenderTexture)
            {
                enabled = false;
                return;
            }

            if (m_cinemachineCameraManagerRef.TryGet(out CinemachineCameraManager cameraManager))
            {
                m_cinemachineCameraManager = cameraManager;
                Initialise(cameraManager);
            }
            else
            {
                m_cinemachineCameraManagerRef.InvokeOnValid(Initialise);
            }
        }

        // 0x06003421 (0x671D90)
        private void Update()
        {
            if (m_useRenderTexture && m_updateBackground)
            {
                RenderBackgroundImage();
                m_updateBackground = false;
            }
        }

        // 0x06003422 (0x671C80): initialises at once when the main camera is ready, and again for every later one.
        private void Initialise(CinemachineCameraManager cinemachineCameraManager)
        {
            m_cinemachineCameraManager = cinemachineCameraManager;
            if (cinemachineCameraManager.MainCameraIsReady)
                InitialiseFromCamera(cinemachineCameraManager.MainCamera);

            cinemachineCameraManager.OnMainCameraReady += InitialiseFromCamera;
        }

        // 0x06003423 (0x671EC0): takes the camera's size, halved until it fits the quality limit, and renders once.
        private void InitialiseFromCamera(Camera mainCamera)
        {
            m_cinemachineCameraManagerRef.Get().OnMainCameraReady -= InitialiseFromCamera;
            m_screenSize = new Vector2Int(mainCamera.pixelWidth, mainCamera.pixelHeight);
            if (m_qualitySettings.TryGet(out QualityBackground quality))
            {
                while ((float)m_screenSize.x > quality.ResolutionMax.x || (float)m_screenSize.y > quality.ResolutionMax.y)
                    m_screenSize = new Vector2Int(m_screenSize.x / 2, m_screenSize.y / 2);
            }

            m_cachedCullingMask = mainCamera.cullingMask;
            RenderBackgroundImage();
        }

        // 0x06003424 (0x671DC0): the camera renders everything it culls normally into the texture, then culls nothing
        // until OnDisable gives the mask back.
        private void RenderBackgroundImage()
        {
            if (!m_cinemachineCameraManager.MainCameraIsReady)
                return;

            Camera camera = m_cinemachineCameraManager.MainCamera;
            camera.cullingMask = m_cachedCullingMask;
            m_renderTexture = new RenderTexture(m_screenSize.x, m_screenSize.y, 0);
            m_renderTexture.Create();
            camera.targetTexture = m_renderTexture;
            camera.Render();
            camera.targetTexture = null;
            camera.cullingMask = 0;
            m_backgroundImage.texture = m_renderTexture;
        }

        // 0x06003425 (0x672090)
        private void OnDisable()
        {
            if (m_useRenderTexture && m_cinemachineCameraManager.MainCameraIsReady)
            {
                m_cinemachineCameraManager.MainCamera.cullingMask = m_cachedCullingMask;
                m_renderTexture.Release();
            }
        }

        // 0x06003426 (0x6720E0)
        public UIContainerPauseBackground()
        {
        }

        [Serializable]
        public class QualityBackground
        {
            [Tooltip("If current resolution exceeds specified then it will be halved until max is met.")]
            public Vector2 ResolutionMax;

            // 0x06003427 (0x672160)
            public QualityBackground()
            {
            }
        }
    }
}
