using UnityEngine;
using UnityEngine.UI;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Collections;

public class MapController : MonoBehaviour
{
    [Header("Setup References")]
    public AbstractMap map;
    public InputField latInput;
    public InputField lonInput;
    public Text heightResultText;

    [Header("Settings")]
    public Color ringColor = Color.red;
    private float divisor = 2.58f; // Your custom scale divisor
    private GameObject visualContainer;

    public void OnClickLoadMap()
    {
        if (double.TryParse(latInput.text, out double lat) && double.TryParse(lonInput.text, out double lon))
        {
            map.SetCenterLatitudeLongitude(new Vector2d(lat, lon));
            map.UpdateMap();

            StopAllCoroutines();
            StartCoroutine(BufferedCleanupAndScan());
        }
    }

    private IEnumerator BufferedCleanupAndScan()
    {
        // Keep boundaries for visual context
        DrawVisualBoundaries(150f / divisor, 250f / divisor);

        // Wait for Mapbox geometry to fully spawn
        yield return new WaitForSeconds(4f);

        // Execute single-pass logic
        float areaMaxHeight = ProcessBuildingsWithVerti();

        // Update UI
        heightResultText.text = areaMaxHeight > 0
            ? $"250m Area Max Height: {areaMaxHeight:F1}m\n(Verti Area Cleared)"
            : "No buildings found in scan radius.";
    }

    private float ProcessBuildingsWithVerti()
    {
        float highestPoint = 0f;
        int removedCount = 0;
        float scanRadius = 250f / divisor; // Full 250m circular scan

        // 1. Locate the "verti" object and its collider
        GameObject vertiObj = GameObject.Find("Cube");
        Collider vertiCollider = vertiObj != null ? vertiObj.GetComponent<Collider>() : null;

        if (vertiCollider == null)
        {
            Debug.LogError("Verti object or Collider not found! Hiding logic skipped.");
        }

        BoxCollider[] buildingColliders = GameObject.FindObjectsOfType<BoxCollider>();

        foreach (var col in buildingColliders)
        {
            // Standard building filter
            if (col.gameObject.name.ToLower().Contains("building") || col.transform.parent.name.Contains("/"))
            {
                // Priority 1: Check for intersection with "verti"
                if (vertiCollider != null && vertiCollider.bounds.Intersects(col.bounds))
                {
                    col.gameObject.SetActive(false); // Remove from scene
                    removedCount++;
                    continue; // SKIP height measurement for this building
                }

                // Priority 2: Measure height if within 250m radius
                Vector3 closestPoint = col.ClosestPoint(Vector3.zero);
                float distance = Vector3.Distance(Vector3.zero, closestPoint);

                if (distance <= scanRadius)
                {
                    float h = col.bounds.size.y;
                    if (h > highestPoint) highestPoint = h;
                }
            }
        }

        Debug.Log($"Verti Cleanup: {removedCount} buildings hidden. Highest active building: {highestPoint}m");
        return highestPoint;
    }

    private void DrawVisualBoundaries(float rad1, float rad2)
    {
        if (visualContainer != null) Destroy(visualContainer);
        visualContainer = new GameObject("VisualBoundaries");
        CreateLine(rad1, "300m_Boundary");
        CreateLine(rad2, "500m_Boundary");
    }

    private void CreateLine(float radius, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.parent = visualContainer.transform;
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 50;
        lr.startWidth = 2f;
        lr.endWidth = 2f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = ringColor;
        lr.endColor = ringColor;
        for (int i = 0; i < 50; i++)
        {
            float angle = i * Mathf.PI * 2 / 49;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 1.5f, Mathf.Sin(angle) * radius));
        }
    }
}