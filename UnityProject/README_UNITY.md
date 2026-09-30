# RPS Arena — Unity

1. Abra `UnityProject` no Unity `6000.4.5f1`.
2. A cena `Assets/Scenes/Main.unity` já está no Build Settings.
3. O bootstrap cria a interface e os serviços em runtime.
4. Configure `RpsGameConfig.apiBaseUrl` e o Google Web Client ID público.
5. Android: use o package `com.dingous.rpsarena`, configure SHA-1/SHA-256 no OAuth e gere AAB assinado.
6. WebGL: publique sob HTTPS; o cliente negocia SignalR e usa WSS automaticamente.

O login Android usa Credential Manager. O login WebGL usa Google Identity Services. O JWT do Dingous não é salvo em PlayerPrefs/localStorage.

`HandGestureView` é a camada visual atual. Pode ser trocada por modelos 3D/Animator com triggers para pedra, papel e tesoura sem alterar o protocolo online.
