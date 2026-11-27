# Cena 5-Tutorial — como é gerada e como verificar

A fase do modo tutorial (`Assets/Scenes/5-Tutorial.unity`) **não é mantida à mão**. Ela é gerada por
`Assets/Editor/TutorialSceneBuilder.cs`, pelo menu `Tools/Tutorial/Gerar cena 5-Tutorial`.

O contrato do modo está em [`TUTORIAL_MODE.md`](TUTORIAL_MODE.md); este documento cobre só a cena,
a entrada no menu e o roteiro de verificação no Editor.

## Por que um builder

Uma cena de fase deste projeto tem ~7.000 linhas de YAML, com fileIDs, GUIDs, instâncias de prefab
aninhadas (a esteira usa escala 1000 × 0.001, as mesas × 500) e um NavMesh assado em asset separado.
Escrever ou editar isso à mão é frágil e impossível de revisar em diff. O builder, em vez disso:

1. copia `3-Audio_Words.unity` como cenário base — piso, paredes, esteiras, mesas, câmera, luz e
   NavMesh já assado vêm de graça e idênticos à fase de pesquisa;
2. aplica as diferenças do tutorial **por configuração de cena**;
3. mede a geometria resultante e loga um relatório de verificação no Console.

Rodar de novo é idempotente: a cena é recriada do zero a partir da mesma fonte.

Nenhum arquivo de outra sessão é tocado. Em particular `TreadmillBehavior.cs` e
`LetterBoxSpawner.cs` continuam intactos: a esteira para porque os **componentes das instâncias
desta cena** são desativados.

## O que a cena tem

| Elemento | Como fica |
|---|---|
| Cenário | igual a `3-Audio_Words` (piso, paredes, 4 esteiras, mesas, fornos, câmera, luz) |
| Esteira | **parada**: todos os `TreadmillBehavior` desativados |
| Spawn | **nenhum**: todos os `LetterBoxSpawner` desativados (os fornos ficam como cenário inerte) |
| Letras | 3 caixas estáticas `Tutorial Letters/LetterBox_{A,E,I}` sobre a esteira, cinemáticas e sem gravidade |
| Lixeira / incinerador / dica | desativados (`InteractableCL` de tipo `Trash`/`Hint`, `LetterBoxIncinerator`, grupos `Trashes` e `Incinerators`) |
| Âncoras | `Anchors/Anchor_Belt_{A,E,I}` e `Anchors/Anchor_Table` |
| Mesa de destino | a mesa central da lista ímpar (7ª de 13) — é a única que o `DeliverTablesManager` liga para palavra de 1 letra |
| Managers | `Tutorial Manager` com `TutorialDirector`, `TutorialChallengeSource` e um `AudioSource` 2D |
| Overlay | `Tutorial Overlay` com `TutorialOverlayView` (a UI se monta em código) |
| Challenge source | `AudioWordChallengeSource` removido do `Game Manager`; `challengeSource` aponta para o `TutorialChallengeSource` |
| `Interaction_Button` | desativado — o `UnityEvent` dele chamava o componente removido |

### Posicionamento das letras e das âncoras

As três caixas ficam sobre o collider da esteira de maior X do `TreadmillGroup1` (a mais perto de
onde o robô nasce), espaçadas 7 unidades em X, encostadas na borda voltada para o corredor e
apoiadas no topo do collider — exatamente onde uma caixa sorteada pararia depois de cair do forno,
então a altura de interação é a mesma das fases de pesquisa.

As âncoras ficam **no piso** (y = 0), logo depois da borda do alvo. A esteira e as caixas têm
`NavMeshModifier` de área "Not Walkable": o robô não para sobre elas, para no corredor ao lado.

## Passos que ainda são manuais

- **Build Settings.** O builder registra a cena sozinho (`EditorBuildSettings`), mas só consegue
  fazer isso depois de a cena existir e ser importada — o GUID não dá para prever. Então
  `ProjectSettings/EditorBuildSettings.asset` **muda quando o builder roda** e precisa entrar no
  commit junto com `Assets/Scenes/5-Tutorial.unity`. Sem a cena habilitada ali,
  `SceneManager.LoadScene("5-Tutorial")` falha em runtime.
- **Scripts do tutorial.** `TutorialDirector`, `TutorialChallengeSource`, `TutorialOverlayView` e
  `TutorialRound` são resolvidos **por nome** (reflexão) e preenchidos via `SerializedObject`. Se
  algum ainda não existir no projeto, a cena é gerada completa e o Console lista o que falta ligar.
  Depois do merge das outras sessões, **rode o builder de novo** para ele ligar tudo.
- **Campo das rodadas.** O contrato define `TutorialRound` mas não batiza o campo que guarda a
  lista. O builder tenta `rounds`, `tutorialRounds`, `roundsList` e `challengeRounds`, no director e
  depois na challenge source. Se o nome for outro, o Console imprime o passo de Inspector com todos
  os valores a preencher.

## Entrada no menu

`Assets/Scenes/0-Main_Menu.unity` ganhou uma quarta entrada no array `levels` do
`LevelSelectController`:

```yaml
  - displayName: Tutorial
    sceneName: 5-Tutorial
```

Foi **acrescentada no fim** de propósito: inserir antes deslocaria os índices das fases existentes,
e os índices são as chaves de `PlayerPrefs["levelUnlocked_<i>"]`. Com `unlockAll = 1` isso não muda
nada hoje, mas remapearia desbloqueios salvos no dia em que o desbloqueio progressivo for ligado.

## Roteiro de verificação no Editor

1. Abra o projeto no Unity (6000.4.1f1) no checkout principal.
2. `Tools/Tutorial/Gerar cena 5-Tutorial` → confirme no diálogo.
3. Leia o Console. O builder loga, em um bloco só:
   - quantos `TreadmillBehavior` e `LetterBoxSpawner` foram desativados (esperado: 2 e 4);
   - quais objetos de erro foram desativados;
   - a posição de cada caixa, com aviso se alguma passar da ponta da esteira;
   - qual esteira e qual mesa foram usadas;
   - **para cada âncora, a distância 3D até o centro do collider do alvo**, com veredito;
   - o que ficou pendente de integração ou de Inspector.
   Um `LogError` aqui é bloqueante; um aviso de "acima do 1.0 do contrato" é esperado (ver abaixo).
4. `Tools/Tutorial/Validar cena 5-Tutorial` roda a verificação de novo sem recriar a cena. Além das
   distâncias, ela confere o que de fato decide se o passo conclui: **em cada âncora, o
   `InteractableCL` mais próximo tem que ser a letra daquela rodada** (é o que
   `PlayerControllerCL.TryInteractWithClosest()` vai escolher). O Console lista tudo que cai no
   alcance em cada âncora.
5. Confira no Inspector, com a cena aberta:
   - `Tutorial Letters` tem 3 filhos; cada `LetterBoxPrefab` tem `Rigidbody` cinemático, sem
     gravidade, e o `LetterText` com A, E e I;
   - `Anchors` tem 4 filhos, todos com y = 0;
   - `Tutorial Manager` tem as 3 rodadas preenchidas e `keyToBeltOverride`/`keyToTableOverride`
     em `None`;
   - `Game Managers/Game Manager` tem `Challenge Source` apontando para o `TutorialChallengeSource`
     e **nenhum** `AudioWordChallengeSource`;
   - `Game Objects/Trashes` e `Game Objects/Incinerators` estão desmarcados.
6. `File > Build Profiles` (ou `Build Settings`): `Assets/Scenes/5-Tutorial.unity` aparece
   habilitada.
7. Jogue pelo menu: abra `0-Main_Menu`, Play, botão **Tutorial**. A cena `Core` carrega e sobe
   `5-Tutorial` additive. Esperado: esteira imóvel, exatamente três letras paradas, sem lixeira nem
   incinerador, HUD com as três estrelas apagadas.
8. **Depois de sair do tutorial, confirme que `Time.timeScale` voltou a 1**: termine as 3 rodadas (ou
   use o botão de menu do HUD), volte ao menu, entre em `Fase 1 - Escrita` e verifique que o robô
   anda e a esteira corre normalmente. Se o jogo ficar congelado, o bug é no `TutorialDirector`
   (contrato, seção "Congelamento"), não na cena.

### Não reassar o NavMesh

O builder copia o NavMesh assado para `Assets/Scenes/5-Tutorial/NavMesh-Floor.asset` e repointa o
`NavMeshSurface` do piso, para o tutorial não depender de um rebake da fase 3. A geometria é a
mesma, então **não há motivo para reassar** — e um rebake sem as mesmas configurações mudaria a
navegação da fase.

## Onde o contrato não bate com a cena

Dois pontos para quem estiver implementando o `TutorialDirector`:

1. **`interactionRadius` é 10, não 1.5.** O contrato parte de 1.5 e deduz daí a regra de "âncora a
   no máximo 1.0 do centro do collider". O valor serializado no `Player` de `Core.unity` é **10**.
   E a regra de 1.0 é **geometricamente impossível** para letras sobre a esteira: o topo do collider
   da esteira está a ~6,7 de altura e o centro da caixa a ~8,4, enquanto a âncora precisa ficar no
   piso para o `NavMeshAgent` (cujo `height` é 1) conseguir mapear o destino. Âncora flutuando na
   altura da caixa satisfaria o número e deixaria o `SetDestination` sem caminho: o robô não sairia
   do lugar. As âncoras desta cena ficam a ~8,8 do centro do alvo — dentro do raio real de 10, com a
   mesma geometria de interação das fases de pesquisa. **A validação do `Start` do director deveria
   comparar com `PlayerControllerCL.interactionRadius`**, não com 1.0, senão ela loga erro em uma
   cena correta.
2. **Replay não recarrega a cena.** `GameCycleManager` vai de `Over` direto para `Playing` pelo
   botão do painel de game over, sem `LoadScene`. Como `PlayerControllerCL.CarryLetterBox` faz
   `SetActive(false)` na caixa pega, as três letras do tutorial estarão desativadas no replay. O
   director precisa reativar `round.letterBox` ao (re)iniciar o tutorial — as caixas são objetos de
   cena comuns, referenciados pelas rodadas, então é só um `SetActive(true)`.
