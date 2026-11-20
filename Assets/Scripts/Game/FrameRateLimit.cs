using UnityEngine;

// Jeu limité à 60 images par seconde sur toutes les machines, dès le lancement : les temps de réaction sont mesurés
// au rythme des images (Time.time), une fréquence commune les rend comparables d'un joueur à l'autre (les écrans
// 120-240 Hz ne donnent pas d'avantage de mesure). Sans effet sur une machine qui n'atteint pas 60 i/s.
public static class FrameRateLimit
{
    public const int FramesPerSecond = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // Avec la synchronisation verticale active, Unity ignore targetFrameRate.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FramesPerSecond;
    }
}
