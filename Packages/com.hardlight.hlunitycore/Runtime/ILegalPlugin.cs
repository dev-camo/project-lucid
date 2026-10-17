using System;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200014b (methods 0x0600093c..0x06000942).
    // The platform legal UI implements this; LegalModule drives it.
    public interface ILegalPlugin
    {
        void RequestPlayerAge(Action<int> callback);

        void RequestConsent(bool underAge, Action callback);

        void RequestConsent(bool underAge, Action callbackAccept, Action callbackReject);

        void OnDataChange(LegalModule.RequestAccessData newDataToSave);

        void OnAccessGranted(bool restrictedMode);

        void OnAccessRevoked(bool dueToOptOut);

        string GetLegalDocumentsFileURL();
    }
}
