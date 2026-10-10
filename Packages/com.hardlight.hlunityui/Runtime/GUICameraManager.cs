using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

// Original HLUnityUI.Runtime 02000002, complete three-method owner. This camera
// participates in the original cutscene/menu handoff. Preserve the unguarded call.
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[Il2CppSetOption(Option.NullChecks, false)]
public class GUICameraManager : MonoSingleton<GUICameraManager>
{
    [SerializeField] private Camera m_menuCamera;

    public Camera MenuCamera => m_menuCamera;
    public void ToggleCameras(bool on) => m_menuCamera.enabled = on;
    public GUICameraManager() { }
}
