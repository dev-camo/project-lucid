using UnityEngine;

namespace HardlightProject
{
    public interface IEntityActivatable
    {
        // Original true abstract contracts 0x06003bcd/0x06003bce, in slot order.
        EntityActivationDefinition Definition { get; }
        Transform Transform { get; }
    }
}
