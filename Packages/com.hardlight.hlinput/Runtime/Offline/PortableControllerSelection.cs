using Hardlight;

namespace ProjectLucid.Offline
{
    // Intentional desktop port selection at HLInputModule's existing provider
    // boundary. The original Apple selection remains in its research branch.
    // Keep the original Unity polling implementation and its configured rate.
    public static class PortableControllerSelection
    {
        public static BaseControllerProvider Create(float pollingRateInSeconds)
        {
            return new UnityControllerNameProvider(pollingRateInSeconds)
            {
                // Original base polling invokes this when the count changes,
                // including before an input monitor has subscribed.
                OnControllerConnectionUpdate = new FastAction()
            };
        }
    }
}
