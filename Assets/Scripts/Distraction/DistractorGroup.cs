using System;
using UnityEngine;

// Une source de distraction du décor (un gyrophare, la brèche, le flux du portail, une cuve...) : les particules,
// lumières et objets animés qui la composent. Le DistractionDirector règle trois valeurs :
// - présence (0 à 1) : la source est active ou retirée (fondu) ;
// - mouvement (0 à 1) : rotation des gyrophares, flottement des spécimens (fondu jusqu'à l'arrêt) ;
// - saillance s (0 à 1,5) : intensité de la source quand elle est active (1 : décor d'origine).
[Serializable]
public class DistractorGroup
{
    public string name;
    public ParticleSystem[] particles = new ParticleSystem[0];
    public Light[] lights = new Light[0];
    public Spinner[] spinners = new Spinner[0];
    public Bobbing[] bobbers = new Bobbing[0];

    // Valeurs de la scène (celles du palier High), relevées au lancement.
    [NonSerialized] private float[] baseRates;
    [NonSerialized] private ParticleSystem.Burst[][] baseBursts;
    [NonSerialized] private float[] baseIntensities;
    [NonSerialized] private LightFlicker[] flickers;

    [NonSerialized] public float Presence = 1f;
    [NonSerialized] public float Motion = 1f;
    [NonSerialized] public float TargetPresence = 1f;
    [NonSerialized] public float TargetMotion = 1f;
    // Multiplicateur passager des lumières (flashs de la brèche et du mur de données).
    [NonSerialized] public float Boost = 1f;

    public bool IsValid => particles.Length + lights.Length + spinners.Length + bobbers.Length > 0
                           && Array.TrueForAll(particles, p => p != null) && Array.TrueForAll(lights, l => l != null);

    public void Init()
    {
        baseRates = new float[particles.Length];
        baseBursts = new ParticleSystem.Burst[particles.Length][];
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.EmissionModule emission = particles[i].emission;
            baseRates[i] = emission.rateOverTimeMultiplier;
            baseBursts[i] = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(baseBursts[i]);
        }
        baseIntensities = new float[lights.Length];
        flickers = new LightFlicker[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            baseIntensities[i] = lights[i].intensity;
            flickers[i] = lights[i].GetComponent<LightFlicker>();
        }
    }

    // Rapproche présence et mouvement de leur cible (vitesse en unités par seconde).
    public void Step(float rate)
    {
        Presence = Mathf.MoveTowards(Presence, TargetPresence, rate);
        Motion = Mathf.MoveTowards(Motion, TargetMotion, rate);
    }

    public void Snap()
    {
        Presence = TargetPresence;
        Motion = TargetMotion;
    }

    // Applique l'état courant. lightFloor : part de l'intensité lumineuse gardée à saillance nulle.
    public void Apply(float salience, float lightFloor)
    {
        // Particules : nouveaux tirs selon présence × saillance ; celles déjà émises finissent leur vie.
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.EmissionModule emission = particles[i].emission;
            emission.rateOverTimeMultiplier = baseRates[i] * Presence * salience;
            ParticleSystem.Burst[] bursts = baseBursts[i];
            for (int b = 0; b < bursts.Length; b++)
            {
                ParticleSystem.Burst burst = bursts[b];
                ParticleSystem.MinMaxCurve count = burst.count;
                count.constantMin *= salience;
                count.constantMax *= salience;
                burst.count = count;
                burst.probability = Presence;
                emission.SetBurst(b, burst);
            }
        }

        // Lumières : jamais éteintes par la saillance seule (plancher), éteintes par la présence.
        float level = Presence * Mathf.LerpUnclamped(lightFloor, 1f, salience) * Boost;
        for (int i = 0; i < lights.Length; i++)
        {
            if (flickers[i] != null)
            {
                flickers[i].Level = level;
                flickers[i].AmountScale = salience;
            }
            else
            {
                lights[i].intensity = baseIntensities[i] * level;
            }
        }

        foreach (Spinner spinner in spinners)
            spinner.Level = Motion;
        foreach (Bobbing bobber in bobbers)
            bobber.Level = Motion;
    }
}
