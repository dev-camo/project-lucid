using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original HLInput.Runtime02000051 is a genuine stripped empty marker interface.
    // Existing ICurrentBaseInputBindingsProvider/IBaseControllerProvider whole contracts are reused.
    public interface IBaseInputMonitor { }

    // Original HLInput.Runtime02000084: one genuinely abstract API.
    public interface IGlyphLookupSystemSetter
    {
        void SetGameInputGlyphMap(Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> generatedMap);
    }

    // Original HLInput.Runtime02000086: two genuinely abstract APIs and genuine engine base constructor.
    public abstract class ScriptableControllerProvider : ScriptableObject
    {
        public abstract BaseControllerProvider GetControllerNameProvider(float controllerConnectionPollingRateInSeconds);
        public abstract bool IsActiveProvider();
        // Implicit protected06000272 invokes ScriptableObject constructor only.
    }
}
