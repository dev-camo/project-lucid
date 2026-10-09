using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SourceController : IInputKeySource, IInputAxisSource
    {
        // Original0600027d..280 ignore the authored joystickIndex argument.
        public virtual bool GetKey(KeyCode keyCode, int joystickIndex) => Input.GetKey(keyCode);
        public virtual bool GetKeyDown(KeyCode keyCode, int joystickIndex) => Input.GetKeyDown(keyCode);
        public virtual bool GetKeyUp(KeyCode keyCode, int joystickIndex) => Input.GetKeyUp(keyCode);
        public virtual float GetAxis(string axisName, int joystickIndex) => Input.GetAxis(axisName);
        public virtual void Update() { } // Original06000281, genuine RET.
        public SourceController() { } // Original06000282.
    }
}
