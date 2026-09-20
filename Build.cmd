@echo off
setlocal
pushd "%~dp0"
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /out:MpcMovieTray.exe /win32manifest:src\app.manifest /win32icon:assets\app.ico /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll src\*.cs
if errorlevel 1 goto :failed
echo Built MpcMovieTray.exe successfully.
set "hash="
for /f "skip=1 tokens=1" %%H in ('certutil -hashfile MpcMovieTray.exe SHA256 ^| findstr /r /v /c:"CertUtil" /c:"^$"') do if not defined hash set "hash=%%H"
if not defined hash goto :nohash
>SHA256.txt echo %hash%  MpcMovieTray.exe
echo Updated SHA256.txt.
goto :done
:nohash
echo Note: SHA256.txt could not be updated.
goto :done
:failed
echo Build failed. Install the .NET Framework 4.8 developer pack if needed.
:done
popd
pause
