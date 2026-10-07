using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class PlayerInfoUI : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private TextMeshProUGUI currentLevelLabel;
    [SerializeField] private TextMeshProUGUI currentXpLabel;
    [SerializeField] private TextMeshProUGUI currentMoneyLabel;
    [SerializeField] private TextMeshProUGUI currentGemsLabel;
    [SerializeField] private TextMeshProUGUI playerNameLabel;
    [SerializeField] private TextMeshProUGUI currentTrophiesLabel;

    [Title("Gold Feedback")]
    [Tooltip("Optional. The coin beside the gold: where coins land and what punches. Empty: the 'Icon' next " +
             "to the gold label.")]
    [SerializeField] private RectTransform moneyIcon;

    [Tooltip("Seconds to wait after the menu opens before gold earned elsewhere (a match) flies in, so the " +
             "scene's own fade-in is over and the player sees it.")]
    [SerializeField, MinValue(0f)] private float arrivalDelay = 0.6f;

    /// <summary>
    /// The gold this header last showed, across scene loads. A menu opening on more than that means gold was
    /// earned while it was away (a match payout banked on the end screen), and that is the moment to count
    /// it in. -1 until a header has shown anything, so the first boot never animates.
    /// </summary>
    private static int s_lastShownGold = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_lastShownGold = -1;

    private BaseClientManager _clientManager;
    private BasePlayerSaveManager _playerSaveManager;
    private CounterAnimator _gold;
    private Tween _arrival;

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();

        // Gold is real now and changes while the menu is open (upgrades, match rewards), so this panel
        // has to react rather than read once. It lives on the persistent header, visible from every page.
        if (ServiceLocator.TryGet(out _playerSaveManager))
            _playerSaveManager.OnGoldChanged += HandleGoldChanged;

        UpdatePlayerInfo();
    }

    private void OnDestroy()
    {
        if (_playerSaveManager != null) _playerSaveManager.OnGoldChanged -= HandleGoldChanged;
        _arrival?.Kill();
    }

    /// <summary>Gold moved while the menu is open: an upgrade spends it, a claim or a purchase adds it.</summary>
    private void HandleGoldChanged()
    {
        int gold = _playerSaveManager.Gold;
        int delta = gold - (_gold != null && _gold.HasValue ? _gold.Shown : gold);
        s_lastShownGold = gold;

        if (_gold == null || delta == 0)
        {
            UpdateCurrentMoneyLabel(gold);
            return;
        }

        if (delta > 0)
        {
            FlyGoldIn(gold, delta, delay: 0f);
            return;
        }

        // Spent: the number rolls down, the cost sinks off it in deny, and the coin answers low.
        _arrival?.Kill();
        _gold.Float(delta);
        _gold.RollTo(gold, Juice.Tokens.Durations.Count * 0.6f);
        Juice.Sfx(SfxId.Coin, -4);
        JuiceFx.Punch(MoneyIcon != null ? MoneyIcon : currentMoneyLabel.transform, Juice.Tokens.Scales.PunchSoft);
    }

    /// <summary>
    /// Coins arc from the middle of the screen to the coin icon and the number rolls up as they land — gold
    /// gained is the one number in the header the player should watch arrive.
    /// </summary>
    private void FlyGoldIn(int gold, int delta, float delay)
    {
        _arrival?.Kill();
        RectTransform icon = MoneyIcon;
        Image iconImage = icon != null ? icon.GetComponent<Image>() : null;

        void Launch()
        {
            if (this == null) return;

            Vector3 from = currentMoneyLabel.canvas != null
                ? currentMoneyLabel.canvas.rootCanvas.transform.position
                : currentMoneyLabel.transform.position;

            int coins = Mathf.Clamp(Mathf.CeilToInt(Mathf.Log(delta + 1, 2f)), 3, 10);
            CoinFlight.Fly(iconImage, from, icon, coins,
                onFirstLanding: remaining =>
                {
                    if (this == null) return;
                    int now = _playerSaveManager != null ? _playerSaveManager.Gold : gold;
                    _gold.RollTo(now, Mathf.Max(remaining, Juice.Tokens.Durations.Count * 0.5f));
                },
                onAllLanded: () =>
                {
                    if (this == null) return;
                    _gold.Float(delta);
                });
        }

        if (delay <= 0f) Launch();
        else _arrival = DOVirtual.DelayedCall(delay, Launch, ignoreTimeScale: true).SetLink(gameObject);
    }

    private RectTransform MoneyIcon
    {
        get
        {
            if (moneyIcon == null && currentMoneyLabel != null && currentMoneyLabel.transform.parent != null)
                moneyIcon = currentMoneyLabel.transform.parent.Find("Icon") as RectTransform;
            return moneyIcon;
        }
    }

    private void UpdatePlayerInfo()
    {
        int gold = _playerSaveManager != null ? _playerSaveManager.Gold : 0;
        _gold = CounterAnimator.On(currentMoneyLabel);

        // Gold earned while the menu was away (a match) counts in from what was last on screen.
        if (s_lastShownGold >= 0 && gold > s_lastShownGold)
        {
            _gold.Set(s_lastShownGold);
            FlyGoldIn(gold, gold - s_lastShownGold, arrivalDelay);
        }
        else
        {
            UpdateCurrentMoneyLabel(gold);
        }

        s_lastShownGold = gold;
        UpdatePlayerNameLabel(_clientManager.UserData.PlayerName);

        //PLACE HOLDERS - account level, xp, gems and trophies are still stubs.
        UpdateCurrentGemsLabel(Random.Range(0, 1000));
        UpdateCurrentLevelLabel(Random.Range(0, 100));
        UpdateCurrentXpLabel(Random.Range(0, 100), 100);
        UpdateCurrentTrophiesLabel(Random.Range(0, 10000));
    }

    private void UpdateCurrentLevelLabel(int level)
    {
        currentLevelLabel.text = level.ToString();
    }

    private void UpdateCurrentXpLabel(int xp, int maxXp)
    {
        currentXpLabel.text = $"{xp}/{maxXp}";
    }

    private void UpdateCurrentMoneyLabel(int money)
    {
        if (_gold != null) _gold.Set(money);
        else currentMoneyLabel.text = $"{money}";
    }

    private void UpdateCurrentGemsLabel(int gems)
    {
        currentGemsLabel.text = $"{gems}";
    }
    
    private void UpdatePlayerNameLabel(string playerName)
    {
        playerNameLabel.text = playerName;
    }

    private void UpdateCurrentTrophiesLabel(int trophies)
    {
        currentTrophiesLabel.text = $"{trophies}";
    }
}
