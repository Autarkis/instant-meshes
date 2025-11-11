/*
    InstantMeshesAPI.cs -- Unity C# wrapper for Instant Meshes

    This provides a Unity-friendly interface to the Instant Meshes native library.
    Handles P/Invoke, memory marshalling, and provides a clean Mesh-based API.

    Usage:
        Mesh inputMesh = GetComponent<MeshFilter>().mesh;
        Mesh optimizedMesh = InstantMeshesAPI.OptimizeMesh(inputMesh);

    BSD-style license
*/

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace InstantMeshes
{
    /// <summary>
    /// Native API wrapper for Instant Meshes DLL
    /// </summary>
    public static class InstantMeshesAPI
    {
        // DLL name - change based on platform
        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            private const string DLL_NAME = "instant_meshes_wrapper";
        #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            private const string DLL_NAME = "libinstant_meshes_wrapper";
        #elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            private const string DLL_NAME = "libinstant_meshes_wrapper";
        #else
            private const string DLL_NAME = "instant_meshes_wrapper";
        #endif

        #region Native Structures and Enums

        public enum IMResult
        {
            Success = 0,
            ErrorInvalidParams = -1,
            ErrorNullPointer = -2,
            ErrorBufferTooSmall = -3,
            ErrorProcessingFailed = -4,
            ErrorOutOfMemory = -5,
            ErrorInvalidMesh = -6,
            ErrorException = -100
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMParameters
        {
            // Input mesh data
            public IntPtr vertices;
            public int vertex_count;
            public IntPtr triangles;
            public int triangle_count;
            public IntPtr normals;

            // Output buffers
            public IntPtr out_vertices;
            public IntPtr out_triangles;
            public int max_out_vertices;
            public int max_out_triangles;

            // Processing parameters
            public int target_vertex_count;
            public int target_face_count;
            public float scale;

            // Algorithm parameters
            public int rosy;
            public int posy;
            public float crease_angle;
            public int smooth_iterations;

            // Flags
            public int extrinsic;
            public int align_to_boundaries;
            public int pure_quad;
            public int deterministic;

            // Advanced
            public int num_threads;

            // Reserved
            public IntPtr reserved0;
            public IntPtr reserved1;
            public IntPtr reserved2;
            public IntPtr reserved3;
            public IntPtr reserved4;
            public IntPtr reserved5;
            public IntPtr reserved6;
            public IntPtr reserved7;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ProgressCallback(string message, float progress);

        #endregion

        #region Native Methods

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void IM_GetVersion(out int major, out int minor, out int patch);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void IM_InitializeParams(ref IMParameters parameters);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IMResult IM_ProcessMesh(
            ref IMParameters parameters,
            out int outVertexCount,
            out int outTriangleCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IMResult IM_EstimateOutputSize(
            int inputVertexCount,
            int inputTriangleCount,
            int targetVertexCount,
            out int estimatedVertices,
            out int estimatedTriangles);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IMResult IM_ValidateMesh(
            IntPtr vertices,
            int vertexCount,
            IntPtr triangles,
            int triangleCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr IM_GetLastError();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void IM_SetLogLevel(int level);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void IM_SetProgressCallback(ProgressCallback callback);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void IM_Cleanup();

        #endregion

        #region Public Configuration Class

        /// <summary>
        /// Configuration parameters for mesh optimization
        /// </summary>
        public class OptimizationParams
        {
            /// <summary>Target vertex count (0 = automatic: 1/16 of input)</summary>
            public int TargetVertexCount = 0;

            /// <summary>Target face count (overrides vertex count if > 0)</summary>
            public int TargetFaceCount = 0;

            /// <summary>Target edge length in world units (overrides counts if > 0)</summary>
            public float Scale = -1f;

            /// <summary>Rotation symmetry: 2, 4, or 6 (default: 4 for quads)</summary>
            public int RotationSymmetry = 4;

            /// <summary>Position symmetry: 4 or 6 (default: 4 for quads)</summary>
            public int PositionSymmetry = 4;

            /// <summary>Crease angle threshold in degrees (-1 = disabled, smooth everywhere)</summary>
            public float CreaseAngle = -1f;

            /// <summary>Number of smoothing iterations (default: 2)</summary>
            public int SmoothIterations = 2;

            /// <summary>Use extrinsic mode (true) or intrinsic (false)</summary>
            public bool Extrinsic = true;

            /// <summary>Align field to mesh boundaries</summary>
            public bool AlignToBoundaries = false;

            /// <summary>Generate pure quad mesh (true) or quad-dominant (false)</summary>
            public bool PureQuad = true;

            /// <summary>Use deterministic (slower) algorithms</summary>
            public bool Deterministic = false;

            /// <summary>Number of threads to use (-1 = automatic)</summary>
            public int NumThreads = -1;

            /// <summary>Progress callback (optional)</summary>
            public Action<string, float> OnProgress = null;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the Instant Meshes wrapper version
        /// </summary>
        public static void GetVersion(out int major, out int minor, out int patch)
        {
            IM_GetVersion(out major, out minor, out patch);
        }

        /// <summary>
        /// Set the logging level for diagnostic output
        /// </summary>
        /// <param name="level">0=none, 1=errors, 2=warnings, 3=info, 4=debug</param>
        public static void SetLogLevel(int level)
        {
            IM_SetLogLevel(level);
        }

        /// <summary>
        /// Optimize a Unity mesh using Instant Meshes algorithm
        /// </summary>
        /// <param name="inputMesh">Input mesh to optimize</param>
        /// <param name="parameters">Optimization parameters (null = defaults)</param>
        /// <returns>Optimized mesh, or null on failure</returns>
        public static Mesh OptimizeMesh(Mesh inputMesh, OptimizationParams parameters = null)
        {
            if (inputMesh == null)
            {
                Debug.LogError("InstantMeshes: Input mesh is null");
                return null;
            }

            // Use default parameters if none provided
            if (parameters == null)
            {
                parameters = new OptimizationParams();
            }

            try
            {
                // Get mesh data
                Vector3[] vertices = inputMesh.vertices;
                int[] triangles = inputMesh.triangles;
                Vector3[] normals = inputMesh.normals;

                // Validate
                if (vertices.Length < 3 || triangles.Length < 3)
                {
                    Debug.LogError("InstantMeshes: Input mesh too small (needs at least 3 vertices and 1 triangle)");
                    return null;
                }

                // Convert to flat arrays for native code
                float[] vertexData = new float[vertices.Length * 3];
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertexData[i * 3 + 0] = vertices[i].x;
                    vertexData[i * 3 + 1] = vertices[i].y;
                    vertexData[i * 3 + 2] = vertices[i].z;
                }

                float[] normalData = null;
                if (normals != null && normals.Length == vertices.Length)
                {
                    normalData = new float[normals.Length * 3];
                    for (int i = 0; i < normals.Length; i++)
                    {
                        normalData[i * 3 + 0] = normals[i].x;
                        normalData[i * 3 + 1] = normals[i].y;
                        normalData[i * 3 + 2] = normals[i].z;
                    }
                }

                // Estimate output size
                IMResult estimateResult = IM_EstimateOutputSize(
                    vertices.Length,
                    triangles.Length / 3,
                    parameters.TargetVertexCount,
                    out int estimatedVertices,
                    out int estimatedTriangles);

                if (estimateResult != IMResult.Success)
                {
                    Debug.LogError($"InstantMeshes: Failed to estimate output size: {GetLastError()}");
                    return null;
                }

                // Allocate output buffers
                float[] outVertexData = new float[estimatedVertices * 3];
                int[] outTriangleData = new int[estimatedTriangles * 3];

                // Pin arrays for native access
                GCHandle vertexHandle = GCHandle.Alloc(vertexData, GCHandleType.Pinned);
                GCHandle triangleHandle = GCHandle.Alloc(triangles, GCHandleType.Pinned);
                GCHandle normalHandle = normalData != null ? GCHandle.Alloc(normalData, GCHandleType.Pinned) : default;
                GCHandle outVertexHandle = GCHandle.Alloc(outVertexData, GCHandleType.Pinned);
                GCHandle outTriangleHandle = GCHandle.Alloc(outTriangleData, GCHandleType.Pinned);

                try
                {
                    // Setup parameters
                    IMParameters nativeParams = new IMParameters();
                    IM_InitializeParams(ref nativeParams);

                    nativeParams.vertices = vertexHandle.AddrOfPinnedObject();
                    nativeParams.vertex_count = vertices.Length;
                    nativeParams.triangles = triangleHandle.AddrOfPinnedObject();
                    nativeParams.triangle_count = triangles.Length / 3;
                    nativeParams.normals = normalHandle.IsAllocated ? normalHandle.AddrOfPinnedObject() : IntPtr.Zero;

                    nativeParams.out_vertices = outVertexHandle.AddrOfPinnedObject();
                    nativeParams.out_triangles = outTriangleHandle.AddrOfPinnedObject();
                    nativeParams.max_out_vertices = estimatedVertices;
                    nativeParams.max_out_triangles = estimatedTriangles;

                    nativeParams.target_vertex_count = parameters.TargetVertexCount;
                    nativeParams.target_face_count = parameters.TargetFaceCount;
                    nativeParams.scale = parameters.Scale;

                    nativeParams.rosy = parameters.RotationSymmetry;
                    nativeParams.posy = parameters.PositionSymmetry;
                    nativeParams.crease_angle = parameters.CreaseAngle;
                    nativeParams.smooth_iterations = parameters.SmoothIterations;

                    nativeParams.extrinsic = parameters.Extrinsic ? 1 : 0;
                    nativeParams.align_to_boundaries = parameters.AlignToBoundaries ? 1 : 0;
                    nativeParams.pure_quad = parameters.PureQuad ? 1 : 0;
                    nativeParams.deterministic = parameters.Deterministic ? 1 : 0;

                    nativeParams.num_threads = parameters.NumThreads;

                    // Set progress callback if provided
                    ProgressCallback progressCallback = null;
                    if (parameters.OnProgress != null)
                    {
                        progressCallback = (msg, progress) => parameters.OnProgress(msg, progress);
                        IM_SetProgressCallback(progressCallback);
                    }

                    // Process mesh
                    IMResult result = IM_ProcessMesh(ref nativeParams, out int outVertexCount, out int outTriangleCount);

                    // Clear progress callback
                    if (progressCallback != null)
                    {
                        IM_SetProgressCallback(null);
                    }

                    if (result != IMResult.Success)
                    {
                        Debug.LogError($"InstantMeshes: Processing failed with error: {result} - {GetLastError()}");
                        return null;
                    }

                    // Convert back to Unity mesh
                    Vector3[] outVertices = new Vector3[outVertexCount];
                    for (int i = 0; i < outVertexCount; i++)
                    {
                        outVertices[i] = new Vector3(
                            outVertexData[i * 3 + 0],
                            outVertexData[i * 3 + 1],
                            outVertexData[i * 3 + 2]);
                    }

                    int[] outTriangles = new int[outTriangleCount * 3];
                    Array.Copy(outTriangleData, outTriangles, outTriangleCount * 3);

                    // Create output mesh
                    Mesh outputMesh = new Mesh();
                    outputMesh.name = inputMesh.name + "_optimized";
                    outputMesh.vertices = outVertices;
                    outputMesh.triangles = outTriangles;
                    outputMesh.RecalculateNormals();
                    outputMesh.RecalculateBounds();

                    Debug.Log($"InstantMeshes: Optimized mesh from {vertices.Length} to {outVertexCount} vertices " +
                              $"({triangles.Length / 3} to {outTriangleCount} triangles)");

                    return outputMesh;
                }
                finally
                {
                    // Release pinned memory
                    vertexHandle.Free();
                    triangleHandle.Free();
                    if (normalHandle.IsAllocated) normalHandle.Free();
                    outVertexHandle.Free();
                    outTriangleHandle.Free();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"InstantMeshes: Exception during mesh optimization: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Get the last error message from the native library
        /// </summary>
        public static string GetLastError()
        {
            IntPtr errorPtr = IM_GetLastError();
            if (errorPtr == IntPtr.Zero)
                return "No error";
            return Marshal.PtrToStringAnsi(errorPtr);
        }

        /// <summary>
        /// Clean up native resources (call on application shutdown)
        /// </summary>
        public static void Cleanup()
        {
            IM_Cleanup();
        }

        #endregion
    }

    #region Unity Editor Integration

    #if UNITY_EDITOR
    /// <summary>
    /// Editor utility for mesh optimization
    /// </summary>
    public static class InstantMeshesEditorUtils
    {
        [UnityEditor.MenuItem("Tools/Instant Meshes/Optimize Selected Mesh")]
        public static void OptimizeSelectedMesh()
        {
            GameObject selected = UnityEditor.Selection.activeGameObject;
            if (selected == null)
            {
                UnityEditor.EditorUtility.DisplayDialog("Error", "No GameObject selected", "OK");
                return;
            }

            MeshFilter meshFilter = selected.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                UnityEditor.EditorUtility.DisplayDialog("Error", "Selected GameObject has no MeshFilter or Mesh", "OK");
                return;
            }

            // Optimize
            var parameters = new InstantMeshesAPI.OptimizationParams
            {
                OnProgress = (msg, progress) =>
                {
                    UnityEditor.EditorUtility.DisplayProgressBar("Instant Meshes", msg, progress);
                }
            };

            Mesh optimizedMesh = InstantMeshesAPI.OptimizeMesh(meshFilter.sharedMesh, parameters);
            UnityEditor.EditorUtility.ClearProgressBar();

            if (optimizedMesh != null)
            {
                // Save as asset
                string path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                    "Save Optimized Mesh",
                    meshFilter.sharedMesh.name + "_optimized",
                    "asset",
                    "Save optimized mesh as asset");

                if (!string.IsNullOrEmpty(path))
                {
                    UnityEditor.AssetDatabase.CreateAsset(optimizedMesh, path);
                    UnityEditor.AssetDatabase.SaveAssets();
                    UnityEditor.EditorUtility.DisplayDialog("Success",
                        $"Optimized mesh saved to {path}", "OK");
                }
            }
        }

        [UnityEditor.MenuItem("Tools/Instant Meshes/About")]
        public static void ShowAbout()
        {
            InstantMeshesAPI.GetVersion(out int major, out int minor, out int patch);
            UnityEditor.EditorUtility.DisplayDialog("Instant Meshes for Unity",
                $"Instant Meshes Wrapper v{major}.{minor}.{patch}\n\n" +
                "Based on Instant Field-Aligned Meshes\n" +
                "by Wenzel Jakob, Marco Tarini, Daniele Panozzo, Olga Sorkine-Hornung\n\n" +
                "ACM Transactions on Graphics (SIGGRAPH Asia 2015)",
                "OK");
        }
    }
    #endif

    #endregion
}
