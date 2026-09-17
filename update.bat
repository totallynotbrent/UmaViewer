@echo off
setlocal
rem quick manual updater: pick a channel, download the release zip, unpack over this folder.
rem all web work runs in one powershell -Command block with TLS12 forced; cmd only
rem handles variables, so no for/f parsing or nested quotes can explode.
set "ROOT=%~dp0"
set "REPO=totallynotbrent/UmaViewer"
set "TMPZIP=%TEMP%\uma_viewer_update.zip"
set "SHASIDE=%TEMP%\uma_viewer.sha256"

:menu
echo UmaViewer updater
echo 1) stable release (latest non-prerelease)
echo 2) experimental (camera-and-postfx prerelease)
set /p CH=Pick a channel: 
if "%CH%"=="2" (set "TAG=camera-and-postfx") else (if "%CH%"=="1" (set "TAG=") else (goto menu))

if not "%TAG%"=="" (
  echo Resolving experimental release %TAG% ...
  powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; $r=Invoke-RestMethod 'https://api.github.com/repos/%REPO%/releases/tags/%TAG%'; $a=$r.assets ^| Where-Object {$_.name -eq 'UmaViewer-Windows-x64.zip'}; if($a){$a.browser_download_url; $r.assets ^| Where-Object {$_.name -eq 'UmaViewer-Windows-x64.zip.sha256'} ^| ForEach-Object{$_.browser_download_url}}" > "%TEMP%\uma_url.txt" 2>"%TEMP%\uma_err.txt"
) else (
  echo Resolving latest stable release ...
  powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; $r=Invoke-RestMethod 'https://api.github.com/repos/%REPO%/releases/latest'; $a=$r.assets ^| Where-Object {$_.name -eq 'UmaViewer-Windows-x64.zip'}; if($a){$a.browser_download_url; $r.assets ^| Where-Object {$_.name -eq 'UmaViewer-Windows-x64.zip.sha256'} ^| ForEach-Object{$_.browser_download_url}}" > "%TEMP%\uma_url.txt" 2>"%TEMP%\uma_err.txt"
)

if not exist "%TEMP%\uma_url.txt" goto fail
set /p ZURL=<"%TEMP%\uma_url.txt"
if "%ZURL%"=="" (
  echo Could not find a release asset for that channel.
  type "%TEMP%\uma_err.txt"
  goto fail
)

echo Downloading %ZURL% ...
powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest '%ZURL%' -OutFile '%TMPZIP%'" 2>>"%TEMP%\uma_err.txt"
if not exist "%TMPZIP%" goto fail

echo Verifying sha256 against the release sidecar ...
set "SIDELINE="
if exist "%TEMP%\uma_url.txt" (
  powershell -NoProfile -Command "$u=Get-Content '%TEMP%\uma_url.txt' ^| Select-Object -Skip 1 -First 1; if($u){[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest $u.Trim() -OutFile '%SHASIDE%'}" 2>nul
)
if exist "%SHASIDE%" (
  set /p SIDELINE=<"%SHASIDE%"
) else (
  echo No sha256 sidecar reachable; skipping hash check.
)

if not "%SIDELINE%"=="" (
  echo %SIDELINE%>"%TEMP%\uma_want.txt"
  powershell -NoProfile -Command "$w=(Get-Content '%TEMP%\uma_want.txt' -First 1) -replace '\s.*',''; $g=(Get-FileHash '%TMPZIP%' -Algorithm SHA256).Hash.ToLower(); if($w.ToLower() -ne $g){Write-Host ('sha256 mismatch, aborting. want='+$w+' got='+$g); exit 1}" 2>>"%TEMP%\uma_err.txt"
  if errorlevel 1 goto fail
  echo sha256 OK
)

echo Unpacking over %ROOT%
powershell -NoProfile -Command "$s='%TEMP%\uma_viewer_update_stage'; if(Test-Path $s){Remove-Item $s -Recurse -Force}; Expand-Archive -LiteralPath '%TMPZIP%' -DestinationPath $s -Force; Copy-Item -Path (Join-Path $s '*') -Destination '%ROOT%' -Recurse -Force; Remove-Item $s -Recurse -Force" 2>>"%TEMP%\uma_err.txt"
if errorlevel 1 goto fail

del "%TMPZIP%" 2>nul
del "%SHASIDE%" 2>nul
del "%TEMP%\uma_url.txt" 2>nul
del "%TEMP%\uma_err.txt" 2>nul
del "%TEMP%\uma_want.txt" 2>nul

echo Update applied. Start UmaViewer.exe when ready.
pause
exit /b 0

:fail
echo.
echo Update FAILED. Details above (and in %%TEMP%%\uma_err.txt).
pause
exit /b 1