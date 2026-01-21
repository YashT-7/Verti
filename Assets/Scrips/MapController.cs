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
        // Draw visual boundaries
        DrawVisualBoundaries(150f / divisor, 250f / divisor);

        // Wait for Mapbox geometry to fully spawn
        yield return new WaitForSeconds(4f);

        // LOCAL SCAN: Hide center buildings and calculate ring height using colliders
        float ringMaxHeight = CleanAndEstimateHeight();

        // Update UI with local scan results
        if (ringMaxHeight > 0)
            heightResultText.text = $"Ring Max Height: {ringMaxHeight:F1}m\n(300m Center Cleared)";
        else
            heightResultText.text = "No buildings found in Scan Zone.";
    }

    private float CleanAndEstimateHeight()
    {
        float highestPoint = 0f;
        int removedCount = 0;

        float innerUnityRadius = 150f / divisor;
        float outerUnityRadius = 250f / divisor;

        BoxCollider[] buildingColliders = GameObject.FindObjectsOfType<BoxCollider>();

        foreach (var col in buildingColliders)
        {
            if (col.gameObject.name.ToLower().Contains("building") || col.transform.parent.name.Contains("/"))
            {
                // Circular detection logic
                Vector3 closestPoint = col.ClosestPoint(Vector3.zero);
                float distance = Vector3.Distance(Vector3.zero, closestPoint);

                // 1. CLEAR: Inside 300m diameter
                if (distance < innerUnityRadius)
                {
                    col.gameObject.SetActive(false);
                    removedCount++;
                }
                // 2. SCAN: Between 300m and 500m diameters
                else if (distance >= innerUnityRadius && distance <= outerUnityRadius)
                {
                    float h = col.bounds.size.y;
                    if (h > highestPoint) highestPoint = h;
                }
            }
        }

        Debug.Log($"Local Scan Complete. Scale: {map.WorldRelativeScale}");
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