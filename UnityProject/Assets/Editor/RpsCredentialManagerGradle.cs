#if UNITY_ANDROID
using System;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

public sealed class RpsCredentialManagerGradle : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 950;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var gradlePath = Path.Combine(path, "build.gradle");
        if (!File.Exists(gradlePath)) throw new FileNotFoundException("unityLibrary/build.gradle não encontrado.", gradlePath);
        var text = File.ReadAllText(gradlePath);
        if (!text.Contains("com.google.android.libraries.identity.googleid:googleid", StringComparison.Ordinal))
        {
            const string marker = "dependencies {";
            var index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) throw new InvalidOperationException("Bloco dependencies não encontrado no build.gradle gerado.");
            var insertion = marker + Environment.NewLine +
                "    implementation 'androidx.credentials:credentials:1.6.0'" + Environment.NewLine +
                "    implementation 'androidx.credentials:credentials-play-services-auth:1.6.0'" + Environment.NewLine +
                "    implementation 'com.google.android.libraries.identity.googleid:googleid:1.2.1'";
            text = text.Remove(index, marker.Length).Insert(index, insertion);
            File.WriteAllText(gradlePath, text);
        }

        var source = Path.Combine(Application.dataPath, "AndroidAuth", "CredManBridge.java");
        if (!File.Exists(source)) throw new FileNotFoundException("CredManBridge.java não encontrado.", source);
        var dir = Path.Combine(path, "src", "main", "java", "com", "dingous", "rpsarena", "auth");
        Directory.CreateDirectory(dir);
        File.Copy(source, Path.Combine(dir, "CredManBridge.java"), true);
    }
}
#endif
