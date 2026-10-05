namespace Hardlight
{
    // Original interface0x0200022b with literal0x0400072e and three abstract contracts; no own native body.
    public interface IActiveConfiguration
    {
        const string ActiveConfigFieldName = "ActiveConfig";
        ISystemConfigurationAssetCollection Config { get; }
        string AssetLocation();
        string GetFileName();
    }
}
