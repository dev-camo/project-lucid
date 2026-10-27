// Original Apple.Arcade.SplashScreen.dll:Apple.Arcade.SplashScreen.Interfaces.ISplashScreenVideoProvider.
// TypeDef 0x02000008, method 0x06000013 (abstract, no body).
using System.Threading.Tasks;
using UnityEngine.Video;

namespace Apple.Arcade.SplashScreen.Interfaces
{
    public interface ISplashScreenVideoProvider
    {
        Task<VideoClip> GetSplashScreenVideo();
    }
}
