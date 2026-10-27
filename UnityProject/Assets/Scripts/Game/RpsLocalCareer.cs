using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RpsArena.Game
{
    public static class RpsRules
    {
        public static bool Valid(RpsChoice c) => c >= RpsChoice.Rock && c <= RpsChoice.Scissors;
        public static int Compare(RpsChoice a, RpsChoice b)
        {
            if (!Valid(a) || !Valid(b)) throw new ArgumentOutOfRangeException(nameof(a));
            if (a == b) return 0;
            return ((int)a - (int)b + 3) % 3 == 1 ? 1 : -1;
        }
        public static int RatingDelta(int rating, int other, double score)
            => (int)Math.Round(32 * (score - 1 / (1 + Math.Pow(10, (other - rating) / 400.0))));
        public static string Name(RpsChoice c) => c switch
        { RpsChoice.Rock => "PEDRA", RpsChoice.Paper => "PAPEL", RpsChoice.Scissors => "TESOURA", _ => "?" };
    }

    [Serializable] public sealed class LocalPlayer
    {
        public string name;
        public int rating = 1000, wins, losses, draws, streak, bestStreak, avatar;
        public int Matches => wins + losses + draws;
    }
    [Serializable] public sealed class RpsLocalCareer
    {
        public List<LocalPlayer> players = new();
        public string selected = "Jogador";
        private static readonly string SaveKey = Array.IndexOf(Environment.GetCommandLineArgs(), "-rps-smoke") >= 0 ? "rps.qa.career.v1" : "rps.career.v1";
        public LocalPlayer Current => players.Find(p => p.name == selected) ?? Select("Jogador");
        public static RpsLocalCareer Load()
        {
            RpsLocalCareer save = null;
            try { save = JsonUtility.FromJson<RpsLocalCareer>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (Exception) { /* An interrupted or incompatible save starts a new local career. */ }
            save ??= new RpsLocalCareer();
            save.players ??= new List<LocalPlayer>();
            save.players.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.name));
            save.Select(save.selected);
            return save;
        }
        public LocalPlayer Select(string name)
        {
            name = new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_').Take(18).ToArray()).Trim();
            if (name.Length == 0) name = "Jogador";
            var player = players.Find(p => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase));
            if (player == null)
            {
                player = new LocalPlayer { name = name };
                players.Add(player);
            }
            selected = player.name;
            return player;
        }
        public int Record(int result, int opponentRating)
        {
            var p = Current;
            int delta = RpsRules.RatingDelta(p.rating, opponentRating, result > 0 ? 1 : result < 0 ? 0 : .5);
            int old = p.rating;
            p.rating = Math.Max(100, p.rating + delta);
            if (result > 0) { p.wins++; p.streak++; p.bestStreak = Math.Max(p.bestStreak, p.streak); }
            else { if (result < 0) p.losses++; else p.draws++; p.streak = 0; }
            Save();
            return p.rating - old;
        }
        public RankingRowDto[] Ranking() => players.Where(p => p.Matches > 0).OrderByDescending(p => p.rating)
            .ThenByDescending(p => p.wins).ThenBy(p => p.name).Select((p, i) => new RankingRowDto
            { position = i + 1, displayName = p.name, rating = p.rating, wins = p.wins, losses = p.losses, draws = p.draws }).ToArray();
        public void Save() { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
    }
}
