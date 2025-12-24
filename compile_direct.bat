@echo off
setlocal

set RIMWORLD_PATH=D:\Games\SteamLibrary\steamapps\common\RimWorld
if not exist "%RIMWORLD_PATH%\RimWorldWin64_Data\Managed\Assembly-CSharp.dll" (
    set RIMWORLD_PATH=D:\Games\_Install\Rimworld
)

set MANAGED_PATH=%RIMWORLD_PATH%\RimWorldWin64_Data\Managed

set CSC_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe
if not exist "%CSC_PATH%" set CSC_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe
if not exist "%CSC_PATH%" set CSC_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe
if not exist "%CSC_PATH%" set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC_PATH%" set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

echo ========================================
echo Компиляция ProductionFlow Mod
echo ========================================
echo.

if not exist "%MANAGED_PATH%\Assembly-CSharp.dll" (
    echo Ошибка: Не найден Assembly-CSharp.dll
    echo Путь: %MANAGED_PATH%
    pause
    exit /b 1
)

if not exist "%CSC_PATH%" (
    echo Ошибка: Не найден компилятор C#
    pause
    exit /b 1
)

if not exist "Assemblies" mkdir Assemblies

set NETSTANDARD_PATH=%MANAGED_PATH%\netstandard.dll
set IMGUI_PATH=%MANAGED_PATH%\UnityEngine.IMGUIModule.dll
set TEXT_PATH=%MANAGED_PATH%\UnityEngine.TextRenderingModule.dll
set REF_ARGS=/reference:"%MANAGED_PATH%\Assembly-CSharp.dll" /reference:"%MANAGED_PATH%\UnityEngine.CoreModule.dll" /reference:System.dll /reference:System.Core.dll
if exist "%NETSTANDARD_PATH%" set REF_ARGS=%REF_ARGS% /reference:"%NETSTANDARD_PATH%"
if exist "%IMGUI_PATH%" set REF_ARGS=%REF_ARGS% /reference:"%IMGUI_PATH%"
if exist "%TEXT_PATH%" set REF_ARGS=%REF_ARGS% /reference:"%TEXT_PATH%"

echo Компиляция...
"%CSC_PATH%" /target:library /out:Assemblies\ProductionFlow.dll %REF_ARGS% ^
/nologo /optimize+ /langversion:latest ^
Source\ProductionFlow\ProductionFlowMod.cs ^
Source\ProductionFlow\MainTabWindow_ProductionFlow.cs ^
Source\ProductionFlow\Properties\AssemblyInfo.cs

if errorlevel 1 (
    echo.
    echo Ошибка компиляции!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Компиляция успешна!
echo DLL создан: Assemblies\ProductionFlow.dll
echo ========================================
echo.
pause

