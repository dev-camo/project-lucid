namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000034; default members 0x0600015b/0x0600015c.
    public interface IManifestFileConfig
    {
        string PathToManifestTemplateFile { get; }

        string PathToManifestFile { get; }

        string DefaultManifestPath => "/NativeAndroid~/AndroidManifest.xml";

        string DefaultManifestTemplatePath => "/NativeAndroid~/TemplateAndroidManifest.xml";
    }
}
