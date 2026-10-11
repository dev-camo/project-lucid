// Original HLUnityUI.Runtime research reconstruction.
// Source/native preservation only: provider algorithms, current generated layout,
// Unity/menu/bootstrap/native faults and full gameplay remain unqualified.
// 0x020000fd Hardlight.SelectableControllerBase
// 0x06000711 System.Boolean ShouldBlockFindSelectable() | abstract declaration; arm64=0 / x86_64=0
// 0x06000712 Hardlight.SelectableBase TryGetCachedSelectable() | abstract declaration; arm64=0 / x86_64=0
// 0x06000713 System.Void SelectThis(Hardlight.SelectableBase selectable, System.Boolean instantTransition) | abstract declaration; arm64=0 / x86_64=0
// 0x06000714 System.Void ActivateSystem() | abstract declaration; arm64=0 / x86_64=0
// 0x06000715 System.Void DeactivateSystem(System.Boolean isBeingDestroyed = False) | abstract declaration; arm64=0 / x86_64=0
// 0x06000716 System.Void RegisterExclusiveScene(System.String sceneName) | abstract declaration; arm64=0 / x86_64=0
// 0x06000717 System.Void DeregisterExclusiveScene(System.String sceneName) | abstract declaration; arm64=0 / x86_64=0
// 0x06000718 System.Void RemoveHighlighterFromScene() | abstract declaration; arm64=0 / x86_64=0
// 0x06000719 System.Single GetButtonTransitionTime() | abstract declaration; arm64=0 / x86_64=0
// 0x0600071a System.Void .ctor() | arm64 0x1b7c304..0x1b7c30c; x86_64 0x1b73a90..0x1b73aa0
using UnityEngine;

namespace Hardlight
{
    public abstract class SelectableControllerBase : MonoBehaviour, ISystem
    {
        public abstract bool ShouldBlockFindSelectable();
        public abstract SelectableBase TryGetCachedSelectable();
        public abstract void SelectThis(SelectableBase selectable, bool instantTransition);
        public abstract void ActivateSystem();
        public abstract void DeactivateSystem(bool isBeingDestroyed = false);
        public abstract void RegisterExclusiveScene(string sceneName);
        public abstract void DeregisterExclusiveScene(string sceneName);
        public abstract void RemoveHighlighterFromScene();
        public abstract float GetButtonTransitionTime();
        protected SelectableControllerBase() { }
    }
}
