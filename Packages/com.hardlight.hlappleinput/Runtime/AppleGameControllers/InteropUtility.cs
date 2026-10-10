// Modified from Apple unityplugins: preserve the shipped plural utility namespace; native library selection is unchanged.
namespace Apple.GameControllers
{
    internal static class InteropUtility
    {
#if UNITY_IOS || UNITY_TVOS
        public const string DLLName = "__Internal";
#else
        public const string DLLName = "GameControllerWrapper";
#endif
    }
}