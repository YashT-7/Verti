using UnityEngine;
using System.Collections.Generic;

public class NoiseScanner : MonoBehaviour
{
    public enum BuildingType { Hospital, School, Library, Residential }

    [System.Serializable]
    public struct SensitiveBuilding
    {
        public BuildingType type;
        public Vector3 position;
        public float maxAllowedDB; // Legal limit
    }

    [Header("VTOL Acoustic Profile")]
    [Tooltip("Reference noise level in dBA (e.g., 85 dBA)")]
    public float referenceNoiseDB = 85f;
    [Tooltip("Distance at which reference noise was measured (meters)")]
    public float referenceDistance = 15f;
    [Tooltip("Atmospheric absorption factor (dB per meter)")]
    public float atmosphericAbsorption = 0.005f; 

    [Header("Flight Path Parameters")]
    [Tooltip("How far out to evaluate the noise impact (meters)")]
    public float evaluationDistance = 2000f; // Evaluate 2km out
    [Tooltip("EASA takeoff climb slope (12.5% = 0.125)")]
    public float climbSlope = 0.125f;

    // This list will be populated by your Mapbox API script
    private List<SensitiveBuilding> mapboxBuildings = new List<SensitiveBuilding>();

    /// <summary>
    /// Call this from your Mapbox script when loading POIs
    /// </summary>
    public void AddSensitiveBuilding(Vector3 worldPos, BuildingType type)
    {
        float limit = 60f; // Default
        switch (type)
        {
            case BuildingType.Hospital: limit = 50f; break; // Strictest
            case BuildingType.School: limit = 55f; break;
            case BuildingType.Library: limit = 55f; break;
            case BuildingType.Residential: limit = 60f; break;
        }

        mapboxBuildings.Add(new SensitiveBuilding { 
            position = worldPos, 
            type = type, 
            maxAllowedDB = limit 
        });
    }

    public void ClearBuildings() => mapboxBuildings.Clear();

    /// <summary>
    /// Evaluates the total noise penalty for a given takeoff heading.
    /// A score of 0 means perfect compliance. Higher score = worse noise violation.
    /// </summary>
    public float EvaluateNoiseImpact(float angle, Vector3 vertiportPosition, float mapDivisor = 1f)
    {
        if (mapboxBuildings.Count == 0) return 0f;

        float totalPenaltyScore = 0f;

        // 1. Define the flight path segment
        Quaternion rot = Quaternion.Euler(0, angle, 0);
        Vector3 flatForward = rot * Vector3.forward;
        Vector3 flightPathDirection = (flatForward + (Vector3.up * climbSlope)).normalized;
        
        Vector3 flightPathStart = vertiportPosition;
        Vector3 flightPathEnd = vertiportPosition + (flightPathDirection * (evaluationDistance / mapDivisor));

        // 2. Evaluate every sensitive building
        foreach (var building in mapboxBuildings)
        {
            // A. Find the Closest Point of Approach (CPA) on the flight path
            Vector3 closestPointOnPath = ClosestPointOnLineSegment(flightPathStart, flightPathEnd, building.position);

            // B. Calculate physical distance in meters (apply your Mapbox divisor)
            float slantDistance = Vector3.Distance(closestPointOnPath, building.position) * mapDivisor;

            // C. Avoid infinite physics at zero distance
            if (slantDistance < referenceDistance) slantDistance = referenceDistance;

            // D. Calculate Acoustic Propagation (Inverse Square + Atmospheric)
            float decibelsAtBuilding = referenceNoiseDB 
                                     - (20f * Mathf.Log10(slantDistance / referenceDistance)) 
                                     - (atmosphericAbsorption * (slantDistance - referenceDistance));

            // E. Calculate Penalty
            if (decibelsAtBuilding > building.maxAllowedDB)
            {
                // Exponential penalty for exceeding limits (e.g., 5dB over is much worse than 1dB over)
                float exceedance = decibelsAtBuilding - building.maxAllowedDB;
                totalPenaltyScore += Mathf.Pow(exceedance, 2); 
            }
        }

        return totalPenaltyScore;
    }

    // --- Math Helper ---
    
    // Calculates the closest point on a finite line segment to a specific point
    private Vector3 ClosestPointOnLineSegment(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();

        float projectLength = Vector3.Dot(point - lineStart, lineDirection);

        // Clamp to ensure we only evaluate the segment, not infinite space behind/ahead of the vertiport
        if (projectLength < 0f) return lineStart;
        if (projectLength > lineLength) return lineEnd;

        return lineStart + lineDirection * projectLength;
    }
}
