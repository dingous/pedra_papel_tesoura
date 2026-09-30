# Pedra, Papel e Tesoura — RPS Arena

Jogo online em Unity 6 para **Android + WebGL**, integrado ao backend central do ecossistema **Dingous / ChatTrade**.

## Arquitetura

Este repositório contém o cliente Unity. **Não existe backend ASP.NET separado.**

O cliente usa o `dingous/ChatTrade` para:

- login Google global (`POST /api/auth/google`, `applicationCode = rps-arena`);
- identidade/usuário central do Dingous;
- perfil e ranking ELO (`/api/game/rps`);
- matchmaking e partida autoritativa via SignalR (`/hubs/rps`);
- PostgreSQL compartilhado do ecossistema.

A escolha do adversário nunca é enviada ao outro jogador antes de as duas jogadas estarem travadas no servidor.

## Funcionalidades do cliente

- Android e WebGL com a mesma conta/ranking;
- login Google;
- matchmaking 1x1;
- melhor de 3;
- pedra, papel e tesoura;
- ranking ELO;
- UI responsiva para celular e navegador;
- personagens/cartões com gesto de mão animado;
- transporte SignalR sobre WebSocket com negociação oficial do ASP.NET Core;
- sessão mantida apenas em memória no cliente (sem JWT em `PlayerPrefs`).

## Unity

Versão usada: `6000.4.5f1`.

Abra a pasta `UnityProject`.

O bootstrap cria a UI automaticamente. A cena `Assets/Scenes/Main.unity` já está no Build Settings.

### Backend

Edite `Assets/Scripts/Config/RpsGameConfig.cs` se o host da API mudar.

Padrão de produção:

```text
https://www.dingous.com.br
```

### Google OAuth

`googleWebClientId` é um **Client ID público**, nunca um Client Secret. Ele precisa estar aceito em `Authentication:Google:GlobalClientIds` no ChatTrade.

Android usa Credential Manager. O pós-processador `RpsCredentialManagerGradle.cs` injeta apenas as dependências AndroidX/Google necessárias no Gradle exportado.

WebGL usa Google Identity Services em `Assets/Plugins/WebGL/RpsGoogleAuth.jslib`.

## Backend Dingous

O módulo esperado no ChatTrade está documentado em `docs/DINGOUS_BACKEND.md`.

Endpoints:

```text
POST /api/auth/google
POST /api/auth/development?applicationCode=rps-arena   # somente Development
GET  /api/game/rps/me
GET  /api/game/rps/ranking?take=50
WS   /hubs/rps
```

Métodos SignalR:

```text
JoinQueue()
LeaveQueue()
Play(matchId, round, choice)
```

Eventos recebidos:

```text
QueueJoined
MatchFound
RoundStarted
ChoiceLocked
RoundResult
MatchFinished
OpponentDisconnected
```

## Produção

Antes de publicar:

1. confirmar o Web Client ID do Google no `RpsGameConfig`;
2. configurar pacote Android `com.dingous.rpsarena` no Google Cloud/Play Console;
3. registrar SHA-1/SHA-256 da chave de assinatura;
4. gerar AAB assinado;
5. publicar WebGL em HTTPS;
6. testar duas contas simultaneamente e reconexão de rede.
