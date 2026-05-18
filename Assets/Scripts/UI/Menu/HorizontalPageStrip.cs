using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class HorizontalPageStrip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Title("References")]
    [Tooltip("Visible window. Its width defines the page width. Usually this GameObject (with a RectMask2D).")]
    [SerializeField] private RectTransform viewportArea;
    [Tooltip("Direct child of viewportArea. Holds the page RectTransforms; moved by drag/tween.")]
    [SerializeField] private RectTransform pageStrip;
    [Tooltip("Pages in left-to-right order. Index-aligned with the MenuNavController buttons.")]
    [SerializeField] private List<MenuPage> pages = new List<MenuPage>();

    [Title("Snap")]
    [SerializeField, MinValue(0.05f)] private float snapDuration = 0.3f;
    [SerializeField] private Ease snapEase = Ease.OutCubic;
    [SerializeField, Range(0.1f, 0.5f)] private float pageChangeDragThreshold = 0.35f;
    [SerializeField, MinValue(0f)] private float flickVelocityThreshold = 600f;
    [SerializeField, Range(0f, 1f)] private float edgeResistance = 0.5f;

    public event Action<int> OnPageChanged;
    public int CurrentPageIndex { get; private set; }
    public int PageCount => pages.Count;
    public IReadOnlyList<MenuPage> Pages => pages;

    private float _dragStartStripX;
    private float _dragStartPointerX;
    private bool _dragging;
    private float _lastPointerX;
    private float _lastPointerTime;
    private float _pointerVelocity;
    private Tween _activeTween;
    private float _lastLaidOutWidth;

    private float PageWidth => viewportArea != null ? viewportArea.rect.width : 0f;

    private void Reset()
    {
        viewportArea = (RectTransform)transform;
    }

    private void Start()
    {
        StartCoroutine(LayoutWhenReady());
    }

    private IEnumerator LayoutWhenReady()
    {
        while (PageWidth <= 0f) yield return null;

        Layout();
        SetStripX(GetTargetX(CurrentPageIndex));
        OnPageChanged?.Invoke(CurrentPageIndex);
    }

    private void Layout()
    {
        if (PageWidth <= 0f || pageStrip == null) return;
        float w = PageWidth;
        _lastLaidOutWidth = w;

        pageStrip.anchorMin = new Vector2(0f, 0f);
        pageStrip.anchorMax = new Vector2(0f, 1f);
        pageStrip.pivot = new Vector2(0f, 0.5f);
        pageStrip.sizeDelta = new Vector2(w * PageCount, 0f);

        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] == null) continue;
            RectTransform p = (RectTransform)pages[i].transform;
            p.anchorMin = new Vector2(0f, 0f);
            p.anchorMax = new Vector2(0f, 1f);
            p.pivot = new Vector2(0f, 0.5f);
            p.sizeDelta = new Vector2(w, 0f);
            p.anchoredPosition = new Vector2(i * w, 0f);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (PageCount <= 1 || PageWidth <= 0f) return;
        _activeTween?.Kill();
        _dragging = true;
        _dragStartStripX = pageStrip.anchoredPosition.x;
        _dragStartPointerX = eventData.position.x;
        _lastPointerX = eventData.position.x;
        _lastPointerTime = Time.unscaledTime;
        _pointerVelocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        float pointerDelta = eventData.position.x - _dragStartPointerX;
        float newX = _dragStartStripX + pointerDelta;

        // Rubber-band at edges so it feels alive at boundaries
        float minX = -(PageCount - 1) * PageWidth;
        const float maxX = 0f;
        if (newX > maxX) newX = maxX + (newX - maxX) * (1f - edgeResistance);
        else if (newX < minX) newX = minX + (newX - minX) * (1f - edgeResistance);

        SetStripX(newX);

        float now = Time.unscaledTime;
        float dt = now - _lastPointerTime;
        if (dt > 1e-4f)
            _pointerVelocity = (eventData.position.x - _lastPointerX) / dt;
        _lastPointerX = eventData.position.x;
        _lastPointerTime = now;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        float w = PageWidth;
        float dragDelta = pageStrip.anchoredPosition.x - _dragStartStripX;
        int targetIndex = CurrentPageIndex;

        // Negative pointer velocity = swiped left = advance to next page (higher index)
        bool flickNext = _pointerVelocity < -flickVelocityThreshold;
        bool flickPrev = _pointerVelocity > flickVelocityThreshold;
        bool draggedNext = dragDelta < -w * pageChangeDragThreshold;
        bool draggedPrev = dragDelta > w * pageChangeDragThreshold;

        if (flickNext || draggedNext) targetIndex = CurrentPageIndex + 1;
        else if (flickPrev || draggedPrev) targetIndex = CurrentPageIndex - 1;

        targetIndex = Mathf.Clamp(targetIndex, 0, PageCount - 1);
        SnapToPage(targetIndex, animated: true);
    }

    public void GoToPage(int index, bool animated = true)
    {
        if (PageCount == 0) return;
        index = Mathf.Clamp(index, 0, PageCount - 1);
        SnapToPage(index, animated);
    }

    private void SnapToPage(int index, bool animated)
    {
        bool changed = index != CurrentPageIndex;
        CurrentPageIndex = index;
        float targetX = GetTargetX(index);

        _activeTween?.Kill();
        if (animated)
        {
            _activeTween = pageStrip.DOAnchorPosX(targetX, snapDuration).SetEase(snapEase);
        }
        else
        {
            SetStripX(targetX);
        }

        if (changed) OnPageChanged?.Invoke(CurrentPageIndex);
    }

    private float GetTargetX(int index) => -index * PageWidth;

    private void SetStripX(float x)
    {
        Vector2 p = pageStrip.anchoredPosition;
        p.x = x;
        pageStrip.anchoredPosition = p;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled || viewportArea == null || pageStrip == null) return;
        float w = PageWidth;
        if (w <= 0f || Mathf.Approximately(w, _lastLaidOutWidth)) return;
        Layout();
        SetStripX(GetTargetX(CurrentPageIndex));
    }

    private void OnDestroy()
    {
        _activeTween?.Kill();
    }
}
