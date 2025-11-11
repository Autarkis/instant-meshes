# Instant Meshes for Unity

High-quality quad-dominant mesh optimization for Unity, powered by the Instant Meshes field-aligned remeshing algorithm.

## Installation via Unity Package Manager

1. Open Unity Package Manager (Window → Package Manager)
2. Click the **+** button → **Add package from git URL**
3. Enter: `https://github.com/Autarkis/instant-meshes.git?path=/unity`
4. Click **Add**

**IMPORTANT**: After installation, you need to manually add the DLL:
1. Download the latest release from: https://github.com/Autarkis/instant-meshes/releases
2. Extract `instant_meshes_wrapper.dll`
3. Place it in: `Assets/Plugins/x86_64/`

## Quick Start

```csharp
using InstantMeshes;

Mesh optimizedMesh = InstantMeshesAPI.OptimizeMesh(inputMesh);
```

## Features

- **50-80% triangle reduction** for typical building meshes
- **Fast processing**: <1 second for 20k triangles
- **Quad-dominant output** ideal for architecture
- **Field-aligned** mesh generation
- **Sharp edge preservation**

## Requirements

- Unity 2020.3+
- Windows x64 (macOS/Linux coming soon)

## Documentation

For complete API reference, examples, and troubleshooting, see:
- Repository: https://github.com/Autarkis/instant-meshes
- Full docs in parent repository

## License

BSD-3-Clause
