using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009df; 0x6A6660..0x6A66E0. Each helper tail-calls
    // one Actor method with a fixed flag.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorVisualProxyModifier : MonoBehaviour
    {
        protected void ShowVisualProxy(Actor actor)
        {
            actor.ToggleFormRenderers(true);
        }

        protected void HideVisualProxy(Actor actor)
        {
            actor.ToggleFormRenderers(false);
        }

        protected void DisableVisualProxy(Actor actor)
        {
            actor.SetVisualProxyActive(false);
        }

        protected void EnableVisualProxy(Actor actor)
        {
            actor.SetVisualProxyActive(true);
        }
    }
}
