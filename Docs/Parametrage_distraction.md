# Système de distraction — paramétrage

Le système de distraction règle les distracteurs de l'arène selon trois **paliers** : High, Medium et Low. **Tous les réglages sont sur un seul objet de la scène de jeu : `DistractionDirector`** (scène `SampleScene`, à la racine de la hiérarchie).

Il fonctionne de deux façons :

- **Fixe** : le palier ne change pas de toute la partie. C'est le mode des groupes de contrôle (High).
- **Adaptatif** : le palier suit la performance du joueur. Il baisse quand le joueur est en difficulté et remonte quand il s'en sort bien.

Le joueur ne voit rien de ce système : pas d'affichage, et les changements se font en fondu.

L'étude compare le mode adaptatif au mode fixe High, chaque session étant affectée à l'un des deux (mode `Balanced`, section 1). Comme le palier vécu dépend de la performance en mode adaptatif, la comparaison porte sur le mode affecté, pas sur le temps passé dans chaque palier (section 8).

---

## 1. Préparer une session

| Condition | Mode | Fixed Tier |
|---|---|---|
| Étude (moitié adaptatif, moitié fixe) | `Balanced` | `High` (pour les sessions en fixe) |
| Groupe adaptatif seul | `Adaptive` | (sans effet) |
| Groupe fixe High seul | `Fixed` | `High` |

1. Ouvrir `SampleScene`, sélectionner l'objet **DistractionDirector**.
2. Choisir **Mode** (et **Fixed Tier** en mode fixe ou `Balanced`).
3. Renseigner aussi le champ **Condition** du `MetricsReporter` (objet `GameManager`).
4. Sauvegarder la scène, puis refaire le build si la session utilise le jeu exporté.

La partie d'entraînement n'est **jamais** adaptative : elle se joue toujours au palier **Practice Tier** (High par défaut). La partie mesurée suit le mode choisi. En mode adaptatif, elle part de **Start Tier** (High).

### Le mode `Balanced` : 50 % adaptatif, 50 % fixe

Chaque session est affectée au mode adaptatif ou au mode fixe par la Google Sheet, qui donne le mode comptant le moins de sessions. Un simple tirage au hasard n'équilibre les groupes que sur un grand nombre de joueurs ; ici, l'écart entre les deux groupes reste d'une session au plus.

1. Au début de la partie d'entraînement, le jeu demande un mode à la feuille (l'URL du `MetricsReporter`).
2. Le script compte, dans l'onglet **Affectations**, les sessions de chaque mode : les sessions **terminées** (dernière partie mesurée reçue), et les sessions commencées depuis moins de **30 min** (sans doute en cours : plusieurs joueurs en même temps ne tombent pas tous dans le même groupe). Une session non terminée après 30 min est considérée comme abandonnée et ne compte plus.
3. Il donne le mode le moins représenté (au hasard à égalité) et ajoute une ligne à l'onglet.
4. Le mode reste le même pour toute la session.

Sans réponse au début de la partie mesurée (pas de réseau, pas d'URL, ou pas de partie d'entraînement pour laisser le temps à la réponse), le mode est tiré au hasard. La colonne `lod_affectation` de la feuille indique `feuille` ou `hasard`.

L'onglet **Affectations** (créé par le script) a une ligne par session : `date`, `session`, `participant`, `mode`, `terminee` (date de réception de la dernière partie, vide sinon). Pour changer la durée de 30 min : `IN_PROGRESS_MINUTES` dans le script.

**À savoir** : les sessions jouées dans l'éditeur Unity (`Send In Editor` coché) comptent aussi. Pour les retirer de l'équilibre, supprimer leurs lignes de l'onglet **Affectations**.

**Installation** : le script `Tools/GoogleSheets/ScoreReceiver.gs` doit être recollé dans la feuille et redéployé : Déployer > Gérer les déploiements > crayon > Version « Nouvelle version » > Déployer. Enregistrer le script ne suffit pas, et « Nouveau déploiement » crée une autre URL. Pour vérifier, ouvrir l'URL `/exec?action=assign` dans un navigateur : elle doit répondre `{"mode":…}` (et ajoute une ligne à **Affectations**, à supprimer). Si elle répond `ScoreReceiver OK`, le script déployé n'est pas à jour ; la console du jeu affiche alors `pas d'affectation par la Google Sheet (200) : ScoreReceiver OK` et le mode est tiré au hasard.

---

## 2. Les paliers

Chaque palier dit quelles sources sont actives et avec quelle intensité (**saillance**, de 0 à 1,5 ; 1 = décor d'origine). La saillance est une variable contrôlée : elle vaut 1 dans les trois paliers, pour que seul le nombre de distracteurs change.

| Palier | Araignées | Gyrophares | Rotation | Brèches | Flashs des brèches | Flux du portail | Sursauts du mur de données | Cuves animées | Saillance |
|---|---|---|---|---|---|---|---|---|---|
| High | 12 | 2 | oui | 2 | oui | oui | oui | 2 | 1 |
| Medium | 8 | 2 | oui | 1 (mur est) | non | oui | non | 2 | 1 |
| Low | 4 | 2 | non | 0 | non | oui | non | 0 | 1 |

Ces valeurs se modifient dans l'inspecteur, sections **High**, **Medium** et **Low** :

| Champ | Effet |
|---|---|
| Spiders | Nombre d'araignées présentes (0 à 12). Au plus 3 par robot : avec 4 robots, 12 araignées les suivent toutes (3, 2 puis 1 par robot en High, Medium, Low), au-delà elles erreraient. L'essaim (`Spiders`, champ Count) crée 12 araignées. |
| Beacons | Nombre de gyrophares allumés (0 à 2) |
| Beacons Rotate | Les gyrophares tournent (décoché : ils restent allumés mais immobiles) |
| Breaches | Nombre de brèches électriques actives (0 à 2) : gerbes d'étincelles et lumière. Les deux brèches sont en miroir, au centre des murs est et ouest ; la brèche est est la dernière retirée. |
| Breach Flash | Flashs soudains des brèches actives à intervalles imprévisibles (voir plus bas). Sans effet si Breaches vaut 0. |
| Portal Stream | Flux de caractères du portail et particules aspirées |
| Portal Flash | Sursauts du mur de données (voir plus bas). Sans effet si Portal Stream est décoché. |
| Tanks | Nombre de cuves animées (bulles, lumière, spécimen qui flotte), 0 à 2 |
| Salience | Intensité des sources actives, de 0 à 1,5 (1 = décor d'origine) |

### Les flashs (brèche et mur de données)

Un flash est un événement bref et imprévisible. Une apparition soudaine capte davantage l'attention qu'une source continue. Il en existe deux sortes :

| Flash | Particules | Lumière |
|---|---|---|
| Brèche | Gerbe de 80 étincelles (× saillance) | Lumière de la brèche |
| Mur de données | Rafale de 80 colonnes de caractères et de 80 caractères qui s'échappent du portail (× saillance) | Lumière du portail |

Dans les deux cas, la lumière monte d'un coup à 5 fois son intensité, puis redescend en 0,3 s.

Les deux sortes partagent **un seul minutage**. Le temps entre deux flashs est tiré au hasard entre 6 et 14 s (un flash toutes les 10 s en moyenne), et chaque flash tombe au hasard sur les brèches ou sur le mur de données, parmi ceux que le palier permet (avec deux brèches actives, l'une des deux au hasard). Avec les deux sortes actives, chacune revient donc en moyenne toutes les 20 s. Pour plus de flashs, réduire Flash Interval.

Le minutage ne dépend pas du jeu : contrairement aux transitions, un flash **peut tomber pendant une attaque ennemie**, et donc pendant la mesure d'un temps de réaction. C'est voulu : un distracteur doit pouvoir gêner la tâche, et un flash qui attendrait les moments calmes deviendrait prévisible (après la mort d'un robot, par exemple). Les flashs ne sont pas enregistrés dans la Google Sheet.

Réglages, section **Flashs (brèche, mur de données)** :

| Champ | Défaut | Effet |
|---|---|---|
| Flash Interval | 6 – 14 | Temps entre deux flashs, tiré au hasard entre les deux valeurs (s) |
| Flash Light | 5 | Intensité de la lumière de la source au pic du flash, × son intensité courante |
| Flash Particles | 80 | Particules projetées par chaque système de la source (étincelles, colonnes, caractères), × saillance |

### Ce que fait la saillance

La saillance s s'applique à toutes les sources actives :

| Élément | Valeur appliquée |
|---|---|
| Particules (flux du portail, bulles, poussière) | taux d'émission d'origine × s |
| Gerbes de la brèche | nombre d'étincelles par gerbe × s |
| Lumières (gyrophares, brèche, cuves, portail) | intensité d'origine × (0,3 + 0,7 s) |
| Vacillement des lumières | amplitude d'origine × s |
| Effet de téléportation des robots | taille et luminosité × (0,3 + 0,7 s) |

Le 0,3 est le champ **Light Floor** : une lumière ou l'effet de téléportation gardent au moins 30 % de leur intensité, même à saillance très basse. Avec s = 1 dans les trois paliers, tout reste à l'intensité d'origine.

**Aucune vitesse n'est modulée** : les araignées, les sorts et les robots gardent leur vitesse dans tous les paliers. Seule la rotation des gyrophares s'arrête en Low, en fondu.

### Ce qui ne change jamais

- **Toujours présents, saillance seule** : la poussière ambiante, la lumière du portail et l'effet de téléportation des robots. L'effet de téléportation annonce l'arrivée d'un ennemi : il reste toujours joué, et le délai de 0,5 s avant l'apparition du robot ne change pas. Il est aussi joué sur le joueur quand il meurt et réapparaît, à la même taille.
- **Jamais touchés** : les lumières des couverts et du sol, la lumière du joueur, l'anneau et le halo du portail, les effets des sorts. Ils servent à jouer : les modifier changerait la difficulté de la tâche, pas la distraction.

---

## 3. Le mode adaptatif

### Le score de la fenêtre

Toutes les 0,25 s, le système calcule un score de performance sur les **15 dernières secondes**, entre 0 et 1. C'est la moyenne de quatre notes, sur les seuls événements de la fenêtre. Esquive et précision se calculent comme dans le score de la partie (`Docs/Metriques_performance.md`, section 6) ; la réactivité a ici sa propre formule, différente de celle du score de la partie :

| Note | Calcul sur la fenêtre |
|---|---|
| Esquive | (attaques évitées + 1) / (attaques ennemies + 2), sur les attaques dont l'issue est connue dans la fenêtre |
| Précision | (sorts qui touchent + 1) / (sorts lancés + 2), idem |
| Réactivité | 1 − médiane des TR / 1500 ms, omissions comptées à 1500 ms, sur les attaques lancées dans la fenêtre (0,5 sans essai). Une réponse ne compte que si elle s'écarte de la trajectoire de l'attaque (`Docs/Metriques_performance.md`, section 2, point 4). |
| Dégâts | 1 − dégâts subis dans la fenêtre / (Damage Reference × vie maximale), ramené entre 0 et 1. Remplace la survie, qui n'a pas de sens sur une fenêtre. |

Avec **Damage Reference** = 0,33 et 1000 PV (vie du joueur), perdre 330 PV sur la fenêtre (6 à 7 coups) donne une note de 0, et un coup la fait baisser de **0,12** (boule de feu, 40 PV) à **0,18** (laser, 60 PV), soit **0,15** en moyenne. Une mort (1000 PV perdus) met la note à 0 pour toute la durée de la fenêtre. Si la vie du joueur ou les dégâts des ennemis changent, ajuster Damage Reference : poids d'un coup = dégâts du coup / (Damage Reference × vie maximale).

```
score_fenêtre = (esquive + précision + réactivité + dégâts) / 4
```

### La décision

- Score **sous 0,40** : on descend d'un palier (High → Medium → Low).
- Score **au-dessus de 0,60** : on monte d'un palier.
- Entre les deux : rien ne change.
- Après chaque changement, le palier reste fixe **au moins 15 s**, sans comparer le score aux seuils. C'est le temps qu'il faut pour que la fenêtre ne contienne plus que des événements du nouveau palier.
- La première décision n'arrive qu'après 15 s de jeu, pour la même raison.
- Un seul palier à la fois : passer de High à Low prend au moins 15 s.

Exemple, joueur en difficulté continue (score toujours sous 0,40) :

```
0 s     début en High
15 s    verrou terminé, score de la fenêtre < 0,40 → Medium (fondu de 3 s)
30 s    verrou terminé, score < 0,40 → Low
```

(Un changement peut être retardé de quelques secondes par une attaque en cours, voir « La transition ».)

### La transition

- Le changement se fait en **fondu de 3 s** : la saillance glisse vers sa nouvelle valeur, les sources retirées s'éteignent et les rotations ralentissent jusqu'à l'arrêt. Les particules déjà émises finissent leur vie normalement.
- **Araignées** : elles n'apparaissent et ne disparaissent que **hors du champ de la caméra**. Une araignée qui doit partir marche vers un point hors champ et disparaît dès qu'elle n'est plus visible. Une nouvelle araignée apparaît hors champ et rejoint un robot en marchant.
- **Attaque en cours** : si une attaque ennemie a été lancée il y a moins de 1,5 s, la transition attend, au plus 3 s. Un changement visuel pendant la mesure d'un temps de réaction fausserait l'essai.

---

## 4. Tous les paramètres de l'objet `DistractionDirector`

| Section | Paramètre | Défaut | Effet |
|---|---|---|---|
| Mode | Mode | Adaptive | `Adaptive`, `Fixed` ou `Balanced` (choisi par la Google Sheet, section 1) |
| Mode | Fixed Tier | High | Palier de toute la partie en mode fixe (et pour les sessions fixes en `Balanced`) |
| Mode | Start Tier | High | Palier de départ en mode adaptatif |
| Mode | Practice Tier | High | Palier de la partie d'entraînement |
| Paliers | High, Medium, Low | voir section 2 | Contenu de chaque palier |
| Flashs | Flash Interval, Flash Light, Flash Particles | 6 – 14, 5, 80 | Voir section 2, « Les flashs (brèche et mur de données) » |
| Décision | Window | 15 | Durée de la fenêtre glissante du score (s) |
| Décision | Lower Threshold | 0,40 | Seuil de descente |
| Décision | Upper Threshold | 0,60 | Seuil de montée |
| Décision | Min Tier Duration | 15 | Temps minimum dans un palier, et avant la première décision (s). À garder au moins égal à Window. |
| Décision | Decision Interval | 0,25 | Intervalle entre deux calculs du score (s) |
| Décision | Damage Reference | 0,33 | Part de la vie maximale dont la perte sur la fenêtre donne une note de dégâts de 0. Plus petit = plus sensible. |
| Transition | Transition Duration | 3 | Durée du fondu (s) |
| Transition | Attack Guard | 1,5 | Délai sans attaque ennemie demandé avant une transition (s) |
| Transition | Max Guard Delay | 3 | Attente maximale d'un moment sans attaque (s) |
| Transition | Light Floor | 0,3 | Part de l'intensité gardée à saillance 0 (lumières, effet de téléportation) |
| Sources | Spiders, Beacons, Breach, Portal Stream, Tanks, Ambient, Enemies | branchés par l'outil | Objets de la scène pilotés par le système (section 6) |
| Références | Game, Metrics | branchés par l'outil | `GameManager` et `PerformanceMetrics` |

Le réglage des araignées qui partent se trouve sur l'objet `Spiders` (composant `SpiderSwarm`, section **Arrivées et départs**) : **Leave Speed** (3,5 m/s) et **Offscreen Margin** (0,08, marge autour de l'écran en deçà de laquelle une araignée compte comme visible).
---

## 5. Suivi pendant une partie

Dans l'inspecteur, en Play mode, la section **Suivi en Play mode** affiche :

- **Current Tier** : le palier en cours ;
- **Window Score** : le score de la fenêtre (mode adaptatif seulement) ;
- **Salience** : la saillance appliquée, qui glisse pendant les fondus.

Ces champs sont écrits par le script : les modifier n'a pas d'effet durable.

**À l'écran** : quand la case **Show Performance Score** du `GameHUD` (objet `GameUI`) est cochée, le cadre « Performance » affiche deux lignes de plus :

```
Palier Medium   P 0,39   VERROU 10 s
Sur 15 s : Dégâts 0,76   Esquive 0,59   Précision 0,33   Réactivité 0,77
```

La fin de la première ligne indique l'un des trois états du système :

| État affiché | Sens |
|---|---|
| `VERROU 12 s` | Aucun changement possible pendant encore 12 s. P est calculé mais ignoré. |
| `surveillance : < 0,40 → Low   > 0,60 → High` | P est comparé aux seuils toutes les 0,25 s : le palier change dès qu'il en franchit un. Rien ne change tant que P reste entre les deux. `—` : pas de palier dans ce sens (rien au-dessus de High, rien en dessous de Low). |
| `→ Low dans 1 s (attaque en cours)` | Changement décidé, qui attend un moment sans attaque ennemie (3 s au plus) |

| Élément | Sens |
|---|---|
| Palier | Palier actif (il change au début du fondu) |
| P | Score de la fenêtre glissante, de 0 à 1 |
| Sur 15 s : … | Les quatre notes de la fenêtre, dont P est la moyenne |

En mode fixe, la ligne affiche seulement `Palier High (fixe)`. Le cadre ne s'affiche que dans l'éditeur Unity : jamais dans le jeu exporté, même si la case est cochée.

La console affiche une ligne au début de chaque partie (`Distraction : mode adaptatif, palier High.`) et à chaque changement (`Distraction : High → Medium (score sur 15 s = 0,39).`).

---

## 6. Installation et reconstruction

**Tools > Distraction > Setup Distraction Director** crée l'objet `DistractionDirector` s'il n'existe pas, puis branche :

| Source | Objets de la scène |
|---|---|
| Gyrophares (2) | `Level/AlarmBeacon` : ses spots, son halo et son rotor |
| Brèches (2) | `Level/Decor/BreachSparks` et `BreachLight` (mur est), `BreachSparks_2` et `BreachLight_2` (mur ouest) |
| Flux du portail | `Level/Portal/DataRain` (avec sa traînée), `FlyingGlyphs` et `PortalMotes` |
| Cuves (2) | `Level/SpecimenTank_*` : `Bubbles`, `TankLight` et le spécimen |
| Ambiance | `Level/Atmosphere/AmbientDust` et `Level/Portal/PortalLight` |
| Araignées | l'objet `Spiders` (`SpiderSwarm`) |
| Effet de téléportation | l'`EnemyManager` |

Il branche aussi le `DistractionDirector` sur le `MetricsReporter`, puis sauvegarde la scène. **Les réglages déjà saisis dans l'inspecteur sont conservés.**

La reconstruction de l'arène (**Tools > Level > Build Sci-Fi Arena**) relance ce branchement automatiquement. Si une source manque, la console l'indique au lancement de la partie (`Distraction : source « … » incomplète`).

---

## 7. Données envoyées à la Google Sheet

Ces colonnes s'ajoutent à la ligne de chaque partie mesurée :

| Colonne | Contenu |
|---|---|
| `lod_mode` | `adaptatif`, ou `fixe_high`, `fixe_medium`, `fixe_low` |
| `lod_affectation` | Mode `Balanced` : `feuille` (mode donné par la Google Sheet) ou `hasard` (sans réponse à temps). Vide dans les autres modes. |
| `lod_temps_high_s` | Temps passé en High pendant la partie (s) |
| `lod_temps_medium_s` | Temps passé en Medium (s) |
| `lod_temps_low_s` | Temps passé en Low (s) |

Pour l'affectation des modes, le script lit aussi `session` (colonne de la feuille, le même identifiant que dans l'onglet **Affectations**) et `_derniere_partie` (sans colonne, 1 pour la dernière partie mesurée de la session).

Le temps d'un palier commence au début de la transition vers ce palier. La somme des trois temps est égale à la durée de la partie : le joueur qui meurt réapparaît et continue de jouer.

Les colonnes apparaissent d'elles-mêmes dans la feuille à la première réception, sans modifier le script Google.

---

## 8. Limites à connaître

- **Score et exposition sont liés en mode adaptatif.** Un joueur en difficulté passe plus de temps en Low. Si le groupe adaptatif obtient un meilleur score que le groupe High fixe, c'est en partie parce qu'il a subi moins de distraction en moyenne. Cela ne prouve pas que l'adaptation elle-même aide.
- **Une période calme fait monter le score.** Sans attaque ni sort dans la fenêtre, les notes valent 0,5 (esquive, précision, réactivité) et 1 (dégâts), soit un score de 0,625, au-dessus du seuil de montée.
- **Seuils absolus.** Un joueur qui reste entre 0,40 et 0,60 garde son palier. Le temps passé dans chaque palier dépend donc beaucoup du niveau du joueur.
- **Araignées.** Si toute l'arène est visible à l'écran, une araignée qui doit partir attend au bord de l'arène jusqu'à sortir du champ.

---

## 9. Fichiers

| Fichier | Rôle |
|---|---|
| `Assets/Scripts/Distraction/DistractionDirector.cs` | Paliers, décision, transitions, flashs (brèche, mur de données), temps par palier |
| `Assets/Scripts/Distraction/DistractorGroup.cs` | Une source du décor : applique présence, mouvement et saillance |
| `Assets/Scripts/Research/PerformanceMetrics.cs` | `WindowScore` : score sur la fenêtre glissante |
| `Assets/Scripts/Decor/SpiderSwarm.cs` | Nombre d'araignées, arrivées et départs hors champ |
| `Assets/Scripts/Enemies/EnemyManager.cs` | Taille de l'effet de téléportation (robots, et joueur à sa réapparition via `GameManager`) |
| `Assets/Scripts/Abilities/LightFlicker.cs`, `Environment/Spinner.cs`, `Environment/Bobbing.cs` | Multiplicateurs pilotés par le système |
| `Assets/Scripts/Research/MetricsReporter.cs` | Colonnes `lod_*` ; demande d'affectation |
| `Assets/Scripts/Research/ModeAssignment.cs` | Mode `Balanced` : affectation de la session par la Google Sheet, ou au hasard |
| `Tools/GoogleSheets/ScoreReceiver.gs` | Script de la feuille : réception des mesures, onglet **Affectations** |
| `Assets/Editor/DistractionSetupBuilder.cs` | Menu **Tools > Distraction > Setup Distraction Director** |
