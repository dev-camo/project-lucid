// Complete original Game.Runtime 02000328. Negative lifetime remains permanent; NaN skips updating.
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterDebug_Object : MonoBehaviour
    {
        private float m_lifetime;
        public void SetLifetime(float lifetime) => m_lifetime = lifetime; // 060013a9
        public void SetScale(Vector3 scale) // 060013aa
        {
            SetAxisScale(0, scale.x);
            SetAxisScale(1, scale.y);
            SetAxisScale(2, scale.z);
        }
        private void SetAxisScale(int axisIndex, float scale) // 060013ab: the local Y scale is written for all three child axes.
        {
            if (scale == 0f) return;
            Transform child = transform.GetChild(axisIndex);
            Vector3 position = child.localPosition;
            position[axisIndex] = scale * 0.5f;
            child.localPosition = position;
            Vector3 localScale = child.localScale;
            localScale.y = scale * 0.5f;
            child.localScale = localScale;
        }
        private void Update() // 060013ac
        {
            if (m_lifetime >= 0f)
            {
                m_lifetime -= Time.deltaTime;
                if (m_lifetime < 0f) Destroy(gameObject);
            }
        }
        public CharacterDebug_Object() { } // 060013ad, original base-only body.
    }
}
