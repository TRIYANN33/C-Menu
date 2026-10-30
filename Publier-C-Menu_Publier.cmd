@echo off
setlocal
cd /d "%~dp0"
set "OUTPUT=C:\Menu\C-Menu_Publier"
set "TEMP_OUTPUT=%TEMP%\CMenuPublier_Build_%RANDOM%_%RANDOM%"
if not exist "%TEMP_OUTPUT%" mkdir "%TEMP_OUTPUT%"
if errorlevel 1 goto publish_error
echo Publication autonome de C-Menu_Publier.exe...
dotnet publish "C-Menu.vbproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:CMenuAssemblyName=C-Menu_Publier -p:DefineConstants= -o "%TEMP_OUTPUT%"
if errorlevel 1 (
  goto publish_error
)
if not exist "%TEMP_OUTPUT%\C-Menu_Publier.exe" (
  echo ERREUR : executable introuvable.
  pause
  exit /b 1
)
echo Copie de l executable vers sa destination...
if not exist "%OUTPUT%" mkdir "%OUTPUT%"
if errorlevel 1 goto copy_error
copy /Y "%TEMP_OUTPUT%\C-Menu_Publier.exe" "%OUTPUT%\C-Menu_Publier.exe"
if errorlevel 1 goto locked_error
rmdir /S /Q "%TEMP_OUTPUT%" 2>nul
echo Copie des sources necessaires au bouton Publier...
if not exist "%OUTPUT%\Sources" mkdir "%OUTPUT%\Sources"
if errorlevel 1 goto copy_error
copy /Y "C-Menu.vbproj" "%OUTPUT%\Sources\C-Menu.vbproj"
if errorlevel 1 goto copy_error
copy /Y "Program.vb" "%OUTPUT%\Sources\Program.vb"
if errorlevel 1 goto copy_error
for %%D in (Forms Data Models Menu-Autonome) do (
  if not exist "%%D" goto copy_error
  xcopy "%%D\*" "%OUTPUT%\Sources\%%D\" /E /I /Y /Q
  if errorlevel 1 goto copy_error
)
echo Sources copiees avec succes.
goto copy_done
:locked_error
echo ERREUR : impossible de remplacer C-Menu_Publier.exe.
echo Fermez C-Menu_Publier et les autres instances du programme puis relancez ce script.
pause
exit /b 1
:publish_error
echo ERREUR : publication impossible.
pause
exit /b 1
:copy_error
echo ERREUR : copie des sources impossible. Verifiez les messages ci-dessus.
pause
exit /b 1
:copy_done
echo.
echo Executable autonome : "%OUTPUT%\C-Menu_Publier.exe"
explorer /select,"%OUTPUT%\C-Menu_Publier.exe"
pause
