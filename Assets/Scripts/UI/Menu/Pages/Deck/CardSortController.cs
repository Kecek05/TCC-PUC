using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The two ordering buttons above the Card Collection: one cycles the sort key, the other flips the
/// direction. Holds no card data - it only reports the chosen (key, ascending) pair and keeps its own
/// label and arrow in sync.
/// </summary>
public class CardSortController : MonoBehaviour
{
    [Serializable]
    public struct SortKeyOption
    {
        public CardSortKey Key;
        public string Label;
    }

    [Title("References")]
    [SerializeField] private Button typeButton;
    [SerializeField] private TextMeshProUGUI typeButtonText;
    [SerializeField] private Button orderButton;
    [SerializeField] private RectTransform orderIcon;

    [Title("Sort Keys")]
    [InfoBox("Cycled top to bottom each time the type button is tapped.")]
    [TableList(AlwaysExpanded = true)]
    [SerializeField] private List<SortKeyOption> keyCycle = new()
    {
        new SortKeyOption { Key = CardSortKey.Name, Label = "By Name" },
        new SortKeyOption { Key = CardSortKey.Rarity, Label = "By Rarity" },
        new SortKeyOption { Key = CardSortKey.Cost, Label = "By Cost" },
        new SortKeyOption { Key = CardSortKey.Type, Label = "By Type" },
    };

    [Title("Order Icon")]
    [InfoBox("The arrow is rotated rather than swapped, so no second sprite is needed.")]
    [SerializeField] private float ascendingZ;
    [SerializeField] private float descendingZ = 180f;
    [SerializeField] private float flipDuration = 0.2f;
    [SerializeField] private Ease flipEase = Ease.OutBack;

    public event Action<CardSortKey, bool> OnSortChanged;

    private int _keyIndex;
    private bool _ascending = true;

    public CardSortKey Key => keyCycle.Count > 0 ? keyCycle[_keyIndex].Key : CardSortKey.Name;

    public bool Ascending => _ascending;

    public void Initialize(CardSortKey key, bool ascending)
    {
        _keyIndex = Mathf.Max(0, keyCycle.FindIndex(option => option.Key == key));
        _ascending = ascending;

        if (typeButton != null) typeButton.onClick.AddListener(CycleKey);
        if (orderButton != null) orderButton.onClick.AddListener(ToggleOrder);

        RefreshLabel();
        RefreshIcon(animated: false);
    }

    private void CycleKey()
    {
        if (keyCycle.Count == 0) return;

        _keyIndex = (_keyIndex + 1) % keyCycle.Count;
        RefreshLabel();
        OnSortChanged?.Invoke(Key, _ascending);
    }

    private void ToggleOrder()
    {
        _ascending = !_ascending;
        RefreshIcon(animated: true);
        OnSortChanged?.Invoke(Key, _ascending);
    }

    private void RefreshLabel()
    {
        if (typeButtonText == null || keyCycle.Count == 0) return;
        typeButtonText.text = keyCycle[_keyIndex].Label;
    }

    private void RefreshIcon(bool animated)
    {
        if (orderIcon == null) return;

        float targetZ = _ascending ? ascendingZ : descendingZ;
        orderIcon.DOKill();

        if (animated) orderIcon.DOLocalRotate(new Vector3(0f, 0f, targetZ), flipDuration).SetEase(flipEase);
        else orderIcon.localRotation = Quaternion.Euler(0f, 0f, targetZ);
    }

    private void OnDestroy()
    {
        if (typeButton != null) typeButton.onClick.RemoveListener(CycleKey);
        if (orderButton != null) orderButton.onClick.RemoveListener(ToggleOrder);
        if (orderIcon != null) orderIcon.DOKill();
    }
}
