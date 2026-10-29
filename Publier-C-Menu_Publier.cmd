@echo off
setlocal
cd /d "%~dp0"
set "OUTPUT=C:\Menu\C-Menu_Publier"
echo Publication autonome de C-Menu_Publier.exe...
dotnet publish "C-Menu.vbproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:CMenuAssemblyName=C-Menu_Publier -p:DefineConstants= -o "%OUTPUT%"
if errorlevel 1 (
  echo ERREUR : publication impossible.
  pause
  exit /b 1
)
if not exist "%OUTPUT%\C-Menu_Publier.exe" (
  echo ERREUR : executable introuvable.
  pause
  exit /b 1
)
echo Copie des sources necessaires au bouton Publier...
if not exist "%OUTPUT%\Sources" mkdir "%OUTPUT%\Sources"
robocopy "%~dp0" "%OUTPUT%\Sources" /E /XD .git .vs bin obj /XF *.db *.zip *.exe *.pdb /R:1 /W:1 >nul
if errorlevel 8 (
  echo ERREUR : copie des sources impossible.
  pause
  exit /b 1
)
echo.
echo Executable autonome : "%OUTPUT%\C-Menu_Publier.exe"
explorer /select,"%OUTPUT%\C-Menu_Publier.exe"
pause
