using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//Only Exists in Client
public class ClientPlayersDataCanvas : MonoBehaviour
{
    [Title(("References"))]
    [InfoBox("The order of the arrays must be Local Player -> Enemy Player")]
    [SerializeField] private TextMeshProUGUI[] playersHealth;
    [SerializeField] private TextMeshProUGUI[] playersWaves;
    [SerializeField] private GameObject[] localPlayerContents;
    [SerializeField] private GameObject[] enemyPlayerContents;
    [SerializeField] private Image[] waveFillImage;
    [Space(5f)]
    
    [Title("Tween Settings")]
    [SerializeField] private float sliderTweenDuration = 0.5f;
    [SerializeField] private Ease tweenEase = Ease.OutBack;

    private BaseServerPlayerHealthManager _playerHealthManager;
    private Tween[] _waveFillTweens = new Tween[2];
    private BaseTeamManager  _teamManager;
    private BaseServerWaveManager  _waveManager;

    // BaseDamage: each health label rolls down on a hit, punches and flashes; the local one also drives the
    // screen overlay (vignette, shake, the away banner).
    private readonly Tween[] _healthRolls = new Tween[2];
    private readonly float[] _shownHealth = { float.NaN, float.NaN };
    private readonly float[] _maxHealth = new float[2];
    private readonly Color[] _healthColors = new Color[2];
    private BaseDamageOverlay _damageOverlay;
    private CameraSide _side = CameraSide.Local;

    private Transform _heart;
    
    private IEnumerator Start()
    {
        _playerHealthManager = ServiceLocator.Get<BaseServerPlayerHealthManager>();
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _waveManager = ServiceLocator.Get<BaseServerWaveManager>();
        
        yield return new WaitUntil(() =>
            _teamManager.HasLocalTeamBeenAssigned()
        );

        for (int i = 0; i < playersHealth.Length && i < _healthColors.Length; i++)
            if (playersHealth[i] != null) _healthColors[i] = playersHealth[i].color;
        _damageOverlay = BaseDamageOverlay.Create(transform);
        if (playersHealth.Length > 0 && playersHealth[0] != null)
        {
            RectTransform pill = playersHealth[0].transform.parent as RectTransform;
            _heart = pill != null ? pill.Find("Heart") : null;
            _damageOverlay.SetHeart(_heart);
        }

        _playerHealthManager.BlueHealth.OnValueChanged += PlayerHealthManager_OnBlueHealthChanged;
        _playerHealthManager.RedHealth.OnValueChanged += PlayerHealthManager_OnRedHealthChanged;
        
        PlayerHealthManager_OnBlueHealthChanged(0, _playerHealthManager.BlueHealth.Value);
        PlayerHealthManager_OnRedHealthChanged(0, _playerHealthManager.RedHealth.Value);
        
        _waveManager.BlueCurrentWave.OnValueChanged += WaveManager_OnBlueWaveChanged;
        _waveManager.RedCurrentWave.OnValueChanged += WaveManager_OnRedWaveChanged;
        
        _waveManager.BlueCurrentWaveProgressNormalized.OnValueChanged += WaveManager_OnBlueWaveProgressChanged;
        _waveManager.RedCurrentWaveProgressNormalized.OnValueChanged += WaveManager_OnRedWaveProgressChanged;
        
        WaveManager_OnBlueWaveChanged(0, _waveManager.BlueCurrentWave.Value);
        WaveManager_OnRedWaveChanged(0, _waveManager.RedCurrentWave.Value);
        WaveManager_OnBlueWaveProgressChanged(0, _waveManager.BlueCurrentWaveProgressNormalized.Value);
        WaveManager_OnRedWaveProgressChanged(0, _waveManager.RedCurrentWaveProgressNormalized.Value);
        
        // Named method (not a lambda) so OnDestroy can unsubscribe. SideChanged is a
        // static event; an un-removed subscription survives this scene and fires on the
        // next match's CameraSlide, hitting this canvas's already-destroyed GameObjects.
        CameraSlide.SideChanged += HandleCameraSideChanged;
    }

    private void HandleCameraSideChanged(CameraSide side)
    {
        GameLog.Info($"Camera side changed to {side}.");
        if (side != _side)
        {
            Juice.Sfx(SfxId.Tick);
            Juice.Haptic(HapticRole.Select);
        }

        _side = side;
        if (side == CameraSide.Local && _damageOverlay != null) _damageOverlay.HideAlert();
        switch (side)
        {
            case CameraSide.Local:
                foreach (GameObject content in localPlayerContents)
                    content.SetActive(true);
                foreach (GameObject content in enemyPlayerContents)
                    content.SetActive(false);
                break;
            case CameraSide.Enemy:
                foreach (GameObject content in localPlayerContents)
                    content.SetActive(false);
                foreach (GameObject content in enemyPlayerContents)
                    content.SetActive(true);
                break;
        }
    }

    private void OnDestroy()
    {
        if (_playerHealthManager != null)
        {
            _playerHealthManager.BlueHealth.OnValueChanged -= PlayerHealthManager_OnBlueHealthChanged;
            _playerHealthManager.RedHealth.OnValueChanged -= PlayerHealthManager_OnRedHealthChanged;
        }

        if (_waveManager != null)
        {
            _waveManager.BlueCurrentWave.OnValueChanged -= WaveManager_OnBlueWaveChanged;
            _waveManager.RedCurrentWave.OnValueChanged -= WaveManager_OnRedWaveChanged;
            _waveManager.BlueCurrentWaveProgressNormalized.OnValueChanged -= WaveManager_OnBlueWaveProgressChanged;
            _waveManager.RedCurrentWaveProgressNormalized.OnValueChanged -= WaveManager_OnRedWaveProgressChanged;
        }

        CameraSlide.SideChanged -= HandleCameraSideChanged;

        foreach (Tween roll in _healthRolls) roll?.Kill();
    }

    private void PlayerHealthManager_OnBlueHealthChanged(float previousValue, float newValue)
    {
        UpdateHealth(previousValue, newValue, _teamManager.GetLocalTeam() == TeamType.Blue);
    }

    private void PlayerHealthManager_OnRedHealthChanged(float previousValue, float newValue)
    {
        UpdateHealth(previousValue, newValue, _teamManager.GetLocalTeam() == TeamType.Red);
    }

    private void UpdateHealth(float previousHealth, float newHealth, bool isLocal)
    {
        int index = isLocal ? 0 : 1;
        _maxHealth[index] = Mathf.Max(_maxHealth[index], newHealth);

        // The first read (previous 0) only states the number; a drop after that is a hit.
        bool hit = previousHealth > 0f && newHealth < previousHealth && !float.IsNaN(_shownHealth[index]);
        float share = _maxHealth[index] > 0f ? newHealth / _maxHealth[index] : 1f;
        ShowHealth(index, newHealth, hit);
        if (!hit || !isLocal || _damageOverlay == null) return;

        int damage = Mathf.RoundToInt(previousHealth - newHealth);
        if (_heart != null) JuiceFx.Punch(_heart, Juice.Tokens.Scales.PunchHard, unscaled: false);
        FloatLoss(playersHealth[index], damage);
        _damageOverlay.Hit(share, _side == CameraSide.Enemy, damage);
    }

    /// <summary>
    /// The number rolls down to the new health (ease-out over a short dur-count), punches and flashes in the
    /// deny colour. The opponent's label gets the same, smaller, beat: their base taking a leak is good news.
    /// </summary>
    private void ShowHealth(int index, float value, bool animate)
    {
        TextMeshProUGUI label = playersHealth[index];
        _healthRolls[index]?.Kill();

        if (!animate || float.IsNaN(_shownHealth[index]) || Juice.ReduceMotion)
        {
            _shownHealth[index] = value;
            label.text = $"{value}";
            return;
        }

        JuiceTokensSO t = Juice.Tokens;
        Color rest = _healthColors[index];
        float from = _shownHealth[index];

        label.color = t.Colors.Deny;
        _healthRolls[index] = DOTween.Sequence().SetLink(label.gameObject)
            .Join(DOVirtual.Float(from, value, t.Durations.Count * 0.6f, v =>
            {
                _shownHealth[index] = v;
                label.text = Mathf.Round(v).ToString();
            }).SetEase(t.Eases.Out))
            .Join(label.DOColor(rest, t.Durations.Settle).SetEase(t.Eases.In))
            .OnKill(() =>
            {
                if (label == null) return;
                _shownHealth[index] = value;
                label.text = $"{value}";
                label.color = rest;
            });

        JuiceFx.Punch(label.transform, index == 0 ? t.Scales.PunchHard : t.Scales.PunchSoft, unscaled: false);
    }

    /// <summary>"-N" rises off the health label in the deny colour and fades: what the hit cost.</summary>
    private static void FloatLoss(TextMeshProUGUI label, int amount)
    {
        if (label == null || amount <= 0) return;

        JuiceTokensSO t = Juice.Tokens;
        TextMeshProUGUI loss = Instantiate(label, label.transform.parent);
        loss.name = "Loss";
        loss.text = $"-{amount}";
        loss.color = t.Colors.Deny;
        loss.fontSize = label.fontSize * 0.7f;
        loss.raycastTarget = false;

        RectTransform rect = loss.rectTransform;
        rect.anchoredPosition += new Vector2(70f, 0f);
        Vector2 from = rect.anchoredPosition;
        DOTween.Sequence().SetLink(loss.gameObject)
            .Join(rect.DOAnchorPos(from + Vector2.up * 34f, t.Durations.Count).SetEase(t.Eases.Out))
            .Insert(t.Durations.Count * 0.5f, loss.DOFade(0f, t.Durations.Count * 0.5f).SetEase(t.Eases.In))
            .OnComplete(() => Destroy(loss.gameObject));
    }

    private void WaveManager_OnBlueWaveChanged(int previousValue, int newValue)
    {
        ChangeWaveCount(newValue, _teamManager.GetLocalTeam() == TeamType.Blue);
    }
    
    private void WaveManager_OnRedWaveChanged(int previousValue, int newValue)
    {
        ChangeWaveCount(newValue, _teamManager.GetLocalTeam() == TeamType.Red);
    }

    private void ChangeWaveCount(int newWave, bool isLocal)
    {
        playersWaves[isLocal ? 0 : 1].text = isLocal ?  $"{newWave}/{_waveManager.GetTotalWaves()}" : $"{newWave}";
    }
    
    private void WaveManager_OnBlueWaveProgressChanged(float previousValue, float newValue)
    {
        ChangeWaveProgress(newValue, _teamManager.GetLocalTeam() == TeamType.Blue);
    }
    
    private void WaveManager_OnRedWaveProgressChanged(float previousValue, float newValue)
    {
        ChangeWaveProgress(newValue, _teamManager.GetLocalTeam() == TeamType.Red);
    }
    
    private void ChangeWaveProgress(float newProgress, bool isLocal)
    {
        int index = isLocal ? 0 : 1;
        _waveFillTweens[index]?.Kill();
        _waveFillTweens[index] = DOTween.To(
            () => waveFillImage[index].fillAmount, 
            x => waveFillImage[index].fillAmount = x, 
            newProgress, sliderTweenDuration)
            .SetEase(tweenEase);
    }
}
