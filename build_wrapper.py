#!/usr/bin/env python3
"""
Build script for Instant Meshes Unity Wrapper
Handles CMake configuration and building on Windows with Visual Studio
"""

import os
import sys
import subprocess
import shutil
from pathlib import Path

# Configuration
SCRIPT_DIR = Path(__file__).parent.absolute()
BUILD_DIR = SCRIPT_DIR / "wrapper_build"
CMAKE_FILE = SCRIPT_DIR / "CMakeLists_Wrapper.txt"
VS_PATH = r"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

# Colors for terminal output
class Colors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'

def print_header(msg):
    print(f"\n{Colors.HEADER}{Colors.BOLD}{'='*60}{Colors.ENDC}")
    print(f"{Colors.HEADER}{Colors.BOLD}{msg}{Colors.ENDC}")
    print(f"{Colors.HEADER}{Colors.BOLD}{'='*60}{Colors.ENDC}\n")

def print_success(msg):
    print(f"{Colors.OKGREEN}[OK] {msg}{Colors.ENDC}")

def print_error(msg):
    print(f"{Colors.FAIL}[ERROR] {msg}{Colors.ENDC}")

def print_info(msg):
    print(f"{Colors.OKCYAN}> {msg}{Colors.ENDC}")

def check_prerequisites():
    """Check if all required tools are available"""
    print_header("Checking Prerequisites")

    # Check if Visual Studio is installed
    if not Path(VS_PATH).exists():
        print_error(f"Visual Studio not found at: {VS_PATH}")
        print_info("Please install Visual Studio 2022 Community with 'Desktop development with C++'")
        return False
    print_success("Visual Studio 2022 found")

    # Check if CMake is available
    try:
        result = subprocess.run(["cmake", "--version"], capture_output=True, text=True)
        cmake_version = result.stdout.split('\n')[0]
        print_success(f"CMake found: {cmake_version}")
    except FileNotFoundError:
        print_error("CMake not found in PATH")
        print_info("Please install CMake from https://cmake.org/download/")
        return False

    # Check if submodules are initialized
    ext_dir = SCRIPT_DIR / "ext" / "nanogui" / "ext" / "glfw"
    if not ext_dir.exists():
        print_error("Git submodules not initialized")
        print_info("Run: git submodule update --init --recursive")
        return False
    print_success("Git submodules initialized")

    # Check if CMakeLists exists
    if not CMAKE_FILE.exists():
        print_error(f"CMakeLists not found: {CMAKE_FILE}")
        return False
    print_success("CMakeLists_Wrapper.txt found")

    return True

def setup_build_directory():
    """Create and setup build directory"""
    print_header("Setting Up Build Directory")

    # Create build directory if it doesn't exist
    BUILD_DIR.mkdir(exist_ok=True)
    print_success(f"Build directory: {BUILD_DIR}")

    # Copy CMakeLists to build directory
    dest_cmake = BUILD_DIR / "CMakeLists.txt"
    shutil.copy2(CMAKE_FILE, dest_cmake)
    print_success("CMakeLists.txt copied to build directory")

    return True

def run_cmake_configure():
    """Run CMake configuration with Visual Studio generator"""
    print_header("Configuring CMake")

    # Prepare CMake command
    cmake_cmd = [
        "cmake",
        "-G", "Visual Studio 17 2022",
        "-A", "x64",
        "-DCMAKE_BUILD_TYPE=Release",
        "."
    ]

    print_info(f"Command: {' '.join(cmake_cmd)}")
    print_info(f"Working directory: {BUILD_DIR}")

    # Run CMake configure
    try:
        result = subprocess.run(
            cmake_cmd,
            cwd=BUILD_DIR,
            capture_output=True,
            text=True,
            timeout=300  # 5 minute timeout
        )

        # Print output
        if result.stdout:
            print(result.stdout)

        if result.returncode != 0:
            print_error("CMake configuration failed")
            if result.stderr:
                print(result.stderr)
            return False

        print_success("CMake configuration completed")
        return True

    except subprocess.TimeoutExpired:
        print_error("CMake configuration timed out after 5 minutes")
        return False
    except Exception as e:
        print_error(f"Error running CMake: {e}")
        return False

def run_cmake_build():
    """Run CMake build"""
    print_header("Building Project")

    # Prepare build command
    build_cmd = [
        "cmake",
        "--build", ".",
        "--config", "Release",
        "--verbose"
    ]

    print_info(f"Command: {' '.join(build_cmd)}")
    print_info("This may take 5-10 minutes for the first build...")

    # Run build
    try:
        # Run with real-time output
        process = subprocess.Popen(
            build_cmd,
            cwd=BUILD_DIR,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1
        )

        # Print output in real-time
        for line in process.stdout:
            print(line, end='')

        process.wait()

        if process.returncode != 0:
            print_error(f"Build failed with exit code {process.returncode}")
            return False

        print_success("Build completed successfully")
        return True

    except Exception as e:
        print_error(f"Error during build: {e}")
        return False

def verify_output():
    """Verify that the DLL was built successfully"""
    print_header("Verifying Build Output")

    # Check for DLL
    dll_path = BUILD_DIR / "Release" / "instant_meshes_wrapper.dll"
    if not dll_path.exists():
        print_error(f"DLL not found at: {dll_path}")
        return False

    # Get DLL size
    dll_size = dll_path.stat().st_size / (1024 * 1024)  # Size in MB
    print_success(f"DLL found: {dll_path}")
    print_info(f"Size: {dll_size:.2f} MB")

    # Check for lib file
    lib_path = BUILD_DIR / "Release" / "instant_meshes_wrapper.lib"
    if lib_path.exists():
        print_success(f"Import library found: {lib_path}")

    return True

def print_final_instructions():
    """Print instructions for using the built DLL"""
    print_header("Build Complete!")

    dll_path = BUILD_DIR / "Release" / "instant_meshes_wrapper.dll"

    print(f"{Colors.OKGREEN}The wrapper DLL has been built successfully!{Colors.ENDC}\n")

    print(f"{Colors.BOLD}DLL Location:{Colors.ENDC}")
    print(f"  {dll_path}\n")

    print(f"{Colors.BOLD}Next Steps:{Colors.ENDC}")
    print(f"  1. Copy DLL to your Unity project:")
    print(f"     → YourUnityProject/Assets/Plugins/x86_64/instant_meshes_wrapper.dll\n")

    print(f"  2. Copy C# scripts:")
    print(f"     → {SCRIPT_DIR / 'unity' / 'InstantMeshesAPI.cs'}")
    print(f"     → {SCRIPT_DIR / 'unity' / 'Example_BuildingMeshOptimizer.cs'}\n")

    print(f"  3. In Unity Inspector, configure DLL settings:")
    print(f"     → Platform: Windows x64")
    print(f"     → CPU: x86_64")
    print(f"     → Load on startup: ✓\n")

    print(f"  4. Test in Unity:")
    print(f"     → Menu: Tools > Instant Meshes > About\n")

    print(f"{Colors.BOLD}Documentation:{Colors.ENDC}")
    print(f"  → Integration Guide: {SCRIPT_DIR / 'UNITY_INTEGRATION.md'}")
    print(f"  → Technical Details: {SCRIPT_DIR / 'ANALYSIS.md'}\n")

def main():
    """Main build process"""
    print_header("Instant Meshes Unity Wrapper Build Script")
    print(f"Script location: {SCRIPT_DIR}\n")

    # Run build steps
    steps = [
        ("Prerequisites", check_prerequisites),
        ("Build Directory", setup_build_directory),
        ("CMake Configure", run_cmake_configure),
        ("CMake Build", run_cmake_build),
        ("Verification", verify_output),
    ]

    for step_name, step_func in steps:
        if not step_func():
            print_error(f"Build failed at step: {step_name}")
            print_info("Check the error messages above for details")
            return 1

    # Print final instructions
    print_final_instructions()
    return 0

if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print(f"\n{Colors.WARNING}Build interrupted by user{Colors.ENDC}")
        sys.exit(1)
    except Exception as e:
        print_error(f"Unexpected error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
