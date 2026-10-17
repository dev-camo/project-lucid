// Original HLUnityUI.Runtime Hardlight.Flip 0200005f (methods 060002c6..060002cd),
// reconstructed from the x86-64 bodies and original metadata. Local names are inferred.
// Shipped assets use this script: in the 2026-10-05 reference export,
// Assets/Scripts/HLUnityUI.Runtime/Hardlight/Flip.cs.meta has script GUID
// 17e3576bbd50325148195785409a2cc1, referenced by 129 UI prefabs (for example
// uicontainer_game_hud, uicontainer_start_menu, uicontainer_character_select).
// The script binding is still pending (no Unity serialization proof yet), so
// those assets do not bind to this type and the .meta GUID is not the export's.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // dump.cs lists ArrayBoundsChecks before NullChecks for this type.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [AddComponentMenu("Hardlight UI/Effects/Flip Sprite", 1)]
    public class Flip : BaseMeshEffect
    {
        [SerializeField]
        private bool flipHorizontal;
        [SerializeField]
        private bool flipVertical;

        // 060002c6: no IsActive guard; the stream is always rebuilt, even with both flips off.
        public override void ModifyMesh(VertexHelper vh)
        {
            List<UIVertex> verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            ModifyVertices(verts);
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }

        // 060002c7: mirrors each flagged axis across the xy bounds of the vertices.
        // Count is read before the flags; the bounds use Vector2.Min/Max(bound, position).
        public void ModifyVertices(List<UIVertex> verts)
        {
            if (verts.Count == 0 || (!flipHorizontal && !flipVertical)) return;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            // A bare enumerator loop, not foreach: the init list (0x1b4da53..96) has
            // GetEnumerator, MoveNext and get_Current but no Enumerator.Dispose, and the
            // body has no finally. script.json has a Dispose MethodInfo for every other
            // List.Enumerator<T> with MoveNext (336 of 337); UIVertex is the exception.
            // Whether Current is read once or twice per step is not visible (inlined).
            List<UIVertex>.Enumerator e = verts.GetEnumerator();
            while (e.MoveNext())
            {
                Vector3 position = e.Current.position;
                max = Vector2.Max(max, position);
                min = Vector2.Min(min, position);
            }
            // addps 0x1b4db6e: the bounds are summed once, after the bounds pass.
            Vector2 sum = min + max;

            for (int i = 0; i < verts.Count; ++i)
            {
                UIVertex vert = verts[i];
                Vector3 position = vert.position;
                if (flipHorizontal) position.x = sum.x - position.x;
                if (flipVertical) position.y = sum.y - position.y;
                vert.position = position;
                verts[i] = vert;
            }
        }

        // 060002c8: toggles unconditionally, then dirties the graphic.
        public void FlipHorizontal()
        {
            flipHorizontal = !flipHorizontal;
            MarkDirty();
        }

        // 060002c9
        public void FlipVertical()
        {
            flipVertical = !flipVertical;
            MarkDirty();
        }

        // 060002ca: an unchanged value returns before writing or dirtying.
        public void SetVerticalFlip(bool on)
        {
            if (flipVertical == on) return;
            flipVertical = on;
            MarkDirty();
        }

        // 060002cb
        public void SetHorizontalFlip(bool on)
        {
            if (flipHorizontal == on) return;
            flipHorizontal = on;
            MarkDirty();
        }

        // 060002cc: each mutator body above contains this sequence (get_graphic,
        // op_Equality, [klass+0x2f8] tail call) and never calls 0x1b4dd80; a call
        // to MarkDirty inlined by the compiler is inferred, not proven.
        private void MarkDirty()
        {
            if (graphic == null) return;
            graphic.SetVerticesDirty();
        }
    }
}
