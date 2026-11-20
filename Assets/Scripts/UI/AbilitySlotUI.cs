using UnityEngine;
using UnityEngine.UI;

// Une case de la barre de compétences : icône, touche, voile de cooldown et secondes restantes.
public class AbilitySlotUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    // Image en mode Filled (Radial 360) assombrissant l'icône pendant le cooldown.
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private Text cooldownText;
    [SerializeField] private Text keyText;
    [SerializeField] private Image readyFlash;
    [SerializeField] private float flashDuration = 0.3f;

    private AbilityCaster.Slot slot;
    private bool wasOnCooldown;
    private float flashTimer;

    public void Bind(AbilityCaster.Slot slot)
    {
        this.slot = slot;
        bool hasAbility = slot != null && slot.ability != null;

        icon.sprite = hasAbility ? slot.ability.Icon : null;
        icon.color = hasAbility ? Color.white : new Color(0.15f, 0.15f, 0.18f, 1f);
        keyText.text = slot != null ? slot.KeyLabel : "";
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        bool onCooldown = slot != null && slot.CooldownRemaining > 0f;

        cooldownOverlay.fillAmount = onCooldown ? slot.CooldownNormalized : 0f;
        cooldownText.text = onCooldown ? FormatSeconds(slot.CooldownRemaining) : "";

        if (wasOnCooldown && !onCooldown)
            flashTimer = flashDuration;
        wasOnCooldown = onCooldown;

        if (readyFlash != null)
        {
            flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
            Color c = readyFlash.color;
            c.a = flashDuration > 0f ? flashTimer / flashDuration : 0f;
            readyFlash.color = c;
        }
    }

    private static string FormatSeconds(float seconds)
    {
        return seconds < 1f ? seconds.ToString("0.0") : Mathf.CeilToInt(seconds).ToString();
    }
}
