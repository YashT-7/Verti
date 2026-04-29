using System.Collections.Generic;
using UnityEngine;

public class VolocopterPathfinder : MonoBehaviour
{
    [Header("Cone Constraints")]
    public float coneHeight = 100f;
    public float coneAngleDegrees = 30f;
    public int verticalLayers = 10;
    public int pointsPerLayer = 8;

    [Header("Algorithm Weights")]
    public float distanceWeight = 1f;
    public float noisePenaltyWeight = 50f; // How hard it avoids noise

    [Header("Drone Acoustic Profile")]
    public float droneBaseDb = 65f; // dB at 100m
    public float referenceDistance = 100f;

    private List<Vector3> optimalPath = new List<Vector3>();

    // This runs automatically when you click your UI button
    public void GenerateOptimalPath()
    {
        optimalPath.Clear();
        NoiseZone[] allZones = FindObjectsOfType<NoiseZone>();
        
        // Step 1: Generate the 3D Graph inside the cone
        List<List<Node>> layeredGraph = GenerateConeGraph();

        // Step 2: Run Pathfinding (Dynamic Programming / Layered A*)
        optimalPath = FindCheapestPath(layeredGraph, allZones);
    }

    private List<List<Node>> GenerateConeGraph()
    {
        List<List<Node>> graph = new List<List<Node>>();
        Vector3 startPos = transform.position;

        // Base node (Vertiport)
        graph.Add(new List<Node> { new Node(startPos) });

        float layerHeightStep = coneHeight / verticalLayers;

        for (int i = 1; i <= verticalLayers; i++)
        {
            List<Node> currentLayer = new List<Node>();
            float currentHeight = i * layerHeightStep;
            float maxRadiusAtHeight = currentHeight * Mathf.Tan(coneAngleDegrees * Mathf.Deg2Rad);

            // Generate points in a circle at this layer
            currentLayer.Add(new Node(startPos + new Vector3(0, currentHeight, 0))); // Center point
            
            for (int r = 1; r <= 3; r++) // Concentric rings
            {
                float ringRadius = (maxRadiusAtHeight / 3f) * r;
                float angleStep = 360f / (pointsPerLayer * r);

                for (float angle = 0; angle < 360; angle += angleStep)
                {
                    float x = Mathf.Cos(angle * Mathf.Deg2Rad) * ringRadius;
                    float z = Mathf.Sin(angle * Mathf.Deg2Rad) * ringRadius;
                    currentLayer.Add(new Node(startPos + new Vector3(x, currentHeight, z)));
                }
            }
            graph.Add(currentLayer);
        }
        return graph;
    }

    private List<Vector3> FindCheapestPath(List<List<Node>> graph, NoiseZone[] zones)
    {
        // Initialize costs
        graph[0][0].costFromStart = 0;

        // Iterate layer by layer
        for (int i = 0; i < graph.Count - 1; i++)
        {
            foreach (Node currentNode in graph[i])
            {
                foreach (Node nextNode in graph[i + 1])
                {
                    float distance = Vector3.Distance(currentNode.position, nextNode.position);
                    float noisePenalty = CalculateNoisePenalty(nextNode.position, zones);
                    
                    float transitionCost = (distance * distanceWeight) + (noisePenalty * noisePenaltyWeight);
                    float totalCost = currentNode.costFromStart + transitionCost;

                    if (totalCost < nextNode.costFromStart)
                    {
                        nextNode.costFromStart = totalCost;
                        nextNode.parent = currentNode;
                    }
                }
            }
        }

        // Find the best node at the top layer
        Node bestTopNode = graph[graph.Count - 1][0];
        foreach (Node n in graph[graph.Count - 1])
        {
            if (n.costFromStart < bestTopNode.costFromStart)
                bestTopNode = n;
        }

        // Backtrack to get the path
        List<Vector3> path = new List<Vector3>();
        Node trace = bestTopNode;
        while (trace != null)
        {
            path.Add(trace.position);
            trace = trace.parent;
        }
        path.Reverse();
        return path;
    }

    private float CalculateNoisePenalty(Vector3 checkPosition, NoiseZone[] zones)
    {
        float totalPenalty = 0f;

        foreach (NoiseZone zone in zones)
        {
            float distanceToZone = Vector3.Distance(checkPosition, zone.transform.position);
            
            // Avoid divide by zero
            if (distanceToZone < 1f) distanceToZone = 1f;

            // Calculate drone dB at this distance using Inverse Square Law
            float droneDbAtDistance = droneBaseDb - 20f * Mathf.Log10(distanceToZone / referenceDistance);

            // Mocking the DB limit based on your zone colors (You can link this directly to your NoiseZone script)
            float zoneLimitDb = 60f; // Default Green
            if (zone.name.Contains("Red") || zone.name.Contains("Hospital")) zoneLimitDb = 45f;
            else if (zone.name.Contains("Orange")) zoneLimitDb = 50f;
            else if (zone.name.Contains("Yellow")) zoneLimitDb = 55f;

            if (droneDbAtDistance > zoneLimitDb)
            {
                totalPenalty += (droneDbAtDistance - zoneLimitDb); // Linear accumulation of violated dB
            }
        }
        return totalPenalty;
    }

    // Visualize the Cone and Path in the Unity Editor
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 1, 0.2f); // Faint Cyan for the cone
        Vector3 topCenter = transform.position + Vector3.up * coneHeight;
        float topRadius = coneHeight * Mathf.Tan(coneAngleDegrees * Mathf.Deg2Rad);
        Gizmos.DrawWireSphere(topCenter, topRadius);
        Gizmos.DrawLine(transform.position, topCenter + new Vector3(topRadius, 0, 0));
        Gizmos.DrawLine(transform.position, topCenter - new Vector3(topRadius, 0, 0));
        Gizmos.DrawLine(transform.position, topCenter + new Vector3(0, 0, topRadius));
        Gizmos.DrawLine(transform.position, topCenter - new Vector3(0, 0, topRadius));

        if (optimalPath != null && optimalPath.Count > 0)
        {
            Gizmos.color = Color.cyan; // Solid Cyan for the chosen path
            for (int i = 0; i < optimalPath.Count - 1; i++)
            {
                Gizmos.DrawLine(optimalPath[i], optimalPath[i + 1]);
                Gizmos.DrawSphere(optimalPath[i], 2f);
            }
            Gizmos.DrawSphere(optimalPath[optimalPath.Count - 1], 2f);
        }
    }

    // Helper class for the graph
    private class Node
    {
        public Vector3 position;
        public float costFromStart = float.MaxValue;
        public Node parent = null;

        public Node(Vector3 pos) { position = pos; }
    }
}