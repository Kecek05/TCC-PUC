using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UI.Game.Shapes.ImmediateComponents;
using Unity.Netcode;
using UnityEngine;

public class ManaCanvas : MonoBehaviour
{
    [Title("References")] 
    [SerializeField] private RectangleImmediateUI manaFill;
    [SerializeField] private RectangleImmediateUI manaBackgroundFill;
    [SerializeField] private TextMeshProUGUI manaText;

    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;
    private NetworkVariable<float> _maxMana;
    private BaseClientManaManager _clientManaManager;
    
    //Bar
    private readonly float _barGap = 0.15f;
    private readonly float _iconWidth = 1f;
    
    private float _filled;
    private float _fraction;
    private float _fillPos;
    private float _fillTotal;
    private float _barFill;

    private bool _initialized = false;

    // ManaBar beats: a whole point gained, the bar filling up, the ceiling rising.
    private int _shownWhole = -1;
    private bool _wasFull;
    private float _maxAtStart = -1f;
    
    private void Start()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();
        _clientManaManager =  ServiceLocator.Get<BaseClientManaManager>();
        
        StartCoroutine(WaitForInitialization());
    }

    private void OnDestroy()
    {
        if (_maxMana != null)
            _maxMana.OnValueChanged -= OnServerMaxManaChanged;
    }

    private IEnumerator WaitForInitialization()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();

        yield return new WaitUntil(() =>
            _teamManager != null &&
            _teamManager.HasLocalTeamBeenAssigned() &&
            _serverManaManager != null);

        _maxMana = _serverManaManager.GetMaxManaNetworkVariable(_teamManager.GetLocalTeam());

        _maxMana.OnValueChanged += OnServerMaxManaChanged;
        _maxAtStart = _maxMana.Value;

        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;
        if (_clientManaManager == null) return;
        
        _filled = Mathf.Floor(_clientManaManager.PredictedMana);
        _fraction = _clientManaManager.PredictedMana - _filled;
        
        _fillPos = _filled * (_iconWidth + _barGap) + _fraction * _iconWidth;
        _fillTotal = _maxMana.Value * (_iconWidth + _barGap);
        _barFill = 1f - (_fillPos / _fillTotal);
        
        manaFill.SetDashFill(_barFill);
        int whole = Mathf.FloorToInt(_clientManaManager.PredictedMana);
        manaText.text = $"{whole}";

        PlayManaBeats(whole);
    }

    /// <summary>
    /// A point that regenerates punches the counter with a mana tick a step higher per point; reaching the
    /// ceiling chimes. Only single-point gains count: a refund or the tutorial's top-up jumps several at once
    /// and stays quiet, so the bar never chatters.
    /// </summary>
    private void PlayManaBeats(int whole)
    {
        bool full = _maxMana != null && whole >= Mathf.FloorToInt(_maxMana.Value);

        if (_shownWhole >= 0 && whole == _shownWhole + 1)
        {
            JuiceFx.Punch(manaText.transform, Juice.Tokens.Scales.PunchSoft, unscaled: false);
            if (full && !_wasFull) Juice.Sfx(SfxId.Full);
            else Juice.Sfx(SfxId.Mana, Mathf.Min(whole, 10), 0.7f);
        }

        _shownWhole = whole;
        _wasFull = full;
    }

    private void OnServerMaxManaChanged(float previousValue, float newValue)
    {
        manaBackgroundFill.SetDashCount(newValue);

        // The ceiling rising is its own beat: "+1 MAX" off the counter, a hard punch, a rising chime.
        if (_maxAtStart < 0f || newValue <= previousValue || previousValue <= 0f) return;

        int gained = Mathf.RoundToInt(newValue - previousValue);
        if (gained <= 0) return;

        CounterAnimator.On(manaText).Float(gained, Juice.Tokens.Colors.ManaGain, " MAX");
        JuiceFx.Punch(manaText.transform, Juice.Tokens.Scales.PunchHard, unscaled: false);
        Juice.Sfx(SfxId.Rise);
    }
}
