@echo off
setlocal
pushd "%~dp0"
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /out:MpcMovieTray.exe /win32manifest:src\app.manifest /win32icon:assets\app.ico /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll src\*.cs
if errorlevel 1 (
  echo Build failed. Install the .NET Framework 4.8 developer pack if needed.
) else (
  echo Built MpcMovieTray.exe successfully.
)
popd
pause
