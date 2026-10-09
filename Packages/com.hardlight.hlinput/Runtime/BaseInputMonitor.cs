using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime02000050: complete nine APIs, one authored field.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class BaseInputMonitor : SingleScriptableObject, IBaseInputMonitor
    {
        private ControlMap m_controlMap;

        // Original06000184 is genuinely abstract.
        public abstract ICurrentBaseInputBindingsProvider CurrentBaseInputBindingsProvider { get; }

        // Original06000185: the controller and glyph arguments are unused by this base body.
        // Publish the map before its initialization, then invoke the original virtual hook.
        public virtual void Initialise(IBaseControllerProvider baseControllerProvider, ControlMap controlMap,
            IGlyphLookupSystemSetter glyphLookupSystemSetter)
        {
            if (!IsActiveOnPlatform()) return;
            m_controlMap = controlMap;
            m_controlMap.InitialiseStackableData();
            InitialiseControlMap(m_controlMap);
        }

        // Original06000186..187 are genuinely abstract.
        protected abstract void InitialiseControlMap(ControlMap controlMap);
        protected abstract void ShutdownControlMap(ControlMap controlMap);

        // Original06000188: read platform activity again; clear the field only after both calls succeed.
        public virtual void Shutdown()
        {
            if (!IsActiveOnPlatform()) return;
            m_controlMap.ShutdownStackableData();
            ShutdownControlMap(m_controlMap);
            m_controlMap = null;
        }

        // Original06000189 is genuinely abstract.
        protected abstract bool IsActiveOnPlatform();

        // Original0600018a invokes genuine ControlMap.ClearFrameInputs, not an invented Update hook.
        public virtual void Update()
        {
            if (IsActiveOnPlatform()) m_controlMap.ClearFrameInputs();
        }

        // Original0600018b is genuinely abstract.
        public abstract void ApplicationFocus(bool focused);
        // Implicit protected0600018c invokes genuine SingleScriptableObject's constructor only.
    }
}
