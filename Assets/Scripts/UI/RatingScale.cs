using System;
using UnityEngine;
using UnityEngine.UI;

// Échelle de notation : une rangée de pastilles numérotées (1 à N), une seule sélectionnée à la fois.
public class RatingScale : MonoBehaviour
{
    // Une pastille par note, dans l'ordre croissant, et son disque coloré selon la sélection.
    [SerializeField] private Button[] options;
    [SerializeField] private Image[] fills;
    [SerializeField] private Color normalFill = new Color(0.1f, 0.13f, 0.18f);
    [SerializeField] private Color selectedFill = new Color(1f, 0.82f, 0.35f);
    [SerializeField] private Color normalLabel = Color.white;
    [SerializeField] private Color selectedLabel = new Color(0.08f, 0.08f, 0.1f);

    // Note choisie (0 : aucune).
    public int Value { get; private set; }

    public event Action<int> Changed;

    private void Awake()
    {
        for (int i = 0; i < options.Length; i++)
        {
            int value = i + 1;
            options[i].onClick.AddListener(() => Select(value));
        }
        Refresh();
    }

    public void Clear()
    {
        Value = 0;
        Refresh();
    }

    private void Select(int value)
    {
        Value = value;
        Refresh();
        Changed?.Invoke(value);
    }

    private void Refresh()
    {
        for (int i = 0; i < options.Length; i++)
        {
            bool selected = i + 1 == Value;
            fills[i].color = selected ? selectedFill : normalFill;
            Text label = options[i].GetComponentInChildren<Text>();
            if (label != null)
                label.color = selected ? selectedLabel : normalLabel;
        }
    }
}
