using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralSafetyCone : MonoBehaviour
{
    [Header("Dynamic Parameters")]
    public float topDiameter = 11.63f;     // The only manual width you set
    public int segments = 60;

    [Header("Outer Extension (Brim)")]
    public float extensionRadius = 100f;
    public float extensionAngle = 5f;

    [Header("Read-Only Data")]
    public float autoBottomDiameter;    // Detected from FATO1
    public float calculatedSlopeAngle;  // Resulting slope

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;

        if (meshRenderer.sharedMaterials.Length < 2)
        {
            meshRenderer.sharedMaterials = new Material[2];
        }
    }

    // MapController calls this and passes the radius found from FATO1
    public void UpdateConeHeight(float maxHeight, Vector3 centerPos, float bottomRadius)
    {
        if (maxHeight <= 0)
        {
            mesh.Clear();
            return;
        }

        transform.position = centerPos;

        // AUTO-ASSIGN: Use the actual FATO1 geometry
        this.autoBottomDiameter = bottomRadius * 2f;

        GenerateMultiMaterialCone(maxHeight);
    }

    void GenerateMultiMaterialCone(float height)
    {
        mesh.Clear();
        mesh.subMeshCount = 2;

        float r1 = autoBottomDiameter / 2f;
        float r2 = topDiameter / 2f;

        // Calculate resulting slope angle: θ = arctan((r2 - r1) / height)
        calculatedSlopeAngle = Mathf.Atan((r2 - r1) / height) * Mathf.Rad2Deg;

        float horizontalDist = extensionRadius - r2;
        float r3Height = height + (horizontalDist * Mathf.Tan(extensionAngle * Mathf.Deg2Rad));

        int vertexCount = (segments + 1) * 6;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];

        List<int> coneTriangles = new List<int>();
        List<int> brimTriangles = new List<int>();

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            float u = (float)i / segments;

            // Define points using auto-detected bottom and manual top
            vertices[i] = new Vector3(x * r1, 0, z * r1);
            vertices[i + (segments + 1)] = new Vector3(x * r2, height, z * r2);
            vertices[i + (segments + 1) * 2] = new Vector3(x * extensionRadius, r3Height, z * extensionRadius);

            // Double-sided vertices
            vertices[i + (segments + 1) * 3] = vertices[i];
            vertices[i + (segments + 1) * 4] = vertices[i + (segments + 1)];
            vertices[i + (segments + 1) * 5] = vertices[i + (segments + 1) * 2];

            uvs[i] = new Vector2(u, 0);
            uvs[i + (segments + 1)] = new Vector2(u, 0.5f);
            uvs[i + (segments + 1) * 2] = new Vector2(u, 1);

            if (i < segments)
            {
                int b = i;
                int m = i + segments + 1;
                int o = i + (segments + 1) * 2;
                int bi = b + (segments + 1) * 3;
                int mi = m + (segments + 1) * 3;
                int oi = o + (segments + 1) * 3;

                // Submesh 0 (Cone)
                coneTriangles.Add(b); coneTriangles.Add(m); coneTriangles.Add(b + 1);
                coneTriangles.Add(m); coneTriangles.Add(m + 1); coneTriangles.Add(b + 1);
                coneTriangles.Add(bi); coneTriangles.Add(bi + 1); coneTriangles.Add(mi);
                coneTriangles.Add(mi); coneTriangles.Add(bi + 1); coneTriangles.Add(mi + 1);

                // Submesh 1 (Brim)
                brimTriangles.Add(m); brimTriangles.Add(o); brimTriangles.Add(m + 1);
                brimTriangles.Add(o); brimTriangles.Add(o + 1); brimTriangles.Add(m + 1);
                brimTriangles.Add(mi); brimTriangles.Add(mi + 1); brimTriangles.Add(oi);
                brimTriangles.Add(oi); brimTriangles.Add(mi + 1); brimTriangles.Add(oi + 1);
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.SetTriangles(coneTriangles, 0);
        mesh.SetTriangles(brimTriangles, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}