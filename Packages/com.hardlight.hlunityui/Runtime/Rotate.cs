// Original HLUnityUI.Runtime Hardlight.Rotate 02000062 (MethodDefs 0x060002e8..eb) and
// its nested Rotation enum 02000063, translated from the x86-64 bodies
// (0x1B4F030..0x1B4F510) and metadata. The index tables are the original
// <PrivateImplementationDetails> array data: each moves the positions of one
// quad's six-entry triangle stream (v0 v1 v2 v2 v3 v0) round by one, two or
// three corners. No shipped .prefab/.unity/.asset in the 2026-10-05 reference
// export references the exported script GUID d0923c82a75b18cbde68a70ded54c08a,
// so the script stays unbound with a fresh GUID.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [AddComponentMenu("Hardlight UI/Effects/Rotate Sprite", 2)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Rotate : BaseMeshEffect
    {
        public enum Rotation
        {
            None = 0,
            Quarter = 1,
            Half = 2,
            ThreeQuarter = 3
        }

        [SerializeField]
        private Rotation rotation;
        private int[] ninety = new int[] { 1, 2, 4, 4, 0, 1 };
        private int[] oneEighty = new int[] { 2, 4, 0, 0, 1, 2 };
        private int[] twoSeventy = new int[] { 4, 0, 1, 1, 2, 4 };

        // 0x060002e8: no guard of its own; the active check lives in ModifyVertices.
        public override void ModifyMesh(VertexHelper vh)
        {
            List<UIVertex> verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            ModifyVertices(verts);
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }

        // 0x060002e9: only positions move; every other channel stays on its vertex.
        // The tables cover a single quad; with array bounds checks off the shipped
        // build reads past them for longer streams instead of throwing.
        public void ModifyVertices(List<UIVertex> verts)
        {
            if (!IsActive() || verts.Count == 0) return;

            Vector3[] positions = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                int source;
                switch (rotation)
                {
                    case Rotation.Quarter:
                        source = ninety[i];
                        break;
                    case Rotation.Half:
                        source = oneEighty[i];
                        break;
                    case Rotation.ThreeQuarter:
                        source = twoSeventy[i];
                        break;
                    default:
                        source = i;
                        break;
                }
                positions[i] = verts[source].position;
            }

            for (int i = 0; i < verts.Count; i++)
            {
                UIVertex vert = verts[i];
                vert.position = positions[i];
                verts[i] = vert;
            }
        }

        // 0x060002ea: dirties the graphic here and again in the base override.
        protected override void OnDidApplyAnimationProperties()
        {
            if (graphic != null) graphic.SetVerticesDirty();
            base.OnDidApplyAnimationProperties();
        }
    }
}
