using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Assembly-CSharp; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class prototype_destructable_wall : MonoBehaviour
    {
        public GameObject ball;
        public UnityEvent wallhit;
        public GameObject surfacefollower;
        public GameObject facePlayerObject;

        // 06000050
        public void CheckBallDistance()
        {
            float distance = Vector3.Distance(transform.position, ball.transform.position);
            print(distance);
            if (distance < 9f)
                wallhit.Invoke();
        }

        // 06000051. Keeps the yaw after facing along the player object and tilts by 5 degrees.
        public void UpdateFacingDir()
        {
            surfacefollower.transform.forward = facePlayerObject.transform.forward;
            surfacefollower.transform.rotation = Quaternion.Euler(5f, surfacefollower.transform.rotation.eulerAngles.y, 0f);
        }

        // 06000052. The shipped body compares the collider with the ball and does nothing more.
        public void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.gameObject == ball)
            {
            }
        }
    }
}
