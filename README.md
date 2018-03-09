# Easy Vessel Switch (EVS)

A mod for [Kerbal Space Program](http://www.kerbalspaceprogram.com/).

* Highlights currently selected vessel with a colored border. It helps navigating using keyboard.
* Allows selecting target vessel with mouse (by default: `Alt`+`Left Mouse`).
* Prevents random camera orientation on the new vessel:
  - For close vessels there are three different modes that allow keeping the context. The modes can
    be switched from keyboard (`F7` by default).
    - `None`: Use KSP default mode.
    - `KeepPosition`: Preserve old camera position and only rotate camera to keep the newly
      selected vessel in focus.
    - `KeepDistanceAndRotation`: Preserve the same relative camera rotation and distance between
      the switches.
  - For vessels that are too far away from each other camera is adjusted so what the new field of
    view has both the new and the old vessels in the focus.
* Shows brief info on the vessel being hovered when in switch mode (`Alt` is pressed).
  - Detects KIS items that are attached to the ground and reports it.
* Allows changing camera focus on an arbitrary part:
  - Hold `O` (default setting) to activate part focus mode.
  - Hover over the part to set focus on and click `Left Mouse`.
  - To reset focus hold `O` and click in space.
* Almost anything can be configured via settings file that's located at:
  `EasyVesselSwitch\Plugins\PluginData\settings.cfg`.

# Forum

See how to install the mod, ask questions and propose suggestions on
[the forum](http://forum.kerbalspaceprogram.com/index.php?/topic/141180-12-easy-vessel-switch-evs-v120/).

# Development

To start your local building envirtonment read [BUILD.md](https://github.com/ihsoft/EasyVesselSwitch/blob/master/BUILD.md).

If you're going to request a pull request, please, read [code rules](https://github.com/ihsoft/EasyVesselSwitch/blob/master/Source/README.md) first.
Changes that don't follow the rules will be **rejected**.
