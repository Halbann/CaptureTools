@echo off
echo This is a handy bat script to quickly get encoder info. First it will ask ffmpeg for a list of available encoders, then it will prompt you to type the name of one of them for information about arguments. You can repeat the process as many times as you need.
pause

:mainLoop
cls
set "encoder="  REM Reset the variable before reading input

ffmpeg.exe -encoders

set /p encoder=Enter encoder name (or leave blank to exit): 
if "%encoder%"=="" goto end

cls
ffmpeg.exe -h encoder=%encoder%

echo.
echo Press Enter to continue...
pause >nul

goto mainLoop

:end