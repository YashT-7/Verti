using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralSafetyCone : MonoBehaviour
{
    [Header("Dynamic Parameters")]
    public float h1_CylinderHeight = 3f;  // Base vertical section
    
    public int segments = 60;

    [Header("Outer Extension (Brim)")]
    public float extensionRadius = 100f;
    public float extensionAngle = 12.5f;

    [Header("Read-Only Data")]
    public float autoBottomDiameter;
    public float calculatedSlopeAngle;

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;

        // Slot 0: Cylinder, Slot 1: Cone, Slot 2: Brim
        if (meshRenderer.sharedMaterials.Length < 3)
        {
            meshRenderer.sharedMaterials = new Material[3];
        }
    }

    public void UpdateConeHeight(float totalHeight, Vector3 centerPos, float bottomRadius, List<float> blockedAngles)
    {
        if (totalHeight <= 0)
        {
            mesh.Clear();
            return;
        }

        transform.position = centerPos;
        this.autoBottomDiameter = bottomRadius * 2f;

        // Pass the blocked list to the generator
        GenerateMultiMaterialCone(totalHeight, blockedAngles);
    }

    void GenerateMultiMaterialCone(float totalHeight, List<float> blockedAngles)
    {
        mesh.Clear();
        mesh.subMeshCount = 3;

        float r1 = autoBottomDiameter / 2f;
        float r2 = r1;
        //float r3 = topDiameter / 2f;
        float r3 = (autoBottomDiameter * 5f) / 2f;
        float r4 = extensionRadius;

        float h1 = h1_CylinderHeight;
        float h2 = totalHeight;
        float h3 = h2 + ((r4 - r3) * Mathf.Tan(extensionAngle * Mathf.Deg2Rad));

        int s = segments + 1;
        Vector3[] vertices = new Vector3[s * 8];
        Vector2[] uvs = new Vector2[s * 8];

        List<int> cylinderTris = new List<int>();
        List<int> coneTris = new List<int>();
        List<int> brimTris = new List<int>();

        for (int i = 0; i <= segments; i++)
        {
            // 1. Calculate the angle (0 to 360)
            float angle = (float)i / segments * 360f;

            // 2. Adjust for Unity's Coordinate System:
            // We swap Sin/Cos and use degrees to radians.
            // This makes 0 degrees point at Forward (Z+) and rotate Clockwise.
            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad); // Swapped Cos for Sin
            float z = Mathf.Cos(rad); // Swapped Sin for Cos
            float u = (float)i / segments;

            // Standard Vertex setup (stays the same)
            vertices[i] = new Vector3(x * r1, 0, z * r1);
            vertices[i + s] = new Vector3(x * r2, h1, z * r2);
            vertices[i + s * 2] = new Vector3(x * r3, h2, z * r3);
            vertices[i + s * 3] = new Vector3(x * r4, h3, z * r4);

            // Double-sided vertices
            for (int j = 0; j < 4; j++) vertices[i + s * (4 + j)] = vertices[i + s * j];

            uvs[i] = new Vector2(u, 0); uvs[i + s] = new Vector2(u, 0.33f);
            uvs[i + s * 2] = new Vector2(u, 0.66f); uvs[i + s * 3] = new Vector2(u, 1f);

            if (i < segments)
            {
                // --- THE CUT LOGIC ---
                // Check if the current angle is blocked
                // We check if any angle in the blocked list is within the current segment step
                bool isBlocked = blockedAngles.Exists(a => Mathf.Abs(a - angle) < (360f / segments) / 2f);

                if (!isBlocked)
                {
                    int rA = i, rB = i + s, rC = i + s * 2, rD = i + s * 3;
                    int iA = rA + s * 4, iB = rB + s * 4, iC = rC + s * 4, iD = rD + s * 4;

                    AddQuad(cylinderTris, rA, rB, rA + 1, rB + 1, false);
                    AddQuad(cylinderTris, iA, iB, iA + 1, iB + 1, true);

                    AddQuad(coneTris, rB, rC, rB + 1, rC + 1, false);
                    AddQuad(coneTris, iB, iC, iB + 1, iC + 1, true);

                    AddQuad(brimTris, rC, rD, rC + 1, rD + 1, false);
                    AddQuad(brimTris, iC, iD, iC + 1, iD + 1, true);
                }
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.SetTriangles(cylinderTris, 0);
        mesh.SetTriangles(coneTris, 1);
        mesh.SetTriangles(brimTris, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void AddQuad(List<int> tris, int bL, int tL, int bR, int tR, bool flip)
    {
        if (!flip)
        {
            tris.Add(bL); tris.Add(tL); tris.Add(bR);
            tris.Add(tL); tris.Add(tR); tris.Add(bR);
        }
        else
        {
            tris.Add(bL); tris.Add(bR); tris.Add(tL);
            tris.Add(tL); tris.Add(bR); tris.Add(tR);
        }
    }
}