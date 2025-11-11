/*
    instant_meshes_wrapper.cpp -- C API wrapper implementation

    This implements the C wrapper for Instant Meshes, converting between
    Unity's simple array format and Instant Meshes' Eigen-based data structures.

    BSD-style license (see LICENSE.txt)
*/

#include "instant_meshes_wrapper.h"
#include "common.h"
#include "meshio.h"
#include "dedge.h"
#include "subdivide.h"
#include "meshstats.h"
#include "hierarchy.h"
#include "field.h"
#include "normal.h"
#include "extract.h"
#include "bvh.h"

#include <string>
#include <sstream>
#include <exception>
#include <cstring>

// Thread-local storage for error messages
static thread_local char g_error_buffer[2048] = {0};
static thread_local int g_log_level = 1; // Default: errors only
static IMProgressCallback g_progress_callback = nullptr;

// TBB task scheduler initialization
static tbb::task_scheduler_init* g_tbb_scheduler = nullptr;
static std::mutex g_init_mutex;

// Global nprocs variable used by field.cpp
int nprocs = -1;

// Helper: set last error message
static void SetLastError(const char* msg) {
    strncpy(g_error_buffer, msg, sizeof(g_error_buffer) - 1);
    g_error_buffer[sizeof(g_error_buffer) - 1] = '\0';

    if (g_log_level >= 1) {
        std::cerr << "Instant Meshes Wrapper Error: " << msg << std::endl;
    }
}

// Helper: log message
static void LogMessage(int level, const char* msg) {
    if (g_log_level >= level) {
        std::cout << msg << std::endl;
    }
}

// Helper: progress callback wrapper
static void ReportProgress(const std::string& message, Float progress) {
    if (g_progress_callback) {
        g_progress_callback(message.c_str(), (float)progress);
    }
}

// Initialize TBB if needed
static void InitializeTBB(int num_threads) {
    std::lock_guard<std::mutex> lock(g_init_mutex);

    if (!g_tbb_scheduler) {
        if (num_threads <= 0) {
            g_tbb_scheduler = new tbb::task_scheduler_init(tbb::task_scheduler_init::automatic);
        } else {
            g_tbb_scheduler = new tbb::task_scheduler_init(num_threads);
        }
    }
}

extern "C" {

IMWRAPPER_API void IMWRAPPER_CALL IM_GetVersion(int* major, int* minor, int* patch) {
    if (major) *major = INSTANT_MESHES_WRAPPER_VERSION_MAJOR;
    if (minor) *minor = INSTANT_MESHES_WRAPPER_VERSION_MINOR;
    if (patch) *patch = INSTANT_MESHES_WRAPPER_VERSION_PATCH;
}

IMWRAPPER_API int IMWRAPPER_CALL IM_GetABIVersion(void) {
    return INSTANT_MESHES_WRAPPER_ABI_VERSION;
}

IMWRAPPER_API void IMWRAPPER_CALL IM_InitializeParams(IMParameters* params) {
    if (!params) return;

    memset(params, 0, sizeof(IMParameters));

    // Set defaults matching Instant Meshes
    params->rosy = 4;
    params->posy = 4;
    params->crease_angle = -1.0f;
    params->smooth_iterations = 2;
    params->extrinsic = 1;
    params->align_to_boundaries = 0;
    params->pure_quad = 1;
    params->deterministic = 0;
    params->num_threads = -1;
    params->scale = -1.0f;
    params->target_vertex_count = 0;
    params->target_face_count = 0;
}

IMWRAPPER_API IMResult IMWRAPPER_CALL IM_ValidateMesh(
    const float* vertices,
    int vertex_count,
    const int* triangles,
    int triangle_count
) {
    if (!vertices || !triangles) {
        SetLastError("Null pointer in mesh data");
        return IM_ERROR_NULL_POINTER;
    }

    if (vertex_count < 3) {
        SetLastError("Mesh must have at least 3 vertices");
        return IM_ERROR_INVALID_MESH;
    }

    if (triangle_count < 1) {
        SetLastError("Mesh must have at least 1 triangle");
        return IM_ERROR_INVALID_MESH;
    }

    // Check for invalid indices
    for (int i = 0; i < triangle_count * 3; i++) {
        if (triangles[i] < 0 || triangles[i] >= vertex_count) {
            std::ostringstream oss;
            oss << "Invalid triangle index: " << triangles[i] << " (vertex count: " << vertex_count << ")";
            SetLastError(oss.str().c_str());
            return IM_ERROR_INVALID_MESH;
        }
    }

    return IM_SUCCESS;
}

IMWRAPPER_API IMResult IMWRAPPER_CALL IM_EstimateOutputSize(
    int input_vertex_count,
    int input_triangle_count,
    int target_vertex_count,
    int* estimated_vertices,
    int* estimated_triangles
) {
    if (!estimated_vertices || !estimated_triangles) {
        SetLastError("Null output pointers in EstimateOutputSize");
        return IM_ERROR_NULL_POINTER;
    }

    // If no target specified, use default (1/16 of input)
    if (target_vertex_count <= 0) {
        target_vertex_count = input_vertex_count / 16;
        if (target_vertex_count < 4) target_vertex_count = 4;
    }

    // Conservative estimate: allow 50% overhead
    *estimated_vertices = target_vertex_count * 3 / 2;
    *estimated_triangles = target_vertex_count * 2 * 3 / 2; // Quads become 2 tris

    return IM_SUCCESS;
}

IMWRAPPER_API IMResult IMWRAPPER_CALL IM_ProcessMesh(
    IMParameters* params,
    int* out_vertex_count,
    int* out_triangle_count
) {
    if (!params || !out_vertex_count || !out_triangle_count) {
        SetLastError("Null parameter pointers");
        return IM_ERROR_NULL_POINTER;
    }

    // Validate input
    IMResult validation = IM_ValidateMesh(params->vertices, params->vertex_count,
                                          params->triangles, params->triangle_count);
    if (validation != IM_SUCCESS) {
        return validation;
    }

    // Validate output buffers
    if (!params->out_vertices || !params->out_triangles) {
        SetLastError("Null output buffer pointers");
        return IM_ERROR_NULL_POINTER;
    }

    try {
        InitializeTBB(params->num_threads);

        LogMessage(3, "Converting mesh to Eigen format...");

        // Convert input to Eigen format
        MatrixXf V(3, params->vertex_count);
        MatrixXu F(3, params->triangle_count);
        MatrixXf N(3, params->vertex_count);

        // Copy vertices
        for (int i = 0; i < params->vertex_count; i++) {
            V(0, i) = params->vertices[i * 3 + 0];
            V(1, i) = params->vertices[i * 3 + 1];
            V(2, i) = params->vertices[i * 3 + 2];
        }

        // Copy triangles
        for (int i = 0; i < params->triangle_count; i++) {
            F(0, i) = params->triangles[i * 3 + 0];
            F(1, i) = params->triangles[i * 3 + 1];
            F(2, i) = params->triangles[i * 3 + 2];
        }

        // Copy or compute normals
        if (params->normals) {
            for (int i = 0; i < params->vertex_count; i++) {
                N(0, i) = params->normals[i * 3 + 0];
                N(1, i) = params->normals[i * 3 + 1];
                N(2, i) = params->normals[i * 3 + 2];
            }
        }

        LogMessage(3, "Computing mesh statistics...");

        // Compute mesh stats
        MeshStats stats = compute_mesh_stats(F, V, params->deterministic != 0);

        // Determine target size
        int vertex_count = params->target_vertex_count;
        int face_count = params->target_face_count;
        Float scale = params->scale;

        if (scale < 0 && vertex_count <= 0 && face_count <= 0) {
            vertex_count = params->vertex_count / 16;
            if (vertex_count < 4) vertex_count = 4;
        }

        if (scale > 0) {
            Float face_area = params->posy == 4 ? (scale*scale) : (std::sqrt(3.f)/4.f*scale*scale);
            face_count = stats.mSurfaceArea / face_area;
            vertex_count = params->posy == 4 ? face_count : (face_count / 2);
        } else if (face_count > 0) {
            Float face_area = stats.mSurfaceArea / face_count;
            vertex_count = params->posy == 4 ? face_count : (face_count / 2);
            scale = params->posy == 4 ? std::sqrt(face_area) : (2*std::sqrt(face_area * std::sqrt(1.f/3.f)));
        } else if (vertex_count > 0) {
            face_count = params->posy == 4 ? vertex_count : (vertex_count * 2);
            Float face_area = stats.mSurfaceArea / face_count;
            scale = params->posy == 4 ? std::sqrt(face_area) : (2*std::sqrt(face_area * std::sqrt(1.f/3.f)));
        }

        LogMessage(3, "Preprocessing mesh...");

        // Subdivide if necessary
        VectorXu V2E, E2E;
        VectorXb boundary, nonManifold;

        if (stats.mMaximumEdgeLength*2 > scale || stats.mMaximumEdgeLength > stats.mAverageEdgeLength * 2) {
            LogMessage(3, "Subdividing mesh...");
            build_dedge(F, V, V2E, E2E, boundary, nonManifold);
            subdivide(F, V, V2E, E2E, boundary, nonManifold,
                     std::min(scale/2, (Float)stats.mAverageEdgeLength*2),
                     params->deterministic != 0);
        }

        // Build directed edge data structure
        build_dedge(F, V, V2E, E2E, boundary, nonManifold);

        // Generate adjacency matrix
        AdjacencyMatrix adj = generate_adjacency_matrix_uniform(F, V2E, E2E, nonManifold);

        // Compute normals if not provided
        std::set<uint32_t> crease_in, crease_out;
        if (!params->normals) {
            if (params->crease_angle >= 0) {
                generate_crease_normals(F, V, V2E, E2E, boundary, nonManifold,
                                       params->crease_angle, N, crease_in);
            } else {
                generate_smooth_normals(F, V, V2E, E2E, nonManifold, N);
            }
        }

        // Compute dual vertex areas
        VectorXf A;
        compute_dual_vertex_areas(F, V, V2E, E2E, nonManifold, A);

        LogMessage(3, "Building multi-resolution hierarchy...");

        // Build hierarchy
        MultiResolutionHierarchy mRes;
        mRes.setE2E(std::move(E2E));
        mRes.setAdj(std::move(adj));
        mRes.setF(std::move(F));
        mRes.setV(std::move(V));
        mRes.setA(std::move(A));
        mRes.setN(std::move(N));
        mRes.setScale(scale);
        mRes.build(params->deterministic != 0);
        mRes.resetSolution();

        // Align to boundaries if requested
        if (params->align_to_boundaries) {
            mRes.clearConstraints();
            for (uint32_t i=0; i<3*mRes.F().cols(); ++i) {
                if (mRes.E2E()[i] == INVALID) {
                    uint32_t i0 = mRes.F()(i%3, i/3);
                    uint32_t i1 = mRes.F()((i+1)%3, i/3);
                    Vector3f p0 = mRes.V().col(i0), p1 = mRes.V().col(i1);
                    Vector3f edge = p1-p0;
                    if (edge.squaredNorm() > 0) {
                        edge.normalize();
                        mRes.CO().col(i0) = p0;
                        mRes.CO().col(i1) = p1;
                        mRes.CQ().col(i0) = mRes.CQ().col(i1) = edge;
                        mRes.CQw()[i0] = mRes.CQw()[i1] = mRes.COw()[i0] =
                            mRes.COw()[i1] = 1.0f;
                    }
                }
            }
            mRes.propagateConstraints(params->rosy, params->posy);
        }

        // Setup BVH for smoothing
        BVH* bvh = nullptr;
        if (params->smooth_iterations > 0) {
            bvh = new BVH(&mRes.F(), &mRes.V(), &mRes.N(), stats.mAABB);
            bvh->build();
        }

        LogMessage(3, "Optimizing orientation field...");
        ReportProgress("Optimizing orientation field", 0.2f);

        // Optimize orientation field
        Optimizer optimizer(mRes, false);
        optimizer.setRoSy(params->rosy);
        optimizer.setPoSy(params->posy);
        optimizer.setExtrinsic(params->extrinsic != 0);
        optimizer.optimizeOrientations(-1);
        optimizer.notify();
        optimizer.wait();

        LogMessage(3, "Optimizing position field...");
        ReportProgress("Optimizing position field", 0.5f);

        // Optimize position field
        optimizer.optimizePositions(-1);
        optimizer.notify();
        optimizer.wait();
        optimizer.shutdown();

        LogMessage(3, "Extracting output mesh...");
        ReportProgress("Extracting mesh", 0.8f);

        // Extract result
        MatrixXf O_extr, N_extr, Nf_extr;
        std::vector<std::vector<TaggedLink>> adj_extr;
        extract_graph(mRes, params->extrinsic != 0, params->rosy, params->posy,
                     adj_extr, O_extr, N_extr, crease_in, crease_out,
                     params->deterministic != 0);

        MatrixXu F_extr;
        extract_faces(adj_extr, O_extr, N_extr, Nf_extr, F_extr, params->posy,
                     scale, crease_out, true, params->pure_quad != 0, bvh,
                     params->smooth_iterations);

        if (bvh) delete bvh;

        LogMessage(3, "Converting output to Unity format...");

        // Check output buffer sizes
        int result_vertex_count = O_extr.cols();
        int result_triangle_count = F_extr.cols();

        if (result_vertex_count > params->max_out_vertices) {
            std::ostringstream oss;
            oss << "Output vertex buffer too small: " << result_vertex_count
                << " vertices generated, buffer size: " << params->max_out_vertices;
            SetLastError(oss.str().c_str());
            return IM_ERROR_BUFFER_TOO_SMALL;
        }

        if (result_triangle_count > params->max_out_triangles) {
            std::ostringstream oss;
            oss << "Output triangle buffer too small: " << result_triangle_count
                << " triangles generated, buffer size: " << params->max_out_triangles;
            SetLastError(oss.str().c_str());
            return IM_ERROR_BUFFER_TOO_SMALL;
        }

        // Copy output vertices
        for (int i = 0; i < result_vertex_count; i++) {
            params->out_vertices[i * 3 + 0] = O_extr(0, i);
            params->out_vertices[i * 3 + 1] = O_extr(1, i);
            params->out_vertices[i * 3 + 2] = O_extr(2, i);
        }

        // Copy output triangles/quads
        // Note: F_extr may contain quads (4 indices) or triangles (3 indices)
        // We need to triangulate quads for Unity
        int tri_idx = 0;
        for (int i = 0; i < result_triangle_count; i++) {
            int verts_per_face = F_extr.rows();

            if (verts_per_face == 3) {
                // Triangle
                params->out_triangles[tri_idx++] = F_extr(0, i);
                params->out_triangles[tri_idx++] = F_extr(1, i);
                params->out_triangles[tri_idx++] = F_extr(2, i);
            } else if (verts_per_face == 4) {
                // Quad - split into two triangles
                params->out_triangles[tri_idx++] = F_extr(0, i);
                params->out_triangles[tri_idx++] = F_extr(1, i);
                params->out_triangles[tri_idx++] = F_extr(2, i);

                params->out_triangles[tri_idx++] = F_extr(0, i);
                params->out_triangles[tri_idx++] = F_extr(2, i);
                params->out_triangles[tri_idx++] = F_extr(3, i);
            }
        }

        *out_vertex_count = result_vertex_count;
        *out_triangle_count = tri_idx / 3;

        ReportProgress("Complete", 1.0f);
        LogMessage(3, "Processing complete!");

        return IM_SUCCESS;

    } catch (const std::bad_alloc& e) {
        SetLastError("Out of memory during mesh processing");
        return IM_ERROR_OUT_OF_MEMORY;
    } catch (const std::exception& e) {
        std::ostringstream oss;
        oss << "Exception during processing: " << e.what();
        SetLastError(oss.str().c_str());
        return IM_ERROR_EXCEPTION;
    } catch (...) {
        SetLastError("Unknown exception during mesh processing");
        return IM_ERROR_EXCEPTION;
    }
}

IMWRAPPER_API const char* IMWRAPPER_CALL IM_GetLastError(void) {
    return g_error_buffer;
}

IMWRAPPER_API void IMWRAPPER_CALL IM_SetLogLevel(int level) {
    g_log_level = level;
}

IMWRAPPER_API void IMWRAPPER_CALL IM_SetProgressCallback(IMProgressCallback callback) {
    g_progress_callback = callback;
}

IMWRAPPER_API void IMWRAPPER_CALL IM_Cleanup(void) {
    std::lock_guard<std::mutex> lock(g_init_mutex);

    if (g_tbb_scheduler) {
        delete g_tbb_scheduler;
        g_tbb_scheduler = nullptr;
    }
}

} // extern "C"
