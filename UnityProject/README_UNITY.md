# RPS Arena — Unity

1. Abra `UnityProject` no Unity `6000.4.5f1`.
2. A cena `Assets/Scenes/Main.unity` já está no Build Settings.
3. O bootstrap cria a interface e os serviços em runtime.
4. Configure `RpsGameConfig.apiBaseUrl` e o Google Web Client ID público.
5. Android: use o package `com.dingous.rpsarena`, configure SHA-1/SHA-256 no OAuth e gere AAB assinado.
6. WebGL: publique sob HTTPS; o cliente negocia SignalR e usa WSS automaticamente.

O login Android usa Credential Manager. O login WebGL usa Google Identity Services. O JWT do Dingous não é salvo em PlayerPrefs/localStorage.

## Personagens e apresentação

`RpsArenaStage` instancia os personagens de `Resources/RpsCharacterCatalog.asset`, monta a arena e controla câmera, áudio e partículas. `RpsCharacterAnimator` toca os gestos dos FBX; `RpsExpressions` solicita as expressões ao SALSA EmoteR real.

Os modelos originais e animações ficam em `Assets/Characters`. Os prefabs do jogo, meshes com quatro blendshapes faciais e materiais da arena ficam em `Assets/GameArt`. Os FBX originais e os arquivos Blender foram preservados. As expressões foram acrescentadas nos meshes usados pelos prefabs do jogo.

SALSA 2.5.6.150 foi importado da cópia licenciada já presente no cache local do Asset Store, em `Assets/Plugins/Crazy Minnow Studio/SALSA LipSync`. Não é uma implementação substituta. O jogo usa o módulo EmoteR para reações e piscar; não inclui dublagem ou áudio de fala.

Para regenerar os recursos, use **RPS → Build Playable Game Assets**. Para validar regras e referências, use **RPS → Validate Game**. `RpsGameAssets.BuildWindows` e `RpsGameAssets.BuildWeb` são pontos de entrada para builds em batchmode. Não execute dois Unity Editors simultâneos sobre este projeto.

## Estado do jogo

O treino é independente da rede: melhor de três, CPU que escolhe antes do jogador, cronômetro de dez segundos, empate, desistência, revanche e persistência de estatísticas/ELO por perfil local. Os dados ficam no PlayerPrefs `rps.career.v1`; som e câmera têm preferências separadas. O ranking local não é uma classificação competitiva segura e não é sincronizado ao servidor.

O cliente online faz negociação SignalR, handshake com timeout, confirmação de invocações, ping, tratamento de fechamento, deduplicação de eventos e cancelamento da fila por desconexão. Resultados e ELO online vêm exclusivamente do servidor. O módulo `ChatTrade.Api/Rps` foi implementado e testado com dois servidores e clientes reais contra PostgreSQL. A sonda autenticada em produção passou, e o usuário confirmou seu nome após o login Google na versão Web. Duas pessoas jogando com contas Google e um aparelho Android não foram testados nesta entrega.

## Verificação

`Builds/Validation/runtime-test.txt` registra o teste do executável: partida completa, bloqueio de entrada duplicada, animação de blendshapes pelo SALSA, seleção de avatar, recarga da persistência, ranking, revanche, desistência única e controles de som/câmera. As imagens da mesma pasta permitem revisar a interface em paisagem e retrato.

O teste é ativado somente pelo argumento `-rps-smoke -rps-report CAMINHO`. Usa `rps.qa.career.v1`, separado do progresso do jogador, e remove esse slot ao terminar. Não confundir essa validação local com teste do backend ou de aparelhos Android.
