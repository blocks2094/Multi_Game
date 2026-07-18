using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider))]
public class ObjectOutline : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    [SerializeField, Min(0.001f)] private float lineWidth = 0.015f;
    [SerializeField, Min(0f)] private float outlinePadding = 0.005f;

    private readonly LineRenderer[] outlineLines = new LineRenderer[12];

    private static readonly int[,] EdgeIndices =
    {
        { 0, 1 }, { 1, 3 }, { 3, 2 }, { 2, 0 },
        { 4, 5 }, { 5, 7 }, { 7, 6 }, { 6, 4 },
        { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
    };

    private BoxCollider boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();

        CreateOutlineLines();
        UpdateOutlinePositions();
        SetVisible(false);
    }

    public void SetVisible(bool isVisible)
    {
        foreach (LineRenderer outlineLine in outlineLines)
        {
            if (outlineLine != null)
            {
                outlineLine.enabled = isVisible;
            }
        }
    }

    private void CreateOutlineLines()
    {
        if (outlineMaterial == null)
        {
            Debug.LogWarning($"{name}의 Outline Material이 설정되지 않았습니다.", this);
            return;
        }

        for (int i = 0; i < outlineLines.Length; i++)
        {
            GameObject lineObject = new GameObject($"OutlineEdge_{i + 1}");
            lineObject.transform.SetParent(transform, false);
            lineObject.layer = gameObject.layer;

            LineRenderer outlineLine = lineObject.AddComponent<LineRenderer>();
            outlineLine.useWorldSpace = false;
            outlineLine.positionCount = 2;
            outlineLine.sharedMaterial = outlineMaterial;
            outlineLine.startWidth = lineWidth;
            outlineLine.endWidth = lineWidth;
            outlineLine.numCapVertices = 2;
            outlineLine.shadowCastingMode = ShadowCastingMode.Off;
            outlineLine.receiveShadows = false;

            outlineLines[i] = outlineLine;
        }
    }

    private void UpdateOutlinePositions()
    {
        Vector3 center = boxCollider.center;
        Vector3 halfSize = boxCollider.size * 0.5f + Vector3.one * outlinePadding;

        Vector3[] corners =
        {
            center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3( halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3(-halfSize.x,  halfSize.y, -halfSize.z),
            center + new Vector3( halfSize.x,  halfSize.y, -halfSize.z),

            center + new Vector3(-halfSize.x, -halfSize.y,  halfSize.z),
            center + new Vector3( halfSize.x, -halfSize.y,  halfSize.z),
            center + new Vector3(-halfSize.x,  halfSize.y,  halfSize.z),
            center + new Vector3( halfSize.x,  halfSize.y,  halfSize.z)
        };

        for (int i = 0; i < outlineLines.Length; i++)
        {
            if (outlineLines[i] == null)
            {
                continue;
            }

            outlineLines[i].SetPosition(0, corners[EdgeIndices[i, 0]]);
            outlineLines[i].SetPosition(1, corners[EdgeIndices[i, 1]]);
        }
    }
}