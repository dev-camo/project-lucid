namespace HLCloud.Plugin
{
    public interface ICloud
    {
        void Native_CloudDidChange(string cloudMessage);
        void OnCloudChange(string[] changedKeys, ChangeReason changeReason);
    }
}
