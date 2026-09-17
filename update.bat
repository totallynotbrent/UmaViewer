@echo off
setlocal EnableDelayedExpansion
rem quick manual updater: pick a channel, download the release zip, unpack over this folder.
rem powershell writes plain lines to temp files and cmd only reads them, so no
rem operator ever crosses the cmd/parser boundary; failures pause, not close.
set "ROOT=%~dp0"
set "REPO=totallynotbrent/UmaViewer"
set "TMPZIP=%TEMP%\uma_viewer_update.zip"
set "URLFILE=%TEMP%\uma_url.txt"
set "ERRFILE=%TEMP%\uma_err.txt"
set "PSARGS=-NoProfile -ExecutionPolicy Bypass -Command"

:menu
echo UmaViewer updater
echo 1) stable release (latest non-prerelease)
echo 2) experimental (rolling prerelease)
set /p CH=Pick a channel:
if "!CH!"=="2" (set "RELURL=https://api.github.com/repos/%REPO%/releases/tags/experimental") else (if "!CH!"=="1" (set "RELURL=https://api.github.com/repos/%REPO%/releases/latest") else (goto menu))

echo Resolving release ...
powershell %PSARGS% "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; $ErrorActionPreference='Stop'; $r=Invoke-RestMethod '!RELURL!'; $r.assets | ForEach-Object { $_.browser_download_url }" > "!URLFILE!" 2>"!ERRFILE!"
if errorlevel 1 goto fail

set "ZURL="
set "SURL="
set /p ZURL=<"!URLFILE!"
if "!ZURL!"=="" (
  echo Could not find a release asset for that channel.
  type "!ERRFILE!" 2>nul
  goto fail
)
set /p SURL=<nul
powershell %PSARGS% "$l=Get-Content '!URLFILE!'; if($l.Count -gt 1){$l[1]}" > "%TEMP%\uma_side.txt" 2>nul
set /p SURL=<"%TEMP%\uma_side.txt" 2>nul

echo Downloading !ZURL! ...
powershell %PSARGS% "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest '!ZURL!' -OutFile '!TMPZIP!'" 2>>"!ERRFILE!"
if errorlevel 1 goto fail
if not exist "!TMPZIP!" goto fail

set "SIDELINE="
if not "!SURL!"=="" (
  echo Verifying sha256 against the release sidecar ...
  powershell %PSARGS% "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest '!SURL!' -OutFile '%TEMP%\uma_viewer.sha256'" 2>>"!ERRFILE!"
  if not errorlevel 1 set /p SIDELINE=<"%TEMP%\uma_viewer.sha256"
)

if not "!SIDELINE!"=="" (
  powershell %PSARGS% "$w=(Get-Content '%TEMP%\uma_viewer.sha256' -First 1); $w=$w -replace '\s.*',''; $g=(Get-FileHash '!TMPZIP!' -Algorithm SHA256).Hash.ToLower(); if($w.Trim().ToLower() -ne $g){ Write-Host ('sha256 mismatch, aborting. want=' + $w + ' got=' + $g); exit 1 } else { Write-Host 'sha256 OK' }" 2>>"!ERRFILE!"
  if errorlevel 1 goto fail
) else (
  echo No sha256 sidecar on this release; skipping hash check.
)

echo Unpacking over !ROOT!
powershell %PSARGS% "$s='%TEMP%\uma_viewer_update_stage'; if(Test-Path $s){Remove-Item $s -Recurse -Force}; Expand-Archive -LiteralPath '!TMPZIP!' -DestinationPath $s -Force; Copy-Item -Path (Join-Path $s '*') -Destination '!ROOT!' -Recurse -Force; Remove-Item $s -Recurse -Force" 2>>"!ERRFILE!"
if errorlevel 1 goto fail

del "!TMPZIP!" 2>nul
del "!URLFILE!" 2>nul
del "!ERRFILE!" 2>nul
del "%TEMP%\uma_viewer.sha256" 2>nul
del "%TEMP%\uma_side.txt" 2>nul

echo Update applied. Start UmaViewer.exe when ready.
pause
exit /b 0

:fail
echo.
echo Update FAILED. Details above (and in %%TEMP%%\uma_err.txt).
pause
exit /b 1