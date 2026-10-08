using System.Text;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x02000293, whole three-method interface.
    public interface IJsonObject
    {
        // 0x06001026, original abstract contract.
        bool Encode(StringBuilder builder);
        // 0x06001027, original abstract contract.
        IJsonObject Clone();
        // 0x06001028, original abstract contract.
        void Release();
    }
}
