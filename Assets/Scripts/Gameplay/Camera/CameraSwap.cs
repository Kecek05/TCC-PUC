using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwap : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    [Header("Camera Positions")]
    [SerializeField] private float downY;
    [SerializeField] private float upY = 10.125f;
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
    private bool _swiping;
    private bool _isUp;
    private float _homeY;
    private bool _initialized;

    public event Action OnSwipeUp;
    public event Action OnSwipeDown;

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            MapTranslator.Instance != null && MapTranslator.Instance.IsInitialized);

        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        var pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            mainCamera.transform.DOKill();
            _startPos = pointer.position.ReadValue();
            _lastPointerY = _startPos.y;
            _homeY = _isUp ? upY : downY;
            _swiping = true;
            ResetSamples();
        }

        if (_swiping && pointer.press.isPressed)
        {
            float currentY = pointer.position.ReadValue().y;
            RecordSample(currentY - _lastPointerY, Time.unscaledDeltaTime);
            _lastPointerY = currentY;
            ApplyDrag(pointer.position.ReadValue());
        }

        if (pointer.press.wasReleasedThisFrame && _swiping)
        {
            _swiping = false;
            EvaluateRelease();
        }
    }

    private void ApplyDrag(Vector2 currentPos)
    {
        float dragPixels = currentPos.y - _startPos.y;
        float worldOffset = -dragPixels * screenToWorldRatio;
        float targetY = Mathf.Clamp(_homeY + worldOffset, downY, upY);

        mainCamera.transform.position = new Vector3(
            mainCamera.transform.position.x,
            targetY,
            mainCamera.transform.position.z
        );
    }

    private void EvaluateRelease()
    {
        float currentY = mainCamera.transform.position.y;
        float totalDistance = upY - downY;
        float progress = (currentY - downY) / totalDistance;
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

        // Flick overrides position — fast intent wins
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
            TweenCameraTo(downY);
            OnSwipeDown?.Invoke();
        }
        else
        {
            _isUp = true;
            TweenCameraTo(upY);
            OnSwipeUp?.Invoke();
        }
    }

    private void SnapBack()
    {
        float homeY = _isUp ? upY : downY;
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