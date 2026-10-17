// Original HLUnityUI.Runtime Hardlight.RaycastMask 020000d9 (MethodDefs 0x060005d2..d4),
// translated from the x86-64 bodies (0x1B6B530..0x1B6BB20) and metadata. An alpha
// hit-test filter: it stays open until Start, samples only Simple (optionally with preserveAspect)
// and Sliced images, and has no fallback for an unreadable texture. Local names are
// inferred.
// Shipped assets use this script: in the 2026-10-05 reference export,
// Assets/Scripts/HLUnityUI.Runtime/Hardlight/RaycastMask.cs.meta has script GUID
// 2987617a66642055dea88c5bf65c46ba, referenced by four components in
// Data/UI/Prefabs/Containers/uicontainer_on_screen_controls.prefab. The script binding
// is still pending, so those assets do not bind to this type and the .meta GUID is
// not the export's.
using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(Mask))]
    public class RaycastMask : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField]
        private Image m_image;
        private bool m_enabled;

        private void Start()
        {
            m_enabled = true;
        }

        // 0x060005d3: maps the local point into sprite texture space and hits only where
        // that texel's alpha is above zero. Simple images truncate toward zero; Sliced
        // images floor. Tiled and Filled images, and any call before Start, always hit.
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!m_enabled) return true;

            Sprite sprite = m_image.sprite;
            RectTransform rectTransform = transform as RectTransform;
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out localPoint);

            // Bottom-left origin. The rect and pivot are each read twice
            // (0x1B6B605..0x1B6B650): x takes the second rect, y the first.
            Rect maskRect = rectTransform.rect;
            Vector2 localPosition = new Vector2(
                localPoint.x + rectTransform.pivot.x * rectTransform.rect.width,
                localPoint.y + rectTransform.pivot.y * maskRect.height);

            Rect spriteRect = sprite.textureRect;
            // Both branches meet on one float texel (0x1B6BA6E), truncated once per axis.
            Vector2 texel;
            switch (m_image.type)
            {
                case Image.Type.Simple:
                {
                    // A preserved aspect draws a square sprite centred on the longer side.
                    Vector2 offset = new Vector2(0f, 0f);
                    Vector2 size = maskRect.size;
                    if (m_image.preserveAspect)
                    {
                        if (maskRect.height > maskRect.width)
                        {
                            offset = new Vector2(0f, (maskRect.height - maskRect.width) * 0.5f);
                            size = new Vector2(maskRect.width, maskRect.width);
                        }
                        else
                        {
                            offset = new Vector2((maskRect.width - maskRect.height) * 0.5f, 0f);
                            size = new Vector2(maskRect.height, maskRect.height);
                        }
                    }
                    texel = new Vector2(
                        spriteRect.x + (localPosition.x - offset.x) / size.x * spriteRect.width,
                        spriteRect.y + (localPosition.y - offset.y) / size.y * spriteRect.height);
                    break;
                }
                case Image.Type.Sliced:
                {
                    Vector4 border = sprite.border;
                    texel = Vector2.zero;
                    if (localPosition.x < border.x)
                    {
                        texel.x = Mathf.Floor(spriteRect.x + localPosition.x);
                    }
                    else if (localPosition.x > maskRect.width - border.z)
                    {
                        texel.x = Mathf.Floor(spriteRect.x + spriteRect.width - (maskRect.width - localPosition.x));
                    }
                    else
                    {
                        texel.x = Mathf.Floor(spriteRect.x + border.x +
                                                 ((localPosition.x - border.x) / (maskRect.width - border.x - border.z)) *
                                                 (spriteRect.width - border.x - border.z));
                    }

                    if (localPosition.y < border.y)
                    {
                        texel.y = Mathf.Floor(spriteRect.y + localPosition.y);
                    }
                    else if (localPosition.y > maskRect.height - border.w)
                    {
                        texel.y = Mathf.Floor(spriteRect.y + spriteRect.height - (maskRect.height - localPosition.y));
                    }
                    else
                    {
                        texel.y = Mathf.Floor(spriteRect.y + border.y +
                                                 ((localPosition.y - border.y) / (maskRect.height - border.y - border.w)) *
                                                 (spriteRect.height - border.y - border.w));
                    }
                    break;
                }
                default:
                    return true;
            }

            int x = (int)texel.x;
            int y = (int)texel.y;

            // A texel outside a packed sprite's own rect belongs to another atlas entry.
            if (sprite.packed && !spriteRect.Contains(new Vector2(x, y))) return false;

            return sprite.texture.GetPixel(x, y).a > 0;
        }
    }
}
