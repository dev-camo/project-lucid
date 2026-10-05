namespace Hardlight
{
    // Original enum constants, HLUnityCore.Runtime.dll metadata.
    public enum FSMUpdateType
    {
        Update = 0, FixedUpdate = 1, LateUpdate = 2, InitialiseUser = 3, Manual = 4
    }

    public enum FSMActionReason
    {
        InitialiseUser = 0, ClearUser = 1, TransitionFired = 2, RemoveActiveState = 3, Forced = 4
    }

    public readonly struct FSMUpdateContext
    {
        // HLUnityCore.Runtime.dll:Hardlight.FSMUpdateContext:0x060004d5;
        // arm64 0x1ad18b4: load the float field.
        public float DeltaTime { get; }

        // 0x060004d6; arm64 0x1ad18bc: load the enum field.
        public FSMUpdateType UpdateType { get; }

        // 0x060004d7; arm64 0x1ad18c4: store delta and zero enum.
        public FSMUpdateContext(float deltaTime)
        {
            DeltaTime = deltaTime;
            UpdateType = FSMUpdateType.Update;
        }

        // 0x060004d8; arm64 0x1abc984: store both arguments unchanged.
        public FSMUpdateContext(float deltaTime, FSMUpdateType updateType)
        {
            DeltaTime = deltaTime;
            UpdateType = updateType;
        }
    }
}
