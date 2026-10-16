# TameableAnimals

Ever wanted a herd of deer on your farm, a bear you can ride into battle or a seal colony at your harbour? **TameableAnimals** makes the animals that can't be tamed in vanilla Valheim tameable, using the game's own taming system, and adds a bear saddle plus colour choices for every saddle.

![Tamed deer with their fawn](https://raw.githubusercontent.com/adrirf7/TameableAnimals/main/docs/deer.jpg)

![Tamed hares with their leveret](https://raw.githubusercontent.com/adrirf7/TameableAnimals/main/docs/hares.jpg)

![Tamed seals](https://raw.githubusercontent.com/adrirf7/TameableAnimals/main/docs/seals.jpg)

![Tamed bears](https://raw.githubusercontent.com/adrirf7/TameableAnimals/main/docs/bears.jpg)

## Features

- **Vanilla taming:** feed the animal by dropping food on the ground, stay out of sight and wait for the taming bar to reach 100%.
- **Breeding:** two tamed, fed animals close to each other build up love and have offspring.
- **Their own offspring:** each species gets offspring that use the same model, smaller and in a different colour tone. The seal uses the game's own seal pup model. Offspring grow up into tamed adults over time.
- **Ride your bear:** craft a **bear saddle**, put it on a tamed bear and ride it like a lox. The saddle moves with the bear's body, and the bear fights nearby enemies while you're on it.
- **Saddle colours:** every saddle (lox, moose, asksvin and bear) can be crafted in **blue, red, green, black, white, yellow or orange** with the style button at the crafting station, just like capes and shields. The reins and head straps are dyed to match.
- **Pet and command any tame:** press **E** to pet ("loves you") and **hold E** to make it follow you or stay put. Works for this mod's animals and for vanilla tames too (boar, lox, hen, moose, asksvin...). Wolves keep their vanilla single press.
- **Travel through portals:** tamed animals that are following you go through portals with you, including vanilla tames.
- **Sensible behaviour:** passive animals stop fearing you once tamed and are not scared of other passive animals or pets. They still flee from monsters that come close. A ridden animal stays where you dismount and defends that spot.
- **Translated:** in-game text follows the game language (English by default, Spanish included).
- **Fully configurable:** choose which animals can be tamed, what they eat, how long taming, breeding and growing up take, the bear saddle recipe, and whether the follow command and saddle colours are enabled.

## Included animals

| Animal | Food | Type |
|---|---|---|
| Deer | Raspberries, blueberries, carrots, turnips, cloudberries | Passive |
| Hare | Carrots, turnips, onions, cloudberries, raspberries, blueberries | Passive |
| Seal | Raw fish and whole fish | Passive |
| Bjorn (bear, rideable) | Raw meat, deer meat, raw fish, honey, berries | Aggressive |
| Unbjorn (disabled by default) | Entrails, raw meat, deer meat | Aggressive |
| Bat | Berries, entrails | Aggressive |

Aggressive animals are tamed like wolves: they attack you until they're tamed, then fight by your side.

The mod also automatically detects any other passive animal in the game. You can add more creatures by hand from the config: the `TameableAnimals.adrirf7.AvailableCreatures.txt` file lists every compatible creature.

**Vanilla tameables (boar, wolf, lox, hen, asksvin...) can't be disabled** and keep their vanilla taming. They only gain the hold-E follow command, saddle colours and portal travel, which can all be turned off.

## Bear saddle

- **Recipe (default):** 4 bear hides, 8 leather scraps and 2 bronze at a workbench level 2 (configurable).
- **Look:** a brown leather saddle with a bear paw on its icon, so it doesn't get mixed up with the lox saddle. Available in the 7 colours too.
- **Use:** put it on a tamed bear, aim at the saddle and press E to ride. Alt + E removes it.
- **Fine-tuning:** if you want the saddle higher, lower or bigger, use the console command `ta_bearsaddle <x> <y> <z> <scale>` and it's saved to the config.

## How to tame

1. Drop one of the animal's foods on the ground near it.
2. Back off or hide: taming only progresses while it's **fed and calm**. If it sees you, it gets scared.
3. Hover over it to see its status: wild, hungry, frightened or taming progress.
4. Once it reaches 100%, it's yours!

To breed, keep two tamed animals of the same species close together (within 3 m) and fed. Breeding stops when there are 6 or more animals of that species within 10 m (configurable).

## Configuration

The files are created automatically in `BepInEx/config` the first time the mod loads:

- **`TameableAnimals.adrirf7.Animals.cfg`**: which animals can be tamed (`Enabled`), their food, whether they use portals, and their offspring's name, model and colour.
- **`TameableAnimals.adrirf7.cfg`**: taming, pregnancy and growth times, offspring size and health, maximum population, fear range, portals, saddles (colours, bear saddle recipe and position) and the follow command (`EnableFollowCommand`).

## Installation

Install with r2modman / Thunderstore Mod Manager, or manually:

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Put `TameableAnimals.dll` in `BepInEx/plugins/TameableAnimals/`.
3. Launch the game.

**Multiplayer:** the mod must be installed on the server and on every player.

## Compatibility

- Don't use it together with other mods that make the same animals tameable (for example BeastMaster).
- If you use another mod that teleports pets through portals, turn one of the two features off (`AnimalsCanUsePortals = false` in this mod) so animals don't travel twice.
- Other mods that change saddles or the crafting style button may conflict with the saddle colours (`SaddleColors = false` turns them off).

## Changelog

- **1.1.0:** bear saddle (craft it and ride tamed bears), 7 colours for every saddle with matching reins, hold-E follow / stay for all tamed animals, ridden animals defend the spot where you dismount, config files renamed to `TameableAnimals.adrirf7.*`.
- **1.0.1:** white deer removed (command-only creature), Unbjorn disabled by default.
- **1.0.0:** first release.

## Source code

[github.com/adrirf7/TameableAnimals](https://github.com/adrirf7/TameableAnimals)
