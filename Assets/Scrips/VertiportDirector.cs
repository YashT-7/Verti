using UnityEngine;
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

    [Header("Positioning")]
    [Tooltip("Drag your FATO1 GameObject here. If left empty, it will default to this object's center.")]
    public Transform visualOrigin; 

    [Header("Visualization")]
    public float visualArrowLengthMultiplier = 4f; 
    public Color safeColor = Color.green;
    public Color midColor = Color.yellow;
    public Color dangerColor = Color.red;
    public Color optimalColor = Color.cyan; 

    private GameObject mainArrowContainer;
    private GameObject radarFanContainer;

    public struct FinalRecommendation {
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

    public void RunDirectionScan(float divisor, Action<FinalRecommendation> onResult)
    {
        StartCoroutine(ScanRoutine(divisor, onResult));
    }

    private IEnumerator ScanRoutine(float divisor, Action<FinalRecommendation> onResult)
    {
        yield return new WaitForFixedUpdate();

        // Determine where the scan should start from (FATO1 if assigned, otherwise itself)
        Transform originTransform = visualOrigin != null ? visualOrigin : transform;
        Vector3 scanCenter = originTransform.position;

        // 1. PHASE 1: AIRSPACE FILTER
        // Pass the FATO1 position to the scanner
        List<AirspaceScanner.AirspaceData> airspaceData = airspaceScanner.ScanAirspace(divisor, scanCenter);
        
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

    // 3. PHASE 3: NOISE SCORING & CIRCULAR MEAN CENTERING
    float bestNoiseScore = float.MaxValue;
    
    // Step A: Find the absolute best noise score among safe paths
    foreach (var candidate in validCandidates)
    {
        float currentNoiseScore = noiseScanner.EvaluateNoiseImpact(candidate.angle, scanCenter);
        if (currentNoiseScore < bestNoiseScore)
        {
            bestNoiseScore = currentNoiseScore;
        }
    }

    // Step B: Collect ALL safe angles that are within an acceptable margin of the best score
    // (e.g., anything within 1.0 of the absolute best score is considered "Optimal")
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
    // Step C: Cluster and apply Circular Mean
    // This prevents averaging North and South and accidentally flying East.
    float finalRecommendedHeading = GetMeanOfLargestSector(optimalAngles);

    // 4. PACKAGE RESULTS
    FinalRecommendation finalResult = new FinalRecommendation {
        heading = finalRecommendedHeading,
        clearDistance = validCandidates[0].clearDistance, // Using distance of the safe group
        isSafeAirspace = isPerfectlySafe,
        noiseScore = bestNoiseScore,
        blockedHeadings = blockedMask
    };
        // 5. VISUALIZE (Passing the originTransform so arrows draw from FATO1)
        DrawRadarFan(airspaceData, divisor, originTransform);
        DrawMainArrow(finalResult.heading, divisor, finalResult.isSafeAirspace, originTransform);

        if (onResult != null) onResult(finalResult);
    }

    // --- VISUALIZERS ---
    private void DrawRadarFan(List<AirspaceScanner.AirspaceData> points, float divisor, Transform origin) 
    {
        if (radarFanContainer != null) Destroy(radarFanContainer);
        radarFanContainer = new GameObject("Visual_RadarFan");
        radarFanContainer.transform.SetParent(transform, false);

        float visualLength = (airspaceScanner.dValue * 1.5f) / divisor; 

        foreach (var point in points) 
        {
            GameObject rayObj = new GameObject($"Ray_{point.angle}");
            rayObj.transform.SetParent(radarFanContainer.transform, false);
            LineRenderer lr = rayObj.AddComponent<LineRenderer>();
            
            // USE WORLD SPACE: Prevents FATO1's scale from messing up arrow sizes
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
            
            // Calculate positions using FATO1's exact world position
            Vector3 startPos = origin.position + (Vector3.up * (0.5f / divisor));
            Quaternion rot = Quaternion.Euler(0, point.angle, 0);
            Vector3 endPos = startPos + (rot * Vector3.forward * visualLength);

            lr.SetPosition(0, startPos);
            lr.SetPosition(1, endPos);
        }
    }

    private void DrawMainArrow(float angle, float divisor, bool isIdeal, Transform origin) 
    {
        if (mainArrowContainer != null) Destroy(mainArrowContainer);
        mainArrowContainer = new GameObject("Visual_MainArrow");
        mainArrowContainer.transform.SetParent(transform, false);

        LineRenderer lr = mainArrowContainer.AddComponent<LineRenderer>();
        
        // USE WORLD SPACE
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = 3f / divisor * 2f; 
        lr.endWidth = 0.1f / divisor;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        
        Color finalColor = isIdeal ? optimalColor : midColor;
        lr.startColor = finalColor; 
        lr.endColor = finalColor;

        // Calculate positions using FATO1's exact world position
        Vector3 startPos = origin.position + (Vector3.up * (1f / divisor));
        Quaternion rot = Quaternion.Euler(0, angle, 0);
        Vector3 dir = (rot * Vector3.forward + (Vector3.up * airspaceScanner.slopeRatio)).normalized;
        float constrainedLength = (airspaceScanner.dValue / divisor) * visualArrowLengthMultiplier;
        
        lr.SetPosition(0, startPos);
        lr.SetPosition(1, startPos + (dir * constrainedLength)); 
    }

// --- CIRCULAR MEAN HELPERS ---
    
    private float GetMeanOfLargestSector(List<float> optimalAngles)
    {
        if (optimalAngles.Count == 1) return optimalAngles[0];

        // 1. Sort the angles
        optimalAngles.Sort();

        List<List<float>> clusters = new List<List<float>>();
        List<float> currentCluster = new List<float>();

        // 2. Group contiguous angles (assuming 5-degree step size from AirspaceScanner)
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

        // 3. Wrap-around Check (Connect 355° to 0°)
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

        // 4. Pick the widest contiguous sector of optimal angles
        var largestCluster = clusters.OrderByDescending(c => c.Count).First();

        // 5. Calculate true Circular Mean of that specific sector
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