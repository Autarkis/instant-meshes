/*
    instant_meshes_wrapper.h -- C API wrapper for Unity integration

    This provides a simplified C-style API for calling Instant Meshes
    from Unity via P/Invoke. The wrapper handles all C++ complexity
    and provides a clean interface for mesh processing.

    BSD-style license (see LICENSE.txt)
*/

#pragma once

#ifdef _WIN32
    #ifdef INSTANT_MESHES_WRAPPER_EXPORTS
        #define IMWRAPPER_API __declspec(dllexport)
    #else
        #define IMWRAPPER_API __declspec(dllimport)
    #endif
    #define IMWRAPPER_CALL __cdecl
#else
    #define IMWRAPPER_API __attribute__((visibility("default")))
    #define IMWRAPPER_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Version information */
#define INSTANT_MESHES_WRAPPER_VERSION_MAJOR 1
#define INSTANT_MESHES_WRAPPER_VERSION_MINOR 0
#define INSTANT_MESHES_WRAPPER_VERSION_PATCH 0

/* Return codes */
typedef enum {
    IM_SUCCESS = 0,
    IM_ERROR_INVALID_PARAMS = -1,
    IM_ERROR_NULL_POINTER = -2,
    IM_ERROR_BUFFER_TOO_SMALL = -3,
    IM_ERROR_PROCESSING_FAILED = -4,
    IM_ERROR_OUT_OF_MEMORY = -5,
    IM_ERROR_INVALID_MESH = -6,
    IM_ERROR_EXCEPTION = -100
} IMResult;

/* Mesh processing parameters structure */
typedef struct {
    /* Input mesh data */
    float* vertices;          /* Input: vertex positions (x,y,z interleaved) */
    int vertex_count;         /* Input: number of vertices */
    int* triangles;           /* Input: triangle indices (ccw) */
    int triangle_count;       /* Input: number of triangles */
    float* normals;           /* Input: vertex normals (x,y,z interleaved, optional - can be NULL) */

    /* Output buffers (pre-allocated by caller) */
    float* out_vertices;      /* Output: resulting vertex positions */
    int* out_triangles;       /* Output: resulting triangle/quad indices */
    int max_out_vertices;     /* Input: maximum vertices output buffer can hold */
    int max_out_triangles;    /* Input: maximum triangles output buffer can hold */

    /* Processing parameters */
    int target_vertex_count;  /* Desired vertex count (0 = automatic: input_vertices/16) */
    int target_face_count;    /* Desired face count (0 = use vertex_count, -1 = use scale) */
    float scale;              /* Desired world-space edge length (-1 = use counts) */

    /* Algorithm parameters */
    int rosy;                 /* Rotation symmetry: 2, 4, or 6 (default: 4) */
    int posy;                 /* Position symmetry: 4 or 6 (default: 4) */
    float crease_angle;       /* Crease angle threshold in degrees (-1 = disabled) */
    int smooth_iterations;    /* Smoothing iterations (default: 2) */

    /* Flags */
    int extrinsic;            /* Use extrinsic mode (1) or intrinsic (0). Default: 1 */
    int align_to_boundaries;  /* Align field to mesh boundaries (1/0). Default: 0 */
    int pure_quad;            /* Generate pure quad mesh (1) vs quad-dominant (0). Default: 1 */
    int deterministic;        /* Use deterministic (slower) algorithms (1/0). Default: 0 */

    /* Advanced parameters */
    int num_threads;          /* Number of threads to use (-1 = automatic) */

    /* Reserved for future use */
    void* reserved[8];
} IMParameters;

/* Progress callback function pointer */
typedef void (IMWRAPPER_CALL *IMProgressCallback)(const char* message, float progress);

/**
 * Get wrapper version information
 * @param major Output: major version number
 * @param minor Output: minor version number
 * @param patch Output: patch version number
 */
IMWRAPPER_API void IMWRAPPER_CALL IM_GetVersion(int* major, int* minor, int* patch);

/**
 * Initialize parameters with default values
 * @param params Parameters structure to initialize
 */
IMWRAPPER_API void IMWRAPPER_CALL IM_InitializeParams(IMParameters* params);

/**
 * Process a mesh using Instant Meshes algorithm
 *
 * @param params Input/output parameters structure
 * @param out_vertex_count Output: actual number of vertices generated
 * @param out_triangle_count Output: actual number of triangles generated
 * @return IM_SUCCESS on success, error code otherwise
 */
IMWRAPPER_API IMResult IMWRAPPER_CALL IM_ProcessMesh(
    IMParameters* params,
    int* out_vertex_count,
    int* out_triangle_count
);

/**
 * Estimate output buffer sizes needed for mesh processing
 * This is a conservative estimate - actual output may be smaller
 *
 * @param input_vertex_count Number of input vertices
 * @param input_triangle_count Number of input triangles
 * @param target_vertex_count Target vertex count (0 = automatic)
 * @param estimated_vertices Output: estimated output vertex count
 * @param estimated_triangles Output: estimated output triangle count
 * @return IM_SUCCESS on success, error code otherwise
 */
IMWRAPPER_API IMResult IMWRAPPER_CALL IM_EstimateOutputSize(
    int input_vertex_count,
    int input_triangle_count,
    int target_vertex_count,
    int* estimated_vertices,
    int* estimated_triangles
);

/**
 * Get the last error message
 * @return Null-terminated error message string (valid until next API call)
 */
IMWRAPPER_API const char* IMWRAPPER_CALL IM_GetLastError(void);

/**
 * Set log level for diagnostic output
 * @param level 0=none, 1=errors, 2=warnings, 3=info, 4=debug
 */
IMWRAPPER_API void IMWRAPPER_CALL IM_SetLogLevel(int level);

/**
 * Set progress callback (optional)
 * @param callback Function to call with progress updates (NULL to disable)
 */
IMWRAPPER_API void IMWRAPPER_CALL IM_SetProgressCallback(IMProgressCallback callback);

/**
 * Validate input mesh for processing
 * Checks for common issues: degenerate triangles, invalid indices, etc.
 *
 * @param vertices Vertex array
 * @param vertex_count Number of vertices
 * @param triangles Triangle index array
 * @param triangle_count Number of triangles
 * @return IM_SUCCESS if mesh is valid, error code otherwise
 */
IMWRAPPER_API IMResult IMWRAPPER_CALL IM_ValidateMesh(
    const float* vertices,
    int vertex_count,
    const int* triangles,
    int triangle_count
);

/**
 * Free any internal resources (call on shutdown)
 */
IMWRAPPER_API void IMWRAPPER_CALL IM_Cleanup(void);

#ifdef __cplusplus
}
#endif
