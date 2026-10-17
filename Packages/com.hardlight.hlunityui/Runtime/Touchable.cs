// Original HLUnityUI.Runtime SonicDash.UI.Touchable 02000014 (MethodDefs 0x06000087..88),
// translated from the x86-64 bodies (0x1B3C730..0x1B3C760). An invisible raycast target:
// a MaskableGraphic that draws nothing.
// Shipped assets use this script: in the 2026-10-05 reference export,
// Assets/Scripts/HLUnityUI.Runtime/SonicDash/UI/Touchable.cs.meta has script GUID
// ba8b219259d957e8bba90884fcdc0f72, referenced by 26 components in 15 prefabs under
// Data/UI/Prefabs/Containers (for example uicontainer_level_select and eight
// WhatsNew/uicontainer_whats_new* prefabs). The script binding is still pending, so
// those assets do not bind to this type and the .meta GUID is not the export's.
using UnityEngine.UI;

namespace SonicDash.UI
{
    public class Touchable : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
