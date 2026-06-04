@echo off
echo ==============================================
echo Building Docker image for DacSanViet app...
echo ==============================================

docker build -t dacsanviet-app:latest .

if %errorlevel% neq 0 (
    echo [ERROR] Failed to build docker image.
    pause
    exit /b %errorlevel%
)

echo.
echo ==============================================
echo Exporting Docker image to .tar file...
echo ==============================================

docker save -o dacsanviet-app.tar dacsanviet-app:latest

if %errorlevel% neq 0 (
    echo [ERROR] Failed to export docker image.
    pause
    exit /b %errorlevel%
)

echo.
echo [SUCCESS] Image successfully built and exported to dacsanviet-app.tar
pause
