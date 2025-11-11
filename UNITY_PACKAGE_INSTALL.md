# Installing Instant Meshes in Unity

There are three ways to install Instant Meshes in your Unity project:

## Method 1: Unity Package Manager (Git URL) - Recommended

1. Open your Unity project
2. Go to **Window → Package Manager**
3. Click the **+** button in the top-left
4. Select **Add package from git URL**
5. Enter: `https://github.com/Autarkis/instant-meshes.git?path=/unity`
6. Click **Add**

Unity will automatically:
- Download the package
- Import the C# scripts
- Configure the DLL for Windows x64

**Note**: You still need to manually download the DLL from the Releases page and place it in `Assets/Plugins/x86_64/`

## Method 2: Release Package (Simplest)

1. Go to the [Releases page](https://github.com/Autarkis/instant-meshes/releases)
2. Download the latest `InstantMeshes-Unity-vX.X.X-Windows-x64.zip`
3. Extract the contents to your Unity project's `Assets/` folder

This includes:
- ✅ Pre-configured DLL
- ✅ C# scripts
- ✅ Documentation
- ✅ Examples

## Method 3: Manual Installation

### Step 1: Build or Download the DLL

**Option A: Download from Releases**
- Get the latest release from the [Releases page](https://github.com/Autarkis/instant-meshes/releases)

**Option B: Build from source**
```bash
git clone --recursive https://github.com/Autarkis/instant-meshes
cd instant-meshes
python build_wrapper.py
```

### Step 2: Copy Files to Unity

Create this folder structure in your Unity project:

```
Assets/
├── Plugins/
│   ├── x86_64/
│   │   └── instant_meshes_wrapper.dll
│   └── instant_meshes_wrapper.h
└── Scripts/
    └── InstantMeshes/
        ├── InstantMeshesAPI.cs
        └── Example_BuildingMeshOptimizer.cs (optional)
```

### Step 3: Configure DLL Import Settings

1. Select `instant_meshes_wrapper.dll` in the Project window
2. In the Inspector:
   - **Platform**: Windows x64
   - **CPU**: x86_64
   - **Load on startup**: ☑
3. Click **Apply**

## Verification

Test the installation:

```csharp
using UnityEngine;
using InstantMeshes;

public class TestInstantMeshes : MonoBehaviour
{
    void Start()
    {
        // Check version
        InstantMeshesAPI.GetVersion(out int major, out int minor, out int patch);
        Debug.Log($"Instant Meshes version: {major}.{minor}.{patch}");

        // Test with a simple mesh
        Mesh cube = GetComponent<MeshFilter>().mesh;
        Mesh optimized = InstantMeshesAPI.OptimizeMesh(cube);

        if (optimized != null)
        {
            Debug.Log("Success! Instant Meshes is working.");
        }
    }
}
```

## Troubleshooting

### "DllNotFoundException: instant_meshes_wrapper"

**Fix**:
1. Ensure DLL is in `Assets/Plugins/x86_64/`
2. Check DLL platform settings (Windows x64, CPU: x86_64)
3. Restart Unity Editor

### "BadImageFormatException"

**Fix**:
- Unity must be 64-bit
- DLL must be x64 (not x86)
- Set Platform to "Standalone" Windows

### "EntryPointNotFoundException"

**Fix**: DLL version mismatch - download latest release

## Platform Support

Currently supported:
- ✅ **Windows x64 Standalone** (Editor and Build)

Coming soon:
- macOS (Intel/Apple Silicon)
- Linux

Not planned:
- Mobile (iOS/Android) - compute-intensive, not suitable for mobile

## Next Steps

See [UNITY_INTEGRATION.md](UNITY_INTEGRATION.md) for:
- Complete API reference
- Parameter tuning guide
- Performance optimization
- Advanced examples
