using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000259.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UpdatePublisherDeltaTime : UpdatePublisher<UpdatePublisherDeltaTime.UpdateType>
    {
        // Original 0x0200025a. The literal value indexes m_updateTypeLookup.
        public enum UpdateType
        {
            DeltaTime = 0,
            SmoothDeltaTime = 1,
            UnscaledDeltaTime = 2
        }

        // Initializer lambdas <.ctor>b__3_0..2 (0x06000f10..12) run before
        // the base constructor (0x06000f0d, 0x1B1B7F0).
        private readonly Func<float>[] m_updateTypeLookup =
        {
            () => Time.deltaTime,
            () => Time.smoothDeltaTime,
            () => Time.unscaledDeltaTime
        };

        // 0x06000f0c; 0x1B1B780. The player omits the bounds check for an
        // undefined UpdateType; managed runtimes throw instead.
        protected override void AdvanceUpdateInfo(UpdateType updateType, UpdateInfo updateInfo)
        {
            updateInfo.OnUpdate(m_updateTypeLookup[(int)updateType]());
        }
    }
}
