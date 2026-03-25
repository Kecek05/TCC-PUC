using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwap : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 100f;
    [SerializeField] private float maxSwipeTime = 0.4f;

    [Header("Camera Positions")]
    [SerializeField] private float downY = 0f;
    [SerializeField] private float upY = 10.125f;
    [SerializeField] private float tweenDuration = 0.4f;
    [SerializeField] private Ease tweenEase = Ease.OutCubic;

    private Vector2 _startPos;
    private float _startTime;
    private bool _swiping;
    private bool _isUp;

    public event Action OnSwipeUp;
    public event Action OnSwipeDown;

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            _startPos = pointer.position.ReadValue();
            _startTime = Time.unscaledTime;
            _swiping = true;
        }

        if (pointer.press.wasReleasedThisFrame && _swiping)
        {
            _swiping = false;
            EvaluateSwipe(pointer.position.ReadValue());
        }
    }

    private void EvaluateSwipe(Vector2 endPos)
    {
        if (Time.unscaledTime - _startTime > maxSwipeTime) return;

        Vector2 delta = endPos - _startPos;

        if (Mathf.Abs(delta.y) < minSwipeDistance) return;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) return;

        if (delta.y < 0f && !_isUp)
        {
            _isUp = true;
            TweenCameraTo(upY);
            OnSwipeUp?.Invoke();
        }
        else if (delta.y > 0f && _isUp)
        {
            _isUp = false;
            TweenCameraTo(downY);
            OnSwipeDown?.Invoke();
        }
    }

    private void TweenCameraTo(float targetY)
    {
        mainCamera.transform.DOKill();
        mainCamera.transform.DOMoveY(targetY, tweenDuration).SetEase(tweenEase);
    }
}