@echo off
echo ============================================
echo Building Stockfish for Android
echo ============================================

REM Check if NDK_ROOT is set
if "%NDK_ROOT%"=="" (
    echo ERROR: NDK_ROOT environment variable is not set
    echo.
    echo Please download Android NDK from:
    echo https://developer.android.com/ndk/downloads
    echo.
    echo Then set NDK_ROOT to point to your NDK installation, for example:
    echo set NDK_ROOT=C:\Android\android-ndk-r25c
    echo.
    pause
    exit /b 1
)

echo Using NDK: %NDK_ROOT%
echo.

REM Navigate to source directory
cd /d "%~dp0Assets\Stockfish\stockfish\src"

echo Cleaning previous build...
if exist libs rmdir /s /q libs
if exist obj rmdir /s /q obj

echo.
echo Building native library...
"%NDK_ROOT%\ndk-build" NDK_PROJECT_PATH=. APP_BUILD_SCRIPT=Android.mk NDK_APPLICATION_MK=Application.mk -j8

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Build successful!
echo ============================================
echo.

REM Create Unity plugin directory structure
set PLUGIN_DIR=%~dp0Assets\Plugins\Android\libs
echo Creating plugin directory: %PLUGIN_DIR%
if not exist "%PLUGIN_DIR%" mkdir "%PLUGIN_DIR%"

REM Copy libraries
echo Copying libraries to Unity project...
if exist libs\arm64-v8a (
    if not exist "%PLUGIN_DIR%\arm64-v8a" mkdir "%PLUGIN_DIR%\arm64-v8a"
    copy /Y libs\arm64-v8a\libstockfish.so "%PLUGIN_DIR%\arm64-v8a\"
    echo - Copied arm64-v8a library
)

if exist libs\armeabi-v7a (
    if not exist "%PLUGIN_DIR%\armeabi-v7a" mkdir "%PLUGIN_DIR%\armeabi-v7a"
    copy /Y libs\armeabi-v7a\libstockfish.so "%PLUGIN_DIR%\armeabi-v7a\"
    echo - Copied armeabi-v7a library
)

echo.
echo ============================================
echo All done! Libraries copied to Unity project.
echo ============================================
echo.
echo You can now build your Unity project for Android!
echo.
pause
