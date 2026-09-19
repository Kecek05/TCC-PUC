using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tutorial's hold on the player's finger: an invisible, full-screen raycast target that decides, point
/// by point, whether a touch reaches the game underneath. Blocking, it leaves only the overlay's own buttons
/// (Continue, Skip) answering above it; opened, it lets through exactly the hole the overlay has cut around
/// the one thing a step is asking for.
/// </summary>
/// <remarks>
/// <para>
/// One graphic with a raycast filter, rather than the four dim panels, for two reasons. The panels frame a
/// hole but cannot close it, and a step that asks for nothing still shows one — the Mana and Hand reads frame
/// the bar and the cards, which are there to be looked at, not touched. And the panels live under the
/// overlay's content, which is hidden while a result settles or a spell is watched; a blocker that vanished
/// with them would hand the board back exactly while the tutorial is waiting on it.
/// </para>
/// <para>
/// UI-level on purpose: every input the game takes arrives through the EventSystem — the cards, and the
/// full-screen <c>CameraSlideArea</c> that carries both the table swipe and a tower tap — so one filter in
/// front of them all is the whole gate. A card's drop is checked against its own <c>BlockCardsCanvas</c>
/// raycaster, never this canvas, so a drag that starts in the hole still lands wherever it is dropped.
/// </para>
/// <para>
/// Draws nothing (<see cref="OnPopulateMesh"/> emits no geometry): a zero-alpha Image would block the same
/// way, but would still rasterise a full-screen quad every frame, which a mobile GPU pays for in fill rate.
/// </para>
/// </remarks>
public class TutorialInputShield : Graphic, ICanvasRaycastFilter
{
    private TutorialInputMode _mode = TutorialInputMode.Free;

    /// <summary>The rect the opening is expressed in — the overlay's canvas, where the hole is computed.</summary>
    private RectTransform _space;

    private Rect _opening;
    private bool _hasOpening;

    public TutorialInputMode Mode => _mode;

    /// <summary>Called once by the overlay that owns the shield, with the rect its hole coordinates live in.</summary>
    public void Initialize(RectTransform space)
    {
        _space = space;
        SetMode(TutorialInputMode.Free);
    }

    public void SetMode(TutorialInputMode mode)
    {
        _mode = mode;

        // Free takes the shield out of raycasting altogether, rather than waving every touch through a filter.
        raycastTarget = mode != TutorialInputMode.Free;
    }

    /// <summary>The area a <see cref="TutorialInputMode.TargetOnly"/> shield lets through. Follows the hole
    /// every frame, so it keeps up with a target that moves.</summary>
    public void SetOpening(Rect rectInSpace)
    {
        _opening = rectInSpace;
        _hasOpening = rectInSpace.width > 0f && rectInSpace.height > 0f;
    }

    public void ClearOpening() => _hasOpening = false;

    /// <summary>True means the shield is hit, and so swallows the touch.</summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // With nothing to let through, an opened shield is a closed one: a step whose target is missing is
        // asking for nothing the player can reach.
        if (_mode != TutorialInputMode.TargetOnly || !_hasOpening || _space == null) return true;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, screenPoint, eventCamera, out Vector2 local);
        return !_opening.Contains(local);
    }

    protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
}
