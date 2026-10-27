using System;
using System.Collections;
using System.IO;
using RpsArena.UI;
using UnityEngine;

namespace RpsArena.Game
{
    // Runs only with an explicit QA command-line argument and uses a separate save slot.
    public sealed class RpsSmokeTest : MonoBehaviour
    {
        private string _directory;
        private bool _failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-rps-smoke")>=0)
                new GameObject("RPS smoke test").AddComponent<RpsSmokeTest>();
        }
        private void OnEnable() => Application.logMessageReceived+=Log;
        private void OnDisable() => Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type)
        { if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) _failed=true; }
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs(); int flag=Array.IndexOf(args,"-rps-report");
            _directory=flag>=0?args[flag+1]:Path.Combine(Application.persistentDataPath,"QA"); Directory.CreateDirectory(_directory);
            yield return new WaitForSecondsRealtime(5);
            var game=FindAnyObjectByType<RpsGameController>();
            if(!game) { Fail("Bootstrap missing"); yield break; }
            game.Career.Select("Validação"); game.Home();
            Screen.SetResolution(720,1100,FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(1);
            yield return Capture("02-portrait");
            Screen.SetResolution(1280,850,FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(1);
            yield return Capture("01-lobby");
            int before=game.Career.Current.Matches;
            game.Ui.femaleButton.onClick.Invoke();
            if(game.Career.Current.avatar!=1) Fail("Avatar selection failed");
            game.StartTraining(); Time.timeScale=6;
            int choices=0; bool captured=false, sawExpression=false; float deadline=Time.realtimeSinceStartup+90;
            while(game.Phase!=RpsGameController.MatchPhase.Finished && Time.realtimeSinceStartup<deadline)
            {
                if(game.Phase==RpsGameController.MatchPhase.Choosing)
                {
                    game.Choose((RpsChoice)(choices++%3+1));
                    game.Choose(RpsChoice.Paper);
                    if(game.Ui.rockButton.interactable) Fail("Duplicate choice enabled");
                }
                if(game.Phase==RpsGameController.MatchPhase.Revealing && game.Ui.countdownText.text=="")
                {
                    var mesh=game.Stage.You.GetComponentInChildren<SkinnedMeshRenderer>();
                    for(int i=0;i<3;i++) if(mesh.GetBlendShapeWeight(i)>.1f) sawExpression=true;
                    if(!captured) { Time.timeScale=1; yield return new WaitForSeconds(.6f); yield return Capture("03-match"); captured=true; Time.timeScale=6; }
                }
                yield return null;
            }
            Time.timeScale=1;
            if(game.Phase!=RpsGameController.MatchPhase.Finished) Fail("Match did not finish");
            if(game.Career.Current.Matches!=before+1) Fail("Match not saved exactly once");
            if(!sawExpression) Fail("SALSA did not animate blendshapes");
            if(RpsLocalCareer.Load().Current.Matches!=game.Career.Current.Matches) Fail("Save reload mismatch");
            yield return new WaitForSeconds(1); yield return Capture("04-result");
            game.Ranking(false); yield return null; yield return Capture("05-ranking");
            game.Ui.backButton.onClick.Invoke(); game.Ui.trainingButton.onClick.Invoke();
            if(game.Phase!=RpsGameController.MatchPhase.Choosing) Fail("Replay failed");
            before=game.Career.Current.Matches;
            game.Ui.exitButton.onClick.Invoke(); game.Ui.exitButton.onClick.Invoke();
            if(game.Career.Current.Matches!=before+1) Fail("Forfeit registered more than once");
            game.Home(); game.Ui.motionButton.onClick.Invoke(); game.Ui.soundButton.onClick.Invoke();
            game.Ui.motionButton.onClick.Invoke(); game.Ui.soundButton.onClick.Invoke();
            File.WriteAllText(Path.Combine(_directory,"runtime-test.txt"),(_failed?"FAIL":"PASS")+"\nComplete match, duplicate input, SALSA blend weights, avatar, persistence, local ranking, replay, forfeit, audio/camera toggles.\nRound choices: "+choices);
            PlayerPrefs.DeleteKey("rps.qa.career.v1"); PlayerPrefs.Save();
            Debug.Log(_failed?"RPS_SMOKE_FAILED":"RPS_SMOKE_PASS"); Application.Quit(_failed?1:0);
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(_directory,name+".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }
        private void Fail(string message) { _failed=true; Debug.LogError("RPS_QA: "+message); }
    }
}
