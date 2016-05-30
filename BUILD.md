## For WINDOWS users

To not deal with project settings make a drive `Q:` which points to the game's folder. Then, just
load the project and it will pickup all the settings.
```
subst Q: <your KSP folder path>
```

E.g. if you have installed your game into:
```
D:\Steam\steamapps\common\Kerbal Space Program\
```
then do:
```
subst Q: "D:\Steam\steamapps\common\Kerbal Space Program\"
```

By default on build completion the mod's binary is copied into:
```
Q:\GameData\EasyVesselSwitch\Plugins
```

But only if path `Q:\GameData` exists. So, if you haven't did the subst then there will be no the
convinience option.
