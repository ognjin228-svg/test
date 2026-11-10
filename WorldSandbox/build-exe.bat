@echo off
chcp 65001 >nul
cd /d "%~dp0"
where npm >nul 2>nul
if errorlevel 1 (
  echo Node.js не найден. Установи его с https://nodejs.org ^(версия LTS^) и запусти этот файл снова.
  pause
  exit /b 1
)
echo Устанавливаю зависимости, это займёт пару минут...
call npm install
if errorlevel 1 goto fail
echo Собираю WorldSandbox.exe...
call npm run dist
if errorlevel 1 goto fail
echo.
echo Готово! Файл лежит здесь: dist\WorldSandbox.exe
start "" "%~dp0dist"
pause
exit /b 0
:fail
echo Что-то пошло не так. Проверь интернет и запусти снова.
pause
exit /b 1
