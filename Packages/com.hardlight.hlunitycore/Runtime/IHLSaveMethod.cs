using System.Collections.Generic;
using System.Text;

namespace Hardlight
{
    // Original interface slots 0..6, methods 0x06000d59..0x06000d5f.
    public interface IHLSaveMethod
    {
        bool TryGetSaveVersion(string saveIdentifier, out long version);
        IReadOnlyList<string> GetAllSaveIdentifiers();
        bool SaveData(HLPropertyStore.FileType fileType, StringBuilder contentBuilder, string saveIdentifier);
        bool BackupData(string saveIdentifier);
        void WipeSaveFile(string saveIdentifier);
        void WipeAllSaveFiles();
        string LoadData(HLPropertyStore.FileType fileType, string saveIdentifier);
    }
}
