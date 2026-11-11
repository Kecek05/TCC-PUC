#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;

namespace KeceK.Capture
{
    /// <summary>
    /// The capture-only finger mark: a light, semi-transparent disc with a ring that follows the virtual
    /// finger. Built in code on its own overlay canvas above everything, never raycast, so it can never take a
    /// touch the game was meant to receive. Exists only while a capture runs.
    /// </summary>
    public class TouchIndicator : MonoBehaviour
    {
        private const float Diameter = 118f;
        private const float FillAlpha = 0.30f;
        private const float RingAlpha = 0.55f;

        private RectTransform _root;
        private RectTransform _canvasRect;
        private Image _fill;
        private Image _ring;
        private CanvasGroup _group;

        private float _alpha;
        private float _targetAlpha;
        private float _scale = 1f;
        private float _targetScale = 1f;

        public static TouchIndicator Create()
        {
            GameObject canvasGo = new GameObject("[CaptureTouchIndicator]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(canvasGo);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            TouchIndicator indicator = canvasGo.AddComponent<TouchIndicator>();
            indicator.Build((RectTransform)canvasGo.transform);
            return indicator;
        }

        private void Build(RectTransform canvasRect)
        {
            _canvasRect = canvasRect;

            GameObject rootGo = new GameObject("Finger", typeof(RectTransform), typeof(CanvasGroup));
            _root = (RectTransform)rootGo.transform;
            _root.SetParent(canvasRect, false);
            _root.sizeDelta = new Vector2(Diameter, Diameter);
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _group = rootGo.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            _fill = MakeImage("Fill", JuiceSprites.Disc, new Color(1f, 1f, 1f, FillAlpha), 0.86f);
            _ring = MakeImage("Ring", JuiceSprites.Ring, new Color(1f, 1f, 1f, RingAlpha), 1f);
        }

        private Image MakeImage(string name, Sprite sprite, Color color, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Diameter * size, Diameter * size);

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Finger down at a screen point: the mark fades in a little small and settles.</summary>
        public void Press(Vector2 screenPoint)
        {
            MoveTo(screenPoint);
            _scale = 0.72f;
            _targetScale = 1f;
            _targetAlpha = 1f;
        }

        public void MoveTo(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out Vector2 local);
            _root.anchoredPosition = local + _canvasRect.rect.size * 0.5f;
        }

        /// <summary>Finger up: the mark grows a touch and fades.</summary>
        public void Lift()
        {
            _targetScale = 1.22f;
            _targetAlpha = 0f;
        }

        public void HideNow()
        {
            _alpha = _targetAlpha = 0f;
            if (_group != null) _group.alpha = 0f;
        }

        private void Update()
        {
            float dt = CaptureTime.Delta;
            _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, dt / 0.12f);
            _scale = Mathf.Lerp(_scale, _targetScale, 1f - Mathf.Exp(-dt * 18f));
            _group.alpha = _alpha;
            _root.localScale = Vector3.one * _scale;
        }
    }
}
#endif
