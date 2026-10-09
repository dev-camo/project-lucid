using System.Collections.Generic;

namespace Hardlight
{
    // Original HLInput.Runtime 0200001c: complete fieldless generic interface.
    // Its genuine IBaseControllerProvider parent has the original three APIs.
    // TController is invariant and has no constraints or authored attributes.
    public interface IControllerProvider<TController> : IBaseControllerProvider
    {
        // Original 06000062: genuine abstract slot0, zero native body pointer.
        IReadOnlyList<TController> GetControllers();
    }
}
