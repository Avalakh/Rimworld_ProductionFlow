# Скрипт компиляции мода ProductionFlow
param(
    [string]$RimWorldPath = ""
)

$ErrorActionPreference = "Stop"

# Поиск RimWorld
if ([string]::IsNullOrEmpty($RimWorldPath)) {
    $searchPaths = @(
        "D:\Games\SteamLibrary\steamapps\common\RimWorld",
        "D:\Games\_Install\Rimworld",
        "C:\Program Files (x86)\Steam\steamapps\common\RimWorld",
        "D:\SteamLibrary\steamapps\common\RimWorld",
        "E:\SteamLibrary\steamapps\common\RimWorld",
        "$env:LOCALAPPDATA\Programs\RimWorld",
        ".\..\..\..",
        ".\..\.."
    )
    
    foreach ($path in $searchPaths) {
        if (Test-Path $path) {
            $managedPath = Join-Path $path "RimWorldWin64_Data\Managed"
            if (Test-Path $managedPath) {
                $RimWorldPath = $path
                Write-Host "Найден RimWorld: $RimWorldPath" -ForegroundColor Green
                break
            }
        }
    }
}

if ([string]::IsNullOrEmpty($RimWorldPath) -or -not (Test-Path (Join-Path $RimWorldPath "RimWorldWin64_Data\Managed"))) {
    Write-Host "Ошибка: Не найден RimWorld. Укажите путь через параметр -RimWorldPath" -ForegroundColor Red
    Write-Host "Пример: .\build.ps1 -RimWorldPath 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'" -ForegroundColor Yellow
    exit 1
}

$managedPath = Join-Path $RimWorldPath "RimWorldWin64_Data\Managed"
$assemblyDll = Join-Path $managedPath "Assembly-CSharp.dll"
$unityDll = Join-Path $managedPath "UnityEngine.CoreModule.dll"
$outputDir = ".\Assemblies"
$outputDll = Join-Path $outputDir "ProductionFlow.dll"

# Проверка наличия DLL
$requiredDlls = @($assemblyDll, $unityDll)
foreach ($dll in $requiredDlls) {
    if (-not (Test-Path $dll)) {
        Write-Host "Ошибка: Не найден файл $dll" -ForegroundColor Red
        exit 1
    }
}

# Создание выходной директории
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Поиск компилятора C#
$cscPath = $null
$cscPaths = @(
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

foreach ($path in $cscPaths) {
    if (Test-Path $path) {
        $cscPath = $path
        break
    }
}

if ($null -eq $cscPath) {
    Write-Host "Ошибка: Не найден компилятор C# (csc.exe)" -ForegroundColor Red
    Write-Host "Установите .NET Framework SDK или Visual Studio" -ForegroundColor Yellow
    exit 1
}

Write-Host "Используется компилятор: $cscPath" -ForegroundColor Green

# Список исходных файлов
$sourceFiles = @(
    "Source\ProductionFlow\ProductionFlowMod.cs",
    "Source\ProductionFlow\MainTabWindow_ProductionFlow.cs",
    "Source\ProductionFlow\Properties\AssemblyInfo.cs"
)

# Полные пути к исходным файлам
$fullSourceFiles = $sourceFiles | ForEach-Object { 
    $fullPath = Join-Path $PSScriptRoot $_
    if (Test-Path $fullPath) {
        $fullPath
    } else {
        Write-Host "Предупреждение: Файл не найден: $fullPath" -ForegroundColor Yellow
        $null
    }
} | Where-Object { $null -ne $_ }

if ($fullSourceFiles.Count -eq 0) {
    Write-Host "Ошибка: Не найдены исходные файлы" -ForegroundColor Red
    exit 1
}

# Компиляция
Write-Host "Компиляция мода..." -ForegroundColor Green

$compileArgs = @(
    "/target:library",
    "/out:$outputDll",
    "/reference:$assemblyDll",
    "/reference:$unityDll",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/nologo",
    "/optimize+",
    "/langversion:latest"
) + $fullSourceFiles

& $cscPath $compileArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "Компиляция успешна! DLL создан: $outputDll" -ForegroundColor Green
} else {
    Write-Host "Ошибка компиляции!" -ForegroundColor Red
    exit 1
}

