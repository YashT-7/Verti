using UnityEngine;
using UnityEngine.UI; // Added for Dropdown
using TMPro; // Added for TMP_Dropdown
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

[RequireComponent(typeof(AirspaceScanner))]
[RequireComponent(typeof(NoiseScanner))]
public class VertiportDirector : MonoBehaviour
{
    [Header("Scanner References")]
    private AirspaceScanner airspaceScanner;
    private NoiseScanner noiseScanner;

    [Header("Dynamic Positioning")]
    public TMP_Dropdown targetDropdown; // Added to match MapController selection

    [Header("Visualization")]
    [Tooltip("Drag your 3D Arrow Prefab here.")]
    public GameObject hoverArrowPrefab; 
    [Tooltip("How high above the vertiport should the arrow hover?")]
    public float hoverHeightRealWorld = 20f; 
    public Color safeColor = Color.green;
    public Color midColor = Color.yellow;
    public Color dangerColor = Color.red;
    public Color optimalColor = Color.cyan;

    private GameObject mainArrowContainer;
    private GameObject radarFanContainer;

    public struct FinalRecommendation
    {
        public float heading;
        public float clearDistance;
        public bool isSafeAirspace;
        public float noiseScore;
        public List<float> blockedHeadings;
    }

    void Awake()
    {
        airspaceScanner = GetComponent<AirspaceScanner>();
        noiseScanner = GetComponent<NoiseScanner>();
    }

    public void RunDirectionScan(float divisor, float clearanceHeight, Action<FinalRecommendation> onResult)
    {
        StartCoroutine(ScanRoutine(divisor, clearanceHeight, onResult));
    }

    private IEnumerator ScanRoutine(float divisor, float clearanceHeight, Action<FinalRecommendation> onResult)
    {
        yield return new WaitForFixedUpdate();

        // --- DYNAMIC SEARCH LOGIC ---
        // Mirroring MapController: find the object from dropdown, then find FATO1 child
        Transform originTransform = transform; // Default fallback

        if (targetDropdown != null)
        {
            string selectedName = targetDropdown.options[targetDropdown.value].text;
            GameObject targetObj = FindHiddenObjectByName(selectedName);

            if (targetObj != null)
            {
                Transform fatoChild = targetObj.transform.Find("FATO1");
                if (fatoChild != null)
                {
                    originTransform = fatoChild;
                }
            }
        }

        Vector3 scanCenter = originTransform.position;

        // 1. PHASE 1: AIRSPACE FILTER
        List<AirspaceScanner.AirspaceData> airspaceData = airspaceScanner.ScanAirspace(divisor, scanCenter, clearanceHeight);

        // 2. PHASE 2: FILTERING CANDIDATES
        List<AirspaceScanner.AirspaceData> validCandidates = new List<AirspaceScanner.AirspaceData>();
        bool isPerfectlySafe = false;

        var perfectlyClearPaths = airspaceData.Where(x => x.isClear).ToList();

        if (perfectlyClearPaths.Count > 0)
        {
            validCandidates = perfectlyClearPaths;
            isPerfectlySafe = true;
        }
        else
        {
            float maxDistanceFound = airspaceData.Max(x => x.clearDistance);
            validCandidates = airspaceData.Where(x => x.clearDistance >= maxDistanceFound - 0.1f).ToList();
            isPerfectlySafe = false;
        }

        // 3. PHASE 3: NOISE SCORING
        float bestNoiseScore = float.MaxValue;
        foreach (var candidate in validCandidates)
        {
            float currentNoiseScore = noiseScanner.EvaluateNoiseImpact(candidate.angle, scanCenter, divisor);
            if (currentNoiseScore < bestNoiseScore)
            {
                bestNoiseScore = currentNoiseScore;
            }
        }

        float noiseTolerance = 1.0f;
        List<float> optimalAngles = new List<float>();

        foreach (var candidate in validCandidates)
        {
            float score = noiseScanner.EvaluateNoiseImpact(candidate.angle, scanCenter);
            if (score <= bestNoiseScore + noiseTolerance)
            {
                optimalAngles.Add(candidate.angle);
            }
        }

        List<float> blockedMask = airspaceData
            .Where(data => !data.isClear)
            .Select(data => data.angle)
            .ToList();

        float finalRecommendedHeading = GetMeanOfLargestSector(optimalAngles);

        // 4. PACKAGE RESULTS
        FinalRecommendation finalResult = new FinalRecommendation
        {
            heading = finalRecommendedHeading,
            clearDistance = validCandidates[0].clearDistance,
            isSafeAirspace = isPerfectlySafe,
            noiseScore = bestNoiseScore,
            blockedHeadings = blockedMask
        };

        // 5. VISUALIZE (Passing the originTransform found dynamically)
        DrawRadarFan(airspaceData, divisor, originTransform);
        DrawMainArrow(finalResult.heading, divisor, finalResult.isSafeAirspace, originTransform);

        if (onResult != null) onResult(finalResult);
    }

    // Helper to find targets even if inactive (same as MapController)
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

    // --- VISUALIZERS ---
    private void DrawRadarFan(List<AirspaceScanner.AirspaceData> points, float divisor, Transform origin)
    {
        if (radarFanContainer != null) Destroy(radarFanContainer);
        radarFanContainer = new GameObject("Visual_RadarFan");
        radarFanContainer.transform.SetParent(transform, false);

        float visualLength = (airspaceScanner.dValue * 1.5f) / divisor;

        // CALCULATE TOP SURFACE OFFSET
        // lossyScale.y is the total height of the cylinder. We move up by half.
        float topSurfaceOffset = origin.lossyScale.y / 2f;

        foreach (var point in points)
        {
            GameObject rayObj = new GameObject($"Ray_{point.angle}");
            rayObj.transform.SetParent(radarFanContainer.transform, false);
            LineRenderer lr = rayObj.AddComponent<LineRenderer>();

            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = 0.5f / divisor;
            lr.endWidth = 0.5f / divisor;
            lr.material = new Material(Shader.Find("Sprites/Default"));

            Color rayColor = point.distanceRatio < 0.5f
                ? Color.Lerp(dangerColor, midColor, point.distanceRatio * 2f)
                : Color.Lerp(midColor, safeColor, (point.distanceRatio - 0.5f) * 2f);

            lr.startColor = rayColor;
            lr.endColor = rayColor;

            // Adjust startPos to the TOP surface
            Vector3 startPos = origin.position + (Vector3.up * topSurfaceOffset);

            Quaternion rot = Quaternion.Euler(0, point.angle, 0);
            Vector3 endPos = startPos + (rot * Vector3.forward * visualLength);

            lr.SetPosition(0, startPos);
            lr.SetPosition(1, endPos);
        }
    }

    private void DrawMainArrow(float angle, float divisor, bool isIdeal, Transform origin) 
    {
        // 1. Clean up the old arrow if it exists
        if (mainArrowContainer != null) Destroy(mainArrowContainer);

        // 2. Fallback check: If you forgot to assign a prefab, don't crash.
        if (hoverArrowPrefab == null)
        {
            Debug.LogError("No Arrow Prefab assigned in VertiportDirector!");
            return;
        }

        // 3. Spawn the 3D Arrow
        mainArrowContainer = Instantiate(hoverArrowPrefab);
        mainArrowContainer.name = "Visual_HoverArrow";
        mainArrowContainer.transform.SetParent(transform, true);

        // 4. Position it: Hovering straight up from the FATO
        Vector3 hoverPos = origin.position + (Vector3.up * (hoverHeightRealWorld / divisor));
        mainArrowContainer.transform.position = hoverPos;

        // 5. Rotate it: Flat with the ground, pointing at the correct heading
        mainArrowContainer.transform.rotation = Quaternion.Euler(0, angle, 0);

        // 6. Scale it: Adjust size based on your map divisor
        float scaleVisual = 10f / divisor; // Adjust '10f' to make the base arrow bigger/smaller
        mainArrowContainer.transform.localScale = new Vector3(scaleVisual, scaleVisual, scaleVisual);

        // 7. Color it: Apply our safe/warning colors to the 3D model's materials
        Color finalColor = isIdeal ? optimalColor : midColor;
        
        // This loops through the arrow model and colors all its parts
        Renderer[] renderers = mainArrowContainer.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            // Note: Your prefab needs a material that uses the Standard shader with a settable _Color
            if (r.material.HasProperty("_Color")) 
            {
                r.material.color = finalColor;
            }
        }
    }


    // --- CIRCULAR MEAN HELPERS ---
    private float GetMeanOfLargestSector(List<float> optimalAngles)
    {
        if (optimalAngles.Count == 0) return 0f;
        if (optimalAngles.Count == 1) return optimalAngles[0];

        optimalAngles.Sort();
        List<List<float>> clusters = new List<List<float>>();
        List<float> currentCluster = new List<float>();

        int stepSize = airspaceScanner.scanStepSize;

        for (int i = 0; i < optimalAngles.Count; i++)
        {
            if (currentCluster.Count == 0 || Mathf.Approximately(optimalAngles[i], currentCluster.Last() + stepSize))
            {
                currentCluster.Add(optimalAngles[i]);
            }
            else
            {
                clusters.Add(new List<float>(currentCluster));
                currentCluster.Clear();
                currentCluster.Add(optimalAngles[i]);
            }
        }
        if (currentCluster.Count > 0) clusters.Add(currentCluster);

        if (clusters.Count > 1)
        {
            var first = clusters.First();
            var last = clusters.Last();
            if (Mathf.Approximately(first.First(), 0f) && Mathf.Approximately(last.Last(), 360f - stepSize))
            {
                last.AddRange(first);
                clusters.RemoveAt(0);
            }
        }

        var largestCluster = clusters.OrderByDescending(c => c.Count).First();
        return CalculateCircularMean(largestCluster);
    }

    private float CalculateCircularMean(List<float> angles)
    {
        float sumSin = 0f;
        float sumCos = 0f;

        foreach (float angle in angles)
        {
            float rad = angle * Mathf.Deg2Rad;
            sumSin += Mathf.Sin(rad);
            sumCos += Mathf.Cos(rad);
        }

        float meanRad = Mathf.Atan2(sumSin / angles.Count, sumCos / angles.Count);
        float meanDeg = meanRad * Mathf.Rad2Deg;

        if (meanDeg < 0) meanDeg += 360f;
        return meanDeg;
    }
}