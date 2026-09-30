using System;

namespace RpsArena.Game
{
    public enum RpsChoice { None = 0, Rock = 1, Paper = 2, Scissors = 3 }

    [Serializable]
    public sealed class PlayerSummaryDto
    {
        public long userId;
        public string displayName;
        public string avatarUrl;
        public int rating;
    }

    [Serializable]
    public sealed class ServerPayload
    {
        public string matchId;
        public PlayerSummaryDto you;
        public PlayerSummaryDto opponent;
        public int bestOf;
        public int secondsPerRound;
        public int round;
        public int seconds;
        public int yourChoice;
        public int opponentChoice;
        public string result;
        public int yourScore;
        public int opponentScore;
        public int draws;
        public int yourRating;
        public int ratingDelta;
    }

    [Serializable]
    public sealed class ServerMessage { public string type; public ServerPayload payload; }

    [Serializable]
    public sealed class MeDto
    {
        public long userId;
        public string displayName;
        public string avatarUrl;
        public int rating;
        public int wins;
        public int losses;
        public int draws;
        public int matchesPlayed;
        public int currentStreak;
        public int bestStreak;
    }

    [Serializable]
    public sealed class RankingRowDto
    {
        public int position;
        public long userId;
        public string displayName;
        public string avatarUrl;
        public int rating;
        public int wins;
        public int losses;
        public int draws;
    }

    [Serializable]
    public sealed class RankingListWrapper { public RankingRowDto[] rows; }

    [Serializable]
    internal sealed class SignalRNegotiateResponse
    {
        public string connectionId;
        public string connectionToken;
    }
}
