@echo off
REM EVS *requires* .NET compiler version 6.0 or higher and Framework version not less than 4.5.
"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe" ..\Source\EasyVesselSwitch.csproj /t:Clean,Build /p:Configuration=Release
