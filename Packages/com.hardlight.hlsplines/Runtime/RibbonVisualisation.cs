using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 020000ac; bodies translated from the 1.10.1 x86-64 binary.
	[ExecuteInEditMode]
	[RequireComponent(typeof(Ribbon))]
	public class RibbonVisualisation : MonoBehaviour
	{
		[SerializeField]
		private Ribbon m_ribbon;

		[SerializeField]
		private GameObject m_meshVisualisation;

		private IRibbon m_currentRibbon;
		private EditableRibbon m_editableRibbon;
		private DateTime? m_storeGeneratedMeshTime;
		private readonly List<Vector3> m_meshVerts = new List<Vector3>();
		private readonly List<int> m_meshTriangles = new List<int>();
		private readonly List<Vector2> m_meshUVs = new List<Vector2>();

		// 060005a0 (1.10.1 RVA 0x1A94ED0)
		private void Reset()
		{
			m_ribbon = GetComponent<Ribbon>();
		}

		// 060005a1 (1.10.1 RVA 0x1A94F20): the previous ribbon is unsubscribed without a null guard.
		private void OnRibbonTypeChanged(IRibbon iRibbon)
		{
			m_currentRibbon.OnRibbonTypeChanged -= OnRibbonTypeChanged;
			m_currentRibbon = iRibbon;
			if (m_currentRibbon is EditableRibbon editableRibbon)
			{
				m_editableRibbon = editableRibbon;
				m_meshVisualisation = StartGeneratingMesh();
			}
			else if (m_editableRibbon != null)
			{
				m_editableRibbon.OnRibbonKnotsUpdated -= UpdateGeneratedMesh;
			}
			m_currentRibbon.OnRibbonTypeChanged += OnRibbonTypeChanged;
		}

		// 060005a2 (1.10.1 RVA 0x1A95240)
		private GameObject StartGeneratingMesh()
		{
			m_editableRibbon.OnRibbonKnotsUpdated += UpdateGeneratedMesh;
			if (m_meshVisualisation == null)
				UpdateGeneratedMesh();
			return m_meshVisualisation;
		}

		// 060005a3 (1.10.1 RVA 0x1A95380)
		private void UpdateGeneratedMesh()
		{
			m_meshVisualisation = GenerateMesh(m_meshVisualisation, m_editableRibbon);
			m_storeGeneratedMeshTime = DateTime.Now + TimeSpan.FromSeconds(RibbonEditorDefinition.Default.GeneratedMeshesSaveDelaySeconds);
		}

		// 060005a4 (1.10.1 RVA 0x1A96EA0): the pending save time clears once it has passed.
		private void StoreMesh()
		{
			if (!m_storeGeneratedMeshTime.HasValue || DateTime.Now < m_storeGeneratedMeshTime.Value || m_editableRibbon == null)
				return;
			StoreGeneratedMesh(m_meshVisualisation.GetComponent<MeshFilter>().sharedMesh);
			m_storeGeneratedMeshTime = null;
		}

		// 060005a5 (1.10.1 RVA 0x1A95490): per step, a right quad then a left quad hang off the
		// centre line. u is the side's distance from the centre over -6 (left) or 6 (right);
		// v advances by the centre chord length over 6.
		private GameObject GenerateMesh(GameObject visualisation, EditableRibbon editableRibbon)
		{
			string meshName = GetMeshName(ref visualisation);
			MeshFilter meshFilter;
			if (visualisation == null)
			{
				visualisation = new GameObject(meshName);
				meshFilter = visualisation.AddComponent<MeshFilter>();
				MeshRenderer meshRenderer = visualisation.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = RibbonEditorDefinition.Default.VisualisationMaterial;
			}
			else
			{
				meshFilter = visualisation.GetComponent<MeshFilter>();
			}

			Transform parent = editableRibbon.transform;
			Transform visualisationTransform = visualisation.transform;
			visualisationTransform.parent = parent;
			visualisationTransform.localPosition = Vector3.zero;
			visualisationTransform.localRotation = Quaternion.identity;
			visualisationTransform.localScale = Vector3.one;

			m_meshVerts.Clear();
			m_meshTriangles.Clear();
			m_meshUVs.Clear();

			int quality = RibbonEditorDefinition.Default.GeneratedMeshQuality;
			float step = 1f / quality;
			float v = 0f;
			for (int knotIndex = 0; knotIndex < editableRibbon.Knots.Count - 1; ++knotIndex)
			{
				for (int i = 0; i < quality; ++i)
				{
					float t0 = i * step;
					float t1 = (i + 1) * step;
					Vector3 centre0 = editableRibbon.GetLocalPosFromKnotT(new KnotT(knotIndex, t0), RibbonAlignment.Center);
					Vector3 centre1 = editableRibbon.GetLocalPosFromKnotT(new KnotT(knotIndex, t1), RibbonAlignment.Center);
					Vector3 left0 = editableRibbon.GetLocalPosFromKnotT(new KnotT(knotIndex, t0), RibbonAlignment.Left);
					Vector3 right1 = editableRibbon.GetLocalPosFromKnotT(new KnotT(knotIndex, t1), RibbonAlignment.Right);
					Vector3 right0 = editableRibbon.GetLocalPosFromKnotT(new KnotT(knotIndex, t0), RibbonAlignment.Right);
					Vector3 left1 = editableRibbon.GetLocalPosFromKnotT(new KnotT(knotIndex, t1), RibbonAlignment.Left);

					float uLeft0 = Vector3.Distance(centre0, left0) / -6f;
					float uRight0 = Vector3.Distance(centre0, right0) / 6f;
					float uLeft1 = Vector3.Distance(centre1, left1) / -6f;
					float uRight1 = Vector3.Distance(centre1, right1) / 6f;
					float vNext = v + Vector3.Distance(centre1, centre0) / 6f;

					m_meshVerts.Add(centre0);
					int centre0Index = m_meshVerts.Count - 1;
					m_meshTriangles.Add(centre0Index);
					m_meshUVs.Add(new Vector2(0f, v));

					m_meshVerts.Add(right1);
					int right1Index = m_meshVerts.Count - 1;
					m_meshTriangles.Add(right1Index);
					m_meshUVs.Add(new Vector2(uRight1, vNext));

					m_meshVerts.Add(right0);
					m_meshTriangles.Add(m_meshVerts.Count - 1);
					m_meshUVs.Add(new Vector2(uRight0, v));

					m_meshTriangles.Add(centre0Index);
					m_meshVerts.Add(centre1);
					int centre1Index = m_meshVerts.Count - 1;
					m_meshUVs.Add(new Vector2(0f, vNext));
					m_meshTriangles.Add(centre1Index);
					m_meshTriangles.Add(right1Index);

					m_meshVerts.Add(left0);
					int left0Index = m_meshVerts.Count - 1;
					m_meshTriangles.Add(left0Index);
					m_meshUVs.Add(new Vector2(uLeft0, v));

					m_meshVerts.Add(centre1);
					int centre1LeftIndex = m_meshVerts.Count - 1;
					m_meshTriangles.Add(centre1LeftIndex);
					m_meshUVs.Add(new Vector2(0f, vNext));

					m_meshVerts.Add(centre0);
					m_meshTriangles.Add(m_meshVerts.Count - 1);
					m_meshUVs.Add(new Vector2(0f, v));

					m_meshTriangles.Add(left0Index);
					m_meshVerts.Add(left1);
					int left1Index = m_meshVerts.Count - 1;
					m_meshUVs.Add(new Vector2(uLeft1, vNext));
					m_meshTriangles.Add(left1Index);
					m_meshTriangles.Add(centre1LeftIndex);

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
			return visualisation;
		}

		// 060005a6 (1.10.1 RVA 0x1A96FD0): named after this object; the argument is not read.
		private string GetMeshName(ref GameObject visualisation)
		{
			return gameObject.name + "[Visualisation]";
		}

		// 060005a7 (1.10.1 RVA 0x1A96FC0): the retail body is empty.
		private void StoreGeneratedMesh(Mesh mesh)
		{
		}

		// 060005a8 (1.10.1 RVA 0x1A97030)
		public RibbonVisualisation()
		{
		}
	}
}
