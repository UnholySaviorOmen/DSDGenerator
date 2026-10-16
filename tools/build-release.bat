@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

rem ============================================================
rem  DSDGenerator release build
rem
rem  Usage:
rem    build-release.bat
rem
rem  Результат:
rem    build_artifacts/DSDGenerator-<version>-win-x64.zip
rem
rem  Что внутри:
rem    DSDGenerator.exe            — CLI
rem    DSDGenerator.*.dll          — наши библиотеки
rem    Microsoft.Extensions.*.dll  — рантайм-зависимости
rem    Assets/README.txt           — инструкция, куда класть esp2dsd.exe
rem
rem  ВАЖНО: esp2dsd.exe НЕ включён в артефакт.
rem  Его лицензия (CC BY-NC-ND 4.0) не позволяет распространять
rem  бинарник в составе сторонних сборок. Пользователь кладёт
rem  esp2dsd.exe в Assets/ вручную.
rem
rem  Скрипт ожидает, что лежит в <repo>\tools\build-release.bat.
rem ============================================================

set "ROOT=%~dp0.."
pushd "%ROOT%" || (echo Failed to cd to "%ROOT%" & exit /b 1)

set "PROPS=Directory.Build.props"
set "CLI_PROJECT=src\DSDGenerator.Cli\DSDGenerator.Cli.csproj"

echo === DSDGenerator release build ===
echo Root: %CD%
echo.

rem --- 1. Читаем версию из Directory.Build.props ---
if not exist "%PROPS%" (
    echo ERROR: %PROPS% not found.
    popd
    exit /b 1
)

set "VERSION="
for /f "usebackq delims=" %%L in (`powershell -NoProfile -Command ^
    "(Select-Xml -Path '%PROPS%' -XPath '//VersionPrefix').Node.InnerText.Trim()"`) do (
    set "VERSION=%%L"
)

if "!VERSION!"=="" (
    echo ERROR: could not read ^<VersionPrefix^> from %PROPS%.
    popd
    exit /b 1
)

echo Version: !VERSION!
echo.

set "ARTIFACT_DIR=build_artifacts\DSDGenerator-!VERSION!-win-x64"
set "ARTIFACT_ZIP=build_artifacts\DSDGenerator-!VERSION!-win-x64.zip"

rem --- 2. Готовим пустую папку для publish ---
if exist "%ARTIFACT_DIR%" rmdir /s /q "%ARTIFACT_DIR%"
if exist "%ARTIFACT_ZIP%" del /q "%ARTIFACT_ZIP%"
mkdir "%ARTIFACT_DIR%" || (echo Failed to create "%ARTIFACT_DIR%" & popd & exit /b 1)

echo Publishing CLI: %CLI_PROJECT%
dotnet publish "%CLI_PROJECT%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -o "%ARTIFACT_DIR%"
if errorlevel 1 (
    echo ERROR: dotnet publish failed.
    popd
    exit /b 1
)
echo.

rem --- 3. Проверяем, что exe на месте ---
set "CLI_EXE=%ARTIFACT_DIR%\DSDGenerator.exe"
if not exist "%CLI_EXE%" (
    echo ERROR: %CLI_EXE% not found after publish.
    popd
    exit /b 1
)

rem --- 4. Создаём Assets/README.txt ---
rem     Пользователь кладёт сюда esp2dsd.exe.
if not exist "%ARTIFACT_DIR%\Assets" mkdir "%ARTIFACT_DIR%\Assets"

> "%ARTIFACT_DIR%\Assets\README.txt" (
    echo DSDGenerator requires esp2dsd.exe in this folder.
    echo.
    echo DSDGenerator does not redistribute esp2dsd.exe because its
    echo license ^(CC BY-NC-ND 4.0^) does not permit bundling the
    echo binary into third-party distributions.
    echo.
    echo How to get esp2dsd.exe:
    echo   1. Get the source from the original repository
    echo      ^(Cutleast^'s esp2dsd^).
    echo   2. Build it with build.bat ^(requires Nuitka + MSVC^),
    echo      or obtain a prebuilt esp2dsd.exe from the author.
    echo   3. Copy esp2dsd.exe into this Assets/ folder next to
    echo      DSDGenerator.exe.
    echo.
    echo Make sure string_records.json is bundled with esp2dsd.exe
    echo if the build requires it at runtime.
)

rem --- 5. Пакуем в zip ---
echo Packing: %ARTIFACT_ZIP%
powershell -NoProfile -Command ^
    "Compress-Archive -Path '%ARTIFACT_DIR%\*' -DestinationPath '%ARTIFACT_ZIP%' -Force"
if errorlevel 1 (
    echo ERROR: Compress-Archive failed.
    popd
    exit /b 1
)

rem --- 6. Итог ---
for %%F in ("%ARTIFACT_ZIP%") do set "ZIP_SIZE=%%~zF"
set /a ZIP_SIZE_MB=!ZIP_SIZE! / 1048576

echo.
echo Done.
echo Zip: %ARTIFACT_ZIP%
echo Size: !ZIP_SIZE! bytes (~!ZIP_SIZE_MB! MB)
echo.
echo Reminder: users must place esp2dsd.exe in Assets/ before
echo running DSDGenerator.exe.
echo.

popd
endlocal
exit /b 0