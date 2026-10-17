using UnityEngine;

// Original HLUnityUI.Runtime RectTransformModifier (global namespace, TypeDefIndex
// 11834), methods 0x0600002b..0x06000062; bodies translated from the 1.10.1 x86-64
// binary. Each property setter inlines its Set* helper, except set_LocalScaleZ,
// which tail-jumps to SetScaleZ (0x1b3b725). The size, position and anchor helpers
// compare through GetTransform() but then write through m_transform directly; the
// offset, pivot and scale helpers go through GetTransform() for every access.
// No shipped asset in the reference export references this script (original
// script GUID b7658853df549da736df76930c660ddc), so the .meta GUID is fresh and no
// binding rule is added.
[RequireComponent(typeof(RectTransform))]
public class RectTransformModifier : MonoBehaviour
{
	private RectTransform m_transform;

	public float Height
	{
		get { return GetTransform().sizeDelta.y; }
		set { SetHeightAndAlterTransform(value); }
	}

	public float Width
	{
		get { return GetTransform().sizeDelta.x; }
		set { SetWidthAndAlterTransform(value); }
	}

	public float XOffset
	{
		get { return GetTransform().anchoredPosition.x; }
		set { SetXOffsetAndAlterTransform(value); }
	}

	public float YOffset
	{
		get { return GetTransform().anchoredPosition.y; }
		set { SetYOffsetAndAlterTransform(value); }
	}

	public float XAnchorMin
	{
		get { return GetTransform().anchorMin.x; }
		set { SetXAnchorMinAndAlterTransform(value); }
	}

	public float XAnchorMax
	{
		get { return GetTransform().anchorMax.x; }
		set { SetXAnchorMaxAndAlterTransform(value); }
	}

	public float YAnchorMin
	{
		get { return GetTransform().anchorMin.y; }
		set { SetYAnchorMinAndAlterTransform(value); }
	}

	public float YAnchorMax
	{
		get { return GetTransform().anchorMax.y; }
		set { SetYAnchorMaxAndAlterTransform(value); }
	}

	public float LeftOffset
	{
		get { return GetTransform().offsetMin.x; }
		set { SetLeftOffsetAndAlterTransform(value); }
	}

	public float RightOffset
	{
		get { return -GetTransform().offsetMax.x; }
		set { SetRightOffsetAndAlterTransform(value); }
	}

	public float BotOffset
	{
		get { return GetTransform().offsetMin.y; }
		set { SetBotOffsetAndAlterTransform(value); }
	}

	public float TopOffset
	{
		// Not negated, unlike RightOffset; as shipped (0x06000041).
		get { return GetTransform().offsetMax.y; }
		set { SetTopOffsetAndAlterTransform(value); }
	}

	public float PivotX
	{
		get { return GetTransform().pivot.x; }
		set { SetPivotXAndAlterTransform(value); }
	}

	public float PivotY
	{
		get { return GetTransform().pivot.y; }
		set { SetPivotYAndAlterTransform(value); }
	}

	public float LocalScaleX
	{
		get { return GetTransform().localScale.x; }
		set { SetScaleX(value); }
	}

	public float LocalScaleY
	{
		get { return GetTransform().localScale.y; }
		set { SetScaleY(value); }
	}

	public float LocalScaleZ
	{
		get { return GetTransform().localScale.z; }
		set { SetScaleZ(value); }
	}

	public RectTransform RectTransform
	{
		get { return GetTransform(); }
		private set { m_transform = value; }
	}

	// 0x0600004f
	private void Awake()
	{
		GetTransform();
	}

	// 0x06000050: lazily caches transform as RectTransform (Unity null test).
	private RectTransform GetTransform()
	{
		if (m_transform == null)
		{
			m_transform = transform as RectTransform;
		}
		return m_transform;
	}

	private void SetHeightAndAlterTransform(float newHeight)
	{
		if (GetTransform().sizeDelta.y == newHeight) return;
		m_transform.sizeDelta = new Vector2(m_transform.sizeDelta.x, newHeight);
	}

	private void SetWidthAndAlterTransform(float newWidth)
	{
		if (GetTransform().sizeDelta.x == newWidth) return;
		m_transform.sizeDelta = new Vector2(newWidth, m_transform.sizeDelta.y);
	}

	private void SetXOffsetAndAlterTransform(float newXOffset)
	{
		if (GetTransform().anchoredPosition.x == newXOffset) return;
		m_transform.anchoredPosition = new Vector2(newXOffset, m_transform.anchoredPosition.y);
	}

	private void SetYOffsetAndAlterTransform(float newYOffset)
	{
		if (GetTransform().anchoredPosition.y == newYOffset) return;
		m_transform.anchoredPosition = new Vector2(m_transform.anchoredPosition.x, newYOffset);
	}

	private void SetXAnchorMinAndAlterTransform(float newAnchorMin)
	{
		if (GetTransform().anchorMin.x == newAnchorMin) return;
		m_transform.anchorMin = new Vector2(newAnchorMin, m_transform.anchorMin.y);
	}

	private void SetXAnchorMaxAndAlterTransform(float newAnchorMax)
	{
		if (GetTransform().anchorMax.x == newAnchorMax) return;
		m_transform.anchorMax = new Vector2(newAnchorMax, m_transform.anchorMax.y);
	}

	private void SetYAnchorMinAndAlterTransform(float newAnchorMin)
	{
		if (GetTransform().anchorMin.y == newAnchorMin) return;
		m_transform.anchorMin = new Vector2(m_transform.anchorMin.x, newAnchorMin);
	}

	private void SetYAnchorMaxAndAlterTransform(float newAnchorMax)
	{
		if (GetTransform().anchorMax.y == newAnchorMax) return;
		m_transform.anchorMax = new Vector2(m_transform.anchorMax.x, newAnchorMax);
	}

	private void SetLeftOffsetAndAlterTransform(float newLeftOffset)
	{
		if (GetTransform().offsetMin.x == newLeftOffset) return;
		GetTransform().offsetMin = new Vector2(newLeftOffset, GetTransform().offsetMin.y);
	}

	// As shipped: compares the raw offsetMax.x with the un-negated value but stores
	// the negation (no xorps before ucomiss, 0x0600005a), so assigning the negation
	// of the current offsetMax.x is skipped.
	private void SetRightOffsetAndAlterTransform(float newRightOffset)
	{
		if (GetTransform().offsetMax.x == newRightOffset) return;
		GetTransform().offsetMax = new Vector2(-newRightOffset, GetTransform().offsetMax.y);
	}

	private void SetBotOffsetAndAlterTransform(float newBotOffset)
	{
		if (GetTransform().offsetMin.y == newBotOffset) return;
		GetTransform().offsetMin = new Vector2(GetTransform().offsetMin.x, newBotOffset);
	}

	// Same quirk as SetRightOffsetAndAlterTransform: ucomiss on the raw offsetMax.y
	// (0x1b3b18e), xorps only on the store (0x1b3b1c9, 0x0600005c).
	private void SetTopOffsetAndAlterTransform(float newTopOffset)
	{
		if (GetTransform().offsetMax.y == newTopOffset) return;
		GetTransform().offsetMax = new Vector2(GetTransform().offsetMax.x, -newTopOffset);
	}

	private void SetPivotXAndAlterTransform(float newPivotX)
	{
		if (GetTransform().pivot.x == newPivotX) return;
		GetTransform().pivot = new Vector2(newPivotX, GetTransform().pivot.y);
	}

	private void SetPivotYAndAlterTransform(float newPivotY)
	{
		if (GetTransform().pivot.y == newPivotY) return;
		GetTransform().pivot = new Vector2(GetTransform().pivot.x, newPivotY);
	}

	// Scale helpers read localScale once, then fetch the transform again to write it.
	private void SetScaleX(float newScale)
	{
		if (GetTransform().localScale.x == newScale) return;
		Vector3 scale = GetTransform().localScale;
		GetTransform().localScale = new Vector3(newScale, scale.y, scale.z);
	}

	private void SetScaleY(float newScale)
	{
		if (GetTransform().localScale.y == newScale) return;
		Vector3 scale = GetTransform().localScale;
		GetTransform().localScale = new Vector3(scale.x, newScale, scale.z);
	}

	private void SetScaleZ(float newScale)
	{
		if (GetTransform().localScale.z == newScale) return;
		Vector3 scale = GetTransform().localScale;
		GetTransform().localScale = new Vector3(scale.x, scale.y, newScale);
	}
}
