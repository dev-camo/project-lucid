using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Assembly-CSharp; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class prototype_timer : MonoBehaviour
    {
        private float spawnedTime;
        public float timerDuration;
        public GameObject spawnedObjects;

        // 06000049. The timer keeps counting down until it passes -0.5.
        private void Update()
        {
            if (spawnedTime <= 0f)
                TimerEnd();
            if (spawnedTime >= -0.5f)
                spawnedTime -= Time.deltaTime;
        }

        // 0600004a
        public void resetTimer()
        {
            spawnedTime = timerDuration;
            spawnedObjects.SetActive(true);
        }

        // 0600004b
        public void TimerEnd()
        {
            spawnedObjects.SetActive(false);
        }
    }
}
