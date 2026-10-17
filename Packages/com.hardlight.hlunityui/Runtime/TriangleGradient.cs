// Original HLUnityUI.Runtime TriangleGradient 02000003 (MethodDefs 0x06000004..06),
// translated from the x86-64 bodies (0x1B389C0..0x1B391C0) and metadata. Local names
// are inferred. No shipped .prefab/.unity/.asset in the 2026-10-05 reference export
// references the exported script GUID a486770814f957cf5264fa361cc0674d, so the script
// stays unbound with a fresh GUID.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public class TriangleGradient : Graphic
{
    [SerializeField]
    private Color m_startColour = Color.white;
    [SerializeField]
    private Color m_endColour = Color.white;
    [SerializeField]
    private Vector2 m_direction = Vector2.up;
    [SerializeField]
    private float m_offset;
    [SerializeField]
    private float m_scale = 1f;
    private float[] m_distances;
    private List<UIVertex> m_verts = new List<UIVertex>();
    private const float NormalizedScale = 0.5f;
    private const int inverse = -1;

    // 0x06000004: one triangle (x, y) (x, -y) (x + width, -y) from the rect origin, so it
    // spans the rect only for a centred vertical pivot. The helper is not cleared again
    // before the recoloured stream is added back.
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        RectTransform rectTransform = gameObject.GetComponent(typeof(RectTransform)) as RectTransform;
        float left = rectTransform.rect.x;
        float bottom = rectTransform.rect.y;
        float right = rectTransform.rect.x + rectTransform.rect.width;
        // Read but never used (0x1B38AB9, 0x1B38AC3).
        float top = rectTransform.rect.y + rectTransform.rect.height;

        vertexHelper.Clear();
        vertexHelper.AddVert(new Vector2(left, bottom), m_startColour, Vector2.zero);
        vertexHelper.AddVert(new Vector2(left, -bottom), m_endColour, Vector2.up);
        vertexHelper.AddVert(new Vector2(right, -bottom), m_endColour, Vector2.one);
        vertexHelper.AddTriangle(0, 1, 2);

        m_verts.Clear();
        vertexHelper.GetUIVertexStream(m_verts);
        ApplyGradient(m_verts);
        vertexHelper.AddUIVertexTriangleStream(m_verts);
    }

    // 0x06000005: each vertex is projected on m_direction, normalised over the projected
    // range, shifted by m_offset and scaled about the middle by m_scale.
    private void ApplyGradient(List<UIVertex> m_verts)
    {
        if (m_verts.Count == 0) return;

        m_distances = new float[m_verts.Count];
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < m_verts.Count; i++)
        {
            Vector2 position = new Vector2(m_verts[i].position.x, m_verts[i].position.y);
            float distance = Vector2.Dot(position, m_direction);
            m_distances[i] = distance;
            if (distance < min) min = distance;
            if (distance > max) max = distance;
        }

        float range = max - min;
        for (int i = 0; i < m_verts.Count; i++)
        {
            // 0x1B38E0B: an inclusive length guard, so it never stops the loop early.
            if (i > m_distances.Length) break;

            float t = ((m_distances[i] - min) / range + m_offset - NormalizedScale) / m_scale + NormalizedScale;
            UIVertex vertex = m_verts[i];
            vertex.color = Color32.Lerp(m_startColour, m_endColour, t);
            m_verts[i] = vertex;
        }
    }
}
