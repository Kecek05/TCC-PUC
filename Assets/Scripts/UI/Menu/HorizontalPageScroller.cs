using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class HorizontalPageScroller : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Title("Snap Settings")]
    [SerializeField] private float snapDuration = 0.4f;
    [SerializeField] private Ease snapEase = Ease.OutCubic;
    [SerializeField] private float flickVelocityThreshold = 800f;

    [Title("Initial State")]
    [SerializeField] private int startingPageIndex = 0;

    public event Action<int> OnPageChanged;
    public int CurrentPageIndex => _currentPageIndex;
    public int PageCount => _content != null ? _content.childCount : 0;

    private const int VelocitySamples = 5;
    private readonly float[] _recentDeltas = new float[VelocitySamples];
    private readonly float[] _recentTimes = new float[VelocitySamples];
    private int _sampleIndex;

    private ScrollRect _scrollRect;
    private RectTransform _viewport;
    private RectTransform _content;
    private float _lastPointerX;
    private bool _dragging;
    private int _currentPageIndex;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        _viewport = _scrollRect.viewport != null
            ? _scrollRect.viewport
            : (RectTransform)_scrollRect.transform;
        _content = _scrollRect.content;
    }

    private void Start()
    {
        _currentPageIndex = Mathf.Clamp(startingPageIndex, 0, Mathf.Max(0, PageCount - 1));
        SnapImmediate(_currentPageIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!enabled || PageCount <= 1) return;

        _dragging = true;
        _content.DOKill();
        // Restore elastic so the rubber-band feel works during drag, even if a previous tween was killed mid-flight.
        _scrollRect.movementType = ScrollRect.MovementType.Elastic;
        _lastPointerX = eventData.position.x;
        ResetSamples();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        float currentX = eventData.position.x;
        RecordSample(currentX - _lastPointerX, Time.unscaledDeltaTime);
        _lastPointerX = currentX;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        _dragging = false;
        EvaluateRelease();
    }

    public void GoToPage(int index, bool animated)
    {
        if (PageCount <= 0) return;

        index = Mathf.Clamp(index, 0, PageCount - 1);
        _dragging = false;

        bool changed = index != _currentPageIndex;
        _currentPageIndex = index;

        if (animated)
        {
            TweenContentTo(GetTargetX(index));
        }
        else
        {
            SnapImmediate(index);
        }

        if (changed)
        {
            OnPageChanged?.Invoke(_currentPageIndex);
        }
    }

    private void EvaluateRelease()
    {
        float pageWidth = GetPageWidth();
        if (pageWidth <= 0f) return;

        float currentX = _content.anchoredPosition.x;
        int nearestIndex = Mathf.RoundToInt(-currentX / pageWidth);
        float velocity = GetRecentVelocity();

        int targetIndex;
        // Flick overrides position, fast intent wins (mirrors CameraSlide convention)
        if (Mathf.Abs(velocity) > flickVelocityThreshold)
        {
            int direction = velocity > 0f ? -1 : 1;
            targetIndex = _currentPageIndex + direction;
        }
        else
        {
            targetIndex = nearestIndex;
        }

        targetIndex = Mathf.Clamp(targetIndex, 0, PageCount - 1);
        bool changed = targetIndex != _currentPageIndex;
        _currentPageIndex = targetIndex;

        TweenContentTo(GetTargetX(targetIndex));

        if (changed)
        {
            OnPageChanged?.Invoke(_currentPageIndex);
        }
    }

    private void TweenContentTo(float targetX)
    {
        _content.DOKill();
        // Suppress ScrollRect's own elastic settle so it doesn't fight our snap tween.
        _scrollRect.movementType = ScrollRect.MovementType.Unrestricted;
        _content.DOAnchorPosX(targetX, snapDuration)
            .SetEase(snapEase)
            .OnComplete(() => _scrollRect.movementType = ScrollRect.MovementType.Elastic);
    }

    private void SnapImmediate(int index)
    {
        _content.DOKill();
        Vector2 pos = _content.anchoredPosition;
        pos.x = GetTargetX(index);
        _content.anchoredPosition = pos;
    }

    private float GetTargetX(int index) => -index * GetPageWidth();

    private float GetPageWidth() => _viewport != null ? _viewport.rect.width : 0f;

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled || _content == null) return;

        SnapImmediate(_currentPageIndex);
    }

    private void OnDisable()
    {
        _dragging = false;

        if (_content != null)
        {
            _content.DOKill();
            SnapImmediate(_currentPageIndex);
        }

        if (_scrollRect != null)
        {
            _scrollRect.movementType = ScrollRect.MovementType.Elastic;
        }
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
