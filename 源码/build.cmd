@echo off
setlocal
pushd "%~dp0"
set "catCompiler=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%catCompiler%" set "catCompiler=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
for %%I in ("%catCompiler%") do set "catWpf=%%~dpIWPF"
"%catCompiler%" /lib:"%catWpf%" /reference:UIAutomationClient.dll /reference:UIAutomationTypes.dll /reference:WindowsBase.dll /nologo /unsafe /target:winexe /platform:anycpu /optimize+ /codepage:65001 /resource:ChineseWords.tsv,ChineseWords.tsv /win32manifest:app.manifest /win32icon:cat.ico /out:"..\PawPaw-Windows.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Program.cs Cats.cs Outfit.cs Motion.cs WordTricks.cs ChineseInput.cs TrickArt.cs CatArt.cs CatFaces.cs CoatArt.cs GarmentFit.cs OutfitArt.cs KeyboardArt.cs Native.cs Preferences.cs WardrobeForm.cs CoverArt.cs InkLook.cs CatEditor.cs OpacityForm.cs PetForm.cs PasswordFocus.cs PetExpressions.cs CompanionArt.cs UnlockBadge.cs
set "catBuildExit=%ERRORLEVEL%"
popd
exit /b %catBuildExit%
