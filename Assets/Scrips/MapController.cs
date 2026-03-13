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
    public VertiportDirector director;
    public InputField latInput;
    public InputField lonInput;
    public Text heightResultText;

    [Header("UI Selection")]
    public TMP_Dropdown targetDropdown; // Drag your UI Dropdown here
    public ProceduralSafetyCone coneScript;

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
        // 1. Draw visual boundaries at 150m and 250m scaled
        DrawVisualBoundaries(150f / divisor, 250f / divisor);

        // 2. Wait for Mapbox geometry and physics to fully initialize
        yield return new WaitForSeconds(4f);

        // 3. Execute the single-pass building filter and height scan
        float areaMaxHeight = ProcessBuildingsWithDynamicVerti();

        if (director != null) 
        {
            // Note: 'result' is now an object containing .heading, .clearDistance, and .isAllClear
            director.RunDirectionScan(divisor, (result) => {
                
                Debug.Log("Recommended Heading: " + result.heading);
                
                if (result.isAllClear) {
                    heightResultText.text += $"\nPath Clear! Recommended: {result.heading}°";
                } else {
                    heightResultText.text += $"\nRestricted! Best Path: {result.heading}°";
                }

                // Apply Rotation
                // myVertiport.transform.rotation = Quaternion.Euler(0, result.heading, 0);
            });
        }

        // 4. Update the Procedural Cone dynamically
        if (coneScript != null)
        {
            string selectedName = targetDropdown.options[targetDropdown.value].text;
            GameObject targetObj = FindHiddenObjectByName(selectedName);

            if (targetObj != null)
            {
                // Find the child object named FATO1
                Transform fatoTransform = targetObj.transform.Find("FATO1");

                float fatoRadius = 0.5f; // Default radius
                Vector3 fatoPos = targetObj.transform.position; // Default to parent center

                if (fatoTransform != null)
                {
                    // Get the exact world position of the cylinder
                    fatoPos = fatoTransform.position;

                    // Calculate the Y offset to sit on top of the cylinder
                    // Standard Unity cylinders are 2 units tall, so scale.y is the total height.
                    // We move the position up by half that height.
                    float fatoHeightOffset = fatoTransform.lossyScale.y;
                    fatoPos.y += fatoHeightOffset;

                    // Calculate radius based on the cylinder's X scale
                    // Note: lossyScale ensures we get the correct scale regardless of parent scaling
                    fatoRadius = fatoTransform.lossyScale.x / 2f;
                }
                else
                {
                    Debug.LogWarning($"Child 'FATO1' not found in {selectedName}. Using parent center.");
                }

                // Trigger the cone with dynamic height, position, and radius
                coneScript.UpdateConeHeight(areaMaxHeight, fatoPos, fatoRadius);
            }
        }

        // 5. Update UI with the results
        heightResultText.text = areaMaxHeight > 0
            ? $"250m Area Max Height: {areaMaxHeight:F1}m\n(Location: {targetDropdown.options[targetDropdown.value].text})"
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