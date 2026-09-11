#!/bin/bash
# Build the two native libraries that the NuGet packages ship for Windows and
# Linux but not macOS: libktx (the KTX2 texture reader) and libmeshoptimizer.
#
# Everything else the engine needs -- glfw, cimgui, nfd, stbi -- already has an
# arm64 build inside its package.
#
# Output lands in NativeLibs/osx-arm64/ and is copied next to the executable by
# IDKEngine.csproj. Run once; the results are checked in.

set -euo pipefail

here="$(cd "$(dirname "$0")/.." && pwd)"
work="${TMPDIR:-/tmp}/idk-natives"
arch="$(uname -m)"
out="$here/NativeLibs/osx-$arch"

mkdir -p "$work" "$out"

if [ ! -d "$work/meshoptimizer" ]; then
    git clone --depth 1 https://github.com/zeux/meshoptimizer.git "$work/meshoptimizer"
fi
cmake -S "$work/meshoptimizer" -B "$work/mo_build" \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES="$arch" \
    -DBUILD_SHARED_LIBS=ON
cmake --build "$work/mo_build" -j"$(sysctl -n hw.ncpu)"
cp "$work/mo_build/libmeshoptimizer.dylib" "$out/"

if [ ! -d "$work/KTX-Software" ]; then
    git clone --depth 1 --recurse-submodules --shallow-submodules \
        https://github.com/KhronosGroup/KTX-Software.git "$work/KTX-Software"
fi
cmake -S "$work/KTX-Software" -B "$work/ktx_build" \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES="$arch" \
    -DKTX_FEATURE_TESTS=OFF -DKTX_FEATURE_TOOLS=OFF \
    -DKTX_FEATURE_STATIC_LIBRARY=OFF -DKTX_FEATURE_DOC=OFF \
    -DKTX_FEATURE_LOADTEST_APPS=OFF
cmake --build "$work/ktx_build" -j"$(sysctl -n hw.ncpu)"
cp "$work/ktx_build/Release/libktx.dylib" "$out/"

for lib in "$out"/*.dylib; do
    install_name_tool -id "@rpath/$(basename "$lib")" "$lib"
done

echo "built into $out:"
ls -la "$out"
