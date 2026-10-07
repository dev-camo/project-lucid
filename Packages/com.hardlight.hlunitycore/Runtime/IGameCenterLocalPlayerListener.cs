using System;
using UnityEngine;

namespace Hardlight
{
    public interface IGameCenterLocalPlayerListener
    {
        string Name { get; }
        event Action<int, Texture2D> OnLoadPhotoForSize;
        event Action<int, bool> OnLoadPhotoForSizeCompleted;
        event Action<bool> OnAuthenticateHandler;
        event Action<string> OnPlayerDidChange;
        void Clear();
    }
}
