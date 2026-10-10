using Hardlight;

namespace ProjectLucid.Preservation
{
    // Explicit research/test route to complete original managed shipping owners.
    // This factory does not select the default offline account or platform APIs.
    public static class ShippingGameCenterObjects
    {
        public static GameCenterLocalPlayer CreateLocalPlayer() =>
            new GameCenterLocalPlayer(new GameCenterLocalPlayerListenerStub());
    }
}
