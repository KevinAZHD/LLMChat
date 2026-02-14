@echo off
chcp 65001 >nul 2>&1
title LLMChat - Build ^& Run
color 0A

echo ╔══════════════════════════════════════════╗
echo ║          LLMChat - Build ^& Run           ║
echo ╚══════════════════════════════════════════╝
echo.

:: Detectar directorio del script
cd /d "%~dp0"

:: Verificar que dotnet está instalado
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    color 0C
    echo [ERROR] No se encontró 'dotnet'. Instala el .NET SDK desde:
    echo         https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

echo [1/3] Restaurando dependencias...
dotnet restore --verbosity quiet
if %ERRORLEVEL% neq 0 (
    color 0C
    echo [ERROR] Fallo al restaurar dependencias.
    pause
    exit /b 1
)
echo       ✓ Dependencias restauradas
echo.

echo [2/3] Compilando proyecto...
dotnet build -c Debug -f net10.0-windows10.0.19041.0 --no-restore -v quiet
if %ERRORLEVEL% neq 0 (
    color 0C
    echo [ERROR] Fallo al compilar. Revisa los errores arriba.
    pause
    exit /b 1
)
echo       ✓ Compilación exitosa
echo.

echo [3/3] Buscando ejecutable...

:: Buscar el .exe generado
set "EXE_PATH="
for /r "bin\Debug" %%f in (LLMChat.exe) do (
    if exist "%%f" (
        set "EXE_PATH=%%f"
    )
)

if "%EXE_PATH%"=="" (
    color 0C
    echo [ERROR] No se encontró LLMChat.exe en bin\Debug
    echo         Intenta compilar manualmente con Visual Studio.
    pause
    exit /b 1
)

echo       ✓ Encontrado: %EXE_PATH%
echo.
echo ════════════════════════════════════════════
echo  Iniciando LLMChat...
echo ════════════════════════════════════════════
echo.

:: Ejecutar la aplicación
start "" "%EXE_PATH%"

echo La aplicación se ha iniciado en una ventana separada.
echo Puedes cerrar esta consola.
echo.
timeout /t 5 >nul
