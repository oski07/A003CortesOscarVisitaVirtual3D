# Visita Virtual 3D — GINEBRÓ GAMES

Activitat 3 Unity · Unity 6000.6 · URP · PC (teclat i ratolí)

## Escenes (Assets/Scenes)
| Escena | Contingut |
|---|---|
| `Splash` | Logo de GINEBRÓ GAMES amb fosa (es pot saltar amb qualsevol tecla) |
| `MainMenu` | Iniciar visita · Crèdits · Sortir, amb música |
| `Credits` | Crèdits amb desplaçament i botó per tornar al menú |
| `VisitaVirtual` | Casa de 4 habitacions + passadís, llums, TV, portes i so |

## Controls
WASD moure · Ratolí mirar · Shift córrer · Ctrl ajupir-se · Espai saltar · **E interactuar** · Esc pausa

## Mecàniques
- **Llums**: 5 interruptors (sala, cuina, dormitori, estudi, passadís). `LightSwitch.cs`
- **Televisió**: `VideoPlayer` sobre RenderTexture. Encendre/apagar mirant la pantalla o amb el botó ON/OFF; canviar de canal amb CANAL + / CANAL - (3 vídeos). `TvController.cs`, `TvButton.cs`
- **Portes**: 4 portes batents amb so d'obrir i tancar. `DoorController.cs`
- **Àudio**: so ambient constant (pluja + to d'habitació), rellotge i nevera 3D, SFX per a cada interacció, música al menú.

## Modelat (Blender)
`Blender/CreateHouse.py` genera l'estructura (terres, parets, llindes i sostre) i l'exporta a `Assets/Models/House.fbx`.
L'editor de Unity l'executa automàticament amb Blender en mode consola la primera vegada (menú **GinebroGames > Regenerate House With Blender** per tornar-ho a fer).
També es pot obrir a Blender > Scripting > Run.

## Construcció automàtica
`Assets/Scripts/Editor/VisitaVirtualBuilder.cs` crea materials, escenes i Build Settings.
Menú **GinebroGames > Rebuild All Scenes**.

## Estructura
```
Assets/
  Audio/        Ambient, Interaction, Music, Ui, SFX (passos)
  Materials/    Models/   Prefabs/   Scenes/   Settings/
  Scripts/      Core, Editor, Interaction, Objects, Player, Ui, Audio
  Sprites/      Textures/   Videos/
Blender/        CreateHouse.py (+ House.blend generat)
```
