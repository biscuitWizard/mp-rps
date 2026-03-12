using UnityEngine;

/// <summary>
/// Resolves a round of rock-paper-scissors. The opponent's card is chosen
/// at random from the session's remaining hand — each card can only be
/// played once per match, mirroring the player's side where cards are
/// destroyed after use. Standard rules apply:
/// Rock > Scissors, Scissors > Paper, Paper > Rock.
/// </summary>
public class PlayCardHandler : MessageHandler<PlayCardMessage>
{
    protected override MessageBase[] HandleMessage(ClientSession sender, PlayCardMessage message)
    {
        var hand = sender.OpponentHand;

        if (hand.Count == 0)
        {
            Debug.LogWarning($"[PlayCardHandler] {sender.Username} has no cards left.");
            return new MessageBase[]
            {
                new ErrorMessage { Nonce = message.Nonce, ErrorText = "No cards remaining." }
            };
        }

        var playerCard = message.Card;
        var pick = Random.Range(0, hand.Count);
        var opponentCard = hand[pick];
        hand.RemoveAt(pick);

        var result = ResolveRound(playerCard, opponentCard);

        Debug.Log($"[PlayCardHandler] {sender.Username}: {playerCard} vs {opponentCard} -> {result}  (opponent has {hand.Count} left)");

        return new MessageBase[]
        {
            new RoundResultMessage
            {
                Nonce = message.Nonce,
                PlayerCard = playerCard,
                OpponentCard = opponentCard,
                Result = result
            }
        };
    }

    private string ResolveRound(CardType player, CardType opponent)
    {
        if (player == opponent) return "Draw";

        bool playerWins = (player == CardType.Rock && opponent == CardType.Scissors)
                       || (player == CardType.Scissors && opponent == CardType.Paper)
                       || (player == CardType.Paper && opponent == CardType.Rock);

        return playerWins ? "Win" : "Lose";
    }
}
