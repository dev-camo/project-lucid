using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class SaveDataResolvableItem<TSaveDataResolvableItem> : SaveDataItem
    {
        // Game.Runtime.dll 0x06002cb3: shared native constructor delegates to SaveDataItem.
        protected SaveDataResolvableItem()
        {
        }

        public abstract void ResolveNewData(TSaveDataResolvableItem saveDataResolvableItem);
    }
}
