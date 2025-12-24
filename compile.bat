@echo off
setlocal

if "%1"=="" (
    echo Использование: compile.bat "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
    echo.
    echo Укажите путь к установке RimWorld в качестве параметра.
    exit /b 1
)

set RIMWORLD_PATH=%1
set SCRIPT_DIR=%~dp0

powershell.exe -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1" -RimWorldPath "%RIMWORLD_PATH%"

if errorlevel 1 (
    echo.
    echo Ошибка компиляции!
    pause
    exit /b 1
)

echo.
echo Компиляция завершена успешно!
pause

