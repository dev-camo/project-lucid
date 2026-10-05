namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000242: five default callbacks have native
    // RET bodies. Only the pause property accessors are abstract contracts.
    public interface ITimeScaled
    {
        // 06000e73 / ARM64 1b1ab6c.
        void OnUpdate(float deltaTime) { }
        // 06000e74 / ARM64 1b1ab70.
        void OnFixedUpdate(float deltaTime) { }
        // 06000e75 / ARM64 1b1ab74.
        void OnLateUpdate(float deltaTime) { }
        // 06000e76 / ARM64 1b1ab78.
        void OnPause() { }
        // 06000e77 / ARM64 1b1ab7c.
        void OnResume() { }
        bool IsPaused { get; set; }
    }
}
