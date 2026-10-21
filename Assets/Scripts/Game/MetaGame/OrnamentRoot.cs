using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020006e2: the parent an OrnamentViewer instantiates ornaments under. It announces itself
    // to the viewer on the string message exchange.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OrnamentRoot : MonoBehaviour
    {
        // 06002533 (0x59FFC0)
        private void Awake()
        {
            ProcessManager.GetSystemAutoCreate<MessageExchange<string>>().PublishMessage(OrnamentViewer.MessageIdentifier, this);
        }

    }
}
