@echo off
setlocal
cd /d "%~dp0"
echo Creation de C-Menu independant pour Windows x64...
dotnet publish "C-Menu.vbproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "C:\Menu\C-Menu"
if errorlevel 1 (
    echo ERREUR : publication impossible. Verifiez le SDK .NET 6 et les droits d'ecriture.
    pause
    exit /b 1
)
if not exist "C:\Menu\C-Menu\C-Menu.exe" (
    echo ERREUR : C-Menu.exe introuvable.
    pause
    exit /b 1
)
echo Creation du raccourci sur le Bureau...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$w=New-Object -ComObject WScript.Shell; $s=$w.CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'),'C-Menu.lnk')); $s.TargetPath='C:\Menu\C-Menu\C-Menu.exe'; $s.WorkingDirectory='C:\Menu\C-Menu'; $s.IconLocation='C:\Menu\C-Menu\C-Menu.exe,0'; $s.Save()"
if errorlevel 1 echo ATTENTION : impossible de creer le raccourci.
echo.
echo C-Menu.exe : C:\Menu\C-Menu\C-Menu.exe
echo Base distribuee : %%LOCALAPPDATA%%\C-Menu-Distribue\CMenu.db
echo La base vierge sera creee au premier lancement.
explorer /select,"C:\Menu\C-Menu\C-Menu.exe"
pause
