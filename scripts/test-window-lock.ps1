$ErrorActionPreference='Stop'
$exe=(Get-Item -LiteralPath (Join-Path $PSScriptRoot '..\PawPaw-Windows.exe')).FullName
$test=Join-Path ([IO.Path]::GetTempPath()) ('PawPaw-lock-'+[guid]::NewGuid().ToString('N')+'.exe')
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path $csc)){$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
try {
    & $csc /nologo /codepage:65001 /r:System.Drawing.dll,System.Windows.Forms.dll "/out:$test" (Join-Path $PSScriptRoot 'test-window-lock.cs')
    if($LASTEXITCODE){throw 'Compiling the lock test failed'}
    & $test $exe
    if($LASTEXITCODE){throw 'Desktop lock regression'}
} finally {if(Test-Path -LiteralPath $test){Remove-Item -LiteralPath $test}}
