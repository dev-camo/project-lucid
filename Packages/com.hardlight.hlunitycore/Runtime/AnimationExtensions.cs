using System.Collections;
using Hardlight.Utils;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000059 (0600023d..0600023f), iterator
    // <WaitUntilBlendIsFinished>d__2 0200005a; bodies from the 1.10.1 x86-64 binary.
    public static class AnimationExtensions
    {
        // 0x0600023d (0x1aae1a0): nothing happens unless the clip exists and is
        // playing. A positive fade on a weighted state blends to zero and stops it
        // from the shared CoroutineUtils host; otherwise it stops at once.
        public static void Stop(this Animation animation, string animationName, float fadeLength, out Coroutine coroutine)
        {
            coroutine = null;
            if (animation == null || animation.GetClip(animationName) == null || !animation.IsPlaying(animationName))
            {
                return;
            }

            if (fadeLength > 0f && animation[animationName].weight > 0f)
            {
                animation.Blend(animationName, 0f, fadeLength);
                coroutine = CoroutineUtils.RunCoroutine(WaitUntilBlendIsFinished(animation, animationName));
            }
            else
            {
                Stop(animation, animationName);
            }
        }

        // 0x0600023e (0x1aae400); inlined into its two callers.
        private static void Stop(Animation animation, string animationName)
        {
            animation.Stop(animationName);
        }

        // 0x0600023f (0x1aae380), MoveNext 0x06000242 (0x1aae450): the state is read
        // once; the stop is skipped when the component has been destroyed.
        private static IEnumerator WaitUntilBlendIsFinished(Animation animation, string animationName)
        {
            AnimationState animationState = animation[animationName];
            while (animationState != null && animationState.weight > 0f)
            {
                yield return null;
            }

            if (animation == null)
            {
                yield break;
            }
            Stop(animation, animationName);
        }
    }
}
