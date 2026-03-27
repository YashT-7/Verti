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

        // 3. PHASE 3: NOISE SCORING 
        AirspaceScanner.AirspaceData bestPath = validCandidates[0];
        float bestNoiseScore = float.MaxValue; 

        foreach (var candidate in validCandidates)
        {
            // Evaluate noise from the FATO1 position
            float currentNoiseScore = noiseScanner.EvaluateNoiseImpact(candidate.angle, scanCenter);

            if (currentNoiseScore < bestNoiseScore)
            {
                bestNoiseScore = currentNoiseScore;
                bestPath = candidate;
            }
        }

        // 4. PACKAGE RESULTS
        FinalRecommendation finalResult = new FinalRecommendation {
            heading = bestPath.angle,
            clearDistance = bestPath.clearDistance,
            isSafeAirspace = isPerfectlySafe,
            noiseScore = bestNoiseScore
        };

        if (isPerfectlySafe) {
            Debug.Log($"<color=cyan>Optimal Path Found! Heading {finalResult.heading}°. (Airspace: Clear | Noise Score: {finalResult.noiseScore})</color>");
        } else {
            Debug.Log($"<color=yellow>Warning: No fully clear paths. Best compromise: {finalResult.heading}°. (Distance: {finalResult.clearDistance}m | Noise Score: {finalResult.noiseScore})</color>");
        }

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
}