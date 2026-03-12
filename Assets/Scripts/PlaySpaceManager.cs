using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates a best-of-three rock-paper-scissors match. Each round a
/// player card and an opponent card are played, animated, scored, and then
/// destroyed. Once all three rounds are complete the winner is declared
/// and the player is returned to the Login screen after a short delay.
/// </summary>
public class PlaySpaceManager : MonoBehaviour
{
    [Header("Player Cards")]
    [SerializeField]
    GameObject _playerRockCard;

    [SerializeField]
    GameObject _playerPaperCard;

    [SerializeField]
    GameObject _playerScissorsCard;

    [Header("Opponent Cards")]
    [SerializeField]
    GameObject[] _opponentCards;

    [Header("Play Slots — empty RectTransforms marking where played cards land")]
    [SerializeField]
    RectTransform _playerPlaySlot;

    [SerializeField]
    RectTransform _opponentPlaySlot;

    [Header("Score")]
    [SerializeField]
    TMP_Text _playerScore;

    [SerializeField]
    TMP_Text _opponentScore;

    [Header("Opponent card face sprites for the flip reveal")]
    [SerializeField]
    Sprite _rockSprite;

    [SerializeField]
    Sprite _paperSprite;

    [SerializeField]
    Sprite _scissorsSprite;

    [SerializeField]
    float _resultDisplayDuration = 2f;

    [SerializeField]
    float _endGameDelay = 3f;

    private Card _playerRock;
    private Card _playerPaper;
    private Card _playerScissors;
    private Card[] _opponentCardComponents;

    private int _playerScoreValue;
    private int _opponentScoreValue;
    private int _roundsPlayed;
    private int _nextOpponentIndex;
    private bool _roundInProgress;
    private RoundResultMessage _pendingResult;

    void Awake()
    {
        _playerRock = RequireCard(_playerRockCard, "Player Rock Card");
        _playerPaper = RequireCard(_playerPaperCard, "Player Paper Card");
        _playerScissors = RequireCard(_playerScissorsCard, "Player Scissors Card");

        if (_playerRock != null) RegisterPlayerCard(_playerRock);
        if (_playerPaper != null) RegisterPlayerCard(_playerPaper);
        if (_playerScissors != null) RegisterPlayerCard(_playerScissors);

        _opponentCardComponents = new Card[_opponentCards.Length];
        for (int i = 0; i < _opponentCards.Length; i++)
        {
            _opponentCardComponents[i] = RequireCard(_opponentCards[i], $"Opponent Card [{i}]");
            if (_opponentCardComponents[i] != null)
                _opponentCardComponents[i].RememberHome();
        }

        EnsurePlaySlots();

        EventBus.Instance.SubscribeResponse<RoundResultMessage>(OnRoundResult);
    }

    private void EnsurePlaySlots()
    {
        var parentRect = GetComponent<RectTransform>();

        if (_playerPlaySlot == null)
        {
            _playerPlaySlot = CreateSlot("Player Play Slot", parentRect,
                new Vector2(0.5f, 0.25f));
        }

        if (_opponentPlaySlot == null)
        {
            _opponentPlaySlot = CreateSlot("Opponent Play Slot", parentRect,
                new Vector2(0.5f, 0.75f));
        }
    }

    private RectTransform CreateSlot(string slotName, RectTransform parent, Vector2 anchor)
    {
        var go = new GameObject(slotName, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private Card RequireCard(GameObject cardObject, string label)
    {
        if (cardObject == null)
        {
            Debug.LogError($"[PlaySpaceManager] {label} GameObject is not assigned.");
            return null;
        }

        var card = cardObject.GetComponent<Card>();
        if (card == null)
            Debug.LogError($"[PlaySpaceManager] {label} is missing a Card component.");

        return card;
    }

    private void RegisterPlayerCard(Card card)
    {
        card.RememberHome();
        card.Clicked += OnCardClicked;
    }

    protected virtual void OnCardClicked(Card card)
    {
        if (_roundInProgress) return;
        StartCoroutine(PlayRoundCoroutine(card));
    }

    protected virtual void OnRoundResult(RoundResultMessage result)
    {
        _pendingResult = result;
    }

    private IEnumerator PlayRoundCoroutine(Card playerCard)
    {
        _roundInProgress = true;

        // --- Player card slides to the play area ---
        bool done = false;
        playerCard.MoveTo(_playerPlaySlot, () => done = true);
        yield return new WaitUntil(() => done);

        // --- Fire-and-forget: server picks a random opponent card ---
        EventBus.Instance.Send(new PlayCardMessage { Card = playerCard.CardType });

        // --- Wait for the server to respond ---
        yield return new WaitUntil(() => _pendingResult != null);
        var result = _pendingResult;
        _pendingResult = null;

        // --- Opponent's hidden card slides to the play area ---
        var opponentCard = PickOpponentCard();
        done = false;
        opponentCard.MoveTo(_opponentPlaySlot, () => done = true);
        yield return new WaitUntil(() => done);

        // --- Flip to reveal what the server played ---
        var faceSprite = GetSpriteForCard(result.OpponentCard);
        done = false;
        opponentCard.Flip(faceSprite, () => done = true);
        yield return new WaitUntil(() => done);

        // --- Update scores ---
        if (result.Result == "Win")
            _playerScoreValue++;
        else if (result.Result == "Lose")
            _opponentScoreValue++;

        _playerScore.text = _playerScoreValue.ToString();
        _opponentScore.text = _opponentScoreValue.ToString();

        // --- Let the player see the outcome ---
        yield return new WaitForSeconds(_resultDisplayDuration);

        // --- Destroy the played cards ---
        Destroy(playerCard.gameObject);
        Destroy(opponentCard.gameObject);

        _roundsPlayed++;

        // --- After all three rounds, declare a winner and go back to login ---
        if (_roundsPlayed >= 3)
        {
            yield return StartCoroutine(DeclareWinnerAndReturn());
            yield break;
        }

        _roundInProgress = false;
    }

    private IEnumerator DeclareWinnerAndReturn()
    {
        string outcome;
        if (_playerScoreValue > _opponentScoreValue)
            outcome = "You Win!";
        else if (_opponentScoreValue > _playerScoreValue)
            outcome = "You Lose!";
        else
            outcome = "It's a Draw!";

        _playerScore.text = outcome;
        _opponentScore.text = $"{_playerScoreValue} - {_opponentScoreValue}";

        Debug.Log($"[PlaySpaceManager] Game over — {outcome} ({_playerScoreValue}-{_opponentScoreValue})");

        yield return new WaitForSeconds(_endGameDelay);

        SceneManager.LoadScene("Loading");
    }

    /// <summary>
    /// Cycles through the opponent hidden cards so each round uses a
    /// different one (they get destroyed after being played).
    /// </summary>
    private Card PickOpponentCard()
    {
        return _opponentCardComponents[_nextOpponentIndex++];
    }

    private Sprite GetSpriteForCard(CardType card)
    {
        return card switch
        {
            CardType.Rock => _rockSprite,
            CardType.Paper => _paperSprite,
            CardType.Scissors => _scissorsSprite,
            _ => null
        };
    }
}
