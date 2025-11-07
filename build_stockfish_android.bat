@echo off
REM Build Stockfish Android Library
REM Requires Android NDK to be installed and NDK path set

set NDK_PATH=C:\Users\%USERNAME%\AppData\Local\Android\Sdk\ndk\25.1.8937393
set STOCKFISH_SRC=%~dp0Assets\Stockfish\stockfish\src

if not exist "%NDK_PATH%" (
    echo Android NDK not found at %NDK_PATH%
    echo Please install Android NDK and update the path in this script
    pause
    exit /b 1
)

cd "%STOCKFISH_SRC%"

REM Build for ARM64
echo Building for ARM64...
call "%NDK_PATH%\ndk-build" NDK_PROJECT_PATH=. APP_BUILD_SCRIPT=Android.mk NDK_APPLICATION_MK=Application.mk APP_ABI=arm64-v8a

if %errorlevel% neq 0 (
    echo Build failed for ARM64
    pause
    exit /b 1
)

REM Build for ARMv7
echo Building for ARMv7...
call "%NDK_PATH%\ndk-build" NDK_PROJECT_PATH=. APP_BUILD_SCRIPT=Android.mk NDK_APPLICATION_MK=Application.mk APP_ABI=armeabi-v7a

if %errorlevel% neq 0 (
    echo Build failed for ARMv7
    pause
    exit /b 1
)

echo Build completed successfully!
echo Copying libraries to Unity plugin...

REM Copy libraries to Unity Android plugin
if not exist "%~dp0Assets\Plugins\Android\StockfishAndroid\src\main\jniLibs" mkdir "%~dp0Assets\Plugins\Android\StockfishAndroid\src\main\jniLibs"
if not exist "%~dp0Assets\Plugins\Android\StockfishAndroid\src\main\jniLibs\arm64-v8a" mkdir "%~dp0Assets\Plugins\Android\StockfishAndroid\src\main\jniLibs\arm64-v8a"
if not exist "%~dp0Assets\Plugins\Android\StockfishAndroid\src\main\jniLibs\armeabi-v7a" mkdir "%~dp0Assets\Plugins\Android\StockfishAndroid\src\main\jniLibs\armeabi-v7a"

copy "libs\arm64-v8a\libstockfish.so" "%~dp0Assets\Plugins\Android\StockfishAndroid\src\main\jniLibs\arm64-v8a\"
copy "libs\armeabi-v7a\libstockfish.so" "%~dp0Assets\Plugins\Android\StockfishAndroid\src\main\jniLibs\armeabi-v7a\"

echo Done! Libraries copied to Unity Android plugin.
pause