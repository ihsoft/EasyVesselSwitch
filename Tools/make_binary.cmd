@echo off
REM EVS *requires* .NET compiler version 6.0 or higher and Framework version not less than 4.5.
"C:\Program Files (x86)\MSBuild\14.0\Bin\MsBuild.exe" ..\Source\EasyVesselSwitch.csproj /t:Clean,Build /p:Configuration=Release
