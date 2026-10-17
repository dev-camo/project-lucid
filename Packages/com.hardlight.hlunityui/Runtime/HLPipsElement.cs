using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

// Original HLUnityUI.Runtime 0200000f (MethodDef 0x0600007b): the events HLPips
// raises on one pip.
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[Il2CppSetOption(Option.NullChecks, false)]
public sealed class HLPipsElement : MonoBehaviour
{
    public UnityEvent OnActivate;
    public UnityEvent OnDeactivate;
    public UnityEvent OnInitialiseActive;
    public UnityEvent OnInitialiseInactive;

    public HLPipsElement() { }
}
