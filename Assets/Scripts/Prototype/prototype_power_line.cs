using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Assembly-CSharp; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class prototype_power_line : MonoBehaviour
    {
        public GameObject target;
        public LineRenderer lineR;
        public bool powerOn;
        public bool powerSource;
        public prototype_power_line mainPower;
        public GameObject powerGlowOn;
        public GameObject powerGlowOff;
        public float poweredTime;
        public ParticleSystem p;

        // 06000054. A power source draws a line to the player for eight seconds; another node
        // switches on only while the main power is on.
        public void OnTriggerEnter(Collider other)
        {
            if (other.tag == "Player")
            {
                if (powerSource)
                {
                    target = other.gameObject;
                    powerOn = true;
                    lineR.useWorldSpace = true;
                    lineR.SetPosition(0, transform.position + Vector3.up * 1.5f);
                    poweredTime = 8f;
                }
                else if (mainPower.powerOn)
                {
                    powerOn = true;
                    PowerNodeOn();
                }
            }
        }

        // 06000055
        private void Update()
        {
            if (target && powerSource)
            {
                poweredTime -= Time.deltaTime;
                if (poweredTime <= 0f)
                {
                    target = null;
                    powerOn = false;
                    lineR.SetPosition(1, transform.position);
                }
                else
                {
                    lineR.SetPosition(1, target.transform.position + Vector3.up * 1.5f);
                }
            }
        }

        // 06000056
        public void PowerNodeOn()
        {
            powerGlowOn.SetActive(true);
            powerGlowOff.SetActive(false);
            p.Play();
        }
    }
}
