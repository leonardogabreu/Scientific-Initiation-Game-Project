# Modo Tutorial — contrato de implementação

Modo guiado para crianças com síndrome de Down que não conseguem navegar pelo mapa nem
sustentar raciocínio de várias etapas. O jogo vira uma sequência de passos com a tela
congelada: só destrava quando a criança aperta exatamente a tecla pedida.

Este documento é o **contrato compartilhado** entre as sessões de desenvolvimento. Cada
sessão implementa a sua parte contra as assinaturas definidas aqui, sem editar os arquivos
das outras. Nenhuma sessão deve mudar este documento: se achar que o contrato está errado,
reporte no final em vez de divergir dele.

## Decisões de produto (já fechadas com o pesquisador)

1. **Cena nova dedicada**: `Assets/Scenes/5-Tutorial.unity`. As fases de pesquisa (2, 3 e 4)
   não mudam de comportamento.
2. **Uma letra por rodada**: toca o som da letra, mostra a letra na tela, e o ciclo completo
   é pegar aquela letra na esteira e colocá-la na mesa.
3. **Sem fim** (ver "Modo infinito" abaixo): a letra e a posição da caixa são sorteadas a cada
   rodada, as estrelas ciclam de 3 em 3 e quem encerra é a professora, pelo `Menu_Button` que já
   vive no `PlayingPanel`. Só a letra pedida fica na esteira, então não existe como pegar errado.
4. **Tecla errada não faz nada**: o jogo não avança, o ícone da tecla pedida pulsa e o áudio
   da letra toca de novo. Sem som de erro, sem punição.
5. **Telemetria marcada como tutorial**: sobe para a plataforma com `game_mode: "tutorial"` e
   **não** alimenta o `DifficultyManager`.

## Como o jogo está montado hoje (contexto)

- `Core.unity` é persistente (managers, HUD, robô). A fase entra additive: `LevelSelection.TargetScene`
  é definido em `LevelSelectController.loadLevel` e carregado por `GameBootstrap`.
- `GameCycleManager` tem `GameStateCL { Start, Playing, Over }` e o evento `onGameStateChanged`.
  Praticamente todo sistema do jogo só roda em `Playing`. **O tutorial roda em `Playing`**:
  HUD, estrelas e coleta de dados continuam funcionando normalmente.
- Movimento: `ChasingLettersGridClicker.Update()` — mouse (`NavMeshAgent.SetDestination`) ou
  setas/WASD contínuo (`NavMeshAgent.Move`), escolhido por `PlayerPrefs["movementControlMode"]`.
  Mapeamento das setas: `UpArrow/W → Vector3.forward`, `DownArrow/S → back`,
  `LeftArrow/A → left`, `RightArrow/D → right`.
- Interação: `PlayerControllerCL.Update()` → `Space` → `InteractWithClosest()` → `OverlapSphere`
  de raio `interactionRadius` (1.5 no Inspector) na `interactionLayer` → switch em
  `InteractableCL.type` (`LetterBox`, `Table`, `Deliver`, `Trash`, `Hint`, `Submit`, `InteractionButton`).
- A palavra da rodada vem de um `ChallengeSource` (abstrato). `ChasingLettersGameManager.SelectNewWord`
  chama `RequestChallenge` e dispara `onWordChanged`; `DeliverTablesManager` reage ligando N mesas
  centralizadas (lista par/ímpar conforme o tamanho da palavra; palavra de 1 letra → lista ímpar, 1 mesa).
- Entrega: `SubmitZoneManager.EvaluateWord()` → acerto toca SFX/VFX, reseta mesas,
  `GameCycleManager.addScore()` (acende estrela; 3 acertos → `Over`) e pede a próxima palavra.
- Áudios de letra já existem: `Assets/Resources/AudioWords/{a,e,i,o,u,b,c,d}.asset`
  (`AudioWord` = `targetWord` + `audioClip`), mp3 em `Assets/Prefabs/SFX/WordSounds/Letters/`.

## Arquitetura do modo tutorial

Namespace de arquivos novos: `Assets/Scripts/5-Tutorial/`.

O `TutorialDirector` é o **único dono do input** enquanto o tutorial está ativo. Ele vive na
cena do tutorial, então `TutorialDirector.Instance == null` em qualquer outra fase — é o mesmo
padrão de `DifficultyManager.HasInstance` já usado no projeto. Isso mantém o resto do jogo
intacto: dois `if` de uma linha nos scripts existentes e nada mais.

### Congelamento

`Time.timeScale = 0f` durante os passos de espera, `1f` durante o auto-andar.

Regras obrigatórias:

- `Input.GetKeyDown` funciona normalmente com `timeScale = 0`, e `AudioSource` também — é por
  isso que essa abordagem serve.
- Toda espera dentro das corrotinas do tutorial usa `WaitForSecondsRealtime` ou
  `Time.unscaledDeltaTime`. `WaitForSeconds` **travaria para sempre**.
- `Time.timeScale` precisa voltar a `1f` em `OnDisable`, `OnDestroy` e em qualquer mudança de
  estado para `Start`/`Over`. Se isso falhar, o jogo fica congelado depois de sair do tutorial —
  é o bug mais grave possível aqui, e tem que ter teste manual descrito.
- `PlayerControllerCL.Update` já protege a divisão por `Time.deltaTime` com `if (Time.deltaTime > 0f)`,
  então o Animator não quebra congelado.

### Passos de uma rodada

Para cada rodada (uma letra):

| # | Passo | timeScale | O que espera |
|---|-------|-----------|--------------|
| 1 | `Cue` | 0 | nada: toca o áudio da letra e mostra a letra na tela |
| 2 | `AwaitKeyToBelt` | 0 | a tecla de direção pedida (seta ou WASD equivalente) |
| 3 | `MoveToBelt` | 1 | auto-andar até a âncora da letra na esteira |
| 4 | `AwaitPick` | 0 | `Space` → pega a letra |
| 5 | `AwaitKeyToTable` | 0 | a tecla de direção pedida |
| 6 | `MoveToTable` | 1 | auto-andar até a âncora da mesa |
| 7 | `AwaitPlace` | 0 | `Space` → coloca a letra |
| 8 | `RoundComplete` | 0 | entrega automática via `SubmitZoneManager.EvaluateWord()` |

A entrega automática no passo 8 reaproveita toda a máquina existente: SFX/VFX de acerto,
reset das mesas, estrela, e `SelectNewWord()` que puxa a letra da rodada seguinte. Três rodadas
acendem as três estrelas e levam a `GameStateCL.Over` sozinhas.

### A tecla pedida é derivada da geometria, não autorada

Pedir uma tecla que não corresponde ao caminho real seria arbitrário para a criança. O director
calcula a tecla a partir do vetor `destino - posição atual`, usando o eixo dominante e o mesmo
mapeamento das setas do jogo:

```csharp
// eixo dominante de (to - from) no plano XZ
// +Z → UpArrow/W, -Z → DownArrow/S, -X → LeftArrow/A, +X → RightArrow/D
public static KeyCode ResolveDirectionKey(Vector3 from, Vector3 to);
```

O gate aceita a seta **ou** a letra WASD equivalente (as duas já são válidas no jogo normal).
Cada rodada pode sobrescrever a tecla por um campo opcional no Inspector, para o caso de o
caminho real do NavMesh não bater com a linha reta.

### Âncoras e raio de interação

O auto-andar precisa terminar **dentro** do `interactionRadius` do player, senão o `Space` não
encontra nada no `OverlapSphere`.

**Correção:** uma versão anterior deste contrato dizia que o raio era 1.5 e exigia a âncora a no
máximo 1.0 do alvo. 1.5 é só o *default* do campo em `PlayerControllerCL.cs`; o valor serializado
no Player em `Core.unity` é **10**, e é o do Inspector que vale em jogo. O director lê o raio do
player da cena e exige a âncora dentro de 90% dele.

Com raio grande, estar no alcance não basta: `GetClosestInteractible` devolve o interactible
**mais próximo**, então uma âncora de onde outro alvo fique mais perto faria a criança pegar a
letra errada e o passo nunca concluir. O director valida as duas coisas no `Start` — `LogError`
para fora de alcance, `LogWarning` quando outro interactible está mais perto que o alvo.

Chegada: `!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance`, com timeout
de segurança de 8 segundos (não escalado) que faz `agent.Warp(âncora)` e segue — o tutorial nunca
pode ficar preso.

## Modo infinito

Ligado por `endlessMode` no `TutorialDirector` (padrão: ligado). O que muda:

- A lista `rounds` deixa de ser a **sequência** e passa a ser o conjunto de **posições possíveis**
  da caixa: de cada entrada só valem `letterBox`, `beltAnchor`, `table` e `tableAnchor`. A `letter`
  e o `letterAudio` de cada entrada são ignorados, porque a letra é sorteada.
- O pool de letras vem de `TutorialLetterLibrary`, que lê os `AudioWord` de
  `Assets/Resources/AudioWords`. **Gravar um áudio novo e salvar o AudioWord nessa pasta já coloca
  a letra no sorteio**, sem código e sem Inspector. Entram só assets com áudio, de uma letra e
  dentro do alfabeto do jogo — as palavras inteiras da mesma pasta ficam de fora. Hoje são oito:
  as cinco vogais mais B, C e D.
- As primeiras `vowelOnlyRounds` rodadas (padrão 5) sorteiam só vogais; depois entram as demais.
  Nunca repete a letra da rodada anterior quando há alternativa.
- A posição gira pela lista a cada rodada. Como a tecla pedida é derivada da geometria, isso faz a
  direção pedida variar em vez de ser sempre a mesma.
- `GameCycleManager.addScore` não encerra a sessão durante o tutorial: a cada `wordsToWin` letras
  as estrelas comemoram, zeram e recomeçam.
- `CurrentRoundIndex` é a posição no ciclo (para o "X de 3" do overlay); `RoundsCompleted` é a
  contagem que não zera, e é a que a telemetria usa.

`endlessMode` desligado volta ao comportamento de lista fixa descrito no resto deste documento.

## Modelo de dados

```csharp
// Assets/Scripts/5-Tutorial/TutorialRound.cs
[Serializable]
public class TutorialRound
{
    public string letter;                 // "A" — vai virar a "palavra" da rodada
    public AudioClip letterAudio;         // de Prefabs/SFX/WordSounds/Letters
    public GameObject letterBox;          // a caixa já posicionada e parada na esteira
    public Transform beltAnchor;          // onde o robô para para pegar
    public GameObject table;              // a mesa de destino
    public Transform tableAnchor;         // onde o robô para para colocar
    public KeyCode keyToBeltOverride;     // KeyCode.None = derivar da geometria
    public KeyCode keyToTableOverride;    // KeyCode.None = derivar da geometria
}

public enum TutorialStepKind
{
    Cue, AwaitKeyToBelt, MoveToBelt, AwaitPick,
    AwaitKeyToTable, MoveToTable, AwaitPlace, RoundComplete
}
```

## Contrato público do TutorialDirector

Esta é a superfície que as outras sessões consomem. **Não mudar assinatura sem avisar.**

```csharp
// Assets/Scripts/5-Tutorial/TutorialDirector.cs
public class TutorialDirector : MonoBehaviour
{
    public static TutorialDirector Instance { get; private set; }

    /// <summary>true quando o modo tutorial está no comando do input.</summary>
    public static bool IsActive => Instance != null && Instance.IsRunning;

    public bool IsRunning { get; private set; }

    /// <summary>true apenas no passo que espera Space.</summary>
    public bool AllowsInteraction { get; private set; }

    public TutorialStepKind CurrentStep { get; private set; }
    public int CurrentRoundIndex { get; private set; }   // 0-based
    public int TotalRounds { get; }
    public string CurrentLetter { get; }

    // ---- eventos para a camada visual e para a telemetria ----

    /// <summary>Passo 1: mostrar a letra na tela. Argumento: a letra ("A").</summary>
    public event Action<string> onCue;

    /// <summary>Passos 2 e 5: mostrar o ícone da tecla pedida.</summary>
    public event Action<KeyCode> onAwaitKey;

    /// <summary>Passos 4 e 7: mostrar o ícone de ESPAÇO.</summary>
    public event Action onAwaitInteract;

    /// <summary>Tecla diferente da pedida: pulsar o ícone e repetir o áudio.
    /// Argumento: a tecla que a criança apertou.</summary>
    public event Action<KeyCode> onWrongKey;

    /// <summary>Alvo do passo atual, para destacar no mundo. null = esconder o destaque.</summary>
    public event Action<Transform> onHighlightTarget;

    /// <summary>Entrou em um passo novo: esconder os prompts do passo anterior.</summary>
    public event Action<TutorialStepKind> onStepStarted;

    /// <summary>Passo concluído com a ação correta. Argumentos: passo, segundos (não escalados)
    /// que a criança levou para acertar, quantas teclas erradas apertou antes.</summary>
    public event Action<TutorialStepKind, float, int> onStepCompleted;

    /// <summary>Rodada concluída. Argumento: índice 0-based da rodada.</summary>
    public event Action<int> onRoundCompleted;

    public event Action onTutorialFinished;

    /// <summary>Repete o áudio da letra da rodada atual.</summary>
    public void ReplayLetterAudio();
}
```

```csharp
// Assets/Scripts/5-Tutorial/TutorialChallengeSource.cs
/// Entrega a letra da rodada atual como se fosse a "palavra", para as mesas e o
/// SubmitZoneManager funcionarem sem mudança. É também a origem do game_mode da telemetria.
public class TutorialChallengeSource : ChallengeSource
{
    public override string GameMode => "tutorial";
    public override void RequestChallenge(Func<string, bool> isPlayable, Action<string, string> onReady);
}
```

## Alterações permitidas em arquivos existentes

Apenas estas, e **só a sessão dona do arquivo** mexe nele:

- `ChasingLettersGridClicker.cs` — no topo de `Update()`: `if (TutorialDirector.IsActive) return;`
  (sem navegação livre no tutorial).
- `PlayerControllerCL.cs` — (a) o trecho do `Space` em `Update()` não roda quando
  `TutorialDirector.IsActive` (o director é o único dono do input); (b) `InteractWithClosest()`
  passa de `private void` para `public bool TryInteractWithClosest()`, devolvendo se interagiu
  com algo, para o director poder checar o resultado.
- `DataCollectionManager.cs` — novos eventos de passo do tutorial (seção de telemetria abaixo).
- `SubmitZoneManager.cs` / `DifficultyManager.cs` — guardas para o tutorial não alimentar as
  métricas de dificuldade.

Nada mais. Em especial: não mexer nas fases 2, 3 e 4, nem no `GameCycleManager`, nem no
`LetterBoxSpawner`, nem no `TreadmillBehavior` (na cena do tutorial a esteira fica parada por
configuração de cena, não por mudança de código).

## Telemetria

- O `game_mode: "tutorial"` já sai de graça pelo `TutorialChallengeSource.GameMode`, que o
  `ChasingLettersGameManager.GameMode` propaga para o `DataCollectionManager`.
- Eventos novos de passo, assinando os eventos do director (nunca chamados de dentro dele):
  qual passo, qual tecla foi pedida, quantos segundos até acertar, quantas teclas erradas,
  quantas repetições de áudio. É dado de pesquisa genuinamente novo sobre tempo de reação.
- O tutorial **não** pode chamar `DifficultyManager.DynamicDifficultyAdjustments()` nem
  registrar `RegisterCollectedData`, senão contamina o ajuste de dificuldade das fases reais.

## Verificação

As sessões **não devem abrir o Unity**. Cada sessão em worktree tem um `Library/` vazio e um
import frio levaria muitos minutos; além disso o Unity só aceita uma instância por projeto.
A compilação é verificada uma vez, no checkout principal, depois da integração dos branches.

Cada sessão entrega, no relatório final:
- os arquivos criados/alterados;
- o roteiro de teste manual no Editor (o que clicar, o que deve acontecer, incluindo
  **sair do tutorial e confirmar que `Time.timeScale` voltou a 1**);
- qualquer ponto em que o contrato acima se mostrou errado.
