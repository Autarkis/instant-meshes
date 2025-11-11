/*
    Example_BuildingMeshOptimizer.cs

    Example script demonstrating how to use Instant Meshes
    for optimizing building meshes in Unity.

    This is specifically tuned for architectural geometry
    where sharp corners and clean quad topology are important.
*/

using UnityEngine;
using InstantMeshes;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Example component for optimizing building meshes using Instant Meshes
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class BuildingMeshOptimizer : MonoBehaviour
{
    [Header("Optimization Settings")]
    [Tooltip("Target vertex count (0 = automatic based on input size)")]
    [Range(0, 50000)]
    public int targetVertexCount = 0;

    [Tooltip("Reduction ratio (only used if targetVertexCount = 0)")]
    [Range(2, 32)]
    public int reductionRatio = 8;

    [Header("Quality Settings")]
    [Tooltip("Crease angle for sharp edges (degrees, -1 = smooth everywhere)")]
    [Range(-1f, 90f)]
    public float creaseAngle = 30f;

    [Tooltip("Number of smoothing iterations")]
    [Range(0, 10)]
    public int smoothIterations = 2;

    [Header("Mesh Topology")]
    [Tooltip("Generate pure quad mesh (better for buildings)")]
    public bool pureQuadMesh = true;

    [Tooltip("Align mesh to boundaries (recommended for buildings)")]
    public bool alignToBoundaries = true;

    [Header("Advanced")]
    [Tooltip("Use deterministic algorithm (slower but reproducible)")]
    public bool deterministic = false;

    [Tooltip("Number of threads (-1 = automatic)")]
    public int numThreads = -1;

    [Header("Output")]
    [Tooltip("Replace current mesh with optimized version")]
    public bool replaceOriginal = false;

    [Tooltip("Save optimized mesh as asset (Editor only)")]
    public bool saveAsAsset = true;

    private MeshFilter meshFilter;
    private Mesh originalMesh;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        originalMesh = meshFilter.sharedMesh;
    }

    /// <summary>
    /// Optimize the building mesh
    /// </summary>
    public void OptimizeBuildingMesh()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshFilter.sharedMesh == null)
        {
            Debug.LogError("No mesh found on MeshFilter!");
            return;
        }

        Mesh inputMesh = meshFilter.sharedMesh;
        Debug.Log($"Optimizing building mesh: {inputMesh.name} " +
                  $"({inputMesh.vertexCount} vertices, {inputMesh.triangles.Length / 3} triangles)");

        // Determine target vertex count
        int targetCount = targetVertexCount;
        if (targetCount <= 0)
        {
            targetCount = inputMesh.vertexCount / reductionRatio;
            targetCount = Mathf.Max(targetCount, 100); // Minimum 100 vertices
        }

        // Configure parameters for building meshes
        var parameters = new InstantMeshesAPI.OptimizationParams
        {
            // Size control
            TargetVertexCount = targetCount,

            // Preserve sharp architectural features
            CreaseAngle = creaseAngle,

            // Smooth iterations
            SmoothIterations = smoothIterations,

            // Topology
            PureQuad = pureQuadMesh,
            AlignToBoundaries = alignToBoundaries,

            // Use 4-symmetry for quads (best for architecture)
            RotationSymmetry = 4,
            PositionSymmetry = 4,

            // Advanced
            Deterministic = deterministic,
            NumThreads = numThreads,
            Extrinsic = true, // Better for buildings

            // Progress callback
            OnProgress = (message, progress) =>
            {
                Debug.Log($"[{progress * 100:F0}%] {message}");
                #if UNITY_EDITOR
                EditorUtility.DisplayProgressBar("Optimizing Building Mesh", message, progress);
                #endif
            }
        };

        // Set log level for debugging
        InstantMeshesAPI.SetLogLevel(3); // Info level

        // Optimize
        Mesh optimizedMesh = InstantMeshesAPI.OptimizeMesh(inputMesh, parameters);

        #if UNITY_EDITOR
        EditorUtility.ClearProgressBar();
        #endif

        if (optimizedMesh != null)
        {
            // Calculate reduction
            float vertexReduction = (1f - (float)optimizedMesh.vertexCount / inputMesh.vertexCount) * 100f;
            float triangleReduction = (1f - (float)optimizedMesh.triangles.Length / inputMesh.triangles.Length) * 100f;

            Debug.Log($"<color=green>Optimization successful!</color>\n" +
                      $"Vertices: {inputMesh.vertexCount} → {optimizedMesh.vertexCount} ({vertexReduction:F1}% reduction)\n" +
                      $"Triangles: {inputMesh.triangles.Length / 3} → {optimizedMesh.triangles.Length / 3} ({triangleReduction:F1}% reduction)");

            #if UNITY_EDITOR
            // Save as asset if requested
            if (saveAsAsset)
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Optimized Building Mesh",
                    inputMesh.name + "_optimized",
                    "asset",
                    "Save optimized mesh as asset");

                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(optimizedMesh, path);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Optimized mesh saved to: {path}");
                }
            }
            #endif

            // Apply to mesh filter if requested
            if (replaceOriginal)
            {
                meshFilter.mesh = optimizedMesh;
                Debug.Log("Original mesh replaced with optimized version");
            }
        }
        else
        {
            string error = InstantMeshesAPI.GetLastError();
            Debug.LogError($"<color=red>Optimization failed:</color> {error}");

            #if UNITY_EDITOR
            EditorUtility.DisplayDialog("Optimization Failed",
                $"Failed to optimize mesh:\n{error}", "OK");
            #endif
        }
    }

    /// <summary>
    /// Restore the original mesh
    /// </summary>
    public void RestoreOriginal()
    {
        if (originalMesh != null && meshFilter != null)
        {
            meshFilter.mesh = originalMesh;
            Debug.Log("Original mesh restored");
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(BuildingMeshOptimizer))]
    public class BuildingMeshOptimizerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BuildingMeshOptimizer optimizer = (BuildingMeshOptimizer)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Optimize Building Mesh", GUILayout.Height(30)))
            {
                optimizer.OptimizeBuildingMesh();
            }

            GUI.enabled = optimizer.replaceOriginal;
            if (GUILayout.Button("Restore Original Mesh"))
            {
                optimizer.RestoreOriginal();
            }
            GUI.enabled = true;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This optimizer is specifically tuned for building meshes.\n\n" +
                "• Crease Angle: Preserves sharp edges (corners, door frames)\n" +
                "• Pure Quad: Generates clean quad topology ideal for architecture\n" +
                "• Align to Boundaries: Ensures edges align with building features",
                MessageType.Info);
        }
    }
    #endif
}
