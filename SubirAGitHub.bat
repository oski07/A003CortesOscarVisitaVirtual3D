@echo off
cd /d "%~dp0"
set /p GHUSER=Usuari de GitHub: 
git init -b main
git add -A
git commit -m "A003 Visita Virtual 3D - GINEBRO GAMES"
git remote remove origin 2>nul
git remote add origin https://github.com/%GHUSER%/A003CortesOscarVisitaVirtual3D.git
git push -u origin main
pause
