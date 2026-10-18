using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009e5. The load handle is not kept, so the
    // texture is never released by this component.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AssignAddressableTexture : MonoBehaviour
    {
        [SerializeField]
        private Camera m_camera;
        [SerializeField]
        private AssetReferenceT<RenderTexture> m_renderTexture;

        private void OnEnable()
        {
            ProcessManager.GetSystem<AddressableManager>().LoadAsset(m_renderTexture,
                prefab => m_camera.targetTexture = prefab);
        }
    }
}
