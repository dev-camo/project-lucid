using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UIContainerManager
    {
        // Original060000c7..ce: eight genuine abstract contracts, no own bodies.
        public abstract UIContainer OpenContainer(UIContainer containerPrefab);
        public abstract void CloseAll();
        public abstract void Close(UIContainerIdentifier identifier);
        public abstract bool IsOpen(UIContainerIdentifier identifier);
        public abstract bool IsAnyOpen();
        public abstract bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers);
        public abstract bool TryGet(UIContainerIdentifier identifier, out UIContainer container);
        public abstract void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor);

        // Original060000cf: initialized local, original virtual TryGet route,
        // then UIContainer's actual virtual Canvas enable operation on success.
        public void SetEnabled(UIContainerIdentifier identifier, bool enable)
        {
            UIContainer container = null;
            if (TryGet(identifier, out container)) container.SetEnabled(enable);
        }
        // Original060000d0: implicit protected Object base constructor.
    }
}
