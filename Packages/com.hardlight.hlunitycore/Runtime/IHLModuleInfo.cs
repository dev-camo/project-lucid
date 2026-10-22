namespace Hardlight
{
    // Original HLUnityCore.Runtime TypeDefIndex 9204 (0x020001c5), getters 0x06000c31..0x06000c36. No type in the
    // shipped metadata implements it, and it carries no custom attributes.
    public interface IHLModuleInfo
    {
        bool IsUnityPackage { get; }
        string ModulePath { get; }
        string AssetPath { get; }
        string AbsolutePath { get; }
        string RealPath { get; }
        UnityPackageManifest Manifest { get; }
    }
}
