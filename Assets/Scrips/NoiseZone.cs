using UnityEngine;

public class NoiseZones : MonoBehaviour
{
    [Header("Acoustic Raycast Settings")]
    [Tooltip("How far out should the drone check for noise zones? (In Unity Units)")]
    public float scanDistance = 3000f; 
    
    [Tooltip("CRITICAL: Assign your 'NoiseZones' layer here so it ignores buildings!")]
    public LayerMask noiseLayer;

    [Header("Visualization")]
    public bool showDebugLasers = true;

    /// <summary>
    /// Shoots a raycast to find all TA Lärm noise zones in this flight path.
    /// Returns a penalty score. Lower score = Quieter, better flight path.
    /// </summary>
    public float EvaluateNoiseImpact(float angle, Vector3 origin)
    {
        float totalPenalty = 0f;
        
        // Replicate the Volocopter climb trajectory (12.5% slope)
        Quaternion rot = Quaternion.Euler(0, angle, 0);
        Vector3 direction = (rot * Vector3.forward + (Vector3.up * 0.125f)).normalized;

        // Shoot a laser through the city that ONLY hits objects on the 'noiseLayer'
        // We use RaycastAll because a flight path might cross multiple overlapping bubbles!
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, scanDistance, noiseLayer);

        foreach (RaycastHit hit in hits)
        {
            NoiseZone zone = hit.collider.GetComponent<NoiseZone>();
            if (zone != null)
            {
                // MATH: The stricter the dB limit, the higher the penalty.
                // Hospital (45 dB) = 55 Penalty points.
                // Industrial (70 dB) = 30 Penalty points.
                float zoneLimit = zone.GetDaytimeNoiseLimit();
                float strictnessPenalty = 100f - zoneLimit;

                // Physics: Hitting a bubble closer to the vertiport is worse than grazing one 2km away.
                float distanceFactor = 1f - (hit.distance / scanDistance);
                
                totalPenalty += strictnessPenalty * distanceFactor;
            }
        }

        // EDGE CASE: What if the Vertiport is built directly inside a bubble?
        // The Raycast won't hit the "wall" of the bubble because it starts inside it.
        // We use an OverlapSphere to check if we are currently standing inside one.
        Collider[] insideZones = Physics.OverlapSphere(origin, 2f, noiseLayer);
        foreach (Collider col in insideZones)
        {
            NoiseZone zone = col.GetComponent<NoiseZone>();
            if (zone != null)
            {
                // Massive flat penalty for starting inside a restricted zone
                totalPenalty += (100f - zone.GetDaytimeNoiseLimit()) * 2f; 
            }
        }

        // Draw visual lasers in the Scene view while playing
        if (showDebugLasers)
        {
            Color laserColor = totalPenalty > 0 ? new Color(1, 0, 0, 0.2f) : new Color(0, 1, 1, 0.2f);
            Debug.DrawRay(origin, direction * (scanDistance / 4f), laserColor, 2f);
        }

        return totalPenalty; 
    }
}
