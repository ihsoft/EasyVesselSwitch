# Prerequisites

## For building

* Get C# runtime of version 4.0 or higher.
* Create a virtual drive pointing to KSP installation: `subst q: <path to KSP root>`.
  I.e. if `KSP.exe` lives in `S:\Steam\Kerbal Space Program\` then this is the root.
  - If you choose not to do that or the drive letter is different then you also need
    to change `EasyVesselSwitch.csproj` project file to correct references and post-build
    actions.
* Clone [Github repository](https://github.com/ihsoft/EasyVesselSwitch).

## For making releases

* Python 2.7 or greater.
* Owner or collaborator permissions in [Github repository](https://github.com/ihsoft/EasyVesselSwitch).
* Owner or maintainer permissions on [Curseforge project](http://kerbal.curseforge.com/projects/easy-vessel-switch-evs).
* Author permissions on [Specedock project](https://spacedock.info/mod/1906/Easy%20Vessel%20Switch).

## For development

You may work with the project from the following IDEs:

* [SharpDevelop](https://en.wikipedia.org/wiki/SharpDevelop).
  It will pickup existing project settings just fine but at the same time can add some new changes.
  Please, don't submit them into the trunk until they are really needed to build the project.
* [Visual Studio Express](https://www.visualstudio.com/en-US/products/visual-studio-express-vs).
  It should work but was not tested.

# Versioning explained

Version number consists of three numbers - X.Y.Z:
* X - MAJOR. A really huge change is required to affect this number. Like releasing a first version:
  it's always a huge change.
* Y - MINOR. Adding a new functionality or removing an old one (highly discouraged) is that kind
  of changes.
* Z - PATCH. Bugfixes, small feature requests, and internal cleanup changes.

# Building

* Review file `Tools\make_binary.cmd` and ensure the path to `MSBuild` is right.
* Run `Tools\make_binary.cmd` having folder `Tools` as current.
* Given there were no compile errors the new DLL file can be found in `.\Source\bin\Release\`.

# Releasing

* Verify that file `EasyVesselSwitch\Source\Properties\AssemblyInfo.cs` has correct version number.
  This will be the release number!
* Run building script from the `Tools` folder to create a release archive: `$ KspReleaseBuilder.py -Jp`
* Check if file `EasyVesselSwitch\Source\CHANGES.md` has an up to date section at the top of the file. The block
  of lines till the first empty line will be extracted and used as the release description.
* Update the publishing arg files (`publish_<project>_args.txt`) to refer the newest release archive.
* Check-in all modified files into Github repository, including the release scripts.
  - **IMPORTANT!** Do not commit the secrets, used in the publishing scripts!
* Publish the new release:
  - Run `publish_github.cmd` to publish on `GitHub`. The release will be created as draft. Sync all local changes to the
    repository, then go to GitHub releases and publish the draft.
  - Run `publish_spacedock.cmd` to publish on `Spacedock`. The release becomes active immediately.
  - Run `publish_curseforge.cmd` to publish on `CurseForge`. Once the release is verified and approved, it will
    become availabe for downloading. Within 24 hours the new archive should also show up in CKAN.
