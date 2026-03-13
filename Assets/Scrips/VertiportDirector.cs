using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class VertiportDirector : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugGizmos = true;
    
    [Header("EASA Settings")]
    public float dValue = 12f;
    public float slopeRatio = 0.125f; 
    public float scanRangeRealWorld = 1000f; 

    [Header("Scanner Config")]
    public LayerMask obstacleLayer;
    private int scanStepSize = 5; // Degrees per ray

    [Header("Visualization")]
    public float visualArrowLengthMultiplier = 4f; 
    public Color safeColor = Color.green;
    public Color midColor = Color.yellow;
    public Color dangerColor = Color.red;

    private GameObject mainArrowContainer;
    private GameObject radarFanContainer;

    public struct DirectionResult {
        public float heading;
        public float clearDistance;
        public bool isAllClear;
    }
    private struct ScanPoint {
        public float angle;
        public float distanceRatio; 
        public bool isClear;
    }

    public void RunDirectionScan(float divisor, Action<DirectionResult> onResult)
    {
        StartCoroutine(ScanRoutine(divisor, onResult));
    }

    private IEnumerator ScanRoutine(float divisor, Action<DirectionResult> onResult)
    {
        yield return new WaitForFixedUpdate();

        float scaledRange = scanRangeRealWorld / divisor;
        float halfWidth = (1.5f * dValue / divisor) / 2f;
        Vector3 boxHalfExtents = new Vector3(halfWidth, 5f / divisor, halfWidth); 
        Vector3 startPos = transform.position + (Vector3.up * (0.5f / divisor)); 

        float maxObstacleDist = -1f;
        float bestObstacleAngle = 0f;
        List<ScanPoint> allScanPoints = new List<ScanPoint>();
        int clearCount = 0;

        // 1. SWEEP 360 DEGREES
        for (int angle = 0; angle < 360; angle += scanStepSize) 
        {
            Quaternion rot = Quaternion.Euler(0, angle, 0);
            Vector3 slopeDir = (rot * Vector3.forward + (Vector3.up * slopeRatio)).normalized;

            RaycastHit hit;
            bool hitSomething = Physics.BoxCast(
                startPos, boxHalfExtents, slopeDir, out hit, rot, scaledRange, obstacleLayer
            );

            float actualDistance = hitSomething ? hit.distance : scaledRange;
            bool isCompletelyClear = !hitSomething;

            allScanPoints.Add(new ScanPoint { 
                angle = angle, 
                distanceRatio = actualDistance / scaledRange,
                isClear = isCompletelyClear
            });

            if (isCompletelyClear) clearCount++;
            else if (hit.distance > maxObstacleDist) {
                maxObstacleDist = hit.distance;
                bestObstacleAngle = angle;
            }
        }

        DirectionResult finalResult = new DirectionResult();

        // 2. ALGORITHM: Point AT the Mean of the Clear Sector
        if (clearCount == allScanPoints.Count)
        {
            // Case A: 100% Clear everywhere. Default to North (0)
            finalResult.isAllClear = true;
            finalResult.clearDistance = scaledRange;
            finalResult.heading = 0f; 
            Debug.Log($"<color=green>100% Clear. Defaulting to North: 0°</color>");
        }
        else if (clearCount > 0) 
        {
            // Case B: Partial obstacles. Find center of the Green Zone
            finalResult.isAllClear = true;
            finalResult.clearDistance = scaledRange;
            
            // This calculates the center of the largest continuous GREEN area.
            finalResult.heading = GetMeanOfLargestClearSector(allScanPoints);
            Debug.Log($"<color=green>Aiming away from obstacles! Center of clear sector: {finalResult.heading:F1}°</color>");
        } 
        else 
        {
            // Case C: Totally surrounded. Pick the longest distance available.
            finalResult.isAllClear = false;
            finalResult.clearDistance = maxObstacleDist;
            finalResult.heading = bestObstacleAngle;
            Debug.Log($"<color=yellow>Surrounded! Using best available gap: {finalResult.heading:F1}°</color>");
        }

        // 3. VISUALIZATION & OUTPUT
        DrawRadarFan(allScanPoints, divisor);
        DrawMainArrow(finalResult.heading, divisor, finalResult.isAllClear);

        if (onResult != null) onResult(finalResult);
    }

    // --- SECTOR CLUSTERING & CIRCULAR MEAN ---
    private float GetMeanOfLargestClearSector(List<ScanPoint> scanPoints)
    {
        List<List<float>> clusters = new List<List<float>>();
        List<float> currentCluster = new List<float>();

        // Step A: Group contiguous clear angles
        for (int i = 0; i < scanPoints.Count; i++)
        {
            if (scanPoints[i].isClear) {
                currentCluster.Add(scanPoints[i].angle);
            } else if (currentCluster.Count > 0) {
                clusters.Add(new List<float>(currentCluster));
                currentCluster.Clear();
            }
        }
        if (currentCluster.Count > 0) clusters.Add(currentCluster);

        if (clusters.Count == 0) return 0f;

        // Step B: Wrap-around Check (Does North connect 355° to 0°?)
        if (clusters.Count > 1)
        {
            var firstCluster = clusters.First();
            var lastCluster = clusters.Last();
            
            if (firstCluster.Contains(0f) && lastCluster.Contains(360f - scanStepSize))
            {
                lastCluster.AddRange(firstCluster);
                clusters.RemoveAt(0); // Merge first into last
            }
        }

        // Step C: Find the largest physical gap
        var largestCluster = clusters.OrderByDescending(c => c.Count).First();

        // Step D: Calculate true Circular Mean
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

    // --- VISUALIZERS ---
    private void DrawRadarFan(List<ScanPoint> points, float divisor) {
        if (radarFanContainer != null) Destroy(radarFanContainer);
        radarFanContainer = new GameObject("Visual_RadarFan");
        radarFanContainer.transform.SetParent(transform, false);
        radarFanContainer.transform.localPosition = Vector3.zero;

        float visualLength = (dValue * 1.5f) / divisor; 

        foreach (var point in points) {
            GameObject rayObj = new GameObject($"Ray_{point.angle}");
            rayObj.transform.SetParent(radarFanContainer.transform, false);
            LineRenderer lr = rayObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.startWidth = 0.5f / divisor; lr.endWidth = 0.5f / divisor;
            lr.material = new Material(Shader.Find("Sprites/Default"));

            Color rayColor = point.distanceRatio < 0.5f 
                ? Color.Lerp(dangerColor, midColor, point.distanceRatio * 2f)
                : Color.Lerp(midColor, safeColor, (point.distanceRatio - 0.5f) * 2f);
            
            lr.startColor = rayColor; lr.endColor = rayColor;
            
            Quaternion rot = Quaternion.Euler(0, point.angle, 0);
            lr.SetPosition(0, Vector3.up * (0.5f / divisor));
            lr.SetPosition(1, (Vector3.up * (0.5f / divisor)) + (rot * Vector3.forward * visualLength));
        }
    }

    private void DrawMainArrow(float angle, float divisor, bool isIdeal) {
        if (mainArrowContainer != null) Destroy(mainArrowContainer);
        mainArrowContainer = new GameObject("Visual_MainArrow");
        mainArrowContainer.transform.SetParent(transform, false);
        mainArrowContainer.transform.localPosition = Vector3.zero;

        LineRenderer lr = mainArrowContainer.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.startWidth = 3f / divisor * 2f; lr.endWidth = 0.1f / divisor;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        
        Color finalColor = isIdeal ? safeColor : midColor;
        lr.startColor = finalColor; lr.endColor = finalColor;

        Vector3 start = Vector3.up * (1f / divisor);
        Quaternion rot = Quaternion.Euler(0, angle, 0);
        Vector3 dir = (rot * Vector3.forward + (Vector3.up * slopeRatio)).normalized;
        float constrainedLength = (dValue / divisor) * visualArrowLengthMultiplier;
        lr.SetPosition(0, start);
        lr.SetPosition(1, start + (dir * constrainedLength)); 
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        float divisor = 2.58f; 
        float scaledRange = scanRangeRealWorld / divisor;
        float halfWidth = (1.5f * dValue / divisor) / 2f;
        Vector3 boxHalfExtents = new Vector3(halfWidth, 5f / divisor, halfWidth);
        Vector3 startPos = transform.position + (Vector3.up * (0.5f / divisor));

        Gizmos.color = new Color(1, 0, 0, 0.3f); 
        
        Quaternion rot = Quaternion.Euler(0, 0, 0); 
        Vector3 slopeDir = (rot * Vector3.forward + (Vector3.up * slopeRatio)).normalized;
        
        Gizmos.DrawLine(startPos, startPos + slopeDir * scaledRange);
        Gizmos.matrix = Matrix4x4.TRS(startPos, rot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2);
    }
}
