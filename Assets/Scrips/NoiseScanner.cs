using UnityEngine;

public class NoiseScanner : MonoBehaviour
{
    [Header("Noise Settings")]
    [Tooltip("Maximum acceptable noise footprint in dB. Above this is heavily penalized.")]
    public float maxAcceptableDecibels = 65f;

    /// <summary>
    /// Evaluates the noise impact for a specific takeoff heading.
    /// Lower score is better (e.g., lower dB impact on populated areas).
    /// </summary>
    public float EvaluateNoiseImpact(float angle, Vector3 vertiportPosition)
    {
        // TODO: Replace this with your actual Mapbox/Noise logic!
        
        // --- MOCK LOGIC FOR TESTING ---
        // Let's pretend North (0/360) is a noisy residential area (bad score), 
        // and South (180) is an industrial park/water body (good score).
        
        // This generates a fake curve where 180 is the lowest value (best) and 0 is the highest (worst)
        float mockNoiseImpact = Mathf.Abs(180f - angle); 
        
        // Return the mock impact. Lower is better!
        return mockNoiseImpact; 
    }
}
