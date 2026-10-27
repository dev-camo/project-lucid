// Original Apple.Arcade.SplashScreen.dll:Apple.Arcade.SplashScreen.ResourcesSplashScreenVideoProvider.
// TypeDef 0x02000004, methods 0x06000003..0x06000008, with its closure class <>c__DisplayClass0_0
// (0x02000005, methods 0x06000009 and 0x0600000a).
// x86_64 GetSplashScreenVideo 0x425580..0x425a00. The native build inlines the three small helpers
// into it; their own bodies are 0x425a10 (aspect), 0x425b20 (orientation), 0x425a60 (aspect text)
// and 0x425b60 (low end). The aspect thresholds are the doubles at 0x29a6bc0: 1.87, 1.77, 1.6.
using System;
using System.Threading.Tasks;
using Apple.Arcade.SplashScreen.Interfaces;
using UnityEngine;
using UnityEngine.Video;

namespace Apple.Arcade.SplashScreen
{
    public class ResourcesSplashScreenVideoProvider : ISplashScreenVideoProvider
    {
        // Asks Resources for "<aspect>/<aspect>_<orientation>" (for example "16x9/16x9_Landscape") and
        // completes with the clip once the request finishes. A fault while starting the request fails the
        // task instead of escaping. Nothing here checks that the request found a clip: a missing clip
        // completes the task with null.
        public Task<VideoClip> GetSplashScreenVideo()
        {
            TaskCompletionSource<VideoClip> tcs = new TaskCompletionSource<VideoClip>();
            ResourceRequest request;
            try
            {
                string aspect = GetAspectRatioRepresentation(GetScreenAspect());
                string orientation = GetOrientationRepresentation();
                string path = aspect + "/" + aspect + "_" + orientation;
                // The native build keeps the "_Low" literal but IsLowEndDevice is constant false, so the
                // branch was removed from the compiled code.
                if (IsLowEndDevice())
                {
                    path += "_Low";
                }
                request = Resources.LoadAsync<VideoClip>(path);
                request.completed += operation => tcs.SetResult((VideoClip)request.asset);
            }
            catch (Exception exception)
            {
                tcs.SetException(exception);
            }
            return tcs.Task;
        }

        // Long side over short side of the screen.
        private float GetScreenAspect()
        {
            return Screen.width < Screen.height
                ? (float)Screen.height / Screen.width
                : (float)Screen.width / Screen.height;
        }

        // The native body reads the screen width and height and discards them, then returns the
        // constant: every screen is "Landscape".
        private string GetOrientationRepresentation()
        {
            _ = Screen.width;
            _ = Screen.height;
            return "Landscape";
        }

        private string GetAspectRatioRepresentation(float aspect)
        {
            if (aspect >= 1.87)
            {
                return "19.5x9";
            }
            if (aspect >= 1.77)
            {
                return "16x9";
            }
            if (aspect >= 1.6)
            {
                return "16x10";
            }
            return "4x3";
        }

        private bool IsLowEndDevice()
        {
            return false;
        }

        public ResourcesSplashScreenVideoProvider() { }
    }
}
