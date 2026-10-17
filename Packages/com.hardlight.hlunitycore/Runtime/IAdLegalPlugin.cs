using System;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200014c (methods 0x06000943..0x06000946).
    // Optional second interface of the registered ILegalPlugin; LegalModule
    // casts to it before every advertising step.
    public interface IAdLegalPlugin
    {
        void RequestAdImprovementConsent(Action callbackAccept, Action callbackReject);

        void RequestAdImprovementConfirmation(Action callbackAccept, Action callbackCancel);

        void RequestAdType(Action<LegalModule.AdType> callbackAccept, Action callbackCancel);

        void OnAdTypeSelectionChanged(LegalModule.AdType adType);
    }
}
