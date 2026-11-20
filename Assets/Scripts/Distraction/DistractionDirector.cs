using System;
using System.Collections.Generic;
using UnityEngine;

public enum DistractionTier
{
    High,
    Medium,
    Low,
}

public enum DistractionMode
{
    // Le palier suit la performance du joueur (fenêtre glissante).
    Adaptive,
    // Le palier reste celui de Fixed Tier toute la partie.
    Fixed,
    // Adaptatif ou fixe selon la session, choisi par la Google Sheet pour garder autant de sessions dans chaque
    // mode (ModeAssignment).
    Balanced,
}

// Ce qui est actif dans un palier, et avec quelle intensité.
[Serializable]
public class TierSettings
{
    [Range(0, 12)] public int spiders;
    // Gyrophares allumés (sur 2), et s'ils tournent.
    [Range(0, 2)] public int beacons;
    public bool beaconsRotate;
    // Brèches électriques actives (sur 2 : mur est, mur ouest).
    [Range(0, 2)] public int breaches;
    // Flashs soudains des brèches actives, à intervalles imprévisibles.
    public bool breachFlash;
    public bool portalStream;
    // Sursauts du mur de données du portail (lumière et rafale de caractères), avec le flux du portail actif.
    public bool portalFlash;
    // Cuves animées (bulles, lumière, spécimen qui flotte), sur 2.
    [Range(0, 2)] public int tanks;
    // Intensité des sources actives (particules, lumières, effet de téléportation) : 1 = décor d'origine,
    // au-dessus : renforcé.
    [Range(0f, 1.5f)] public float salience;

    public TierSettings(int spiders, int beacons, bool beaconsRotate, int breaches, bool breachFlash, bool portalStream, bool portalFlash, int tanks, float salience)
    {
        this.spiders = spiders;
        this.beacons = beacons;
        this.beaconsRotate = beaconsRotate;
        this.breaches = breaches;
        this.breachFlash = breachFlash;
        this.portalStream = portalStream;
        this.portalFlash = portalFlash;
        this.tanks = tanks;
        this.salience = salience;
    }
}

// Système de distraction de l'étude : règle les distracteurs de l'arène selon trois paliers (High, Medium, Low).
// Mode fixe : un palier pour toute la partie. Mode adaptatif : le palier baisse quand le score des 15 dernières
// secondes passe sous un seuil, et remonte quand il dépasse l'autre ; il reste ensuite au moins 15 s dans le
// nouveau palier. Les changements se font en fondu ; les araignées arrivent et partent hors champ.
// Tous les réglages sont sur cet objet. Détail : Docs/Parametrage_distraction.md.
public class DistractionDirector : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private DistractionMode mode = DistractionMode.Adaptive;
    // Palier de toute la partie en mode fixe.
    [SerializeField] private DistractionTier fixedTier = DistractionTier.High;
    // Palier de départ en mode adaptatif.
    [SerializeField] private DistractionTier startTier = DistractionTier.High;
    // Palier de la partie d'entraînement (le système n'y est jamais adaptatif).
    [SerializeField] private DistractionTier practiceTier = DistractionTier.High;

    [Header("Paliers")]
    // Saillance identique dans les trois paliers (variable contrôlée) : seul le nombre de distracteurs change.
    [SerializeField] private TierSettings high = new TierSettings(12, 2, true, 2, true, true, true, 2, 1f);
    [SerializeField] private TierSettings medium = new TierSettings(8, 2, true, 1, false, true, false, 2, 1f);
    [SerializeField] private TierSettings low = new TierSettings(4, 2, false, 0, false, true, false, 0, 1f);

    [Header("Flashs (brèche, mur de données)")]
    // Temps entre deux flashs (aléatoire entre x et y, secondes). Chaque flash tombe au hasard sur l'une des
    // sources permises par le palier.
    [SerializeField] private Vector2 flashInterval = new Vector2(6f, 14f);
    // Intensité de la lumière de la source au pic du flash (× son intensité courante).
    [SerializeField] private float flashLight = 5f;
    // Particules projetées par chaque système de la source (étincelles, caractères), × saillance.
    [SerializeField] private int flashParticles = 80;

    [Header("Décision (mode adaptatif)")]
    // Durée de la fenêtre glissante du score (secondes).
    [SerializeField] private float window = 15f;
    // Score de la fenêtre (0 à 1) en dessous duquel on descend d'un palier.
    [SerializeField, Range(0f, 1f)] private float lowerThreshold = 0.4f;
    // Score de la fenêtre au-dessus duquel on monte d'un palier.
    [SerializeField, Range(0f, 1f)] private float upperThreshold = 0.6f;
    // Temps minimum dans un palier, et avant la première décision (secondes) : au moins la durée de la fenêtre,
    // pour que le score ne porte que sur le palier en cours.
    [SerializeField] private float minTierDuration = 15f;
    // Intervalle entre deux calculs du score (secondes).
    [SerializeField] private float decisionInterval = 0.25f;
    // Note de dégâts de la fenêtre : part de la vie maximale dont la perte sur la fenêtre donne 0
    // (0,33 avec 1000 PV : perdre 330 PV en 15 s, un coup pesant environ 0,15). Plus petit = plus sensible.
    [SerializeField, Range(0.05f, 1f)] private float damageReference = 0.33f;

    [Header("Transition")]
    // Durée du fondu d'un palier à l'autre (secondes).
    [SerializeField] private float transitionDuration = 3f;
    // Une transition attend qu'aucune attaque ennemie n'ait été lancée depuis ce délai (secondes)...
    [SerializeField] private float attackGuard = 1.5f;
    // ... mais pas plus longtemps que ceci (secondes).
    [SerializeField] private float maxGuardDelay = 3f;
    // Part de l'intensité des lumières et de la taille de l'effet de téléportation gardée à saillance 0.
    [SerializeField, Range(0f, 1f)] private float lightFloor = 0.3f;

    [Header("Sources (remplies par Tools > Distraction > Setup Distraction Director)")]
    [SerializeField] private SpiderSwarm spiders;
    [SerializeField] private DistractorGroup[] beacons = new DistractorGroup[0];
    [SerializeField] private DistractorGroup[] breaches = new DistractorGroup[0];
    [SerializeField] private DistractorGroup portalStream = new DistractorGroup();
    [SerializeField] private DistractorGroup[] tanks = new DistractorGroup[0];
    // Poussière et lumière du portail : toujours présentes, ne suivent que la saillance.
    [SerializeField] private DistractorGroup ambient = new DistractorGroup();
    // Effet de téléportation des robots : toujours joué, ne suit que la saillance.
    [SerializeField] private EnemyManager enemies;

    [Header("Références")]
    [SerializeField] private GameManager game;
    [SerializeField] private PerformanceMetrics metrics;

    [Header("Suivi en Play mode (lecture seule)")]
    [SerializeField] private DistractionTier currentTier;
    [SerializeField] private float windowScore;
    [SerializeField] private float salience;

    private readonly float[] timeInTier = new float[3];
    private WindowScore windowNotes;
    // Toutes les sources du décor, ambiance comprise.
    private DistractorGroup[] groups;
    private bool adaptive;
    private float lockedUntil;
    private float nextDecision;
    private float lastEnemyAttack = float.NegativeInfinity;
    // Changement décidé, en attente d'un moment sans attaque récente.
    private DistractionTier? pending;
    private float pendingSince;
    private float targetSalience;
    private float salienceRate;
    private float nextFlash;
    private float flashTimer;
    // Brèches actives, candidates au prochain flash.
    private readonly List<DistractorGroup> flashBreaches = new List<DistractorGroup>();
    // Source dont la lumière porte le flash en cours (brèche, ou ambiance pour la lumière du portail).
    private DistractorGroup flashLights;

    // Durée du flash : pic immédiat, puis retour à la normale (secondes).
    private const float FlashDuration = 0.3f;

    public DistractionTier Tier => currentTier;
    public bool IsBalanced => mode == DistractionMode.Balanced;
    public bool IsAdaptive => adaptive;
    // Suivi de la décision (HUD de débogage) : fenêtre, dernier score calculé, seuils, verrou restant, changement
    // décidé en attente d'un moment sans attaque.
    public float Window => window;
    public WindowScore WindowNotes => windowNotes;
    public float LowerThreshold => lowerThreshold;
    public float UpperThreshold => upperThreshold;
    public float LockRemaining => Mathf.Max(0f, lockedUntil - Time.time);
    public DistractionTier? Pending => pending;
    // Attente restante avant que le changement en attente commence (fin du délai après une attaque, ou attente maximale).
    public float PendingRemaining => pending.HasValue ? Mathf.Max(0f, Mathf.Min(lastEnemyAttack + attackGuard, pendingSince + maxGuardDelay) - Time.time) : 0f;
    // Mode de la partie pour la Google Sheet : « adaptatif » ou « fixe_high », « fixe_medium », « fixe_low ».
    public string ModeLabel => adaptive ? "adaptatif" : "fixe_" + currentTier.ToString().ToLowerInvariant();

    // Temps passé dans un palier depuis le début de la partie (figé à la fin de la partie).
    public float TimeIn(DistractionTier tier) => timeInTier[(int)tier];

    private void OnEnable()
    {
        AttackRecord.Launched += OnAttackLaunched;
    }

    private void OnDisable()
    {
        AttackRecord.Launched -= OnAttackLaunched;
    }

    private void Start()
    {
        var all = new List<DistractorGroup>(beacons);
        all.AddRange(breaches);
        all.Add(portalStream);
        all.AddRange(tanks);
        all.Add(ambient);
        groups = all.ToArray();
        foreach (DistractorGroup group in groups)
        {
            if (group.IsValid)
                group.Init();
            else
                Debug.LogWarning($"Distraction : source « {group.name} » incomplète. Lancez Tools > Distraction > Setup Distraction Director.");
        }

        adaptive = !game.IsPractice && (mode == DistractionMode.Adaptive || (mode == DistractionMode.Balanced && ModeAssignment.Resolve()));
        DistractionTier tier = game.IsPractice ? practiceTier : adaptive ? startTier : fixedTier;
        SetTier(tier, true);
        lockedUntil = Time.time + minTierDuration;
        Debug.Log($"Distraction : mode {ModeLabel}, palier {tier}.");
    }

    private void OnAttackLaunched(AttackRecord attack)
    {
        if (attack.CasterTeam == Team.Enemy)
            lastEnemyAttack = Time.time;
    }

    private void Update()
    {
        if (!game.IsOver)
        {
            timeInTier[(int)currentTier] += Time.deltaTime;
            if (adaptive)
                Decide();
        }

        // Fondus.
        float dt = Time.deltaTime;
        salience = Mathf.MoveTowards(salience, targetSalience, salienceRate * dt);
        UpdateFlash(dt);
        foreach (DistractorGroup group in groups)
        {
            if (!group.IsValid)
                continue;
            group.Step(dt / transitionDuration);
            group.Apply(salience, lightFloor);
        }
        if (enemies != null)
            enemies.TeleportVfxScale = Mathf.LerpUnclamped(lightFloor, 1f, salience);
    }

    // Flashs à intervalles aléatoires, chacun sur la brèche (gerbe d'étincelles) ou sur le mur de données du portail
    // (rafale de caractères), avec un pic de la lumière de la source. Un seul minutage pour les deux, indépendant du
    // jeu : un flash peut tomber pendant une attaque (c'est un distracteur, il doit pouvoir gêner la tâche).
    private void UpdateFlash(float dt)
    {
        flashTimer = Mathf.Max(0f, flashTimer - dt);
        foreach (DistractorGroup b in breaches)
            b.Boost = 1f;
        ambient.Boost = 1f;
        if (flashLights != null)
            flashLights.Boost = 1f + (flashLight - 1f) * flashTimer / FlashDuration;

        TierSettings settings = Settings(currentTier);
        flashBreaches.Clear();
        if (settings.breachFlash)
            foreach (DistractorGroup b in breaches)
                if (b.IsValid && b.TargetPresence > 0f)
                    flashBreaches.Add(b);
        bool atBreach = flashBreaches.Count > 0;
        bool atPortal = settings.portalFlash && portalStream.IsValid && portalStream.TargetPresence > 0f && ambient.IsValid;
        if ((!atBreach && !atPortal) || game.IsOver)
        {
            nextFlash = Time.time + UnityEngine.Random.Range(flashInterval.x, flashInterval.y);
            return;
        }
        if (Time.time < nextFlash)
            return;

        nextFlash = Time.time + UnityEngine.Random.Range(flashInterval.x, flashInterval.y);
        if (atBreach && atPortal)
            atBreach = UnityEngine.Random.value < 0.5f;
        DistractorGroup breach = atBreach ? flashBreaches[UnityEngine.Random.Range(0, flashBreaches.Count)] : null;
        DistractorGroup source = atBreach ? breach : portalStream;
        flashLights = atBreach ? breach : ambient;
        flashTimer = FlashDuration;
        foreach (ParticleSystem particles in source.particles)
            particles.Emit(Mathf.RoundToInt(flashParticles * salience * source.Presence));
    }

    private void Decide()
    {
        if (Time.time >= nextDecision)
        {
            nextDecision = Time.time + decisionInterval;
            windowNotes = metrics.MeasureWindow(window, damageReference);
            windowScore = windowNotes.Total;
            if (pending == null && Time.time >= lockedUntil)
            {
                if (windowScore < lowerThreshold && currentTier != DistractionTier.Low)
                    pending = currentTier + 1;
                else if (windowScore > upperThreshold && currentTier != DistractionTier.High)
                    pending = currentTier - 1;
                if (pending.HasValue)
                    pendingSince = Time.time;
            }
        }

        // Pas de changement visuel pendant la mesure d'un temps de réaction (sauf attente trop longue).
        if (pending.HasValue && (Time.time - lastEnemyAttack >= attackGuard || Time.time - pendingSince >= maxGuardDelay))
        {
            Debug.Log($"Distraction : {currentTier} → {pending.Value} (score sur {window:0} s = {windowScore:0.00}).");
            SetTier(pending.Value, false);
            pending = null;
            lockedUntil = Time.time + minTierDuration;
        }
    }

    private void SetTier(DistractionTier tier, bool instant)
    {
        currentTier = tier;
        TierSettings settings = Settings(tier);

        for (int i = 0; i < beacons.Length; i++)
        {
            beacons[i].TargetPresence = i < settings.beacons ? 1f : 0f;
            beacons[i].TargetMotion = settings.beaconsRotate ? 1f : 0f;
        }
        for (int i = 0; i < breaches.Length; i++)
            breaches[i].TargetPresence = i < settings.breaches ? 1f : 0f;
        portalStream.TargetPresence = settings.portalStream ? 1f : 0f;
        for (int i = 0; i < tanks.Length; i++)
        {
            float on = i < settings.tanks ? 1f : 0f;
            tanks[i].TargetPresence = on;
            tanks[i].TargetMotion = on;
        }

        targetSalience = settings.salience;
        if (instant)
        {
            salience = targetSalience;
            foreach (DistractorGroup group in groups)
                group.Snap();
        }
        salienceRate = transitionDuration > 0f ? Mathf.Abs(targetSalience - salience) / transitionDuration : float.MaxValue;

        if (spiders != null)
            spiders.SetPresentCount(settings.spiders, instant);
    }

    private TierSettings Settings(DistractionTier tier) => tier == DistractionTier.High ? high : tier == DistractionTier.Medium ? medium : low;

#if UNITY_EDITOR
    // Branchement des sources par l'outil d'installation (les réglages de l'inspecteur sont conservés).
    public void SetSources(SpiderSwarm spiders, DistractorGroup[] beacons, DistractorGroup[] breaches, DistractorGroup portalStream,
        DistractorGroup[] tanks, DistractorGroup ambient, EnemyManager enemies, GameManager game, PerformanceMetrics metrics)
    {
        this.spiders = spiders;
        this.beacons = beacons;
        this.breaches = breaches;
        this.portalStream = portalStream;
        this.tanks = tanks;
        this.ambient = ambient;
        this.enemies = enemies;
        this.game = game;
        this.metrics = metrics;
    }
#endif
}
