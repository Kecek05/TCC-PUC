using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraSlide : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Camera mainCamera;

    [Header("Camera Positions")]
    [SerializeField] private MapSettingsSO  mapSettingsSO;
    [SerializeField] private float tweenDuration = 0.4f;
    [SerializeField] private Ease tweenEase = Ease.OutCubic;

    [Header("Drag Settings")]
    [SerializeField] private float screenToWorldRatio = 0.015f;
    [SerializeField] private float commitThreshold = 0.3f;
    [SerializeField] private float flickVelocityThreshold = 800f;
    [SerializeField] private float snapBackDuration = 0.3f;
    [SerializeField] private Ease snapBackEase = Ease.OutBack;

    private const int VelocitySamples = 5;
    private readonly float[] _recentDeltas = new float[VelocitySamples];
    private readonly float[] _recentTimes = new float[VelocitySamples];
    private int _sampleIndex;

    private Vector2 _startPos;
    private float _lastPointerY;
    private bool _isUp;
    private float _homeY;
    private bool _initialized;
    private bool _dragging;

    private void Awake()
    {
        float orthoForWidth = mapSettingsSO.TargetWorldWidth / (2f * mainCamera.aspect);
        float orthoForHeight = mapSettingsSO.TargetWorldHeight / 2f;
        mainCamera.orthographicSize = Mathf.Max(orthoForWidth, orthoForHeight);
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            MapTranslator.Instance != null && MapTranslator.Instance.IsInitialized);

        _initialized = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_initialized || !enabled) return;

        _dragging = true;
        mainCamera.transform.DOKill();
        _startPos = eventData.position;
        _lastPointerY = _startPos.y;
        _homeY = _isUp ? mapSettingsSO.BluePlayerMapY : mapSettingsSO.RedPlayerMapY;
        ResetSamples();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        float currentY = eventData.position.y;
        RecordSample(currentY - _lastPointerY, Time.unscaledDeltaTime);
        _lastPointerY = currentY;
        ApplyDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        _dragging = false;
        EvaluateRelease();
    }

    private void OnDisable()
    {
        if (_dragging)
        {
            _dragging = false;
            SnapBack();
        }
    }

    private void ApplyDrag(Vector2 currentPos)
    {
        float dragPixels = currentPos.y - _startPos.y;
        float worldOffset = -dragPixels * screenToWorldRatio;
        float targetY = Mathf.Clamp(_homeY + worldOffset, mapSettingsSO.RedPlayerMapY, mapSettingsSO.BluePlayerMapY);

        mainCamera.transform.position = new Vector3(
            mainCamera.transform.position.x,
            targetY,
            mainCamera.transform.position.z
        );
    }

    private void EvaluateRelease()
    {
        float currentY = mainCamera.transform.position.y;
        float totalDistance = mapSettingsSO.RedPlayerMapY - mapSettingsSO.BluePlayerMapY;
        float progress = (currentY - mapSettingsSO.RedPlayerMapY) / totalDistance;
        float velocity = GetRecentVelocity();
        
        bool flickingToCommit = _isUp
            ? velocity > flickVelocityThreshold
            : velocity < -flickVelocityThreshold;

        bool flickingToCancel = _isUp
            ? velocity < -flickVelocityThreshold
            : velocity > flickVelocityThreshold;

        bool pastThreshold = _isUp
            ? progress <= 1f - commitThreshold
            : progress >= commitThreshold;

        // Flick overrides position, fast intent wins
        if (flickingToCancel)
        {
            SnapBack();
            return;
        }

        if (flickingToCommit || pastThreshold)
        {
            Commit();
            return;
        }

        SnapBack();
    }

    private void Commit()
    {
        if (_isUp)
        {
            _isUp = false;
            TweenCameraTo(mapSettingsSO.RedPlayerMapY);
        }
        else
        {
            _isUp = true;
            TweenCameraTo(mapSettingsSO.BluePlayerMapY);
        }
    }

    private void SnapBack()
    {
        float homeY = _isUp ? mapSettingsSO.BluePlayerMapY : mapSettingsSO.RedPlayerMapY;
        mainCamera.transform.DOKill();
        mainCamera.transform.DOMoveY(homeY, snapBackDuration).SetEase(snapBackEase);
    }

    private void TweenCameraTo(float targetY)
    {
        mainCamera.transform.DOKill();
        mainCamera.transform.DOMoveY(targetY, tweenDuration).SetEase(tweenEase);
    }

    private void ResetSamples()
    {
        _sampleIndex = 0;
        for (int i = 0; i < VelocitySamples; i++)
        {
            _recentDeltas[i] = 0f;
            _recentTimes[i] = 0f;
        }
    }

    private void RecordSample(float deltaPixels, float deltaTime)
    {
        int idx = _sampleIndex % VelocitySamples;
        _recentDeltas[idx] = deltaPixels;
        _recentTimes[idx] = deltaTime;
        _sampleIndex++;
    }

    private float GetRecentVelocity()
    {
        float totalDelta = 0f;
        float totalTime = 0f;
        int count = Mathf.Min(_sampleIndex, VelocitySamples);

        for (int i = 0; i < count; i++)
        {
            totalDelta += _recentDeltas[i];
            totalTime += _recentTimes[i];
        }

        if (totalTime <= 0f) return 0f;
        return totalDelta / totalTime;
    }
}