using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000a25; methods 06003a0a..06003a1c. Every
    // setter writes the cached property id to the target material, unguarded.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ShaderParameterBinderTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Assign renderer to use it's material as target instead. If left null then assign value to renderer material instead.")]
        private Renderer m_rendererAsMaterialSource;
        [ShowIf("m_rendererAsMaterialSource", null)]
        [Tooltip("Will instance the material of the renderer at runtime.")]
        [SerializeField]
        private bool m_instanceMaterial;
        [DisableIf("m_rendererAsMaterialSource", null)]
        [SerializeField]
        private Material m_rendererMaterial;
        [SerializeField]
        private string m_propertyName;
        private int m_propertyId;

        // 06003a0a: only an unassigned material is filled, from the instanced
        // material of this GameObject's renderer.
        private void Reset()
        {
            if (m_rendererMaterial != null)
                return;
            Renderer ownRenderer = GetComponent<Renderer>();
            if (ownRenderer != null)
                m_rendererMaterial = ownRenderer.material;
        }

        // 06003a0b: a renderer source replaces the serialized material; the id
        // is resolved only for a named property and an available material.
        private void Awake()
        {
            if (string.IsNullOrEmpty(m_propertyName))
                return;
            if (m_rendererAsMaterialSource != null)
                m_rendererMaterial = m_instanceMaterial ? m_rendererAsMaterialSource.material : m_rendererAsMaterialSource.sharedMaterial;
            if (m_rendererMaterial == null)
                return;
            m_propertyId = Shader.PropertyToID(m_propertyName);
        }

        public void SetInt(int value) => m_rendererMaterial.SetInt(m_propertyId, value);
        public void SetFloat(float value) => m_rendererMaterial.SetFloat(m_propertyId, value);
        public void SetColor(Color value) => m_rendererMaterial.SetColor(m_propertyId, value);
        public void SetVector(Vector4 value) => m_rendererMaterial.SetVector(m_propertyId, value);
        public void SetMatrix(Matrix4x4 value) => m_rendererMaterial.SetMatrix(m_propertyId, value);
        public void SetTexture(Texture value) => m_rendererMaterial.SetTexture(m_propertyId, value);
        public void SetBuffer(ComputeBuffer value) => m_rendererMaterial.SetBuffer(m_propertyId, value);
        public void SetBuffer(GraphicsBuffer value) => m_rendererMaterial.SetBuffer(m_propertyId, value);
        public void SetFloatArray(List<float> values) => m_rendererMaterial.SetFloatArray(m_propertyId, values);
        public void SetFloatArray(float[] values) => m_rendererMaterial.SetFloatArray(m_propertyId, values);
        public void SetColorArray(List<Color> values) => m_rendererMaterial.SetColorArray(m_propertyId, values);
        public void SetColorArray(Color[] values) => m_rendererMaterial.SetColorArray(m_propertyId, values);
        public void SetVectorArray(List<Vector4> values) => m_rendererMaterial.SetVectorArray(m_propertyId, values);
        public void SetVectorArray(Vector4[] values) => m_rendererMaterial.SetVectorArray(m_propertyId, values);
        public void SetMatrixArray(List<Matrix4x4> values) => m_rendererMaterial.SetMatrixArray(m_propertyId, values);
        public void SetMatrixArray(Matrix4x4[] values) => m_rendererMaterial.SetMatrixArray(m_propertyId, values);
    }
}
