using System;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200082c; bodies 0x606880..0x606A40 (1.10.1 x86-64).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplicationInfoView : MonoBehaviour
    {
        [SerializeField]
        private Strings m_versionPrefix;
        public readonly Bindable<string> BuildVersion = new Bindable<string>("...");
        public readonly Bindable<string> BuildCode = new Bindable<string>("...");
        public readonly Bindable<DateTime> BuildTime = new Bindable<DateTime>();

        // 06002f79
        private void OnEnable()
        {
            CoroutineUtils.RunCoroutine(ApplicationInfo.LoadOrCreateDefault(OnApplicationInfoLoaded));
        }

        // 06002f7a: the stored FILETIME is converted with FromFileTimeUtc, mirroring WriteToFile.
        private void OnApplicationInfoLoaded(ApplicationInfo appInfo)
        {
            BuildVersion.Value = StringTable.GetString(m_versionPrefix) + Application.version;
            BuildCode.Value = appInfo.BuildCode;
            BuildTime.Value = DateTime.FromFileTimeUtc(appInfo.BuildTime);
        }
    }
}
