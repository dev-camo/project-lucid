using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x02000061 (TypeDefIndex 11921), methods
	// 0x060002e1..0x060002e7, translated from the x86-64 bodies. The effect
	// only writes (greyscale, brightness) into each vertex's uv1 for a UI
	// shader to read; the setters store values unclamped (the Range
	// attributes only limit the inspector). No shipped asset references this
	// script, so it has a fresh GUID and no script binding.
	[AddComponentMenu("Hardlight UI/Effects/Greyscale And Brightness Modifier", 15)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class GreyscaleAndBrightnessModifier : BaseMeshEffect
	{
		[SerializeField]
		[Range(0f, 1f)]
		private float m_greyscaleFactor;

		// .ctor 0x060002e7 stores 1f before the BaseMeshEffect constructor.
		[Range(0f, 100f)]
		[SerializeField]
		private float m_brightness = 1f;

		public float GreyscaleFactor
		{
			get { return m_greyscaleFactor; }
			set
			{
				m_greyscaleFactor = value;
				if (graphic != null)
					graphic.SetVerticesDirty();
			}
		}

		public float Brightness
		{
			get { return m_brightness; }
			set
			{
				m_brightness = value;
				if (graphic != null)
					graphic.SetVerticesDirty();
			}
		}

		public override void ModifyMesh(VertexHelper vh)
		{
			if (!IsActive())
				return;

			List<UIVertex> verts = new List<UIVertex>();
			vh.GetUIVertexStream(verts);
			// 0x060002e5 returns before Clear when the stream is empty, so a
			// helper holding vertices but no triangles is left untouched.
			if (verts.Count == 0)
				return;

			for (int i = 0; i < verts.Count; i++)
			{
				UIVertex vertex = verts[i];
				// uv1 carries (greyscale, brightness) for the shader; z/w are zero.
				vertex.uv1 = new Vector4(m_greyscaleFactor, m_brightness, 0f, 0f);
				verts[i] = vertex;
			}

			vh.Clear();
			vh.AddUIVertexTriangleStream(verts);
		}

		// Stores both fields, then dirties the graphic once.
		public void SetGreyscaleFactorAndBrightness(float greyscaleFactor, float brightness)
		{
			m_greyscaleFactor = greyscaleFactor;
			m_brightness = brightness;
			if (graphic != null)
				graphic.SetVerticesDirty();
		}
	}
}
