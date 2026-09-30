# Integração com Dingous ChatTrade

O RPS Arena não possui serviço próprio. O backend pertence ao repositório `dingous/ChatTrade`.

## Aplicação do ecossistema

Adicionar `RpsArena` a `DingousApplicationProject` com código `rps-arena`.

Assim o endpoint global `POST /api/auth/google` registra o último login e vincula o usuário ao projeto sem duplicar a tabela `users`.

## Persistência

- `game_rps_player_stats`
- `game_rps_queue`
- `game_rps_matches`
- `game_rps_rounds`

Todas referenciam o `users.id` central.

## Realtime

Hub: `/hubs/rps`.

O servidor é autoritativo. A escolha só pode ser gravada uma vez por rodada. A resolução ocorre dentro de transação/lock no PostgreSQL, portanto dois envios simultâneos não expõem a escolha do adversário nem duplicam pontuação/rating.

## ELO

Rating inicial: 1000. K-factor: 32. O rating é atualizado apenas ao finalizar uma partida.
