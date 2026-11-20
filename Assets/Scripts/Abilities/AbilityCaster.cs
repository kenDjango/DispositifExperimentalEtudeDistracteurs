using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Gère les compétences du joueur : touches, cooldowns et lancement vers la souris.
public class AbilityCaster : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public Ability ability;
        // "#(a)" = la touche marquée A sur le clavier (fonctionne en AZERTY comme en QWERTY).
        public string keyBinding = "<Keyboard>/#(a)";

        [NonSerialized] public float CooldownRemaining;
        [NonSerialized] public InputAction Action;

        public bool IsReady => ability != null && CooldownRemaining <= 0f;
        public float CooldownNormalized => ability != null && ability.Cooldown > 0f ? CooldownRemaining / ability.Cooldown : 0f;
        // Libellé de la touche principale (sans les positions de remplacement du navigateur).
        public string KeyLabel => Action != null ? Action.GetBindingDisplayString(0) : "";
    }

    // Dans un navigateur, Unity ne connaît pas la disposition du clavier : « #(a) » y désigne la touche placée en A
    // sur un clavier QWERTY. On accepte donc aussi la position de la lettre sur un clavier AZERTY (lettre → touche
    // QWERTY au même endroit). Seules les lettres qui changent de place sont listées.
    private static readonly (char Letter, string AzertyPosition)[] AzertyPositions =
    {
        ('a', "q"), ('q', "a"), ('z', "w"), ('w', "z"), ('m', "semicolon"),
    };

    [SerializeField] private Camera viewCamera;
    [SerializeField] private float groundHeight = 0f;
    [SerializeField] private Slot[] slots =
    {
        new Slot { keyBinding = "<Keyboard>/#(a)" },
        new Slot { keyBinding = "<Keyboard>/#(z)" },
        new Slot { keyBinding = "<Keyboard>/#(e)" },
    };

    public Slot[] Slots => slots;

    // (index du slot)
    public event Action<int> AbilityCast;
    // Touche pressée, que la compétence soit prête ou non (index du slot).
    public event Action<int> AbilityKeyPressed;

    private void Awake()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;

        foreach (Slot slot in slots)
        {
            slot.Action = new InputAction(type: InputActionType.Button, binding: slot.keyBinding);
#if UNITY_WEBGL && !UNITY_EDITOR
            foreach ((char letter, string position) in AzertyPositions)
                if (slot.keyBinding == $"<Keyboard>/#({letter})")
                    slot.Action.AddBinding($"<Keyboard>/{position}");
#endif
        }
    }

    private void OnEnable()
    {
        foreach (Slot slot in slots)
            slot.Action?.Enable();
    }

    private void OnDisable()
    {
        foreach (Slot slot in slots)
            slot.Action?.Disable();
    }

    private void OnDestroy()
    {
        foreach (Slot slot in slots)
            slot.Action?.Dispose();
    }

    private void Update()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot.CooldownRemaining > 0f)
                slot.CooldownRemaining = Mathf.Max(0f, slot.CooldownRemaining - Time.deltaTime);

            if (slot.Action.WasPressedThisFrame())
            {
                AbilityKeyPressed?.Invoke(i);
                TryCast(i);
            }
        }
    }

    public bool TryCast(int index)
    {
        Slot slot = slots[index];
        if (!slot.IsReady)
            return false;

        Vector3 aimPoint = GetMouseGroundPoint();
        Vector3 toAim = aimPoint - transform.position;
        toAim.y = 0f;
        if (toAim.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toAim);

        slot.ability.Cast(new AbilityContext
        {
            Caster = gameObject,
            Origin = transform.position,
            AimPoint = aimPoint,
        });
        slot.CooldownRemaining = slot.ability.Cooldown;
        AbilityCast?.Invoke(index);
        return true;
    }

    // Remet à zéro le cooldown des compétences de ce type (ex. recharge de dash ramassée).
    // Vrai si l'une d'elles était en recharge.
    public bool ResetCooldown<T>() where T : Ability
    {
        bool reset = false;
        foreach (Slot slot in slots)
        {
            if (slot.ability is T && slot.CooldownRemaining > 0f)
            {
                slot.CooldownRemaining = 0f;
                reset = true;
            }
        }
        return reset;
    }

    // Point visé par la souris sur le sol (direction d'un dash, par exemple).
    public Vector3 GetMouseGroundPoint()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || viewCamera == null)
            return transform.position + transform.forward;

        Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position + transform.forward;
    }
}
