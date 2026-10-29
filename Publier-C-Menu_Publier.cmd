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
echo.
echo Executable autonome : "%OUTPUT%\C-Menu_Publier.exe"
explorer /select,"%OUTPUT%\C-Menu_Publier.exe"
pause
