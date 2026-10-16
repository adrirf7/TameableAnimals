# TameableAnimals (Valheim)

Mod de BepInEx que hace domesticables a los animales que en vanilla no lo son (ciervos, liebres y cualquier otra criatura con IA de animal) usando la domesticación normal de Valheim, y permite que críen.

## Cómo se juega

1. Tira al suelo comida cerca del animal (ciervo: frambuesas, arándanos, zanahorias, nabos, moras de las llanuras; liebre: zanahorias, nabos, cebollas, moras de las llanuras, frambuesas y arándanos).
2. Aléjate o escóndete: solo avanza la domesticación cuando está **alimentado y tranquilo** (si te ve, se asusta y huye).
3. Cuando llega al 100 %, queda domesticado: ya no huye de los jugadores, al pulsar *Usar* (E) lo acaricias ("te quiere") y al **mantener pulsado** *Usar* alterna entre seguirte y quedarse quieto.
4. Dos animales domesticados, alimentados y juntos (a menos de 3 m) acumulan amor, la hembra se queda embarazada y nace una **cría**: el mismo modelo, a la mitad de tamaño y con otro tono de color (Cervatillo / Lebrato). Al cabo de un tiempo crece y se convierte en un adulto domesticado.

El seguir / quedarse con *Usar* mantenido funciona con todos los animales domesticados, también los vanilla (jabalí, lox, gallina, alce, asksvin...). El lobo mantiene su pulsación simple de vanilla. Se desactiva con `EnableFollowCommand` en `[7 - Follow]`.

## Monturas

- **Montura de oso:** se fabrica (por defecto 4 pieles de oso, 8 restos de cuero y 2 de bronce en el banco de trabajo nivel 2), se le pone a un oso domesticado y se monta como el lox. Va pegada a su columna, así que se mueve con el cuerpo, y el oso ataca a los enemigos cercanos mientras lo montas. Es de cuero marrón y su icono lleva una huella para no confundirla con la del lox. Su posición se puede afinar con el comando de consola `ta_bearsaddle <x> <y> <z> <escala>`.
- **Colores:** las monturas de lox, alce, asksvin y oso se pueden fabricar en azul, rojo, verde, negro, blanco, amarillo o naranja con el botón de estilo, como las capas y escudos. Las riendas y correas de la cabeza se tiñen a juego.
- Al bajarte de un animal se queda quieto donde lo dejas y defiende ese sitio.

Todo se configura en la sección `[6 - Saddles]` de `TameableAnimals.adrirf7.cfg`.

## Portales

Los animales domesticados que te siguen (modo *seguir*) y están a menos de 20 m cuando cruzas un portal aparecen a tu lado al llegar y siguen siguiéndote. Funciona también con los domesticables vanilla (lobos, jabalíes, lox...). No se aplica a las entradas de mazmorras ni al animal que estés montando. Se activa o desactiva con `AnimalsCanUsePortals` en la sección `[5 - Portals]` de `TameableAnimals.adrirf7.cfg`, y por especie con `CanUsePortals` en `TameableAnimals.adrirf7.Animals.cfg`.

## Configuración

Ambos archivos se crean solos en `BepInEx/config` la primera vez que se carga el mod:

- `TameableAnimals.adrirf7.Animals.cfg`: **qué animales se pueden domesticar**. Cada animal tiene su sección `[Creature X]` con `Enabled = true/false`, su comida, el nombre de la cría y su color (`BabyTint = R,G,B`). `AutoDetectOtherAnimals` controla si se incluyen otros animales detectados, además de Deer y Hare, y `ExtraCreatures` permite añadir más. Los animales domesticables en vanilla no se tocan y siempre se pueden domesticar.
  Animales incluidos de serie: Deer, Hare, Seal (que huyen) y Bjorn, Bat (que atacan y se domestican como el lobo). Unbjorn aparece en la lista pero desactivado por defecto (`Enabled = false`).
- `TameableAnimals.adrirf7.AvailableCreatures.txt` (se genera al entrar en un mundo): todas las criaturas del juego, separadas en domesticables por el mod, domesticables en vanilla y otras compatibles que se pueden añadir a `ExtraCreatures`.
- `TameableAnimals.adrirf7.cfg`: tiempos de domesticación, embarazo y crecimiento, tamaño y vida de la cría, población máxima, portales, monturas y la orden de seguir.

## Compilar

Requiere el SDK de .NET 8 y Valheim con BepInEx instalado.

```
dotnet build -c Release
```

Copia el `.dll` automáticamente a `BepInEx/plugins/TameableAnimals`. Si Valheim está en otra ruta: `dotnet build -c Release -p:ValheimDir="D:\Steam\steamapps\common\Valheim"`.

En multijugador, todos los jugadores y el servidor deben tener el mod.
