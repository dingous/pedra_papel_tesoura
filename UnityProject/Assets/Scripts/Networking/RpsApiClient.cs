using System;
using System.Collections;
using RpsArena.Config;
using RpsArena.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace RpsArena.Networking
{
    public sealed class RpsApiClient : MonoBehaviour
    {
        private RpsGameConfig _config;
        public void Initialize(RpsGameConfig config) => _config = config;

        public IEnumerator GetMe(string token, Action<MeDto> ok, Action<string> fail)
            => Get("/api/game/rps/me", token, text => ok?.Invoke(JsonUtility.FromJson<MeDto>(text)), fail);

        public IEnumerator GetRanking(string token, Action<RankingRowDto[]> ok, Action<string> fail)
        {
            return Get("/api/game/rps/ranking?take=50", token, text =>
            {
                var wrapped = "{\"rows\":" + text + "}";
                ok?.Invoke(JsonUtility.FromJson<RankingListWrapper>(wrapped)?.rows ?? Array.Empty<RankingRowDto>());
            }, fail);
        }

        private IEnumerator Get(string path, string token, Action<string> ok, Action<string> fail)
        {
            using var request = UnityWebRequest.Get(_config.ApiBaseUrl + path);
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                fail?.Invoke("HTTP " + request.responseCode + " - " + request.error);
                yield break;
            }
            ok?.Invoke(request.downloadHandler.text);
        }
    }
}
