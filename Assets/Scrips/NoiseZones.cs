using UnityEngine;

public enum ZoneType
{
    Schools_Hospitals,   // 45 dB(A)
    Purely_Residential,  // 50 dB(A)
    General_Residential, // 55 dB(A)
    Village_Mixed,       // 60 dB(A)
    Urban_POIs,          // 63 dB(A)
    Industrial           // 70 dB(A)
}

public class NoiseZone : MonoBehaviour
{
    [Header("Zone Settings")]
    public ZoneType zoneType = ZoneType.Schools_Hospitals;

    public float GetDaytimeNoiseLimit()
    {
        switch (zoneType)
        {
            case ZoneType.Schools_Hospitals: return 45f;
            case ZoneType.Purely_Residential: return 50f;
            case ZoneType.General_Residential: return 55f;
            case ZoneType.Village_Mixed: return 60f;
            case ZoneType.Urban_POIs: return 63f;
            case ZoneType.Industrial: return 70f;
            default: return 75f;
        }
    }

    // Volocopter Physics: Safe Distance Radii based on 65dB at 100m
    public float GetZoneRadius()
    {
        switch (zoneType)
        {
            case ZoneType.Schools_Hospitals:   return 1000f;
            case ZoneType.Purely_Residential:  return 562f;
            case ZoneType.General_Residential: return 316f;
            case ZoneType.Village_Mixed:       return 177f;
            case ZoneType.Urban_POIs:          return 125f;
            case ZoneType.Industrial:          return 56f;
            default:                           return 100f;
        }
    }

    // Shrink the bubble mathematically to match Mapbox's tiny scale
    void Start()
    {
        SphereCollider sphereCol = GetComponent<SphereCollider>();
        if (sphereCol != null)
        {
            float mapScaleFactor = transform.lossyScale.x; 
            sphereCol.radius = GetZoneRadius() / mapScaleFactor; 
        }
    }

 // Draw the colored bubbles in the Editor
    void OnDrawGizmos()
    {
        // 1. Strict Zones (RED)
        if (zoneType == ZoneType.Schools_Hospitals || zoneType == ZoneType.Industrial)
            Gizmos.color = new Color(1, 0, 0, 0.4f); 

        // 2. Medium-Strict Zones (ORANGE) - Split this out!
        else if (zoneType == ZoneType.Purely_Residential)
            Gizmos.color = new Color(1, 0.5f, 0, 0.4f); 

        // 3. Medium-Relaxed Zones (YELLOW)
        else if (zoneType == ZoneType.General_Residential)
            Gizmos.color = new Color(1, 0.92f, 0.016f, 0.4f); 

        // 4. Relaxed Zones (GREEN)
        else
            Gizmos.color = new Color(0, 1, 0, 0.3f); 

        float mapScaleFactor = transform.lossyScale.x;
        float scaledRadius = GetZoneRadius() / mapScaleFactor;
        
        Gizmos.DrawWireSphere(transform.position, scaledRadius);
    }
}    

    