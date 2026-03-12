using UnityEngine;

/// <summary>
/// Resolves a round of rock-paper-scissors. The opponent's card is chosen
/// at random by the server. Standard rules apply:
/// Rock > Scissors, Scissors > Paper, Paper > Rock.
/// </summary>
public class PlayCardHandler : MessageHandler<PlayCardMessage>
{
    private static readonly CardType[] AllCards =
    {
        CardType.Rock, CardType.Paper, CardType.Scissors
    };

    protected override MessageBase[] HandleMessage(ClientSession sender, PlayCardMessage message)
    {
        var playerCard = message.Card;
        var opponentCard = AllCards[Random.Range(0, AllCards.Length)];
        var result = ResolveRound(playerCard, opponentCard);

        Debug.Log($"[PlayCardHandler] {sender.Username}: {playerCard} vs {opponentCard} -> {result}");

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
