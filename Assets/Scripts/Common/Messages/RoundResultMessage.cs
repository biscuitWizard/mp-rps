using System;

[Serializable]
public class RoundResultMessage : MessageBase
{
    public CardType PlayerCard;
    public CardType OpponentCard;
    public string Result;
    public int PlayerScore;
    public int OpponentScore;
}
