# Mesures de performance — définitions et calculs

Ce document décrit les mesures calculées à chaque partie pour l'étude sur l'effet des distracteurs sur la performance. Ces mesures ne sont jamais affichées au joueur. Elles sont envoyées à la Google Sheet dans l'onglet **« Mesures »**, à raison d'une ligne par partie, avec les réponses au questionnaire NASA-TLX de la partie (section 5).

**Présentation de l'étude.** Le jeu s'ouvre sur la scène `StudyIntro` : message d'information, questionnaire démographique et case de consentement (colonnes en section 7). Le participant ne peut continuer vers l'écran des commandes qu'après avoir tout rempli et coché le consentement. Le message se modifie dans l'inspecteur du composant `StudyIntroScreen` ; les questions et leurs choix dans `Assets/Scripts/Research/ParticipantProfile.cs`, puis **Tools > Game Loop > Build Study Intro**.

**Déroulé d'une session.** Une session comporte **une seule partie mesurée** (`Games Per Session`), pour alléger la passation et limiter les abandons.

1. **Partie d'entraînement de 60 s** (`Practice Duration` du `GameManager` ; 0 la supprime), signalée par un bandeau « ENTRAÎNEMENT », sans questionnaire. Elle se joue au palier High pour tout le monde, avant que les groupes diffèrent : c'est la **mesure de base**. Sa ligne (`partie` = 0) est envoyée dès sa fin.
2. **Partie mesurée de 150 s** (`partie` = 1), dans le mode affecté à la session.
3. Écran de résultat, bouton « Continuer », **questionnaire NASA-TLX**. La ligne de la partie est envoyée **à la validation du questionnaire** : si le participant quitte le jeu avant, cette partie n'est pas enregistrée.
4. **Question ouverte** : sa réponse complète ensuite la ligne déjà envoyée. Un abandon à cette étape ne perd donc que la remarque.
5. Écran de remerciement.

Un envoi qui échoue est retenté trois fois (après 2, 5 et 15 s). Le script de la feuille ignore une ligne déjà reçue (même `session`, même `partie`). Pour un nouveau participant, relancer le jeu.

Code concerné :

| Fichier | Rôle |
|---|---|
| `Assets/Scripts/Research/PerformanceMetrics.cs` | Recueil des événements et calcul des mesures |
| `Assets/Scripts/Research/PerformanceScore.cs` | Score de performance (section 6) |
| `Assets/Scripts/Research/MetricsReporter.cs` | Contenu des lignes envoyées à la Google Sheet |
| `Assets/Scripts/Research/SheetPost.cs` | File d'envoi : ordre des envois, nouvelles tentatives |
| `Assets/Scripts/Combat/AttackRecord.cs` | Suivi de chaque attaque, du lancement à son issue |
| `Tools/GoogleSheets/ScoreReceiver.gs` | Réception côté Google |

---

## 0. Conventions communes

- **Horloge.** Les instants sont relevés avec le temps de jeu Unity (`Time.time`). C'est l'instant de l'image (frame) où l'événement est traité, d'où une résolution d'une image, environ 16,7 ms. Les durées sont converties en millisecondes.
- **Fréquence d'images fixée à 60 i/s** (`Assets/Scripts/Game/FrameRateLimit.cs`, au lancement, synchronisation verticale désactivée). Sans cette limite, la résolution de la mesure et le délai d'affichage dépendraient de l'écran du joueur (7 ms par image à 144 i/s, 17 ms à 60 i/s). Une machine qui n'atteint pas 60 i/s reste en dessous : sa mesure est moins précise et, comme High affiche plus d'effets, la fréquence peut y baisser davantage qu'en Low. Le délai entre la création de l'attaque et son affichage à l'écran (1 à 2 images, plus le délai propre à l'écran) s'ajoute à chaque TR et ne peut pas être mesuré par le jeu.
- **Période mesurée.** Elle va du début de la partie à la fin du temps (`GameManager.EndTime`) : la partie dure toujours jusqu'au bout, même si le joueur meurt (section 1). Le ralenti qui suit la fin n'est pas pris en compte : un événement survenu après `EndTime` est ignoré.
- **Protection après une mort.** Pendant les 2 s d'invulnérabilité qui suivent une réapparition, le joueur ne peut pas être blessé. Les attaques ennemies qui se déroulent, même en partie, pendant cette protection sont **exclues** de l'esquive et du temps de réaction : elles ne disent rien de la capacité à esquiver. L'attaque qui a tué le joueur reste comptée.
- **Attaque.** Une attaque est un sort offensif : une **boule de feu** ou un **laser**. Le **dash** est un déplacement, pas une attaque.
- **Issue d'une attaque.** Elle est connue :
  - pour la boule de feu, quand le projectile percute quelque chose (ennemi, joueur ou décor) ou atteint sa portée maximale ;
  - pour le laser, à la fin de la charge, au moment du tir.

  Une attaque **touche** si elle inflige des dégâts à au moins un adversaire, c'est-à-dire une entité du camp opposé au lanceur. Une attaque arrêtée par le décor ou qui ne rencontre personne **manque**.
- **Attaques en cours à la fin de la partie.** Une attaque dont l'issue n'est pas connue à `EndTime` est **exclue** de tous les calculs : on ne sait pas si elle aurait touché.
- **Valeur vide.** Une mesure non calculable, par exemple une précision quand aucun sort n'a été lancé, est envoyée vide (`null`) et non à 0, pour ne pas fausser les moyennes.

---

## 1. Morts

**Définition.** Nombre de fois où la vie du joueur (1000 PV) tombe à 0 pendant la partie (colonne `morts`).

- À chaque mort, le joueur **réapparaît sur place avec toute sa vie**, invulnérable pendant 2 s (il clignote). La partie continue jusqu'à la fin du temps.
- Tout le monde joue donc la durée complète (150 s) : mêmes conditions d'exposition et nombre d'essais comparable, quel que soit le niveau. Les temps passés dans chaque palier de distraction totalisent toujours la durée de la partie.
- Le score affiché au joueur perd 50 points par mort (`Death Penalty`), et le bonus de vie restante n'est accordé qu'aux parties sans mort.

---

## 2. Temps de réaction (TR) aux attaques ennemies

### Principe

Le temps de réaction est classiquement défini comme l'intervalle entre l'apparition d'un stimulus et la réponse du participant (Science Buddies). Dans un jeu, on peut le recueillir directement pendant la partie, en relevant l'instant du stimulus et celui de la réponse dans le moteur de jeu (Claypool et al., outil *Reflex*, WPI 2025). C'est l'approche retenue ici. Elle mesure un **temps de réaction de choix en situation**, pas un TR simple de laboratoire.

### Essai

Chaque attaque ennemie est un essai :

| | Définition |
|---|---|
| **Stimulus** | Apparition de l'attaque à l'écran : création du projectile de boule de feu, ou apparition de la ligne de visée du laser (début de la charge). Instant `t_stimulus = AttackRecord.LaunchTime`. Le geste d'attaque de l'ennemi, quelques dixièmes de seconde avant, n'est pas pris comme stimulus : il est moins net et varie selon le modèle de robot. |
| **Réponse** | Première **action d'évitement** du joueur : un **ordre de déplacement** (clic droit sur le sol) ou un **appui sur la touche du dash** (vers la souris), même si le dash est encore en recharge. L'appui traduit la réaction, que le dash parte ou non. L'action ne compte que si elle **s'écarte de la trajectoire de l'attaque** (voir ci-dessous). Les sorts offensifs ne sont pas des réponses d'évitement. |
| **TR de l'essai** | `TR = (t_réponse − t_stimulus) × 1000` en ms, pour la **première** réponse qui respecte les conditions ci-dessous. |

### Conditions de validité d'une réponse

1. **Anticipations exclues.** Une réponse donnée moins de **100 ms** après le stimulus ne peut pas être une réaction à ce stimulus : c'est un clic déjà engagé. Elle est ignorée, et l'essai continue d'attendre une réponse valide. Le seuil de 100 ms est une convention répandue pour écarter les réponses anticipées. Il est réglable (`Min Reaction Ms`).
2. **Avant l'impact.** La réponse doit arriver avant l'issue de l'attaque. Réagir après l'impact n'est plus une réaction d'évitement.
3. **Fenêtre maximale de 1 500 ms**, réglable (`Max Reaction Ms`). Au-delà, la réponse n'est plus attribuée à ce stimulus.
4. **Direction d'esquive.** Le déplacement demandé (du joueur vers la destination du clic, ou vers la souris pour le dash) doit s'écarter de la trajectoire de l'attaque d'au moins **30°**, du côté où se trouve le joueur. Avancer vers le tireur, reculer le long de la trajectoire ou traverser vers l'autre côté ne sont pas des esquives : la réponse est ignorée pour cet essai, qui continue d'attendre. Si le joueur est presque sur la trajectoire (à moins de 10 cm), s'écarter d'un côté ou de l'autre convient. La trajectoire est le point de départ et la direction de l'attaque au lancement (ligne du projectile, ou ligne de visée du laser). L'angle est réglable (`Min Evade Angle`).

Exemple : une boule de feu arrive de face. Un clic sur le côté (angle de 90° avec la trajectoire) est une esquive. Un clic vers l'arrière en biais de 20° ne l'est pas : le projectile, plus rapide, rattrape le joueur.

### Omissions

Un essai sans réponse valide, dont l'attaque est arrivée à son terme ou dont la fenêtre est écoulée avant la fin de la partie, est compté comme une **omission** (`tr_omissions`). Les essais encore ouverts à la fin de la partie sont exclus.

### Attaques simultanées

Une même réponse peut valoir pour plusieurs essais en cours. Si deux attaques sont en vol, un seul clic d'évitement répond aux deux, chacune avec son propre délai. C'est cohérent avec la situation de jeu : une seule esquive peut éviter plusieurs tirs.

### Agrégation par partie

| Colonne | Calcul |
|---|---|
| `tr_mediane_ms` | Médiane des TR valides de la partie. **Mesure à privilégier** : les distributions de TR sont asymétriques (queue à droite), et la médiane y est peu sensible aux valeurs extrêmes. |
| `tr_moyenne_ms` | Moyenne arithmétique des TR valides. |
| `tr_ecart_type_ms` | Écart-type échantillon, avec n − 1 au dénominateur. Vide si n < 2. |
| `tr_reponses` | Nombre de TR valides (n). |
| `tr_omissions` | Nombre d'essais sans réponse valide. |

Pour l'analyse, il est conseillé de travailler sur **la distribution entière des TR** plutôt que d'en retirer beaucoup de valeurs a priori : les troncatures agressives augmentent l'erreur et réduisent la sensibilité aux effets (voir les sources sur l'analyse des TR). C'est pourquoi le jeu n'applique que deux filtres, le seuil d'anticipation et la fenêtre maximale. Les données envoyées sont des agrégats par partie : les essais individuels ne sont pas transmis.

### Limites à connaître

- **Résolution d'une image et latence du matériel.** La souris, l'écran et le moteur ajoutent un délai que le logiciel ne voit pas. Ce bruit existe même sur un matériel d'expérimentation standardisé (étude avec un microcontrôleur Arduino comme référence). Pour comparer des conditions, utilisez **le même poste, le même écran et la même fréquence d'images** pour tous les participants, ou mesurez la latence du dispositif.
- **Joueur déjà en mouvement.** Un joueur qui clique sans arrêt pour se déplacer peut produire des réponses qui ne sont pas des réactions au tir. La condition de direction (point 4) écarte les clics qui ne s'éloignent pas de la trajectoire, et le seuil de 100 ms les clics déjà engagés. Un clic sans rapport avec le tir mais qui part par hasard sur le côté est encore compté. Le taux d'esquive (section 4) complète la mesure : il indique si l'action a réellement évité l'attaque.
- **Joueur déjà hors de la trajectoire.** S'il n'a pas besoin de bouger pour être épargné, l'essai sans réponse est compté comme une omission.

---

## 3. Précision (accuracy)

**Définition.** C'est la proportion des sorts offensifs du joueur qui touchent au moins un ennemi.

```
precision = sorts_touches / sorts_lances
```

- `sorts_lances` : boules de feu et lasers lancés par le joueur dont l'issue est connue avant la fin de la partie.
- `sorts_touches` : parmi ceux-ci, les sorts ayant infligé des dégâts à au moins un ennemi.
- Un laser qui traverse plusieurs ennemis compte **une** réussite : la précision mesure si le tir a atteint sa cible, pas le nombre de cibles atteintes.
- Une boule de feu arrêtée par le décor est un **raté**.
- Le dash n'est pas compté.
- Précision par sort :
  - `precision_boule_de_feu = boules_de_feu_touchees / boules_de_feu_lancees`
  - `precision_laser = lasers_touches / lasers_lances`

  Chacune est vide si le sort n'a pas été utilisé.
- Valeurs entre 0 et 1, arrondies à 3 décimales. Elles peuvent être affichées en pourcentage dans la feuille.

Cette définition (tirs réussis / tirs effectués) est celle des études de tir en jeu vidéo, qui relèvent séparément la vitesse et l'erreur des tirs pour étudier le compromis vitesse-précision (Donovan et al., 2022 ; Lenz et al., 2008).

---

## 4. Taux d'esquive (mesure complémentaire)

**Définition.** C'est la proportion des attaques ennemies qui n'ont pas touché le joueur.

```
taux_esquive = attaques_evitees / attaques_ennemies
```

- `attaques_ennemies` : attaques ennemies dont l'issue est connue avant la fin de la partie.
- `attaques_evitees` : celles qui n'ont pas infligé de dégâts au joueur. Une attaque arrêtée par un obstacle du décor compte comme évitée : se mettre à couvert est une esquive.

Cette mesure complète le TR : réagir vite ne suffit pas si l'action d'évitement est mauvaise. Elle dépend aussi du réglage de la visée anticipée des ennemis. Ce réglage doit rester **identique pour toutes les conditions** de l'étude.

---

## 5. Charge de travail perçue (NASA-TLX)

**Questionnaire.** Après chaque partie, le participant note six dimensions de **1 (faible) à 10 (élevée)**. Il doit répondre aux six pour valider. L'en-tête rappelle le participant, la tâche (« Partie de jeu ») et la date. Les textes des questions sont ceux du questionnaire papier fourni (`WorkloadForm.Dimensions` dans `Assets/Scripts/UI/WorkloadForm.cs`).

| Colonne | Dimension |
|---|---|
| `tlx_exigence_mentale` | Exigence mentale |
| `tlx_exigence_physique` | Exigence physique |
| `tlx_exigence_temporelle` | Exigence temporelle |
| `tlx_performance` | Performance : **note haute = réussite perçue élevée** (sens du questionnaire fourni, inverse du NASA-TLX d'origine où l'échelle va de « bon » à « mauvais ») |
| `tlx_effort` | Effort |
| `tlx_frustration` | Frustration |
| `tlx_global` | Score global « Raw TLX » : moyenne des six notes, **avec la performance inversée** (`11 − tlx_performance`) pour qu'une note haute traduise toujours une charge élevée. Vide si une note manque. |

Le score global non pondéré (Raw TLX) évite la phase de comparaison par paires du NASA-TLX complet. Si l'analyse doit utiliser une autre convention, recalculer à partir des six notes brutes.

---

## 6. Score de performance

**Principe.** Le score résume la partie en un nombre de 0 à 100. C'est la **moyenne de quatre notes sur 1**, une par compétence. Il n'utilise **aucun poids** (les quatre notes comptent autant) et **aucune donnée de référence** : ses seules constantes sont des réglages du jeu (durée de la partie, fenêtre de réaction). Il est calculé dans `Assets/Scripts/Research/PerformanceScore.cs`, à partir des mesures des sections 1 à 4.

Ce score est **distinct du score du jeu** (points des ennemis tués, affiché au joueur). Il est **caché par défaut** : voir l'option d'affichage plus bas.

### 6.1 Les quatre notes

**Morts**

```
M = 1 / (1 + morts)
```

Aucune mort : M = 1. Une mort : 0,5. Deux : 0,33. Trois : 0,25. Chaque mort supplémentaire coûte moins que la précédente.

**Esquive** et **précision** : règle de succession de Laplace

```
E = (attaques_evitees + 1) / (attaques_ennemies + 2)
A = (sorts_touches   + 1) / (sorts_lances      + 2)
```

On compte un succès et un échec fictifs en plus des essais réels. Cette correction classique estime une proportion sans hypothèse préalable. Ses effets :

| Situation | Proportion brute | Note |
|---|---|---|
| Aucun essai (aucun sort lancé, aucune attaque subie) | vide | 1/2 = 0,5 (neutre) |
| 1 réussite sur 1 essai | 1 | 2/3 ≈ 0,67 (prudent) |
| 40 réussites sur 50 essais | 0,80 | 41/52 ≈ 0,79 (presque inchangé) |

Avec peu d'essais, la note reste proche de 0,5 : un coup de chance ou de malchance pèse peu. Avec beaucoup d'essais, elle rejoint la proportion réelle. Les essais retenus sont ceux de la section 3 pour la précision et de la section 4 pour l'esquive.

**Réactivité** : taux de réponse × vitesse

```
R = tr_reponses / (tr_reponses + tr_omissions) × (1 − tr_mediane_ms / W)      W = Max Reaction Ms = 1500 ms
                                                                              (R = 0,5 s'il n'y a aucun essai)
```

R mesure à la fois le fait de réagir et la vitesse.
- 100 % de réponses, réaction médiane de 450 ms : R = 0,7.
- 50 % de réponses, même médiane : R = 0,35.
- Aucune réponse : R = 0.
- La médiane n'est pas faussée par une réaction isolée très lente.

Les TR valides et les omissions sont ceux de la section 2. La note se recalcule à partir des colonnes de la feuille. Le produit taux de réponse × vitesse varie continûment : une médiane qui compterait chaque omission à W tomberait brutalement à 0 dès que les omissions dépassent la moitié des essais.

**Limite.** Les attaques qui ne menaçaient pas le joueur restent comptées comme omissions quand il n'a pas réagi (section 2) : R dépend donc aussi de son placement.

**Fenêtre glissante.** Le système de distraction utilise son propre calcul de réactivité sur sa fenêtre de 15 s (`Docs/Parametrage_distraction.md`, section 3).

### 6.2 Le score

```
score_performance = 100 × (M + E + A + R) / 4
```

**Exemple.** Une mort, 14 attaques évitées sur 20, 6 sorts touchés sur 10, 12 réponses et 8 omissions, médiane de 450 ms :

| Note | Calcul | Valeur |
|---|---|---|
| Morts M | 1 / (1 + 1) | 0,500 |
| Esquive E | (14 + 1) / (20 + 2) | 0,682 |
| Précision A | (6 + 1) / (10 + 2) | 0,583 |
| Réactivité R | 12 / 20 × (1 − 450 / 1500) | 0,420 |

score_performance = 100 × (0,500 + 0,682 + 0,583 + 0,420) / 4 ≈ **54,6**

**Propriétés.** Le score est toujours calculable (aucune note n'est vide) et borné entre 0 et 100. Il est comparable d'une partie et d'un participant à l'autre, car ses bornes ne dépendent pas des autres joueurs. Il est robuste au hasard (Laplace sur les proportions, médiane sur les temps). Chaque note peut aussi s'analyser séparément : un distracteur peut faire baisser la précision et monter la réactivité sans changer beaucoup le total.

**Limite.** C'est une moyenne arithmétique : une note très haute compense une note très basse.

### 6.3 Calcul en temps réel et affichage

- Le score est recalculé **4 fois par seconde** pendant la partie (`Live Interval` du `PerformanceMetrics`), sur les mêmes règles qu'en fin de partie avec « maintenant » comme instant de fin. Seules les attaques dont l'issue est déjà connue sont comptées. La note de morts suit les morts déjà survenues. Un calcul prend environ 0,005 ms : il n'a pas d'effet mesurable sur les performances du jeu.
- En début de partie, rien n'est encore mesuré : M = 1, E = A = R = 0,5, soit un score de 62,5.
- À la fin de la partie, le score est figé sur sa valeur finale, celle qui est envoyée à la Google Sheet.
- **Affichage** : case **Show Performance Score** du composant `GameHUD`, sur l'objet `GameUI` de la scène de jeu. Une fois cochée, un cadre sous le score du jeu affiche le score et ses quatre notes. Elle est **décochée par défaut** et doit le rester pendant les passations, pour que le participant ne voie pas sa mesure. Elle se modifie aussi en cours de partie, et elle est conservée quand l'interface est reconstruite (**Tools > Game Loop > Setup Scene**).
- La partie d'entraînement a aussi un score en temps réel (visible si l'option est cochée) ; sa valeur finale est envoyée sur la ligne `partie` = 0.

---

## 7. Colonnes envoyées à la Google Sheet

Onglet « Mesures », une ligne par partie. La date est ajoutée par le script Google.

| Colonne | Contenu |
|---|---|
| `date` | Date et heure de réception |
| `participant` | Code du participant (champ **Participant Id** du `MetricsReporter`). S'il est vide, c'est un identifiant anonyme de 8 caractères propre au **navigateur** : plusieurs personnes sur le même poste partagent le même code. |
| `condition` | Condition expérimentale (champ **Condition** du `MetricsReporter`), par ex. « sans distracteur ». |
| `session` | Identifiant de la session, le même que dans l'onglet **Affectations**. C'est la clé pour relier les lignes entre elles et à leur mode. |
| `partie` | 0 pour l'entraînement (mesure de base), 1 pour la partie mesurée. |
| `morts` | Nombre de morts pendant la partie (section 1) |
| `duree_partie_s` | Durée de la partie |
| `tr_mediane_ms`, `tr_moyenne_ms`, `tr_ecart_type_ms`, `tr_reponses`, `tr_omissions` | Section 2 |
| `precision`, `sorts_lances`, `sorts_touches`, `precision_boule_de_feu`, `precision_laser` | Section 3 |
| `attaques_ennemies`, `attaques_evitees`, `taux_esquive` | Section 4 |
| `note_morts`, `note_esquive`, `note_precision`, `note_reactivite` | Les quatre notes du score, de 0 à 1 (section 6.1) |
| `score_performance` | Score de performance, de 0 à 100 (section 6.2) |
| `lod_mode`, `lod_affectation`, `lod_temps_high_s`, `lod_temps_medium_s`, `lod_temps_low_s` | Système de distraction : mode de la partie, origine du mode en `Balanced` et temps passé dans chaque palier (`Docs/Parametrage_distraction.md`, section 7) |
| `age` | Tranche d'âge : « Moins de 18 ans », « 18 à 24 ans », « 25 à 34 ans », « 35 à 44 ans », « 45 à 54 ans », « 55 ans et + » |
| `temps_jeu_semaine` | « Moins de 2 h », « 2 à 5 h », « 5 à 10 h », « 10 à 20 h », « Plus de 20 h » |
| `experience_moba` | Expérience des MOBA, de 1 (aucune) à 5 (très grande) |
| `experience_hack_n_slash` | Expérience des hack'n'slash, de 1 (aucune) à 5 (très grande) |
| `daltonien` | « Oui » ou « Non » |
| `consentement` | « oui » (le participant ne peut pas commencer sans) |
| `tlx_exigence_mentale` … `tlx_frustration`, `tlx_global` | Section 5 |
| `remarque_libre` | Réponse libre à la question posée une seule fois, après le dernier NASA-TLX et avant le remerciement : « Avez-vous remarqué quelque chose de particulier ou d'inhabituel pendant les parties ? ». Sert à savoir si le joueur a perçu le système de distraction sans le lui suggérer (le système n'est pas nommé). Facultative ; envoyée à part, elle complète la ligne de la partie mesurée. À comparer entre groupes adaptatif et fixe : le groupe fixe donne le taux de « changements » signalés sans adaptation. |
| `version` | Version du jeu |

Sur la ligne de l'entraînement, `lod_mode` vaut `fixe_high` pour tout le monde (le mode affecté se lit sur la ligne `partie` = 1 ou dans **Affectations**), et les colonnes `tlx_*` sont vides.

Les réponses démographiques sont les mêmes sur les deux lignes d'une session. Elles restent vides pour une partie lancée directement depuis la scène de jeu, sans passer par la présentation (tests dans l'éditeur).

Une colonne absente de la feuille s'ajoute d'elle-même à droite à la première réception, sans redéployer le script.

---

## 8. Paramètres réglables (inspecteur Unity, objet `GameManager` sauf mention)

| Composant | Paramètre | Défaut | Effet |
|---|---|---|---|
| `GameManager` | Games Per Session | 1 | Parties mesurées par session avant l'écran de remerciement |
| `GameManager` | Practice Duration | 60 | Durée de la partie d'entraînement non mesurée en début de session (0 : aucune) |
| `GameManager` | Respawn Protection | 2 | Invulnérabilité après une réapparition (s) ; les attaques pendant ce temps sont exclues des mesures (section 0) |
| `GameManager` | Score Rules > Death Penalty | 50 | Points retirés au score affiché pour chaque mort (section 1) |
| `Capsule` (joueur) | Health > Max Health | 1000 | Vie du joueur |
| `PerformanceMetrics` | Min Reaction Ms | 100 | Seuil des anticipations (section 2) |
| `PerformanceMetrics` | Max Reaction Ms | 1500 | Fenêtre maximale de réponse (section 2), et valeur W du score de réactivité (section 6) |
| `PerformanceMetrics` | Min Evade Angle | 30 | Écart minimal (degrés) entre le déplacement et la trajectoire de l'attaque pour qu'une action compte comme réponse (section 2) |
| `PerformanceMetrics` | Live Interval | 0,25 | Intervalle de recalcul du score en temps réel (s) |
| `GameHUD` (objet `GameUI`) | Show Performance Score | décoché | Affiche le score de performance en temps réel (section 6.3) |
| `MetricsReporter` | Participant Id | (vide) | Code du participant |
| `MetricsReporter` | Condition | (vide) | Condition expérimentale |
| `MetricsReporter` | Web App Url | URL du script | Destination des données |
| `MetricsReporter` | Send In Editor | coché | Envoie aussi les parties jouées dans l'éditeur |

Les réglages du système de distraction (mode adaptatif ou fixe, paliers, seuils) sont sur l'objet `DistractionDirector` : voir `Docs/Parametrage_distraction.md`.

---

## 9. Règles d'analyse proposées (à confirmer dans le protocole, avant la collecte)

- **Comparaison principale** : mode affecté (adaptatif ou fixe) sur la ligne `partie` = 1, en ajustant sur la mesure de base (`partie` = 0, même `session`). Le groupe est celui de l'affectation, pas le temps passé dans un palier, qui dépend du niveau du joueur (`Docs/Parametrage_distraction.md`, section 8).
- **Mesure de base à vérifier** : 60 s, c'est court. Contrôler sa corrélation avec la partie mesurée avant de s'y fier.
- **Sessions rejouées** : recharger la page crée une nouvelle session, qui peut tomber dans l'autre mode. `participant` ne suffit pas à repérer ces cas (un même navigateur peut servir à plusieurs personnes). Par `participant` et profil démographique identiques, garder seulement la première session par date.
- **Abandons** : les rapporter par groupe à partir de l'onglet **Affectations**. Aucune ligne `partie` = 0 : arrêt pendant l'entraînement, sans lien avec le mode. Une ligne 0 sans ligne 1 : arrêt pendant ou juste après la partie mesurée, donc possiblement lié au mode. L'équilibrage remplace les sessions abandonnées, mais ne corrige pas un abandon plus fréquent dans un groupe.
- **Limite du plan** : une seule partie mesurée ne permet pas d'étudier l'évolution d'une partie à l'autre (apprentissage, habituation aux distracteurs).

---

## Sources

- Définition du temps de réaction : [Science Buddies — Reaction time](https://www.sciencebuddies.org/science-fair-projects/project_ideas/HumBio_p025.shtml)
- Mesure du TR pendant le jeu, données brutes par essai : [Claypool et al. — Reflex, WPI (QoMEX 2025)](https://web.cs.wpi.edu/~claypool/papers/reflex-qomex-25/paper.pdf)
- Tâches de réponse dans des affichages de type jeu : [Carleton — GI 2025 Response Tasks](https://www.csit.carleton.ca/~rteather/pdfs/GI2025_Response_Tasks.pdf)
- Latence matériel et logiciel dans la mesure du TR : [Liverpool Hope — ASCS 04 0325](https://hira.hope.ac.uk/3604/1/ASCS-04-0325.pdf)
- Vitesse et précision de tir en jeu (Aim Lab) : [Donovan et al. 2022, *Frontiers in Human Neuroscience*](https://www.frontiersin.org/journals/human-neuroscience/articles/10.3389/fnhum.2022.979293/full)
- Temps pour abattre une cible et nombre de tirs selon le périphérique : [Lenz, Chaparro & Chaparro 2008](https://portfolio.erau.edu/en/publications/the-effect-of-input-device-on-first-person-shooter-target-acquisi/)
- Analyse des TR et effets de la troncature : [Springer — DOI 10.1007/BF03395630](https://link.springer.com/doi/10.1007/BF03395630) ; [Baayen & Milin 2010, *Analyzing reaction times*](https://revistas.usb.edu.co/index.php/IJPR/article/view/807) ; [Investigating the effects of common analytical techniques on reaction time data](https://hdl.handle.net/2097/40196)

> Le seuil de 100 ms pour les anticipations est une convention courante dans la littérature sur les TR. Je n'ai pas pu en vérifier le texte d'origine (par ex. Whelan, 2008) lors de mes recherches : citez une référence que vous aurez consultée vous-même.
