using UnityEngine;

// Barre de compétences en bas de l'écran : relie chaque case à un slot du AbilityCaster.
public class AbilityBarUI : MonoBehaviour
{
    [SerializeField] private AbilityCaster caster;
    [SerializeField] private AbilitySlotUI[] slotViews;

    private void Start()
    {
        for (int i = 0; i < slotViews.Length; i++)
            slotViews[i].Bind(caster != null && i < caster.Slots.Length ? caster.Slots[i] : null);
    }
}
