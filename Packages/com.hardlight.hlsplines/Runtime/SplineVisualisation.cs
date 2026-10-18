using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 020000ad; bodies translated from the 1.10.1 x86-64 binary.
	[ExecuteInEditMode]
	[RequireComponent(typeof(Spline))]
	public class SplineVisualisation : MonoBehaviour
	{
		[SerializeField]
		private Spline m_spline;

		[SerializeField]
		private GameObject m_meshVisualisation;

		private ISpline m_currentSpline;
		private EditableSpline m_editableSpline;
		private DateTime? m_storeGeneratedMeshTime;
		private readonly List<Vector3> m_meshVerts = new List<Vector3>();
		private readonly List<int> m_meshTriangles = new List<int>();
		private readonly List<Vector2> m_meshUVs = new List<Vector2>();

		// 060005a9 (1.10.1 RVA 0x1A97140)
		private void Reset()
		{
			m_spline = GetComponent<Spline>();
		}

		// 060005aa (1.10.1 RVA 0x1A97190): the previous spline is unsubscribed without a null guard.
		private void OnSplineTypeChanged(ISpline iSpline)
		{
			m_currentSpline.OnSplineTypeChanged -= OnSplineTypeChanged;
			m_currentSpline = iSpline;
			if (m_currentSpline is EditableSpline editableSpline)
			{
				m_editableSpline = editableSpline;
				m_meshVisualisation = StartGeneratingMesh();
			}
			else if (m_editableSpline != null)
			{
				m_editableSpline.OnSplineKnotsUpdated -= UpdateGeneratedMesh;
			}
			m_currentSpline.OnSplineTypeChanged += OnSplineTypeChanged;
		}

		// 060005ab (1.10.1 RVA 0x1A97450)
		private GameObject StartGeneratingMesh()
		{
			m_editableSpline.OnSplineKnotsUpdated += UpdateGeneratedMesh;
			if (m_meshVisualisation == null)
				UpdateGeneratedMesh();
			return m_meshVisualisation;
		}

		// 060005ac (1.10.1 RVA 0x1A97520)
		private void UpdateGeneratedMesh()
		{
			m_meshVisualisation = GenerateMesh(m_meshVisualisation, m_editableSpline);
			m_storeGeneratedMeshTime = DateTime.Now + TimeSpan.FromSeconds(SplineEditorDefinition.Default.GeneratedMeshesSaveDelaySeconds);
		}

		// 060005ad (1.10.1 RVA 0x1A983C0): the pending save time clears once it has passed.
		private void StoreMesh()
		{
			if (!m_storeGeneratedMeshTime.HasValue || DateTime.Now < m_storeGeneratedMeshTime.Value || m_editableSpline == null)
				return;
			StoreGeneratedMesh(m_meshVisualisation.GetComponent<MeshFilter>().sharedMesh);
			m_storeGeneratedMeshTime = null;
		}

		// 060005ae (1.10.1 RVA 0x1A97630): one quad of fixed local-x width per step; v advances by twice the step's chord length.
		private GameObject GenerateMesh(GameObject visualisation, EditableSpline editableSpline)
		{
			string meshName = GetMeshName(ref visualisation);
			MeshFilter meshFilter;
			if (visualisation == null)
			{
				visualisation = new GameObject(meshName);
				meshFilter = visualisation.AddComponent<MeshFilter>();
				MeshRenderer meshRenderer = visualisation.AddComponent<MeshRenderer>();
				// The spline visualisation shares the ribbon definition's material.
				meshRenderer.sharedMaterial = RibbonEditorDefinition.Default.VisualisationMaterial;
			}
			else
			{
				meshFilter = visualisation.GetComponent<MeshFilter>();
			}

			Transform parent = editableSpline.transform;
			Transform visualisationTransform = visualisation.transform;
			visualisationTransform.parent = parent;
			visualisationTransform.localPosition = Vector3.zero;
			visualisationTransform.localRotation = Quaternion.identity;
			visualisationTransform.localScale = Vector3.one;

			m_meshVerts.Clear();
			m_meshTriangles.Clear();
			m_meshUVs.Clear();

			int quality = SplineEditorDefinition.Default.GeneratedMeshQuality;
			float width = SplineEditorDefinition.Default.GeneratedMeshWidth;
			float step = 1f / quality;
			float v = 0f;
			for (int knotIndex = 0; knotIndex < editableSpline.Knots.Count - 1; ++knotIndex)
			{
				for (int i = 0; i < quality; ++i)
				{
					float t0 = i * step;
					float t1 = (i + 1) * step;
					Vector3 p0 = editableSpline.GetLocalPosFromKnotT(new KnotT(knotIndex, t0));
					Vector3 p1 = editableSpline.GetLocalPosFromKnotT(new KnotT(knotIndex, t1));
					float vNext = v + Vector3.Distance(p1, p0) * 2f;

					Vector3 left0 = p0 + Vector3.left * width;
					Vector3 right0 = p0 + Vector3.right * width;
					Vector3 left1 = p1 + Vector3.left * width;
					Vector3 right1 = p1 + Vector3.right * width;

					m_meshVerts.Add(left0);
					int left0Index = m_meshVerts.Count - 1;
					m_meshTriangles.Add(left0Index);
					m_meshUVs.Add(new Vector2(0f, v));

					m_meshVerts.Add(right1);
					int right1Index = m_meshVerts.Count - 1;
					m_meshTriangles.Add(right1Index);
					m_meshUVs.Add(new Vector2(1f, vNext));

					m_meshVerts.Add(right0);
					m_meshTriangles.Add(m_meshVerts.Count - 1);
					m_meshUVs.Add(new Vector2(1f, v));

					m_meshTriangles.Add(left0Index);
					m_meshVerts.Add(left1);
					int left1Index = m_meshVerts.Count - 1;
					m_meshUVs.Add(new Vector2(0f, vNext));
					m_meshTriangles.Add(left1Index);
					m_meshTriangles.Add(right1Index);

					v = vNext;
				}
			}

			Mesh mesh = new Mesh();
			mesh.name = visualisationTransform.name;
			mesh.hideFlags = HideFlags.None;
			mesh.SetVertices(m_meshVerts);
			mesh.SetTriangles(m_meshTriangles, 0);
			mesh.SetUVs(0, m_meshUVs);
			mesh.RecalculateNormals();
			mesh.RecalculateTangents();
			mesh.RecalculateUVDistributionMetrics();
			mesh.OptimizeReorderVertexBuffer();
			mesh.Optimize();
			mesh.RecalculateBounds();
			mesh.UploadMeshData(true);
			meshFilter.mesh = mesh;
			return visualisationTransform.gameObject;
		}

		// 060005af (1.10.1 RVA 0x1A984F0): named after this object; the argument is not read.
		private string GetMeshName(ref GameObject visualisation)
		{
			return gameObject.name + "[Visualisation]";
		}

		// 060005b0 (1.10.1 RVA 0x1A984E0): the retail body is empty.
		private static void StoreGeneratedMesh(Mesh mesh)
		{
		}

		// 060005b1 (1.10.1 RVA 0x1A98550)
		public SplineVisualisation()
		{
		}
	}
}
