namespace Hardlight
{
    public class FSMUser : IGraphUser
    {
        // HLUnityCore.Runtime.dll:Hardlight.FSMUser:0x060004d9/0x060004da;
        // arm64 0x1ad18d0/0x1ad18d8: auto-property accessors.
        public IGraphStorage Storage { get; private set; }

        // 0x060004db; arm64 0x1ad18e0: retain supplied storage, or create
        // FSMStorage with the original default history maximum of five.
        public FSMUser(IGraphStorage graphStorage = null)
        {
            Storage = graphStorage ?? new FSMStorage(5);
        }

        // 0x060004dc; arm64 0x1ad1960: clear without nulling the property.
        public void DestroyUser() => Storage.Clear();
    }
}
