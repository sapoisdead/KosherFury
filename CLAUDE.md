# KosherFury

Brawler 3D in Unity (URP). Il player è il **Rabbi**, i nemici base sono i **simp** (`Valerio_Simp`).
Il combattimento è ispirato a Bloodborne: spacing, letture, punizione del recovery.

La lingua di lavoro è l'**italiano**, commenti nel codice compresi. **Ma non i nomi**:
classi, metodi, campi e variabili vanno sempre in inglese, senza eccezioni — solo i
commenti (e i testi user-facing) restano in italiano.

---

## Come si prova

Unity si pilota via **Coplay MCP** (`execute_script`, `play_game`, `stop_game`,
`check_compile_errors`). Cinque trappole già pagate care:

- **Contare i frame non è contare il tempo.** Il gioco gira a ~260 fps: 40 frame sono
  0.15 s. Le prove sui tempi vanno fatte accumulando `Time.deltaTime`, o si misura il nulla.
- **Modificare script durante il Play Mode** ricarica il dominio e azzera gli alberi di
  comportamento. Uscire e rientrare dopo ogni modifica.
- **`Time.time` non avanza** dentro un ciclo sincrono di uno script: per misurare serve
  una coroutine su frame veri.
- **`get_unity_logs` restituisce troppo rumore.** Conviene che il test scriva un blocco
  con un titolo riconoscibile e poi rileggerlo da `~/Library/Logs/Unity/Editor.log`.
- **L'Input System vivo interferisce** con i valori iniettati per riflessione: nei test
  va disattivato il `PlayerInput`.

Scene: `Assets/Scenes/Test.unity` (piano piatto, prove rapide) e
`Assets/Scenes/Arena/Arena.unity` (quella vera).

---

## Combattimento

### Fasi dell'attacco

`Combat/CombatState.cs` espone in che punto del proprio attacco si trova un personaggio.
Le fasi **non hanno tempi propri**: le muovono gli stessi animation event che aprono e
chiudono le hitbox, quindi spostando un evento si sposta la fase.

| fase | cosa comporta |
|---|---|
| `Startup` | vulnerabile, il colpo subìto **annulla** l'attacco (counter-hit) |
| `Active` | **armor**: incassa il danno ma non viene interrotto, e non subisce spinta |
| `Recovery` | vulnerabile e incapace di agire: è la finestra di punizione |

`AttackId` cambia a ogni colpo: serve a chi osserva per tirare il dado **una volta per
attacco** invece che a ogni frame.

Attaccare **non** dà più invulnerabilità totale. Restano invulnerabili solo la reazione
al colpo e lo scatto.

### Hitbox

`Combat/Hitbox.cs` è unica per pugni, calci e armi. Il Collider serve **solo come
descrittore di forma**: viene tenuto trigger e disabilitato, e i controlli sono query
manuali in `FixedUpdate` lungo il percorso compiuto fra un frame e l'altro.
`Activate` fa anche una lettura immediata, così una finestra di pochi millisecondi non
cade fra due step della fisica.

Attorno stanno `HitStop`, `CameraShake` e `Knockback`.
Knockback: `distanza = v² / (2 · decay)`, con `decay = 9`. A **1.5** dà ~0.125 m —
valori più alti spezzano le combo di entrambi, perché il bersaglio esce di portata.

### Nomi degli animation event

Nominati sull'**arto**, non sul tipo di colpo, così un gomito riusa lo stesso evento:

```
OnRightHandStart / OnRightHandEnd      OnLeftHandStart / OnLeftHandEnd
OnRightFootStart / OnRightFootEnd      OnLeftFootStart / OnLeftFootEnd
OnWeaponStart    / OnWeaponEnd
```

I metodi devono stare su un componente dello **stesso GameObject** dell'Animator.

> Nel `ModelImporter` il campo `time` degli eventi è **normalizzato 0..1**, non in
> secondi — al contrario di `AnimationUtility.GetAnimationEvents`, che li dà in secondi.

---

## Numeri misurati

Non sono stime: vengono da misure in Play Mode. Cambiare le animazioni li invalida.

**Portate** (distanza massima fra i due personaggi perché il colpo entri)

| | portata |
|---|---|
| player, tutti i colpi | 1.05 – 1.10 m |
| simp `Punch_right` / `Punch_left` | 1.06 m |
| simp `Kick_left` | 0.99 m |
| distanza minima fisica fra i due corpi | 0.80 m (capsule 0.40 + 0.40) |

**Il simp è surclassato in portata**: per colpire deve entrare nella zona in cui può
essere colpito. La finestra di rischio è voluta.

**Velocità**: sul prefab il player cammina **2.5** e corre **8.5**. Simp corre 4.2 in
avvicinamento e cammina 1.8 in combattimento (`SetMovementSpeed`, con isteresi sulla
soglia).

> Le soglie dei blend tree sono in **m/s reali**, ma sono **4.8 e 9.6**: non
> combaciano con le velocità del player, e non devono. La camminata a 2.5 sta sotto
> la soglia della propria clip, quindi la clip correrebbe più del personaggio: a
> tenere insieme passo e spostamento ci pensa `SpeedMultiplier` (vedi *Animazioni*).
> Chi cambia le soglie del blend tree deve aggiornare `walkClipSpeed` su
> `PlayerAnimator`, che è la copia di quella soglia usata per la compensazione.

**Tempi degli attacchi**: player 0.42–0.74 s, simp 0.53 s.
Lo startup del player è **0.10–0.20 s**, sotto il tempo di reazione umano (~0.25 s):
è per questo che la schivata reattiva del simp è disattivata.

---

## IA dei simp

`Enemy/ValerioBT.cs` costruisce l'albero; il grosso sta in
`Enemy/BehaviourTree/AttackPlayer.cs`, che è un ciclo a impegno in cinque modi:

```
Spacing  →  Closing  →  Striking  →  Retreating  →  Spacing
   ↑                        │
   └──── Evading ←──────────┘   (spento)
```

L'idea portante: **l'avvicinamento fa parte dell'attacco**. Il simp non decide ogni
frame "sono in portata?", ma sceglie un momento, chiude la distanza e colpisce senza
ripensarci. È così che il colpo arriva per costruzione invece che per fortuna.

- Concatena da 1 a 3 colpi per impegno, facendo partire il successivo già in
  **recovery** del precedente, e **avanzando** durante il proprio colpo per recuperare
  la spinta.
- **Punisce** il recovery del player, con un ritardo di reazione di 0.16–0.26 s pescato
  a caso **per istanza**, o un gruppo reagirebbe all'unisono.
- Le reazioni sono valutate solo da `Spacing` e `Retreating`: una volta partito il colpo
  è impegnato.

`Enemy/MinionCoordinator.cs` limita a 2 gli attaccanti simultanei e fa **ruotare il
turno**: chi ha appena attaccato non può riprendersi lo slot per 1.2 s. Sono valori
`static`, quindi **condivisi da tutti i minion**.

`Enemy/WaveSpawner.cs` + `Enemy/MinionEntrance.cs`: ondate a intervalli. Un minion che
nasce lontano sarebbe fuori dal `detectionRange` e resterebbe fermo, quindi
`MinionEntrance` **spegne l'albero nel proprio Awake**, impone il tragitto d'ingresso e
lo riaccende all'arrivo, chiamando prima `ValerioBT.SetHome` — i nodi ricevono la
posizione di casa per copia alla costruzione dell'albero.

> `CanSeePlayer` usa un cono di **90°** finché non è allertato: un simp piazzato di
> spalle non ingaggia mai. Chi viene messo in scena a mano va ruotato verso il player.
> Prendere danno vale come avvistamento, a qualunque distanza.

---

## Player

`Player/PlayerMovement.cs` + `Player/PlayerAnimator.cs`, sullo stesso GameObject.

- Combo a 4 colpi: `Punch_right → Kick_left → Punch_left → Kick_right`, e 4 di spada.
- **Buffer di input** da 0.25 s: il comando è sempre registrato e parte appena lo stato
  lo consente. La parata è annullabile in attacco; la reazione al colpo no.
- **Schivata**: solo agganciati, nelle 4 direzioni, con fotogrammi di invulnerabilità.
  La capriola è stata rimossa.
- Lo spostamento dello scatto viene dal **root motion** della clip, instradato in
  `OnAnimatorMove` dentro il `CharacterController` — scrivere sul Transform
  scavalcherebbe le collisioni. Viene applicato nel riferimento **congelato alla
  partenza**: senza, il player ruotando verso il nemico incurva la traiettoria in un
  arco e non si allontana (33% di efficienza contro 60%).
- La postura pacifica è stata rimossa: `Stance` la decide solo l'arma equipaggiata.

---

## Controlli

`InputActions/PlayerInputAction.inputactions`, due schemi: **Keyboard** (tastiera +
mouse) e **Pad** (`<DualShockGamepad>`). I gestori sono metodi `OnXxx(InputValue)`
consegnati per `SendMessages` dal `PlayerInput` su `Prefabs/Player/Rabbi_Player.prefab`.

| PS4 | azione | tastiera |
|---|---|---|
| stick sinistro | `Move` | WASD |
| stick destro | camera | mouse |
| **R1** | `Punch` — attacco leggero | tasto sinistro |
| **R2** | `AreaSlash` — pesante, solo armati | tasto destro |
| **L1** | `Block` — parata | Q |
| **Cerchio** | `Dodge` | Cmd sinistro |
| **Croce** | `Jump` | spazio |
| **L3** (tenuto) | `Sprint` | Shift sinistro |
| **R3** | `Lock` | X |
| **D-pad su/giù** | `ScrollWeapon` | rotella |
| **Options** | `Pause` | Esc |

Quadrato, Triangolo e L2 sono liberi. `ToggleStance` è stata rimossa dall'asset: non
aveva alcun gestore da quando la postura la decide l'arma.

> **Un binding senza `groups` non appartiene a nessuno schema** e viene disattivato
> appena uno schema è attivo. È il motivo per cui `Dodge` e `Block` sul pad non
> funzionavano pur essendo mappati.

> **`leftStick/up|down|left|right` sono controlli a PULSANTE**, con soglia di
> pressione a 0.5: un composito 2D costruito su quelli legge 0 sotto metà corsa e 1
> sopra, e butta via l'analogico prima ancora che arrivi al codice. Per un movimento
> proporzionale serve **un solo binding su `<Gamepad>/leftStick`**.

**Velocità analogica**: sul pad la velocità è l'inclinazione dello stick per la
velocità massima (`MoveSpeed` in `PlayerMovement`). Agganciati il tetto scende alla
camminata: girare attorno al nemico a stick pieno è il gesto più comune del
combattimento e farne partire la corsa sgancerebbe il lock ogni volta. Da tastiera il
composito vale sempre 1, quindi resta la camminata fissa e la corsa la fa lo sprint —
`analogMove` distingue le due sorgenti chiedendo all'azione da quale controllo è
partito il valore, perché il modulo da solo non basta (WASD in diagonale vale 1
esatto, come lo stick a fondo corsa).

**Pausa** (`Game/PauseController.cs`, sul GameObject del player): congelamento secco,
`Time.timeScale = 0` e nient'altro — nessun menu, il cursore resta bloccato. Tre cose
non vengono da sole:

- **`ExecutionSlowMo` va annullato entrando in pausa.** Conta in secondi *reali* e si
  riporta a 1 da solo quando finisce: lasciandolo vivo, dopo mezzo secondo di pausa
  spauserebbe il gioco da sé.
- **`CameraController` va fermato a mano**: legge il mouse in `LateUpdate`, e
  `timeScale` non tocca quella lettura.
- **I comandi continuano ad arrivare** a tempo fermo, perché l'Input System non guarda
  `timeScale`: senza `SetInputLocked(true)` un pugno premuto in pausa partirebbe alla
  ripresa. Il valore precedente va conservato e rimesso, o spausare restituirebbe il
  controllo anche in mezzo a un blocco scriptato.

> Chi scrive prove sulla pausa deve accumulare `Time.unscaledDeltaTime`: a gioco fermo
> `deltaTime` vale zero e l'attesa non finisce mai.

La camera legge il mouse **e** lo stick destro sommandoli già convertiti in gradi:
sono due grandezze diverse (pixel per frame contro posizione ferma fra -1 e 1) e una
sola sensibilità per entrambe non può funzionare. Da qui `stickSensitivityX/Y` su
`CameraController`, separati da `sensitivityX/Y`.

---

## Animazioni

Convenzione d'import del Rabbi e del simp: **`Human` + `Create From This Model`**,
`loop` attivo sulle locomotorie, `bake into pose` su XZ/Y/rotazione per tutto **tranne**
le clip di dash, che hanno XZ sbloccato per il root motion.

> Le clip esportate da Blender arrivano regolarmente come **`Generic` senza avatar**:
> vanno sempre controllate.

Blend tree del player: `Locomotion` (1D su `Speed`) contiene `CombatWalk2D`
(Cartesian 2D su `MoveX`/`MoveZ`, 4 direzioni) a soglia 4.8, e la corsa a 9.6.
Il simp ha `Locomotion2D`, stessa impostazione.

> Nessun nodo al centro dei blend tree 2D: un nodo a (0,0) ruba peso a tutte le
> diagonali e smorza le gambe.

Le quattro clip direzionali devono avere la **stessa durata**, o sulle diagonali le
gambe vanno fuori sincrono.

**`SpeedMultiplier`** è un parametro float del controller del Rabbi legato alla
velocità di riproduzione dei tre stati di locomozione (`Locomotion`,
`Locomotion_Free`, `Locomotion_Free_Sword`), calcolato in `PlayerAnimator` come
velocità reale ÷ `walkClipSpeed`. Serve alle andature **sotto** la soglia di
camminata, dove il blend tree mescola idle e camminata ma la clip continua a girare
al proprio ritmo e i piedi slittano. Sopra la soglia resta 1.

> Va sugli stati, non sull'Animator intero: `animator.speed` rallenterebbe anche
> pugni e parate. Il moltiplicatore si somma alla velocità già impostata sullo stato
> (`Locomotion_Free` sta a 2), quindi a valore 1 nulla cambia.

Il rig è **Mixamo** (`mixamorig:LeftUpLeg`): il *Paste Pose Flipped* di Blender vuole i
suffissi `.L`/`.R` e su questo rig non funziona.

---

## Arena

`Assets/Scenes/Arena/Arena.unity`. Sala di 27 × 27 m (allargata di 1.35 su X e Z,
altezza intatta), area calpestabile 25.3 m, scalinata a nord, pulpito a sud.

Era pura geometria decorativa: ho aggiunto 29 MeshCollider, marcato tutto Navigation
Static e cotto la NavMesh. `Agent Climb` è a **1.0** e `cellSize` a **0.05**.

> Il `cellSize` è un'impostazione **di progetto**: le altre scene con NavMesh cotta
> (Catacombs, Graveyard) vanno ricotte.

Il player nasce dove sta il `PlayerSpawnPoint` (`Game/PlayerSpawnPoint.cs`); senza,
il `PlayerManager` ricade sull'origine, che nell'arena cade dentro l'arredamento.
`DebugBootstrap` crea i manager e fa comparire player e camera quando si avvia la scena
direttamente.

**Se aggiungi ostacoli, ricuoci la NavMesh.** Il caso già capitato: i simp
attraversavano i due bimah. Non era colpa dei collider — un `NavMeshAgent` segue solo
la superficie di navigazione e **ignora del tutto i collider fisici**, quindi l'unica
cosa che ferma un agente è l'assenza di NavMesh. Il MeshCollider e il flag Navigation
Static erano stati messi *dopo* l'ultima cottura, e la superficie correva dritta e
piatta sotto entrambi. Ricotta l'8 agosto 2026: ora i tragitti li aggirano.

> Il player non attraversa nulla perché ha un `CharacterController`, che le collisioni
> le rispetta davvero. Quando un ostacolo ferma il player ma non i nemici, il sospetto
> è sempre la NavMesh, non il collider.

> `Assets/Scenes/Arena/Arena/` **non è tracciata da git**: la NavMesh cotta non finisce
> nei commit, e chi clona il progetto trova l'arena senza superficie di navigazione
> finché non ricuoce.

### Perché i personaggi non fluttuano più

Erano due cause diverse, entrambe di sistema e non di animazione:

- il `NavMeshAgent` appoggia sulla **superficie di navigazione**, non sulla geometria, e
  quella stava 16.7 cm più in alto → `cellSize` a 0.05 e `baseOffset = −0.034`;
- il `CharacterController` tiene la capsula staccata di **`skinWidth`** → portato a 0.04
  e compensato alzando `center.y` a `height/2 + skinWidth`.
  A riposo vale `transform.y = terreno + skinWidth − center.y + height/2`: **abbassare**
  il centro non serve a niente, alza il personaggio della stessa quantità.

Restano ~2.5 cm fra origine e punta del piede: quella sì è la posa delle clip.

---

## Aperto

- **`Kick_right` del simp non registra mai**, a nessuna distanza, pur risultando la
  hitbox attiva e sovrapposta al player (0.443 contro una soglia di 0.500). È **fuori
  rotazione** in `EnemyAnimator.AttackTriggers`; stato ed eventi sono rimasti al loro posto.
- **Schivata reattiva del simp spenta** (`evadeChance = 0`): lo startup del player dura
  meno del tempo di reazione, quindi non scatterebbe mai. Il ramo è pronto per quando gli
  attacchi saranno più telegrafati.
- **Camminata e corsa del player da rifare.** Sopra la soglia di 9.6 entra ancora la corsa
  Mixamo, che non è una posa in guardia. Attenzione: 4.8 m/s su un personaggio di 1.64 m
  **non è una camminata**, è una corsetta — la falcata implicita non torna con un ciclo da
  1.16 s. `SpeedMultiplier` toglie lo scivolamento ma non rifà la posa: alle andature
  molto lente rallenta la clip fino al minimo di `minLocomotionPlaybackRate` (0.35).
- Mancano le clip di dash diagonali; sotto lock le diagonali ricadono su una delle quattro.
- **La pausa non ha menu**, è solo un congelamento (vedi *Controlli*). Il menu — riprendi,
  opzioni, esci — è da fare, e per essere usabile col pad va navigabile senza mouse:
  serve rimettere il cursore che `PlayerMovement.Awake` blocca, oppure una UI a fuoco
  esplicito. `Lock` e `ScrollWeapon` non guardano `InputLocked`, quindi in pausa
  rispondono ancora: quando ci sarà un menu conviene spegnere la mappa `Movement`
  e spostare `Pause` su una mappa che resti accesa.
- La cima di `Small_bimah` ha superficie di navigazione ma **scollegata** dal pavimento:
  lo scalino di 0.90 m sta sotto l'`Agent Climb` di 1.0, ma dopo l'erosione per il raggio
  dell'agente (0.5) non resta bordo per il collegamento. I simp lo aggirano invece di
  salirci.
- `detectionRange` è 15 m ma la diagonale dell'arena supera i 30: si può stare a un capo
  e vedere i nemici aspettare all'altro.
- `abandonRange` (10) è minore di `detectionRange` (15): le due soglie si accavallano.
