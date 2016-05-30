# KSP: Easy Vessel Switch
####Mod for [Kerbal Space Program](http://www.kerbalspaceprogram.com/)

A mod to make vessel switching more user friendly.

####Main features
- Allows selecting target vessel with mouse (by default: `Alt`+`Left Mouse`).
- Prevents random camera orientation on the new vessel:
  - For close vessels there are two different modes that allow keeping the context. The modes can be
    switched from keyboard (`F7` by default).
    - `KeepPosition`: Preserve old camera position and only rotate camera to keep the newly selected vessel in focus.
    - `KeepDistanceAndRotation`: Preserve the same relative camera rotation and distance between the switches.
  - For vessels that are too far away from each other camera is adjusted so what the new field of
    view has both the new and the old vessels.
- In switch mode shows brief info on the vessel being hovered.
