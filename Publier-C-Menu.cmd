@echo off
setlocal
cd /d "%~dp0"
echo Publication de C-Menu pour Windows x64...
dotnet publish "C-Menu.vbproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "C:\Menu\C-Menu"
if errorlevel 1 (
    echo.
    echo ERREUR : la publication a echoue. Verifiez le SDK .NET 6 et les droits d'ecriture.
    pause
    exit /b 1
)
echo.
echo Application publiee dans C:\Menu\C-Menu
echo Lancez C:\Menu\C-Menu\C-Menu.exe
explorer "C:\Menu\C-Menu"
pause
