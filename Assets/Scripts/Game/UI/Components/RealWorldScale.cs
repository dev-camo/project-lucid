using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a1e: sizes a RectTransform to a physical size in
    // inches from the screen's DPI.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RealWorldScale : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("If the object to be scaled is under a CanvasScaler in the hierarchy, provide its transform here to ensure the correct scale factor is applied.")]
        private RectTransform m_canvasScaler;
        [SerializeField]
        [Tooltip("The transform that will have its size changed to match the \"real\" size.")]
        private RectTransform m_scaleTransform;
        [SerializeField] private Vector2 m_dimensionsInInches;
        [SerializeField] private Vector2 m_multiplyOnLowerAspects = Vector2.one;
        [SerializeField] private float m_widescreenAspectRatio = 0.57f;
        [SerializeField] private bool m_disableInGameWindow = true;
        private Vector2 m_totalScreenPixels;
        private Vector2 m_activeProportionOfScreen;
        private float m_screenDPI;

        public Vector2 TotalScreenPixels => m_totalScreenPixels;
        public Vector2 ActiveProportionOfScreen => m_activeProportionOfScreen;

        private void Awake()
        {
            ScaleToRealWorldInches();
        }

        // 060039f4: the multiplier applies unless the height-to-width ratio is below the
        // widescreen ratio, so an unordered ratio also applies it. m_disableInGameWindow
        // is not read.
        public void ScaleToRealWorldInches()
        {
            float scaleFactor = m_canvasScaler != null ? 1f / m_canvasScaler.localScale.x : 1f;
            m_totalScreenPixels.x = Screen.width;
            m_totalScreenPixels.y = Screen.height;
            m_screenDPI = Screen.dpi;
            float aspectRatio = m_totalScreenPixels.y / m_totalScreenPixels.x;
            Vector2 dimensions = aspectRatio < m_widescreenAspectRatio ? m_dimensionsInInches : m_dimensionsInInches * m_multiplyOnLowerAspects;
            m_activeProportionOfScreen = dimensions / (m_totalScreenPixels / m_screenDPI);
            m_scaleTransform.sizeDelta = scaleFactor * m_screenDPI * dimensions;
        }
    }
}
