# Pedra, Papel e Tesoura — RPS Arena

Jogo 3D em Unity 6 com personagens cartoon masculino e feminino, gestos articulados, câmeras animadas e expressões faciais pelo **EmoteR do SALSA LipSync Suite**.

O treino contra CPU funciona sem conexão e salva ELO, vitórias, derrotas, empates e melhor sequência por nome de jogador neste dispositivo. O cliente online segue o contrato Dingous / ChatTrade; a disponibilidade do backend é independente do treino.

## Jogar

- Windows: execute `Builds/Windows/DuelArena.exe`, mantendo a pasta inteira ao lado do executável.
- WebGL: sirva `Builds/WebGL` por HTTP/HTTPS; `index.html` não deve ser aberto por `file://`.
- Versão publicada: https://dingous.com.br/Games/pedra-papel-tesoura/
- Unity: abra `UnityProject/Assets/Scenes/Main.unity` e pressione Play.

Escolha o personagem, digite um nome local e clique em **JOGAR TREINO**. Use os botões ou as teclas **1 / 2 / 3**. Vence quem conquistar duas rodadas. Empates repetem a rodada; após 20 rodadas vale o placar acumulado. Ao esgotar 10 segundos, uma jogada aleatória é escolhida e informada. Desistir registra derrota. A CPU decide antes da entrada do jogador.

**RANKING → NESTE DISPOSITIVO** lista somente os perfis locais que concluíram partidas; **ONLINE** consulta o servidor com a conta Google. Não há jogadores ou resultados online simulados.

O backend foi implementado e publicado no ChatTrade em 30/09/2026: fila, partidas autoritativas, PostgreSQL, ELO e ranking. `Builds/Validation/backend-test.txt` registra o teste com dois servidores SignalR e clientes simultâneos. O usuário confirmou que o login Google na versão Web exibe seu nome. A verificação interativa com duas pessoas e o teste em aparelho Android são etapas distintas. O executável Windows oferece treino; o login Google está implementado para Android e WebGL.

`Builds/Validation/backend-production-test.txt` registra a sonda autenticada em produção: perfil persistido, ranking, negociação SignalR, WSS e token na query como no WebGL. A sonda não entrou na fila pública e desativou a conta técnica ao terminar.

## Arquitetura

Este repositório contém o cliente Unity. O backend fica no módulo `src/ChatTrade.Api/Rps` do repositório `D:\Git\Diego\ChatTrade\ChatTrande`, usando a API e o banco existentes do Dingous.

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
- dois personagens 3D com nove animações cada, incluindo gestos das duas mãos;
- expressões Smile, Frown, Surprise e Blink configuradas no SALSA EmoteR;
- câmeras de apresentação, contagem, revelação e retrato do vencedor, com opção de movimento reduzido;
- efeitos sonoros, contagem visual, seleção de personagem e revanche;
- ranking local persistente separado do ranking online;
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

A página pública em <https://dingous.com.br/Games/pedra-papel-tesoura/> usa uma apresentação
HTML indexável antes de carregar o jogo. O template está em
`UnityProject/Assets/WebGLTemplates/Duel`: preserve `index.html`, `arena.css` e
`arena-preview.png` nos próximos builds WebGL. Ele inclui regras, modos, perguntas
frequentes, canonical, metadados de compartilhamento e dados estruturados do jogo.
O botão **Jogar agora** baixa o WebGL sob demanda; **Sobre o jogo** permite consultar
a apresentação sem reiniciar a arena. Uma partida online continua ativa nesse intervalo.
Os sitemaps e o link do rodapé são mantidos no projeto Dingous/ChatTrade.
O endereço anterior em `/jogos/pedra-papel-tesoura/` redireciona permanentemente
para `/Games/pedra-papel-tesoura/`, incluindo os arquivos do WebGL. Na hospedagem,
os arquivos ficam em `/app/mvc/wwwroot/Games/pedra-papel-tesoura/`.

Antes de publicar:

1. confirmar o Web Client ID do Google no `RpsGameConfig`;
2. configurar pacote Android `com.dingous.rpsarena` no Google Cloud/Play Console;
3. registrar SHA-1/SHA-256 da chave de assinatura;
4. gerar AAB assinado;
5. publicar WebGL em HTTPS;
6. testar duas contas simultaneamente e reconexão de rede.
