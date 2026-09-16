@echo off
setlocal EnableDelayedExpansion
rem quick manual updater: pick a channel, download the release zip, unpack over this folder.
set "ROOT=%~dp0"
set "REPO=totallynotbrent/UmaViewer"
set "API=https://api.github.com/repos/%REPO%/releases"
set "TMPZIP=%TEMP%\uma_viewer_update.zip"
set "STAGE=%TEMP%\uma_viewer_update_stage"

echo UmaViewer updater
echo 1) stable release
echo 2) experimental (prerelease)
choice /C 12 /M "Pick a channel"
if "%ERRORLEVEL%"=="2" (set "CH=2") else (set "CH=1")

if "%CH%"=="2" (
  echo Resolving latest experimental release...
  for /f "delims=" %%u in ('powershell -NoProfile -Command "(Invoke-RestMethod '%API%' -Headers @{'User-Agent'='uma-update'} | Where-Object prerelease -eq $true | Select-Object -First 1).assets | Where-Object name -like 'UmaViewer-Windows-x64.zip' | Select-Object -First 1 -ExpandProperty browser_download_url"') do set "ZURL=%%u"
) else (
  echo Resolving latest stable release...
  for /f "delims=" %%u in ('powershell -NoProfile -Command "(Invoke-RestMethod '%API%/latest' -Headers @{'User-Agent'='uma-update'}).assets | Where-Object name -like 'UmaViewer-Windows-x64.zip' | Select-Object -First 1 -ExpandProperty browser_download_url"') do set "ZURL=%%u"
)

if "%ZURL%"=="" (
  echo Could not find a release asset for that channel.
  pause
  exit /b 1
)

echo Downloading %ZURL%
curl -sL -o "%TMPZIP%" "%ZURL%"
if not exist "%TMPZIP%" (
  echo Download failed.
  pause
  exit /b 1
)

echo Verifying sha256 against the release sidecar...
curl -sL -o "%TEMP%\uma_viewer.sha256" "%ZURL%.sha256" >nul 2>&1
for /f "delims=" %%h in ('type "%TEMP%\uma_viewer.sha256"') do set "WANT=%%h"
for /f "delims=" %%h in ('certutil -hashfile "%TMPZIP%" SHA256 ^| findstr /R "^[0-9a-fA-F]*$"') do set "GOT=%%h"
if not "%WANT:~0,64%"=="%GOT%" (
  echo sha256 mismatch, aborting.
  pause
  exit /b 1
)

echo Unpacking over %ROOT%
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
powershell -NoProfile -Command "Expand-Archive -LiteralPath '%TMPZIP%' -DestinationPath '%STAGE%' -Force"
xcopy /E /I /Y "%STAGE%\*" "%ROOT%"
rmdir /S /Q "%STAGE%" 2>nul
del "%TMPZIP%" 2>nul
del "%TEMP%\uma_viewer.sha256" 2>nul

echo Update applied. Start UmaViewer.exe when ready.
pause