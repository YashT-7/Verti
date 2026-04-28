using UnityEngine;
using UnityEngine.UI;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement; // Required for restarting


public class MapController : MonoBehaviour
{
    [Header("Setup References")]
    public AbstractMap map;
    public VertiportDirector director;
    public InputField latInput;
    public InputField lonInput;
    public Text heightResultText;

    [Header("UI Containers")]
    public GameObject controlsContainer; // Drag the parent of your UI elements here

    [Header("Restart UI")]
    public GameObject restartButtonObj; // Drag your new Restart Button here

    [Header("Instruction UI")]
    public GameObject instructionPanel;
    public GameObject infoButton;

    [Header("UI Selection")]
    public TMP_Dropdown targetDropdown; // Drag your UI Dropdown here
    public ProceduralSafetyCone coneScript;

    [Header("Settings")]
    public Color ringColor = Color.red;
    private float divisor = 2.58f;
    private GameObject visualContainer;

    [Header("New Orientation Settings")]
    public InputField orientationInput; // Drag your new Orientation Input Field here

    void Start()
    {
        // 1. Show instructions immediately when the tool starts
        ShowInstructions(true);
    }

    public void ShowInstructions(bool show)
    {
        if (instructionPanel != null) instructionPanel.SetActive(show);

        // 2. If the panel is open, hide the small 'i' button. 
        // If the panel is closed, show the 'i' button in the corner.
        if (infoButton != null) infoButton.SetActive(!show);
    }

    public void OnClickLoadMap()
    {
        // Use System.Globalization to ensure dots are always treated as decimals
        if (double.TryParse(latInput.text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
            double.TryParse(lonInput.text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
        {
            SetUIState(false);

            // 1. Rotate the Vertiport BEFORE the scan starts
            ApplyVertiportRotation();

            // 1. Create the coordinate object
            Vector2d center = new Vector2d(lat, lon);

            // 2. Set the center via the official method
            map.SetCenterLatitudeLongitude(center);

            // 3. FORCE the string property (this fixes the "Wrong number of arguments" bug)
            // We use InvariantCulture to ensure it's "12.34, 56.78" and not "12,34, 56,78"
            map.Options.locationOptions.latitudeLongitude = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}, {1}", lat, lon);

            // 4. Trigger the visual update
            map.UpdateMap();

            StopAllCoroutines();
            StartCoroutine(BufferedCleanupAndScan());
        }
        else
        {
            Debug.LogError("Input Parse Failed! Check if you used a comma instead of a dot.");
        }
    }

    private void SetUIState(bool state)
    {
        if (controlsContainer != null)
        {
            controlsContainer.SetActive(state); // true = Visible, false = Hidden
        }
    }

    private void ApplyVertiportRotation()
    {
        if (orientationInput != null && !string.IsNullOrEmpty(orientationInput.text))
        {
            if (float.TryParse(orientationInput.text, out float targetAngle))
            {
                string selectedName = targetDropdown.options[targetDropdown.value].text;
                GameObject targetObj = FindHiddenObjectByName(selectedName);

                if (targetObj != null)
                {
                    // 1. Get the current rotation in Euler angles
                    Vector3 currentRotation = targetObj.transform.rotation.eulerAngles;

                    // 2. Apply ONLY the new Y (North-based heading) 
                    // while keeping the original X and Z
                    targetObj.transform.rotation = Quaternion.Euler(currentRotation.x, targetAngle, currentRotation.z);

                    Debug.Log($"Rotated {selectedName} to Y:{targetAngle}° (Preserved X:{currentRotation.x} Z:{currentRotation.z})");
                }
            }
        }
    }

    private IEnumerator BufferedCleanupAndScan()
    {
        // 1. Hide the entire UI block immediately
        SetUIState(false);

        // Ensure the restart button is hidden while processing
        if (restartButtonObj != null) restartButtonObj.SetActive(false);

        //DrawVisualBoundaries(150f / divisor, 250f / divisor);

        // 2. Wait for Mapbox geometry and physics to fully initialize
        yield return new WaitForSeconds(4f);

        float areaMaxHeight = ProcessBuildingsWithDynamicVerti();

        // 3. Apply safety logic: h2 must be at least 20m
        float finalConeHeight = Mathf.Max(20f, areaMaxHeight);

        if (director != null)
        {
            director.RunDirectionScan(divisor, (result) => {

                // UI Update reflecting chosen height and scan findings
                string heightStatus = areaMaxHeight > 15f ? "Building Restricted" : "Default Minimum";

                // ADDED: Latitude and Longitude from the input fields
                heightResultText.text = $"Area Scan Complete.\n" +
                                      $"Location: {latInput.text}, {lonInput.text}\n" + // <--- Added this line
                                      $"Max Building: {areaMaxHeight:F1}m\n" +
                                      $"Cone Height: {finalConeHeight:F1}m ({heightStatus})";

                heightResultText.text += result.isSafeAirspace
                    ? $"\nPath Clear! Recommended: {result.heading:F1}°"
                    : $"\nRestricted! Best Path: {result.heading:F1}°";

                if (coneScript != null)
                {
                    string selectedName = targetDropdown.options[targetDropdown.value].text;
                    GameObject targetObj = FindHiddenObjectByName(selectedName);

                    if (targetObj != null)
                    {
                        Transform fatoTransform = targetObj.transform.Find("FATO1");
                        float fatoRadius = 0.5f;
                        Vector3 fatoPos = targetObj.transform.position;

                        if (fatoTransform != null)
                        {
                            fatoPos = fatoTransform.position;
                            fatoPos.y += fatoTransform.lossyScale.y;
                            fatoRadius = fatoTransform.lossyScale.x / 2f;
                        }

                        coneScript.UpdateConeHeight(finalConeHeight, fatoPos, fatoRadius, result.blockedHeadings);
                    }
                }

                // 4. THE KEY CHANGE: Only show the restart button. 
                // The main UI (SetUIState) stays FALSE so the user can't click Generate again.
                if (restartButtonObj != null) restartButtonObj.SetActive(true);
            });
        }
        else
        {
            // If director fails, we show everything so the user can try again
            SetUIState(true);
        }
    }

    // This method will be linked to your Restart Button
    public void OnClickRestart()
    {
        // Reloads the currently active scene from scratch
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
    /*
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
    }*/
}