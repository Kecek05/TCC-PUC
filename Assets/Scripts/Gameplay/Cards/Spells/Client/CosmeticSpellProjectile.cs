using DG.Tweening;
using UnityEngine;

public class CosmeticSpellProjectile : MonoBehaviour
{
    [SerializeField] private float spawnHeight = 5f;

    [Tooltip("How far to the side, as a share of the height, the spell comes in from: thrown, not dropped.")]
    [SerializeField] private float entrySide = 0.45f;

    [Tooltip("The spell's size where it is thrown from, as a share of its size on landing: it grows as it nears.")]
    [SerializeField, Range(0.1f, 1f)] private float entryScale = 0.6f;

    [Tooltip("The blast colour on impact (flash, ring, sparks). Defaults to the blast token.")]
    [SerializeField] private bool useBlastToken = true;
    [SerializeField] private Color blastColor = new(0.945f, 0.576f, 0.306f, 1f);

    [Tooltip("The ground shadow's width as a share of the spell's reach, and its opacity at landing.")]
    [SerializeField] private float shadowWidth = 0.8f;
    [SerializeField, Range(0f, 1f)] private float shadowAlpha = 0.35f;

    private Vector2 _targetPosition;
    private float _range;
    private SpriteRenderer _shadow;

    public void Initialize(Vector2 targetPosition, float travelTime, float range)
    {
        _targetPosition = targetPosition;
        _range = range;
        
        // SpellCast: thrown in from above and to the side, growing as it comes, on ease-fall.
        Vector3 startPos = new Vector3(targetPosition.x + spawnHeight * entrySide, targetPosition.y + spawnHeight, 0f);
        transform.position = startPos;
        Vector3 rest = transform.localScale;
        transform.localScale = rest * entryScale;
        transform.DOScale(rest, travelTime).SetEase(Ease.InQuad).SetLink(gameObject);

        transform.DOMove(new Vector3(targetPosition.x, targetPosition.y, 0f), travelTime)
            .SetEase(Ease.InQuad)
            .OnComplete(OnImpact);

        CastShadow(travelTime);

        // SpellCast: the throw is heard where it can be seen.
        if (CameraFeedback.IsOnScreen(_targetPosition, 0.1f)) Juice.Sfx(SfxId.Whoosh);
    }

    /// <summary>
    /// SpellCast release: a dark ellipse on the ground where it will land, growing and darkening as the spell
    /// falls (ease-fall over its travel time), so the landing spot is telegraphed before it hits.
    /// </summary>
    private void CastShadow(float travelTime)
    {
        GameObject go = new GameObject("SpellShadow", typeof(SpriteRenderer));
        go.transform.position = new Vector3(_targetPosition.x, _targetPosition.y - 0.04f, 0f);
        _shadow = go.GetComponent<SpriteRenderer>();
        _shadow.sprite = JuiceSprites.Disc;
        _shadow.color = new Color(0f, 0f, 0f, 0f);

        // On the ground: under the enemies it is about to hit, over the board.
        _shadow.sortingLayerName = "Enemy";
        _shadow.sortingOrder = -10;

        float width = Mathf.Max(0.4f, _range * shadowWidth * 2f) / 1.28f;
        Vector3 full = new Vector3(width, width * 0.37f, 1f);
        Transform tr = go.transform;
        tr.localScale = full * 0.3f;

        JuiceTokensSO t = Juice.Tokens;
        DOTween.Sequence().SetLink(go)
            .Join(tr.DOScale(full, travelTime).SetEase(t.Eases.Fall))
            .Join(_shadow.DOFade(shadowAlpha, travelTime).SetEase(t.Eases.Fall));
    }

    /// <summary>
    /// SpellCast landing: the screen washes in the blast colour, a white-hot flash, a ring out to the edge of the
    /// area the spell hit and sparks, then a boom, a small shake and a buzz when it lands on the field the player
    /// is looking at.
    /// </summary>
    private void OnImpact()
    {
        Vector3 at = new Vector3(_targetPosition.x, _targetPosition.y, 0f);
        JuiceTokensSO t = Juice.Tokens;
        Color blast = useBlastToken ? t.Colors.Blast : blastColor;
        if (_shadow != null) Destroy(_shadow.gameObject);

        if (CameraFeedback.IsOnScreen(at, 0.1f))
        {
            ScreenFlash.Flash(blast, 0.35f);
            WorldFx.Debris(at, Mathf.Max(0.5f, _range), blast);
            Juice.Sfx(SfxId.Boom);
            Juice.Haptic(HapticRole.Medium);
            JuiceCamera.Shake(t.Shake.Small, t.Durations.ShakeLong);
        }

        WorldFx.Blast(at, Mathf.Max(0.5f, _range), blast);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // A projectile torn down before it lands (the match ending) takes its shadow with it.
        if (_shadow != null) Destroy(_shadow.gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_targetPosition, _range);
    }
#endif
}
