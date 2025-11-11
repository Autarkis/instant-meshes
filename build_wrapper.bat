@echo off
REM Build script for Instant Meshes Unity Wrapper (Windows)
REM Requires Visual Studio 2015 or later and CMake

echo ========================================
echo Instant Meshes Unity Wrapper Build
echo ========================================
echo.

REM Check if submodules are initialized
if not exist "ext\nanogui\ext\glfw" (
    echo ERROR: Submodules not initialized!
    echo Please run: git submodule update --init --recursive
    pause
    exit /b 1
)

REM Create build directory
if not exist "build_wrapper" mkdir build_wrapper
cd build_wrapper

echo.
echo Generating Visual Studio solution...
cmake -G "Visual Studio 16 2019" -A x64 -DCMAKE_BUILD_TYPE=Release -f ..\CMakeLists_Wrapper.txt
if errorlevel 1 (
    echo ERROR: CMake generation failed!
    cd ..
    pause
    exit /b 1
)

echo.
echo Building Release configuration...
cmake --build . --config Release
if errorlevel 1 (
    echo ERROR: Build failed!
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Output DLL location:
echo   build_wrapper\bin\Release\instant_meshes_wrapper.dll
echo.
echo Copy this file to your Unity project:
echo   Assets\Plugins\x86_64\instant_meshes_wrapper.dll
echo.
pause
