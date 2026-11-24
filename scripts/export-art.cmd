@echo off
setlocal
if "%~1"=="" (
    echo Usage: export-art.cmd OUTPUT_DIRECTORY
    exit /b 2
)
set "artOutput=%~f1"
set "artSource="
for /d %%I in ("%~dp0..\*") do if exist "%%~fI\macOS\ExportArt.cs" set "artSource=%%~fI"
if not defined artSource exit /b 1
pushd "%artSource%" || exit /b 1
set "artCsc=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%artCsc%" set "artCsc=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
for %%I in ("%artCsc%") do set "artWpf=%%~dpIWPF"
set "artTool=%TEMP%\PawPaw-Art-%RANDOM%-%RANDOM%.exe"
"%artCsc%" /nologo /unsafe /optimize+ /codepage:65001 /resource:ChineseWords.tsv,ChineseWords.tsv /lib:"%artWpf%" /r:UIAutomationClient.dll,UIAutomationTypes.dll,WindowsBase.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll /main:KeyboardCat.ExportArt /out:"%artTool%" *.cs macOS\ExportArt.cs
if errorlevel 1 (popd & exit /b 1)
"%artTool%" "%artOutput%"
set "artResult=%ERRORLEVEL%"
del "%artTool%"
popd
exit /b %artResult%
