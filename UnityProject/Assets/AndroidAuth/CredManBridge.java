package com.dingous.rpsarena.auth;

import android.app.Activity;
import android.os.CancellationSignal;
import android.os.Handler;
import android.os.Looper;
import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CredentialOption;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;
import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;

public final class CredManBridge {
    private static final String UNITY_OBJECT = "RpsArena";
    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static final Executor EXECUTOR = Executors.newSingleThreadExecutor();
    private static volatile CancellationSignal activeCancellation;

    private CredManBridge() { }

    public static void signInSilent(Activity activity, String webClientId, String attemptId) {
        if (activity == null || empty(webClientId) || empty(attemptId)) { sendError(attemptId, "SilentFailed"); return; }
        GetGoogleIdOption option = new GetGoogleIdOption.Builder()
                .setFilterByAuthorizedAccounts(true)
                .setServerClientId(webClientId)
                .setAutoSelectEnabled(true)
                .build();
        requestCredential(activity, option, true, attemptId);
    }

    public static void signInInteractive(Activity activity, String webClientId, String attemptId) {
        if (activity == null || empty(webClientId) || empty(attemptId)) { sendError(attemptId, "GoogleConfiguration"); return; }
        GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(webClientId).build();
        requestCredential(activity, option, false, attemptId);
    }

    private static void requestCredential(Activity activity, CredentialOption option, boolean silent, String attemptId) {
        CredentialManager manager = CredentialManager.create(activity);
        GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
        CancellationSignal previous = activeCancellation;
        if (previous != null) previous.cancel();
        CancellationSignal cancellation = new CancellationSignal();
        activeCancellation = cancellation;
        manager.getCredentialAsync(activity, request, cancellation, EXECUTOR,
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override public void onResult(GetCredentialResponse result) {
                        clearActive(cancellation); handleResult(result, silent, attemptId);
                    }
                    @Override public void onError(GetCredentialException error) {
                        clearActive(cancellation);
                        if (silent || error instanceof NoCredentialException) sendError(attemptId, "SilentFailed");
                        else sendError(attemptId, error == null ? "GoogleSignInError" : error.getClass().getSimpleName());
                    }
                });
    }

    private static void handleResult(GetCredentialResponse response, boolean silent, String attemptId) {
        if (response == null) { sendError(attemptId, silent ? "SilentFailed" : "CredentialEmpty"); return; }
        Credential credential = response.getCredential();
        if (!(credential instanceof CustomCredential)) { sendError(attemptId, silent ? "SilentFailed" : "UnsupportedCredential"); return; }
        CustomCredential custom = (CustomCredential) credential;
        if (!GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(custom.getType())) {
            sendError(attemptId, silent ? "SilentFailed" : "UnsupportedCredential"); return;
        }
        try {
            GoogleIdTokenCredential google = GoogleIdTokenCredential.createFrom(custom.getData());
            sendToUnity("OnGoogleIdToken", payload(attemptId, google.getIdToken()));
        } catch (Exception error) { sendError(attemptId, "GoogleIdTokenParsingError"); }
    }

    public static void clearCredentialState(Activity activity) {
        CancellationSignal previous = activeCancellation;
        activeCancellation = null;
        if (previous != null) previous.cancel();
        if (activity == null) return;
        CredentialManager.create(activity).clearCredentialStateAsync(new ClearCredentialStateRequest(), null, EXECUTOR,
                new CredentialManagerCallback<Void, ClearCredentialException>() {
                    @Override public void onResult(Void ignored) { }
                    @Override public void onError(ClearCredentialException error) { }
                });
    }

    private static void clearActive(CancellationSignal cancellation) { if (activeCancellation == cancellation) activeCancellation = null; }
    private static boolean empty(String value) { return value == null || value.trim().isEmpty(); }
    private static String payload(String attemptId, String value) { return (attemptId == null ? "" : attemptId) + "|" + (value == null ? "" : value); }
    private static void sendError(String attemptId, String reason) { sendToUnity("OnSignInError", payload(attemptId, reason)); }
    private static void sendToUnity(String method, String value) { MAIN.post(() -> UnityPlayer.UnitySendMessage(UNITY_OBJECT, method, value == null ? "" : value)); }
}
