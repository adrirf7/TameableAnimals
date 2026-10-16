# TameableAnimals

A BepInEx mod for Valheim that makes animals that are not tameable in vanilla (deer, hares, seals, Bjorn, bats and other passive animals; Unbjorn optional) tameable with the game's own taming system. Tamed animals breed and have their own smaller, recoloured offspring, tamed animals that follow you travel through portals with you, bears can be saddled and ridden, and every saddle can be crafted in 7 colours.

Vanilla tameables (boar, wolf, lox, hen, asksvin...) keep their vanilla taming; they only gain the hold-E follow command, saddle colours and portal travel (all optional).

[Versión en español](README.es.md)

## Features

- Taming through the vanilla system: drop the animal's food on the ground, stay out of sight and wait.
- Breeding: two tamed, fed animals close together have offspring that grow into tamed adults.
- Offspring use the adult's model, scaled down and tinted (the seal uses the vanilla seal pup model).
- Press Use (E) to pet, hold Use to toggle follow / stay, for every tamed animal (wolves keep their vanilla single press).
- Tamed animals that follow you travel through portals with you.
- Bear saddle: a craftable saddle to ride tamed Bjorn like a lox (console command `ta_bearsaddle` to fine-tune its position).
- Saddle colours: lox, moose, asksvin and bear saddles can be crafted in blue, red, green, black, white, yellow or orange (style button), with matching reins.
- A ridden animal stays where you dismount and defends that spot.
- In-game text follows the game language (English by default, Spanish included).
- Everything is configurable in `BepInEx/config/TameableAnimals.adrirf7.cfg` and `TameableAnimals.adrirf7.Animals.cfg`.

## How it works

The code only uses Harmony patches and Unity components. It does not access the network or any files outside the game process, except for writing its own config files and `TameableAnimals.adrirf7.AvailableCreatures.txt` in `BepInEx/config`.

- `src/CreatureSetup.cs`: before `ZNetScene` registers prefabs, adds `Tameable` + `Procreation` to the selected creatures and creates an offspring prefab (a scaled-down, tinted clone with `Growup`).
- `src/TameableAnimal.cs`: vanilla `Tameable` only works with `MonsterAI`. For animals with `AnimalAI`, this component provides eating food from the ground, taming progress, follow/stay and threat detection.
- `src/Patches.cs`: Harmony patches that redirect `Tameable` and `AnimalAI` to `TameableAnimal` for those animals.
- `src/PortalTravel.cs`: moves following tamed animals next to the player after a portal teleport.
- `src/FollowCommand.cs`: hold Use to make any tamed animal follow / stay.
- `src/BearSaddle.cs`: bear saddle item and recipe; builds a rigid copy of the lox saddle seat (skinned by hand from the lox pose) on each bear, attached to a spine bone.
- `src/SaddleColors.cs` + `src/ReinMasks.cs`: saddle colour variants (recoloured textures and icons) and the texture areas of the reins.
- `src/RidingPatches.cs`: clears / sets the stay point when mounting and dismounting.
- `src/Translations.cs`: registers the mod's in-game text with Valheim's localization.
- `src/Plugin.cs`: BepInEx entry point and configuration.

## Building

Requirements:

- [.NET SDK 8](https://dotnet.microsoft.com/download) or newer
- Valheim with [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) installed (the project references the game's and BepInEx's assemblies from the install folder)

Steps:

```
git clone <this repository>
cd TameableAnimals
dotnet build -c Release -p:ValheimDir="C:\Program Files (x86)\Steam\steamapps\common\Valheim" -p:DeployToGame=false
```

The compiled mod is `bin/Release/TameableAnimals.dll`. `ValheimDir` defaults to the Steam path above. Without `-p:DeployToGame=false`, the build also copies the DLL to `BepInEx/plugins/TameableAnimals/` in that Valheim install.

The NuGet package `BepInEx.AssemblyPublicizer.MSBuild` is restored automatically. It lets the code access non-public members of `assembly_valheim.dll` at compile time, which is standard practice for Valheim mods.

## Installation

1. Install BepInExPack Valheim.
2. Put `TameableAnimals.dll` in `BepInEx/plugins/TameableAnimals/`.
3. In multiplayer, the server and every player need the mod.
