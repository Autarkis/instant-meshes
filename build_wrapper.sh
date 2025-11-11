#!/bin/bash
# Build script for Instant Meshes Unity Wrapper (macOS/Linux)

echo "========================================"
echo "Instant Meshes Unity Wrapper Build"
echo "========================================"
echo ""

# Check if submodules are initialized
if [ ! -d "ext/nanogui/ext/glfw" ]; then
    echo "ERROR: Submodules not initialized!"
    echo "Please run: git submodule update --init --recursive"
    exit 1
fi

# Detect platform
if [[ "$OSTYPE" == "darwin"* ]]; then
    PLATFORM="macOS"
    LIB_EXT="dylib"
else
    PLATFORM="Linux"
    LIB_EXT="so"
fi

echo "Building for $PLATFORM..."
echo ""

# Create build directory
mkdir -p build_wrapper
cd build_wrapper

# Generate Makefiles
echo "Generating Makefiles..."
cmake -DCMAKE_BUILD_TYPE=Release -f ../CMakeLists_Wrapper.txt
if [ $? -ne 0 ]; then
    echo "ERROR: CMake generation failed!"
    exit 1
fi

# Build
echo ""
echo "Building..."
make -j$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed!"
    exit 1
fi

cd ..

echo ""
echo "========================================"
echo "Build Complete!"
echo "========================================"
echo ""
echo "Output library location:"
echo "  build_wrapper/libinstant_meshes_wrapper.$LIB_EXT"
echo ""
echo "Copy this file to your Unity project:"
if [[ "$OSTYPE" == "darwin"* ]]; then
    echo "  Assets/Plugins/macOS/libinstant_meshes_wrapper.dylib"
else
    echo "  Assets/Plugins/Linux/libinstant_meshes_wrapper.so"
fi
echo ""
