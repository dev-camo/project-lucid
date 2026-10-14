using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Assembly-CSharp; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class laserTest : MonoBehaviour
    {
        public GameObject laserEndPoint;
        public GameObject endball;
        public LineRenderer lineRender;

        // 0600004d. The shipped body is the same as UpdateLaser.
        public void Start()
        {
            UpdateLaser();
        }

        // 0600004e. The line end uses the local position, the ball the world position.
        public void UpdateLaser()
        {
            lineRender.SetPosition(1, laserEndPoint.transform.localPosition);
            endball.transform.position = laserEndPoint.transform.position;
        }
    }
}
