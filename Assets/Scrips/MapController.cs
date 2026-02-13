using UnityEngine;
using UnityEngine.UI;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class MapController : MonoBehaviour
{
    [Header("Setup References")]
    public AbstractMap map;
    public InputField latInput;
    public InputField lonInput;
    public Text heightResultText;

    [Header("UI Selection")]
    public TMP_Dropdown targetDropdown; // Drag your UI Dropdown here

    [Header("Settings")]
    public Color ringColor = Color.red;
    private float divisor = 2.58f;
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
        DrawVisualBoundaries(150f / divisor, 250f / divisor);

        yield return new WaitForSeconds(4f);

        // Run the logic that now handles UI selection and activation
        float areaMaxHeight = ProcessBuildingsWithDynamicVerti();

        heightResultText.text = areaMaxHeight > 0
            ? $"250m Area Max Height: {areaMaxHeight:F1}m\n(Selection: {targetDropdown.options[targetDropdown.value].text})"
            : "No buildings found in scan radius.";
    }

    private float ProcessBuildingsWithDynamicVerti()
    {
        float highestPoint = 0f;
        float scanRadius = 250f / divisor;

        // 1. Get Selected Name from Dropdown
        string selectedName = targetDropdown.options[targetDropdown.value].text;

        // 2. Find and Activate the object (even if hidden)
        GameObject targetObj = FindHiddenObjectByName(selectedName);
        Collider targetCollider = null;

        if (targetObj != null)
        {
            targetObj.SetActive(true); // Ensure it is active for physics
            targetCollider = targetObj.GetComponent<Collider>();
        }
        else
        {
            Debug.LogWarning($"Target '{selectedName}' not found. Height scan only.");
            return PerformHeightScanOnly(scanRadius);
        }

        BoxCollider[] buildingColliders = GameObject.FindObjectsOfType<BoxCollider>();

        foreach (var col in buildingColliders)
        {
            if (col == null) continue;
            if (targetCollider != null && col == targetCollider) continue;

            bool isBuilding = col.gameObject.name.ToLower().Contains("building");
            if (!isBuilding && col.transform.parent != null)
            {
                if (col.transform.parent.name.Contains("/")) isBuilding = true;
            }

            if (isBuilding)
            {
                bool wasRemoved = false;
                if (targetCollider != null && targetCollider.enabled)
                {
                    try
                    {
                        if (targetCollider.bounds.Intersects(col.bounds))
                        {
                            col.gameObject.SetActive(false);
                            wasRemoved = true;
                        }
                    }
                    catch { /* Physics data warming up */ }
                }

                if (wasRemoved) continue;

                float distance = Vector3.Distance(Vector3.zero, col.bounds.center);
                if (distance <= scanRadius)
                {
                    float h = col.bounds.size.y;
                    if (h > highestPoint) highestPoint = h;
                }
            }
        }
        return highestPoint;
    }

    // --- HELPER METHODS ---

    // Finds objects even if they are inactive (SetActive(false))
    private GameObject FindHiddenObjectByName(string name)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name == name && obj.hideFlags == HideFlags.None)
            {
                return obj;
            }
        }
        return null;
    }

    // Fixed the CS0103 error by providing the missing method
    private float PerformHeightScanOnly(float radius)
    {
        float highest = 0f;
        BoxCollider[] buildingColliders = GameObject.FindObjectsOfType<BoxCollider>();
        foreach (var col in buildingColliders)
        {
            if (col == null) continue;
            if (col.gameObject.name.ToLower().Contains("building") || (col.transform.parent != null && col.transform.parent.name.Contains("/")))
            {
                float dist = Vector3.Distance(Vector3.zero, col.bounds.center);
                if (dist <= radius)
                {
                    if (col.bounds.size.y > highest) highest = col.bounds.size.y;
                }
            }
        }
        return highest;
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