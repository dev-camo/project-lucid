// Original HLUnityUI.Runtime Hardlight.Skew 02000064 and its iterator <LerpToSkew>d__7
// 02000065, reconstructed from the x86-64 bodies (0x1B4F510..0x1B4FBC0) and metadata.
// Member order fixes the generated iterator name: LerpToSkew is member ordinal 7 (fields
// m_skewY..m_lerpTime are 0..3, then ModifyMesh 4, ModifyVertices 5, OnEnable 6), which
// gives <LerpToSkew>d__7. Original local names are inferred; the hoisted iterator names
// are original. No shipped .prefab/.unity/.asset in the 2026-10-05 reference export
// references the exported script GUID 46de07523a795b4c6dfa093b41fdc62e, so the script
// stays unbound with a fresh GUID.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [AddComponentMenu("Hardlight UI/Effects/Skew Sprite", 4)]
    public class Skew : BaseMeshEffect
    {
        [SerializeField]
        private float m_skewY;
        [SerializeField]
        private float m_skewX;
        [SerializeField]
        private bool m_lerpFromZeroOnEnable;
        [SerializeField]
        private float m_lerpTime = 0.2f;

        // 0x060002ec: rebuild the mesh as a flat triangle stream with the sheared vertices.
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            List<UIVertex> verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            ModifyVertices(verts);
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }

        // 0x060002ed: shear about the stream-weighted centre (duplicate stream entries count
        // again); both axes read the pre-shear position.
        public void ModifyVertices(List<UIVertex> verts)
        {
            Vector2 center = Vector2.zero;
            for (int i = 0; i < verts.Count; i++)
                center += (Vector2)verts[i].position / verts.Count;

            for (int i = 0; i < verts.Count; i++)
            {
                UIVertex vert = verts[i];
                Vector3 position = vert.position;
                Vector2 offset = (Vector2)position - center;
                vert.position = new Vector3(position.x + offset.y * m_skewX, position.y + offset.x * m_skewY, position.z);
                verts[i] = vert;
            }
        }

        // 0x060002ee: the reset lerp caches whatever skew is live at enable time.
        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_lerpFromZeroOnEnable)
                StartCoroutine(LerpToSkew());
        }

        // 0x060002ef @0x1B4F8E0 only builds d__7; the body is d__7.MoveNext 0x060002f4
        // @0x1B4FA40, whose ucomiss/jbe guards at 0x1B4FAA7 and 0x1B4FB38 are on X alone,
        // so a non-positive X (or no reset) restores the cached pair on the first step.
        private IEnumerator LerpToSkew(bool resetToZeroFist = true)
        {
            float cachedSkewX = m_skewX;
            float cachedSkewY = m_skewY;
            if (resetToZeroFist)
            {
                m_skewX = 0f;
                m_skewY = 0f;
            }
            float timer = 0f;
            while (cachedSkewX > m_skewX)
            {
                timer += Time.deltaTime / m_lerpTime;
                m_skewX = Mathf.Lerp(0f, cachedSkewX, timer);
                m_skewY = Mathf.Lerp(0f, cachedSkewY, timer);
                yield return null;
            }
            m_skewX = cachedSkewX;
            m_skewY = cachedSkewY;
        }

        // 0x060002f0: the non-instant path starts a lerp without the reset. On an active,
        // enabled owner StartCoroutine runs the first MoveNext synchronously, which restores
        // the just-stored pair and returns false; on an inactive owner StartCoroutine refuses
        // and logs, and only the stores at 0x1B4F987/0x1B4F98C remain. The quirk is kept.
        public void ChangeSkewAmount(float X, float Y, bool instant)
        {
            m_skewX = X;
            m_skewY = Y;
            if (instant) return;
            StartCoroutine(LerpToSkew(false));
        }
    }
}
