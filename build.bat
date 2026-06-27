@echo off
rem Tetris をビルドするバッチファイル。
rem 使い方:
rem   build.bat            … Release 構成でビルド
rem   build.bat Debug      … 構成を指定してビルド
setlocal
cd /d "%~dp0"

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

echo Building Tetris (%CONFIG%) ...
dotnet build Tetris.sln -c %CONFIG%
pause
exit /b %errorlevel%
