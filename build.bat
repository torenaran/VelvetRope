@echo off
setlocal
cd /d "%~dp0"
echo Building Velvet Rope 0.3.10 (Release)...
dotnet build VelvetRope.slnx -c Release
if errorlevel 1 exit /b %errorlevel%
echo.
echo Build complete.
echo Look under VelvetRope\bin\x64\Release for the plugin DLL/package output.
