using UnityEngine;
using System.Collections.Generic;

public class AirspaceScanner : MonoBehaviour
{
    [Header("EASA Settings")]
    public float dValue = 12f;
    public float slopeRatio = 0.125f; 
    public float scanRangeRealWorld = 1000f; 
    public LayerMask obstacleLayer;
    public int scanStepSize = 5; 

    // Data structure to hold the scan results for a single angle
    public struct AirspaceData 
    {
        public float angle;
        public float clearDistance;
        public float distanceRatio; 
        public bool isClear;
    }

    /// <summary>
    /// Performs the 360-degree sweep and returns the safety data for every angle.
    /// </summary>
    public List<AirspaceData> ScanAirspace(float divisor, Vector3 startPosition, float clearanceHeight)
    {
        List<AirspaceData> results = new List<AirspaceData>();

        float scaledRange = scanRangeRealWorld / divisor;
        float halfWidth = (1.5f * dValue / divisor) / 2f;
        Vector3 boxHalfExtents = new Vector3(halfWidth, 5f / divisor, halfWidth); 
        
        // CHANGE: Elevate the start position completely above h2 (clearanceHeight)
        Vector3 startPos = startPosition + (Vector3.up * clearanceHeight); 

        for (int angle = 0; angle < 360; angle += scanStepSize) 
        {
            Quaternion rot = Quaternion.Euler(0, angle, 0);
            Vector3 slopeDir = (rot * Vector3.forward + (Vector3.up * slopeRatio)).normalized;

            RaycastHit hit;
            bool hitSomething = Physics.BoxCast(
                startPos, boxHalfExtents, slopeDir, out hit, rot, scaledRange, obstacleLayer
            );

            float actualDistance = hitSomething ? hit.distance : scaledRange;

            results.Add(new AirspaceData { 
                angle = angle, 
                clearDistance = actualDistance,
                distanceRatio = actualDistance / scaledRange,
                isClear = !hitSomething
            });
        }
        return results;
    }
    

}