using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Complete original Game.Runtime 0200042d: eight public fields and base-only ctor.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ComicPanelBehaviour : PlayableBehaviour
    {
        public ComicPanel PanelTemplate; // 04000e22
        public RenderTexture RenderTexture; // 04000e23
        public Vector2 ScreenPosition; // 04000e24
        public Vector3 Rotation; // 04000e25
        public Vector2 Scale; // 04000e26
        public AnimationClip EnterAnim; // 04000e27
        public AnimationClip LoopAnim; // 04000e28
        public AnimationClip ExitAnim; // 04000e29

        // Original 06001894: only the genuine PlayableBehaviour base constructor.
        public ComicPanelBehaviour() { }
    }
}
