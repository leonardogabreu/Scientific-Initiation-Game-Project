# Integração com a plataforma Adapt2Learn

Como o jogo conversa com a API do Adapt2Learn: de onde vêm as palavras, o que é registrado e
como testar. Base da API: `https://adapt2learn-895112363610.us-central1.run.app`.

## 1. Ponte com o host (WebGL)

O jogo roda dentro de um iframe da plataforma e recebe a sessão do HTML que o hospeda:

| Direção | Como |
| --- | --- |
| Unity → host | `PingReady()` (`Assets/Plugins/WebGL/WebGLBridge.jslib`) chama `window.UnityReady()` |
| host → Unity | `SendMessage("WebGLManager", "OnReceiveParams", json)` e `SendMessage("WebGLManager", "OnReceiveAuthToken", token)` |

O nome do GameObject (`WebGLManager`) e os nomes dos dois métodos fazem parte do contrato:
renomear qualquer um deles quebra a integração **sem erro de compilação**.

Parâmetros, lidos da query string pelo host: `discipline`, `subarea`, `school_id`, `game_id`,
`session_number`.

O template que faz isso é `Assets/WebGLTemplates/Adapt2Learn/` (Player Settings →
`PROJECT:Adapt2Learn`), derivado do template do `maze-revolution`. Ele já inclui o SDK do
Firebase Auth e envia o ID token para o Unity a cada refresh.

## 2. Palavras

`GET /api/word-challenges/puzzles?school_id=&discipline=&subarea=` → `{ challenge_id, word, image_url, puzzle }`

`ChasingLettersApiManager` busca **todas** as palavras da partida (uma por rodada), já deixando
a próxima adiantada enquanto o aluno joga a atual. Uma palavra é descartada e outra é sorteada
quando:

- tem algum caractere fora de `ChasingLettersGameManager.Alphabet` (a esteira não saberia produzir a letra);
- não cabe nas mesas da paridade correspondente (`DeliverTablesManager.CanFitWord`);
- já foi servida nesta sessão.

Sem sessão da plataforma (Editor, token ausente, 404 por não haver desafio cadastrado para a
escola/disciplina/subárea), `ChasingLettersGameManager` cai no banco local em
`Assets/Resources/WordData`. O `puzzle` (letras embaralhadas e slots) devolvido pela API não é
usado: neste jogo as letras chegam pela esteira, sorteadas pelo `LetterBoxSpawner`.

## 3. Respostas aos desafios

`POST /api/word-challenges/responses` → `{ challenge_id, game_session_number, correct }`

Enviado **a cada tentativa completa** de montar a palavra: `correct: false` quando o aluno
preencheu todas as mesas e entregou errado, `correct: true` quando acertou. Entrega com mesas
vazias não é tentativa e não gera resposta — só o evento `word_mistake` com `complete: false`.

**Não é opcional:** este é o único lugar de onde o backend sabe quais desafios o aluno já viu.
O `get_word_puzzle` chama `load_answered_challenge_ids()`, que lê exatamente esta coleção, e
`pick_word_challenge()` prefere os desafios ausentes dela. Sem esses POSTs, todo desafio conta
como não visto e o sorteio vira aleatório puro — entre sessões o aluno repete palavras e pode
nunca ver parte do banco. (A repetição *dentro* de uma sessão é evitada no cliente, pelo
`servedChallengeIds` do `ChasingLettersApiManager`.)

Efeito colateral a ter em mente: o backend marca o desafio como visto na primeira resposta,
inclusive numa errada. Se o aluno erra a palavra inteira e abandona a sessão sem completá-la,
aquele desafio passa a ser despriorizado no sorteio mesmo sem ter sido aprendido.

Para análise, este endpoint é redundante: `word_completed` e `word_mistake` em
`/api/events/game` carregam o mesmo `challenge_id` e muito mais contexto. Ele existe aqui pelo
efeito no sorteio, não pelo dado.

## 4. Eventos de jogo

`POST /api/events/game` → `{ event_type, game_id, payload }`

Enviados pela `GameEventReporter` (fila persistente, um por vez, com retry). Todos os payloads
carregam `session_number`.

| `event_type` | Quando | Campos além de `session_number` |
| --- | --- | --- |
| `start_session` | aluno aperta jogar | `school_id`, `discipline`, `subarea`, `words_to_win` |
| `start_word` | palavra da rodada definida | `word_index`, `challenge_id`, `word`, `word_length`, `source` (`platform`/`local`) |
| `letter_picked` | aluno pega uma letra | `word_index`, `challenge_id`, `word`, `letter`, `source` (`belt`/`table`), `slot`, `action_index`, `time` |
| `letter_placed` | aluno põe a letra numa mesa | idem, com `source` = `hand` |
| `letter_swapped` | aluno troca a letra carregada pela de uma caixa ou mesa | idem, mais `given_letter` (a que ficou no lugar); `letter` é a que passou a carregar |
| `letter_discarded` | aluno joga a letra no lixo | idem, com `slot` = -1 |
| `word_mistake` | entrega errada na zona de submit | `word_index`, `challenge_id`, `word`, `submitted_word`, `mistake_number`, `complete`, `time` |
| `hint_used` | aluno pede dica | `word_index`, `challenge_id`, `word`, `hint_number`, `time` |
| `word_completed` | palavra montada corretamente | `word_index`, `challenge_id`, `word`, `mistakes`, `hints`, `actions`, `time`, `first_try` |
| `finish_session` | última palavra concluída | `words_completed`, `total_mistakes`, `total_time` |

`challenge_id` vem vazio quando a palavra veio do banco local. `time` é em segundos, contado a
partir do início da palavra (ou da sessão, em `finish_session`).

Em `word_mistake`, `complete: false` marca a entrega feita com mesas ainda vazias — é ruído de
manuseio, não erro de conteúdo, e vale filtrar na análise.

**Trajetória.** Os quatro eventos de letra reconstroem a montagem passo a passo: `action_index`
é sequencial dentro da palavra e `slot` é a posição na palavra (0 = primeira letra, -1 quando
não se aplica). Dá para ver o aluno montando `CAOLA`, tirando a letra do slot 2 e corrigindo —
não só que a entrega deu errado. Todo evento é enviado na hora, e não acumulado até o fim da
palavra, para que uma sessão abandonada no meio ainda deixe o registro do que aconteceu.

O ponto de instrumentação é o `PlayerControllerCL.ExecuteInteraction`, por onde passa toda ação
do aluno; o `slot` vem do `DeliverTablesManager.GetSlotIndex`.

Para conferir no backend: `GET /api/events?origin=game&game_id=<id>` (professor) ou
`GET /api/events/game/<game_id>/fields?event_type=word_completed`.

## 5. Camada compartilhada

`Assets/Scripts/Platform/` não tem nada específico do Chasing Letters e serve para os outros
jogos do projeto:

- **`WebGLManager`** — token e parâmetros da sessão; evento `onAuthenticated`. Se a cena de jogo
  for aberta direto (sem passar pelo menu), ele se cria sozinho em runtime.
- **`PlatformApiClient`** — única saída HTTP: injeta o `Bearer`, aplica timeout de 15s e repete
  falhas de rede e 5xx (3 tentativas, backoff exponencial). Vive num objeto persistente, então
  uma requisição em voo sobrevive à troca de cena.
- **`GameEventReporter`** — fila de eventos com envio serial.

## 6. Testando

**No Editor**, sem token, o jogo roda com as palavras locais e os eventos aparecem no Console
em vez de irem para a API (`[GameEventReporter] Sem sessão da plataforma — evento não enviado: {...}`).

Para bater na API de verdade a partir do Editor, no GameObject `WebGLManager` da cena
`0-Main_Menu` preencha, em "Somente no Editor", um ID token do Firebase e os parâmetros da
sessão. O `school_id` precisa ser o mesmo do token — o endpoint devolve 403 se não bater.

**No build**, abra com a query string que a plataforma usa:
`?discipline=...&subarea=...&school_id=...&game_id=...&session_number=1`.
