# Changelog

All notable changes to the Instant Meshes Unity package will be documented in this file.

## [1.0.0] - 2025-11-11

### Added
- Initial release of Unity wrapper for Instant Meshes
- C API wrapper for cross-language compatibility
- Unity C# P/Invoke integration
- Windows x64 DLL build
- Complete API for mesh optimization
- Parameter control for advanced users
- Progress callback support
- Error handling and validation
- Example component: BuildingMeshOptimizer
- Comprehensive documentation
- GitHub Actions build workflow
- Release package workflow

### Features
- Quad-dominant mesh generation
- 50-80% typical triangle reduction
- Field-aligned remeshing
- Sharp edge preservation (crease angle control)
- Pure quad or quad-dominant output
- Multi-threaded processing via Intel TBB
- Memory-safe caller-allocated buffers
- ABI versioning for compatibility

### Platform Support
- Windows x64 (Standalone and Editor)
- Unity 2020.3+

### Performance
- Linear time complexity O(n)
- <1 second for 20k triangle meshes
- Parallel processing via TBB

### Known Limitations
- Windows x64 only (macOS/Linux support planned)
- No mobile support (compute-intensive)
- UV coordinates not preserved
- Vertex colors not preserved
