# TameableAnimals

Ever wanted a herd of deer on your farm, a bear as your bodyguard or a seal colony at your harbour? **TameableAnimals** makes the animals that can't be tamed in vanilla Valheim tameable, using the game's own taming system: no new items, no strange mechanics.

![Tamed deer with their fawn](https://raw.githubusercontent.com/adrirf7/TameableAnimals/main/docs/deer.jpg)

![Tamed hares with their leveret](https://raw.githubusercontent.com/adrirf7/TameableAnimals/main/docs/hares.jpg)

![Tamed seals](https://raw.githubusercontent.com/adrirf7/TameableAnimals/main/docs/seals.jpg)

![Tamed bears](https://raw.githubusercontent.com/adrirf7/TameableAnimals/main/docs/bears.jpg)

## Features

- **Vanilla taming:** feed the animal by dropping food on the ground, stay out of sight and wait for the taming bar to reach 100%.
- **Breeding:** two tamed, fed animals close to each other build up love and have offspring.
- **Their own offspring:** each species gets offspring that use the same model, smaller and in a different colour tone. The seal uses the game's own seal pup model. Offspring grow up into tamed adults over time.
- **Pet and command:** press **E** to pet ("loves you") and **hold E** to make it follow you or stay put.
- **Travel through portals:** tamed animals that are following you go through portals with you, including vanilla tames (wolves, boars, lox...).
- **Sensible behaviour:** passive animals stop fearing you once tamed and are not scared of other passive animals or pets. They still flee from monsters that come close.
- **Translated:** in-game text follows the game language (English by default, Spanish included).
- **Fully configurable:** choose which animals can be tamed, what they eat, and how long taming, breeding and growing up take.

## Included animals

| Animal | Food | Type |
|---|---|---|
| Deer | Raspberries, blueberries, carrots, turnips, cloudberries | Passive |
| Hare | Carrots, turnips, onions, cloudberries, raspberries, blueberries | Passive |
| Seal | Raw fish and whole fish | Passive |
| Bjorn | Raw meat, deer meat, raw fish, honey, berries | Aggressive |
| Unbjorn | Entrails, raw meat, deer meat | Aggressive |
| Bat | Berries, entrails | Aggressive |

Aggressive animals are tamed like wolves: they attack you until they're tamed, then fight by your side.

The mod also automatically detects any other passive animal in the game. You can add more creatures by hand from the config: the `adrirf7.TameableAnimals.AvailableCreatures.txt` file lists every compatible creature.

**Vanilla tameables (boar, wolf, lox, hen, asksvin...) are not modified** and work exactly as before.

## How to tame

1. Drop one of the animal's foods on the ground near it.
2. Back off or hide: taming only progresses while it's **fed and calm**. If it sees you, it gets scared.
3. Hover over it to see its status: wild, hungry, frightened or taming progress.
4. Once it reaches 100%, it's yours!

To breed, keep two tamed animals of the same species close together (within 3 m) and fed.

## Configuration

The files are created automatically in `BepInEx/config` the first time the mod loads:

- **`adrirf7.TameableAnimals.Animals.cfg`**: which animals can be tamed (`Enabled`), their food, whether they use portals, and their offspring's name, model and colour.
- **`adrirf7.TameableAnimals.cfg`**: taming, pregnancy and growth times, offspring size and health, maximum population, how close a monster must be to scare a tamed animal, and portal options.

## Installation

Install with r2modman / Thunderstore Mod Manager, or manually:

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Put `TameableAnimals.dll` in `BepInEx/plugins/TameableAnimals/`.
3. Launch the game.

**Multiplayer:** the mod must be installed on the server and on every player.

## Compatibility

- Don't use it together with other mods that make the same animals tameable (for example BeastMaster).
- If you use another mod that teleports pets through portals, turn one of the two features off (`AnimalsCanUsePortals = false` in this mod) so animals don't travel twice.

## Source code

[github.com/adrirf7/TameableAnimals](https://github.com/adrirf7/TameableAnimals)
