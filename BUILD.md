##WINDOWS users

###Prerequisites
- For building:
  - Get C# runtime of version 4.0 or higher.
  - Create a virtual drive pointing to KSP installation: `subst q: <path to KSP root>`. I.e. if `KSP.exe` lives in `S:\Steam\Kerbal Space Program\` then this is the root.
    - If you choose not to do that or the drive letter is different then you also need to change `EasyVesselSwitch.csproj` project file to correct references and post-build actions.
- For making releases:
  - Python 2.7 or greater.
  - Owner or collaborator permissions in [Github repository](https://github.com/ihsoft/EasyVesselSwitch).
  - Owner or maintainer permissions on [Curseforge project](http://kerbal.curseforge.com/projects/easy-vessel-switch-evs).
- For development do one of the following things:
  - Install an open source [SharpDevelop](https://en.wikipedia.org/wiki/SharpDevelop). It will pickup existing project settings just fine but at the same time can add some new changes. Please, don't submit them into the trunk until they are really needed to build the project.
  - Get a free copy of [Visual Studio Express](https://www.visualstudio.com/en-US/products/visual-studio-express-vs). It should work but was not tested.

###Versioning explained
Version number consists of three numbers - X.Y.Z:
- X - MAJOR. A really huge change is required to affect this number. Like releasing a first version: it's always a huge change.
- Y - MINOR. Adding a new functionality or removing an old one (highly discouraged) is that kind of changes.
- Z - PATCH. Bugfixes, small feature requests, and internal cleanup changes.

###Building
- Review file `Tools\make_binary.cmd` and ensure the path to `MSBuild` is right.
- Run `Tools\make_binary.cmd` having folder `Tools` as current.
- Given there were no compile errors the new DLL file can be found in `.\Source\bin\Release\`.

_Note_: If you don't want building yourself you can use the DLL from the repository. It is updated by the maintainer each time a new version is released.

###Releasing
- Review file `Tools\make_binary.cmd` and ensure the path to `MSBuild` is right.
- Review file `Tools\make_release.py` and ensure `ZIP_BINARY` points to a ZIP compatible command line executable.
- Verify that file `EasyVesselSwitch\Source\Properties\AssemblyInfo.cs` has correct version number. This will be the release number!
- Check if file `EasyVesselSwitch\Source\CHANGES.md` has any "alpha" changes since the last release:
  - Only consider changes of types: Fix, Feature, Enhancement, and Change. Anything else is internal stuff which is not interesting to the outer world.
  - Copy the changes into `CHANGELOG.md` and add the release date.
  - Go thru issues having #XX in the title, and update each releveant Github issue with the version where it was addressed. Usually it means closing of the issue but there can be exceptions.
  - Drop all changes from `CHANGES.md` (but leave the file).
- Run `Tools\make_release.py -p` having folder `Tools` as current.
- Given there were no compile errors the new release arhcive will live in main EVS folder (`Release` folder will have unzipped setup).
- Upload new package to [Curseforge](http://kerbal.curseforge.com/projects/easy-vessel-switch-evs/files). Once verified the package will become available for downloading.
- Update KSP-CKAN config `EasyVesselSwitch.netkan` with new version from Curseforge: update "download" field.
- Update [Github repository](https://github.com/ihsoft/EasyVesselSwitch) with the files updated during the release.
- Create a new release in the [Github repository releases](https://github.com/ihsoft/EasyVesselSwitch/releases). Use changes from `CHANGELOG.md` as a release description. Do **not** add ZIP into the release. Primary source for the release binaries is Curseforge.
- EVS is listed on [KSP-CKAN](http://forum.kerbalspaceprogram.com/index.php?/topic/90246-the-comprehensive-kerbal-archive-network-ckan-package-manager-v1180-19-june-2016/). Once Github files are submitted CKAN should pickup new version within a day. If it doesn't happen escalate via a CKAN's issue on Github.

_Note_: You can run `make_release.py` without parameter `-p`. In this case release folder structure will be created in folder `Release` but no archive will be prepared.

_Note_: As a safety measure `make_release.py` checks if the package being built is already existing, and if it does then release process aborts. When you need to override an existing package either delete it manually or pass flag `-o` to the release script.
