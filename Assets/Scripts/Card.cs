using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a single card's visual state and animation. Supports smooth
/// position interpolation to a target slot and a flip-reveal effect
/// (scale X to 0, swap sprite, scale back). Attach to any card prefab —
/// player face cards or hidden opponent cards.
///
/// Cards track their "home" parent and sibling index so they can return
/// to a LayoutGroup after being played without breaking the layout.
/// Requires a sibling Button component for click detection.
/// </summary>
public class Card : MonoBehaviour
{
    [SerializeField]
    CardType _cardType;

    [SerializeField]
    float _moveDuration = 0.4f;

    [SerializeField]
    float _flipDuration = 0.3f;

    private RectTransform _rectTransform;
    private Image _image;
    private Sprite _originalSprite;
    private Transform _homeParent;
    private int _homeSiblingIndex;
    private bool _isAnimating;

    public CardType CardType => _cardType;
    public bool IsAnimating => _isAnimating;

    /// <summary>
    /// Fired when the player clicks this card. The PlaySpaceManager
    /// subscribes to this to kick off a round.
    /// </summary>
    public event Action<Card> Clicked;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
        _originalSprite = _image.sprite;

        var button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    /// <summary>
    /// Snapshot the card's parent and sibling index so ReturnToHand
    /// can put it back exactly where it was in the LayoutGroup.
    /// Call once after the scene finishes loading.
    /// </summary>
    public void RememberHome()
    {
        _homeParent = transform.parent;
        _homeSiblingIndex = transform.GetSiblingIndex();
    }

    /// <summary>
    /// Smoothly slide the card to the center of the target RectTransform.
    /// Re-parents to the Canvas root first so the LayoutGroup no longer
    /// controls position during the animation.
    /// </summary>
    public void MoveTo(RectTransform target, Action onComplete = null)
    {
        if (_isAnimating) return;
        StartCoroutine(MoveCoroutine(target, onComplete));
    }

    /// <summary>
    /// Card-flip effect: scale X to 0, swap the sprite to revealSprite,
    /// then scale X back to original. Pass null to flip back to the
    /// original sprite.
    /// </summary>
    public void Flip(Sprite revealSprite, Action onComplete = null)
    {
        if (_isAnimating) return;
        StartCoroutine(FlipCoroutine(revealSprite, onComplete));
    }

    /// <summary>
    /// Instantly return the card to its original LayoutGroup position
    /// and restore the sprite it had when the scene loaded.
    /// </summary>
    public void ReturnToHand()
    {
        StopAllCoroutines();
        _isAnimating = false;

        transform.SetParent(_homeParent);
        transform.SetSiblingIndex(_homeSiblingIndex);
        _rectTransform.localScale = Vector3.one;
        _image.sprite = _originalSprite;
    }

    private void OnButtonClicked()
    {
        if (!_isAnimating)
            Clicked?.Invoke(this);
    }

    private IEnumerator MoveCoroutine(RectTransform target, Action onComplete)
    {
        _isAnimating = true;

        // Snapshot world position before re-parenting so the card
        // doesn't visually jump when it leaves the LayoutGroup.
        var worldPos = _rectTransform.position;
        var canvas = GetComponentInParent<Canvas>().transform;
        transform.SetParent(canvas);
        _rectTransform.position = worldPos;

        var startPos = _rectTransform.position;
        var endPos = target.position;
        float elapsed = 0f;

        while (elapsed < _moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / _moveDuration);
            _rectTransform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        _rectTransform.position = endPos;
        _isAnimating = false;
        onComplete?.Invoke();
    }

    private IEnumerator FlipCoroutine(Sprite revealSprite, Action onComplete)
    {
        _isAnimating = true;
        var scale = _rectTransform.localScale;
        float half = _flipDuration / 2f;
        float elapsed = 0f;

        // Collapse horizontally
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            _rectTransform.localScale = new Vector3(
                Mathf.Lerp(scale.x, 0f, t), scale.y, scale.z);
            yield return null;
        }

        // Swap sprite at the midpoint when the card is edge-on
        _image.sprite = revealSprite != null ? revealSprite : _originalSprite;

        // Expand back
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            _rectTransform.localScale = new Vector3(
                Mathf.Lerp(0f, scale.x, t), scale.y, scale.z);
            yield return null;
        }

        _rectTransform.localScale = scale;
        _isAnimating = false;
        onComplete?.Invoke();
    }
}
