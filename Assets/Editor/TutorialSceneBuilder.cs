using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gera e atualiza <c>Assets/Scenes/5-Tutorial.unity</c>, a fase determinística do modo tutorial
/// (ver <c>docs/TUTORIAL_MODE.md</c>).
///
/// POR QUE UM BUILDER E NÃO UMA CENA ESCRITA À MÃO
/// Uma cena de fase deste projeto tem ~7.000 linhas de YAML, com fileIDs, GUIDs, instâncias de
/// prefab aninhadas e um NavMesh assado em asset separado. Escrever isso à mão é frágil e
/// impossível de revisar. Este script, em vez disso:
///   1. copia a fase de pesquisa mais próxima (3-Audio_Words) como cenário base, o que preserva
///      piso, paredes, esteiras, mesas, câmera, luz e o NavMesh já assado;
///   2. aplica, por configuração de cena, as diferenças do tutorial (esteira parada, sem spawn
///      aleatório, sem lixeira/incinerador/dica, três letras fixas, âncoras, managers);
///   3. mede a geometria resultante e loga um relatório de verificação.
/// Rodar de novo é idempotente: a cena é recriada do zero a partir da mesma fonte.
///
/// NENHUM ARQUIVO DE OUTRA SESSÃO É TOCADO. A esteira para porque os componentes
/// <c>TreadmillBehavior</c> e <c>LetterBoxSpawner</c> são desativados *nas instâncias desta cena*
/// — <c>TreadmillBehavior.cs</c> e <c>LetterBoxSpawner.cs</c> continuam intactos.
///
/// OS SCRIPTS DO TUTORIAL SÃO RESOLVIDOS POR NOME. <c>TutorialDirector</c>,
/// <c>TutorialChallengeSource</c>, <c>TutorialRound</c> e <c>TutorialOverlayView</c> estão sendo
/// escritos em paralelo por outras sessões. Este builder os procura por nome em runtime
/// (<see cref="FindTypeByName"/>) e preenche os campos serializados via <see cref="SerializedObject"/>,
/// então ele compila e roda antes da integração. Quando um tipo ainda não existe, a cena é gerada
/// completa e o builder loga exatamente o que falta ligar no Inspector.
/// </summary>
public static class TutorialSceneBuilder
{
    // ------------------------------------------------------------------ caminhos

    private const string SourceScenePath = "Assets/Scenes/3-Audio_Words.unity";
    private const string TargetScenePath = "Assets/Scenes/5-Tutorial.unity";
    private const string TargetSceneFolder = "Assets/Scenes/5-Tutorial";

    private const string LetterBoxPrefabPath =
        "Assets/Prefabs/Game Objects/2-Chasing_Letters_Game_Objects/3D/LetterBoxPrefab.prefab";

    /// <summary>mp3 por letra, já usados pela fase de escuta.</summary>
    private const string LetterAudioFolder = "Assets/Prefabs/SFX/WordSounds/Letters";

    // ------------------------------------------------------- objetos da cena base

    // Caminhos de hierarquia na cena copiada. Se a fase 3 for reorganizada, é aqui que quebra —
    // e o builder loga erro nomeando o caminho que não achou, em vez de gerar cena silenciosamente
    // errada.
    private const string FloorPath = "Floor";
    private const string BeltGroupPath = "Game Objects/Treadmills/TreadmillGroup1";
    private const string OddTablesPath = "Game Objects/Odd_Deliver_Tables";
    private const string TrashesPath = "Game Objects/Trashes";
    private const string IncineratorsPath = "Game Objects/Incinerators";
    private const string GameManagerPath = "Game Managers/Game Manager";
    private const string InteractionButtonPath = "Game Objects/Interaction_Button";

    // ----------------------------------------------------- objetos novos do tutorial

    private const string AnchorsGroupName = "Anchors";
    private const string LettersGroupName = "Tutorial Letters";
    private const string TutorialManagerName = "Tutorial Manager";
    private const string OverlayName = "Tutorial Overlay";

    // --------------------------------------------------------------- geometria

    /// <summary>
    /// As três vogais do tutorial, na ordem das rodadas. Combina com as 3 estrelas do HUD
    /// (<c>GameCycleManager.wordsToWin</c>) e com os áudios a.mp3 / e.mp3 / i.mp3.
    /// </summary>
    private static readonly string[] RoundLetters = { "A", "E", "I" };

    /// <summary>
    /// Distância em X entre as três caixas na esteira. Precisa ser maior que a largura da caixa
    /// (3.5 unidades) para elas não se interpenetrarem, e grande o bastante para a caixa vizinha
    /// ficar fora do <see cref="PlayerInteractionRadius"/> da âncora — assim
    /// <c>PlayerControllerCL.TryInteractWithClosest()</c> nunca tem como pegar a letra errada.
    /// 7.0 satisfaz as duas coisas e ainda cabe inteira sobre uma única esteira (17.17 de
    /// comprimento). O builder verifica as duas condições e avisa se a margem se perder.
    /// </summary>
    private const float LetterBoxSpacing = 7f;

    /// <summary>
    /// Folga horizontal entre a borda do alvo e a âncora: raio do NavMeshAgent do robô (0.5,
    /// ver Player.prefab) + margem. A esteira e as caixas têm <c>NavMeshModifier</c> com área
    /// "Not Walkable", então o robô não consegue parar sobre elas: a âncora tem que ficar no
    /// corredor, logo depois da borda.
    /// </summary>
    private const float AnchorClearance = 1.0f;

    /// <summary>
    /// Lado pelo qual o robô chega aos alvos. O robô nasce em (0, 0.16, 0) na cena Core, as
    /// esteiras ficam em z ≈ -7 e as mesas de entrega em z ≈ -31: tudo é abordado pelo lado +Z.
    /// É o único botão a girar se o cenário mudar de orientação.
    /// </summary>
    private static readonly Vector3 AnchorApproachDirection = Vector3.forward;

    /// <summary>
    /// Regra do contrato (docs/TUTORIAL_MODE.md, "Âncoras e raio de interação"): âncora a no
    /// máximo 1.0 unidade do centro do collider do alvo. O <c>TutorialDirector</c> valida isso no
    /// <c>Start</c>. ATENÇÃO: nesta cena a regra é geometricamente impossível para letras sobre a
    /// esteira — ver <see cref="ReportAnchor"/>.
    /// </summary>
    private const float ContractMaxAnchorDistance = 1.0f;

    /// <summary>
    /// <c>PlayerControllerCL.interactionRadius</c> como está serializado em <c>Core.unity</c>.
    /// O contrato diz 1.5, mas o valor real no Inspector do Player é 10 — e é 10 que decide se o
    /// <c>Space</c> acha a letra. O builder valida contra este número.
    /// </summary>
    private const float PlayerInteractionRadius = 10f;

    /// <summary>Altura do pivô do robô acima do piso (Player em Core.unity: y = 0.16).</summary>
    private const float PlayerPivotHeight = 0.16f;

    // ============================================================== menu: gerar

    [MenuItem("Tools/Tutorial/Gerar cena 5-Tutorial", false, 10)]
    public static void BuildScene()
    {
        if (!EditorUtility.DisplayDialog(
                "Gerar 5-Tutorial",
                $"Isto recria '{TargetScenePath}' a partir de '{SourceScenePath}'.\n\n" +
                "Tudo que tiver sido editado à mão na cena do tutorial será perdido.\n\nContinuar?",
                "Gerar", "Cancelar"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
        {
            Debug.LogError($"[TutorialSceneBuilder] Cena base não encontrada: {SourceScenePath}");
            return;
        }

        // Recria do zero: idempotência vale mais que estabilidade de GUID aqui, porque nada
        // referencia a cena por GUID (LevelSelection usa o *nome* da cena) e o registro no
        // Build Settings é refeito no fim.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) != null)
        {
            AssetDatabase.DeleteAsset(TargetScenePath);
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, TargetScenePath))
        {
            Debug.LogError($"[TutorialSceneBuilder] Falhou copiar {SourceScenePath} -> {TargetScenePath}");
            return;
        }

        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        var log = new List<string>();

        GiveSceneItsOwnNavMesh(scene, log);
        StopTheBelt(scene, log);
        RemoveWaysToGoWrong(scene, log);

        Transform lettersGroup = CreateGroup(scene, LettersGroupName, log);
        Transform anchorsGroup = CreateGroup(scene, AnchorsGroupName, log);

        List<GameObject> letterBoxes = PlaceLetterBoxes(scene, lettersGroup, log);
        List<Transform> beltAnchors = PlaceBeltAnchors(scene, anchorsGroup, letterBoxes, log);
        GameObject deliverTable = FindDeliverTable(scene, log);
        Transform tableAnchor = PlaceTableAnchor(scene, anchorsGroup, deliverTable, log);

        GameObject tutorialManager = CreateTutorialManager(scene, letterBoxes, beltAnchors, deliverTable, tableAnchor, log);
        CreateOverlay(scene, tutorialManager, log);
        SwapChallengeSource(scene, tutorialManager, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TargetScenePath);
        AssetDatabase.Refresh();

        RegisterInBuildSettings(log);

        Debug.Log("[TutorialSceneBuilder] Cena gerada:\n  " + string.Join("\n  ", log));

        ValidateOpenScene();
    }

    // ============================================================ menu: validar

    [MenuItem("Tools/Tutorial/Validar cena 5-Tutorial", false, 11)]
    public static void ValidateScene()
    {
        if (SceneManager.GetActiveScene().path != TargetScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) == null)
            {
                Debug.LogError($"[TutorialSceneBuilder] {TargetScenePath} não existe. Rode 'Gerar cena 5-Tutorial' primeiro.");
                return;
            }
            EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        ValidateOpenScene();
    }

    // ===================================================== passos da montagem

    /// <summary>
    /// A cena copiada ainda aponta para <c>Assets/Scenes/3-Audio_Words/NavMesh-Floor.asset</c>.
    /// O dado é válido (a geometria é idêntica), mas deixar assim acopla o tutorial a uma fase de
    /// pesquisa: um rebake na fase 3 mudaria a navegação do tutorial sem ninguém perceber. Aqui o
    /// asset é copiado para a pasta da própria cena e o <c>NavMeshSurface</c> é repontado.
    /// O componente é achado e editado por nome/SerializedObject para não exigir referência de
    /// compilação ao pacote com.unity.ai.navigation.
    /// </summary>
    private static void GiveSceneItsOwnNavMesh(Scene scene, List<string> log)
    {
        Component surface = FindComponentByTypeName(scene, "NavMeshSurface");
        if (surface == null)
        {
            log.Add("AVISO: nenhum NavMeshSurface na cena — navegação pode não funcionar.");
            return;
        }

        var so = new SerializedObject(surface);
        SerializedProperty dataProp = so.FindProperty("m_NavMeshData");
        if (dataProp == null || dataProp.objectReferenceValue == null)
        {
            log.Add("AVISO: NavMeshSurface sem NavMesh assado. Use Window > AI > Navigation para assar.");
            return;
        }

        string sourceAssetPath = AssetDatabase.GetAssetPath(dataProp.objectReferenceValue);
        string fileName = System.IO.Path.GetFileName(sourceAssetPath);

        if (!AssetDatabase.IsValidFolder(TargetSceneFolder))
        {
            AssetDatabase.CreateFolder("Assets/Scenes", "5-Tutorial");
        }

        string copyPath = $"{TargetSceneFolder}/{fileName}";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(copyPath) != null)
        {
            AssetDatabase.DeleteAsset(copyPath);
        }

        if (!AssetDatabase.CopyAsset(sourceAssetPath, copyPath))
        {
            log.Add($"AVISO: não copiei o NavMesh; a cena segue usando {sourceAssetPath}.");
            return;
        }

        AssetDatabase.Refresh();

        var copied = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(copyPath);
        dataProp.objectReferenceValue = copied;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.Add($"NavMesh próprio: {copyPath} (NÃO reassar — o rebake descartaria a malha assada da fase 3).");
    }

    /// <summary>
    /// Esteira parada e sem sorteio de letras, só por configuração de cena.
    ///
    /// Duas coisas moveriam a letra da rodada, e as duas são desligadas:
    ///   - <c>TreadmillBehavior.OnCollisionStay</c> empurra qualquer objeto com tag "LetterBox"
    ///     ao longo da esteira → componente desativado nas instâncias desta cena;
    ///   - <c>LetterBoxSpawner.Update</c> faz spawn aleatório a cada 4,5–8 s → componente
    ///     desativado (os fornos continuam visíveis como cenário, só ficam inertes).
    /// Além disso cada caixa do tutorial fica cinemática e sem gravidade (ver
    /// <see cref="PlaceLetterBoxes"/>), então nem a física nem um empurrão acidental a tiram do
    /// lugar. Se a letra certa pudesse sumir enquanto a criança pensa, o tutorial quebraria.
    /// </summary>
    private static void StopTheBelt(Scene scene, List<string> log)
    {
        int treadmills = 0;
        foreach (TreadmillBehavior belt in FindAll<TreadmillBehavior>())
        {
            belt.enabled = false;
            treadmills++;
        }

        int spawners = 0;
        foreach (LetterBoxSpawner spawner in FindAll<LetterBoxSpawner>())
        {
            spawner.enabled = false;
            spawners++;
        }

        log.Add($"Esteira parada: {treadmills} TreadmillBehavior e {spawners} LetterBoxSpawner desativados.");
    }

    /// <summary>
    /// No tutorial não existe caminho para errar: nada de lixeira, incinerador ou objeto de dica.
    /// A desativação é feita por semântica (tipo do <c>InteractableCL</c> e presença do
    /// <c>LetterBoxIncinerator</c>) e não só pelos grupos nomeados, para não deixar passar um
    /// objeto solto fora do grupo.
    /// </summary>
    private static void RemoveWaysToGoWrong(Scene scene, List<string> log)
    {
        var disabled = new List<string>();

        foreach (InteractableCL interactable in FindAll<InteractableCL>())
        {
            if (interactable.type == InteractableType.Trash || interactable.type == InteractableType.Hint)
            {
                if (interactable.gameObject.activeSelf)
                {
                    interactable.gameObject.SetActive(false);
                    disabled.Add($"{interactable.type}:{interactable.name}");
                }
            }
        }

        foreach (LetterBoxIncinerator incinerator in FindAll<LetterBoxIncinerator>())
        {
            if (incinerator.gameObject.activeSelf)
            {
                incinerator.gameObject.SetActive(false);
                disabled.Add($"Incinerator:{incinerator.name}");
            }
        }

        // Os grupos inteiros também saem: deixa a hierarquia honesta e evita que um objeto sem
        // InteractableCL (só cenário da lixeira) continue na tela.
        foreach (string path in new[] { TrashesPath, IncineratorsPath })
        {
            GameObject group = FindInScene(scene, path);
            if (group != null && group.activeSelf)
            {
                group.SetActive(false);
                disabled.Add($"Grupo:{path}");
            }
        }

        log.Add($"Sem caminho para errar: {disabled.Count} objetos desativados ({string.Join(", ", disabled)}).");
    }

    private static Transform CreateGroup(Scene scene, string name, List<string> log)
    {
        GameObject existing = FindInScene(scene, name);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        var group = new GameObject(name);
        if (group.scene != scene) SceneManager.MoveGameObjectToScene(group, scene);
        group.transform.position = Vector3.zero;
        log.Add($"Grupo '{name}' criado.");
        return group.transform;
    }

    /// <summary>
    /// Três caixas estáticas sobre a esteira, com A, E e I.
    ///
    /// Posicionamento, eixo por eixo:
    ///   X — centradas no collider da esteira escolhida (a de maior X do TreadmillGroup1, que é a
    ///       mais perto do ponto onde o robô nasce), espaçadas por <see cref="LetterBoxSpacing"/>.
    ///   Y — exatamente sobre o topo do collider da esteira (topo + meia altura da caixa). É onde
    ///       uma caixa sorteada iria parar depois de cair do forno, então a altura de interação é
    ///       a mesma das fases de pesquisa.
    ///   Z — encostadas na borda da esteira voltada para o corredor
    ///       (<see cref="AnchorApproachDirection"/>), o que encurta a distância até a âncora e
    ///       deixa a letra bem visível de onde o robô para.
    /// A rotação copia a que o <c>LetterBoxSpawner</c> aplicaria nesta esteira — o builder lê o
    /// <c>isALeftSpawner</c> do spawner mais próximo em vez de chutar o ângulo, para a letra ficar
    /// virada para o mesmo lado das fases de pesquisa.
    /// </summary>
    private static List<GameObject> PlaceLetterBoxes(Scene scene, Transform parent, List<string> log)
    {
        var boxes = new List<GameObject>();

        GameObject beltGroup = FindInScene(scene, BeltGroupPath);
        if (beltGroup == null)
        {
            Debug.LogError($"[TutorialSceneBuilder] Esteira não encontrada: {BeltGroupPath}");
            return boxes;
        }

        Collider belt = PickBeltSegment(beltGroup);
        if (belt == null)
        {
            Debug.LogError($"[TutorialSceneBuilder] {BeltGroupPath} não tem collider de esteira.");
            return boxes;
        }

        Bounds beltBounds = WorldBounds(belt);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LetterBoxPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[TutorialSceneBuilder] Prefab da caixa não encontrado: {LetterBoxPrefabPath}");
            return boxes;
        }

        float spawnerSign = LetterRotationSignFor(beltBounds.center);

        for (int i = 0; i < RoundLetters.Length; i++)
        {
            string letter = RoundLetters[i];

            var box = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            box.name = $"LetterBox_{letter}";
            box.transform.SetParent(parent, true);

            // Mesma rotação que o spawner desta esteira daria à caixa.
            box.transform.rotation = Quaternion.Euler(0f, -spawnerSign * 90f, 0f);

            Bounds boxBounds = WorldBounds(box.GetComponent<BoxCollider>());
            float offsetFromCenter = (i - (RoundLetters.Length - 1) * 0.5f) * LetterBoxSpacing;

            Vector3 position = new Vector3(
                beltBounds.center.x + offsetFromCenter,
                beltBounds.max.y + boxBounds.extents.y,
                beltBounds.max.z - boxBounds.extents.z);

            // A borda "de chegada" depende do lado pelo qual o robô se aproxima.
            if (AnchorApproachDirection.z < 0f) position.z = beltBounds.min.z + boxBounds.extents.z;

            box.transform.position = position;

            // Cinemática e sem gravidade: a caixa não cai, não rola e não é empurrada. É a
            // garantia dura de que a letra da rodada está sempre no mesmo lugar.
            Rigidbody body = box.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            InteractableCL interactable = box.GetComponent<InteractableCL>();
            if (interactable == null)
            {
                Debug.LogError($"[TutorialSceneBuilder] {box.name} sem InteractableCL.");
            }
            else
            {
                SetSerializedEnum(interactable, "type", (int)InteractableType.LetterBox);
                SetLetterText(interactable, letter, box.name);

                // O prompt "INTERAGIR" nasce virado para o eixo errado quando a caixa é rodada;
                // é a mesma correção que o LetterBoxSpawner faz ao soltar uma letra.
                if (interactable.InteractionPromptText != null)
                {
                    interactable.InteractionPromptText.transform.localRotation =
                        Quaternion.Euler(0f, spawnerSign * 90f, 0f);
                }
            }

            boxes.Add(box);

            bool overBelt = position.x - boxBounds.extents.x >= beltBounds.min.x - 0.01f &&
                            position.x + boxBounds.extents.x <= beltBounds.max.x + 0.01f;
            log.Add($"Caixa '{letter}' em {Fmt(position)}" + (overBelt ? "" : "  AVISO: passa da ponta da esteira"));
        }

        log.Add($"Esteira usada: '{belt.transform.parent?.name}/{belt.name}' topo y={beltBounds.max.y:F2}, " +
                $"x de {beltBounds.min.x:F2} a {beltBounds.max.x:F2}.");

        return boxes;
    }

    /// <summary>
    /// Entre os segmentos de esteira do grupo, o de maior X: é o que fica mais perto do centro da
    /// sala, onde o robô nasce, e portanto o que exige o caminho mais curto e mais legível.
    /// </summary>
    private static Collider PickBeltSegment(GameObject beltGroup)
    {
        return beltGroup.GetComponentsInChildren<Collider>(true)
            .Where(c => c is BoxCollider)
            .OrderByDescending(c => c.transform.position.x)
            .FirstOrDefault();
    }

    /// <summary>
    /// <c>letterRotationFixingConst</c> do <c>LetterBoxSpawner</c> mais próximo desta esteira:
    /// -1 para spawner da esquerda, +1 para o da direita. É o que decide para que lado a letra
    /// fica virada.
    /// </summary>
    private static float LetterRotationSignFor(Vector3 beltCenter)
    {
        LetterBoxSpawner nearest = FindAll<LetterBoxSpawner>()
            .OrderBy(s => Vector3.Distance(s.transform.position, beltCenter))
            .FirstOrDefault();

        if (nearest == null) return -1f;

        SerializedProperty isLeft = new SerializedObject(nearest).FindProperty("isALeftSpawner");
        return (isLeft != null && isLeft.boolValue) ? -1f : 1f;
    }

    private static List<Transform> PlaceBeltAnchors(Scene scene, Transform parent, List<GameObject> boxes, List<string> log)
    {
        var anchors = new List<Transform>();

        GameObject beltGroup = FindInScene(scene, BeltGroupPath);
        Collider belt = beltGroup != null ? PickBeltSegment(beltGroup) : null;
        if (belt == null) return anchors;

        Bounds beltBounds = WorldBounds(belt);
        float floorY = FloorHeight(scene);

        foreach (GameObject box in boxes)
        {
            // X da caixa, Z logo depois da borda da esteira (a esteira é área "Not Walkable": o
            // robô não consegue parar sobre ela), Y no piso para o NavMeshAgent conseguir mapear
            // o destino — ver ReportAnchor sobre a regra de 1.0 do contrato.
            float z = AnchorApproachDirection.z >= 0f
                ? beltBounds.max.z + AnchorClearance
                : beltBounds.min.z - AnchorClearance;

            Transform anchor = CreateAnchor(scene, parent, $"Anchor_Belt_{LetterOf(box)}",
                new Vector3(box.transform.position.x, floorY, z));

            anchors.Add(anchor);
            ReportAnchor(anchor, box, log);
        }

        return anchors;
    }

    /// <summary>
    /// A mesa de destino da rodada. Com palavra de 1 letra o <c>DeliverTablesManager</c> usa a
    /// lista ímpar e centraliza: <c>startIndex = (13 - 1) / 2 = 6</c>, ou seja a 7ª mesa. O índice
    /// é calculado a partir da contagem real de filhos, então continua certo se a lista mudar de
    /// tamanho. As três rodadas usam a mesma mesa — é a única que o jogo liga.
    /// </summary>
    private static GameObject FindDeliverTable(Scene scene, List<string> log)
    {
        GameObject parent = FindInScene(scene, OddTablesPath);
        if (parent == null)
        {
            Debug.LogError($"[TutorialSceneBuilder] Mesas não encontradas: {OddTablesPath}");
            return null;
        }

        int count = parent.transform.childCount;
        if (count == 0)
        {
            Debug.LogError($"[TutorialSceneBuilder] {OddTablesPath} está vazio.");
            return null;
        }

        int index = (count - 1) / 2; // mesma centralização do DeliverTablesManager.SpawnTables()
        GameObject table = parent.transform.GetChild(index).gameObject;

        log.Add($"Mesa de destino: '{table.name}' (filho {index} de {count} em {OddTablesPath}; " +
                "fica desativada na cena e é ligada em runtime pelo DeliverTablesManager).");

        return table;
    }

    private static Transform PlaceTableAnchor(Scene scene, Transform parent, GameObject table, List<string> log)
    {
        if (table == null) return null;

        Bounds tableBounds = WorldBounds(FirstBoxCollider(table));
        float floorY = FloorHeight(scene);

        float z = AnchorApproachDirection.z >= 0f
            ? tableBounds.max.z + AnchorClearance
            : tableBounds.min.z - AnchorClearance;

        Transform anchor = CreateAnchor(scene, parent, "Anchor_Table",
            new Vector3(tableBounds.center.x, floorY, z));

        ReportAnchor(anchor, table, log);
        return anchor;
    }

    private static Transform CreateAnchor(Scene scene, Transform parent, string name, Vector3 position)
    {
        var anchor = new GameObject(name);
        if (anchor.scene != scene) SceneManager.MoveGameObjectToScene(anchor, scene);
        anchor.transform.SetParent(parent, true);
        anchor.transform.position = position;
        return anchor.transform;
    }

    /// <summary>
    /// Mede a âncora contra as duas regras e loga as duas, porque elas discordam nesta cena.
    ///
    /// REGRA DO CONTRATO: âncora a ≤ 1.0 do centro do collider do alvo, partindo de
    /// <c>interactionRadius = 1.5</c>. Em 5-Tutorial ela é impossível para letras na esteira: o
    /// topo do collider da esteira está a ~6,7 de altura e o centro da caixa a ~8,4, enquanto a
    /// âncora tem que ficar no piso (y = 0) para o <c>NavMeshAgent</c> — cujo <c>height</c> é 1 —
    /// conseguir mapear o destino. Âncora flutuando na altura da caixa satisfaria o número e
    /// deixaria o <c>SetDestination</c> sem caminho: o robô não sairia do lugar.
    ///
    /// REGRA QUE DECIDE DE VERDADE: a distância da âncora ao centro do collider tem que caber no
    /// <c>interactionRadius</c> serializado em Core.unity, que é <see cref="PlayerInteractionRadius"/>
    /// = 10, não 1.5. É essa que o builder cobra como erro, e ela reproduz exatamente a geometria
    /// de interação das fases de pesquisa (lá o robô também pega letras a ~8,8 de distância).
    /// </summary>
    private static void ReportAnchor(Transform anchor, GameObject target, List<string> log)
    {
        Bounds targetBounds = WorldBounds(FirstBoxCollider(target));
        Vector3 center = targetBounds.center;

        // De onde o OverlapSphere sai de fato: o pivô do robô, 0.16 acima do piso.
        Vector3 playerPivot = anchor.position + Vector3.up * PlayerPivotHeight;
        float distance = Vector3.Distance(playerPivot, center);
        float horizontal = Vector2.Distance(new Vector2(playerPivot.x, playerPivot.z), new Vector2(center.x, center.z));

        string verdict;
        if (distance > PlayerInteractionRadius)
        {
            verdict = $"ERRO: {distance:F2} > interactionRadius {PlayerInteractionRadius} — o Space não acharia o alvo";
            Debug.LogError($"[TutorialSceneBuilder] Âncora '{anchor.name}' longe de '{target.name}': {verdict}");
        }
        else if (distance > ContractMaxAnchorDistance)
        {
            verdict = $"ok por {PlayerInteractionRadius - distance:F2} de margem " +
                      $"(acima do 1.0 do contrato — esperado: altura da esteira, ver comentário em ReportAnchor)";
        }
        else
        {
            verdict = "ok, dentro do 1.0 do contrato";
        }

        log.Add($"Âncora '{anchor.name}' -> '{target.name}': 3D {distance:F2} (horizontal {horizontal:F2}, " +
                $"vertical {Mathf.Abs(center.y - playerPivot.y):F2}) — {verdict}");
    }

    /// <summary>
    /// <c>Tutorial Manager</c>: <c>TutorialDirector</c> + <c>TutorialChallengeSource</c> + um
    /// <c>AudioSource</c> 2D para o áudio da letra (o <c>AudioWordChallengeSource</c> da fase 3
    /// usava um AudioSource de cena que sai junto com ele). As três <c>TutorialRound</c> são
    /// preenchidas aqui; os overrides de tecla ficam em <c>KeyCode.None</c> para o director
    /// derivar a tecla da geometria, como manda o contrato.
    /// </summary>
    private static GameObject CreateTutorialManager(Scene scene, List<GameObject> boxes, List<Transform> beltAnchors,
        GameObject table, Transform tableAnchor, List<string> log)
    {
        GameObject existing = FindInScene(scene, TutorialManagerName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        var manager = new GameObject(TutorialManagerName);
        if (manager.scene != scene) SceneManager.MoveGameObjectToScene(manager, scene);
        manager.transform.position = Vector3.zero;

        AudioSource audio = manager.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f; // 2D: a letra tem que soar igual onde o robô estiver

        Component director = AddComponentByName(manager, "TutorialDirector", log);
        Component challengeSource = AddComponentByName(manager, "TutorialChallengeSource", log);

        foreach (Component component in new[] { director, challengeSource })
        {
            if (component == null) continue;
            TrySetObjectField(component, new[] { "audioSource", "letterAudioSource" }, audio);
        }

        if (director != null && challengeSource != null)
        {
            TrySetObjectField(director, new[] { "challengeSource", "tutorialChallengeSource", "source" }, challengeSource);
            TrySetObjectField(challengeSource, new[] { "director", "tutorialDirector" }, director);
        }

        FillRounds(director, challengeSource, boxes, beltAnchors, table, tableAnchor, log);

        return manager;
    }

    private static void FillRounds(Component director, Component challengeSource, List<GameObject> boxes,
        List<Transform> beltAnchors, GameObject table, Transform tableAnchor, List<string> log)
    {
        // O contrato define TutorialRound mas não batiza o campo que guarda a lista. Tenta os
        // nomes plausíveis, no director e depois na challenge source.
        string[] candidates = { "rounds", "tutorialRounds", "roundsList", "challengeRounds" };

        foreach (Component host in new[] { director, challengeSource })
        {
            if (host == null) continue;

            var so = new SerializedObject(host);
            foreach (string name in candidates)
            {
                SerializedProperty prop = so.FindProperty(name);
                if (prop == null || !prop.isArray) continue;

                prop.arraySize = RoundLetters.Length;

                for (int i = 0; i < RoundLetters.Length; i++)
                {
                    SerializedProperty round = prop.GetArrayElementAtIndex(i);
                    SetRelativeString(round, "letter", RoundLetters[i]);
                    SetRelativeObject(round, "letterAudio", LoadLetterAudio(RoundLetters[i], log));
                    SetRelativeObject(round, "letterBox", i < boxes.Count ? boxes[i] : null);
                    SetRelativeObject(round, "beltAnchor", i < beltAnchors.Count ? beltAnchors[i] : null);
                    SetRelativeObject(round, "table", table);
                    SetRelativeObject(round, "tableAnchor", tableAnchor);
                    SetRelativeEnum(round, "keyToBeltOverride", (int)KeyCode.None);
                    SetRelativeEnum(round, "keyToTableOverride", (int)KeyCode.None);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                log.Add($"3 TutorialRound preenchidas em {host.GetType().Name}.{name}.");
                return;
            }
        }

        log.Add("PASSO MANUAL: nenhum campo de rodadas encontrado (procurei por " +
                string.Join("/", candidates) + "). No Inspector do 'Tutorial Manager', preencha 3 rodadas: " +
                "letter A/E/I; letterAudio = Prefabs/SFX/WordSounds/Letters/{a,e,i}.mp3; " +
                "letterBox = Tutorial Letters/LetterBox_{A,E,I}; beltAnchor = Anchors/Anchor_Belt_{A,E,I}; " +
                "table = a mesa logada acima; tableAnchor = Anchors/Anchor_Table; " +
                "keyToBeltOverride e keyToTableOverride em None.");
    }

    private static void CreateOverlay(Scene scene, GameObject tutorialManager, List<string> log)
    {
        GameObject existing = FindInScene(scene, OverlayName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        var overlay = new GameObject(OverlayName);
        if (overlay.scene != scene) SceneManager.MoveGameObjectToScene(overlay, scene);
        overlay.transform.position = Vector3.zero;

        // A UI do overlay se monta sozinha em código (sessão da camada visual): aqui só existe o
        // GameObject que hospeda o componente.
        Component view = AddComponentByName(overlay, "TutorialOverlayView", log);

        if (view != null && tutorialManager != null)
        {
            Component director = FindComponentOnObjectByName(tutorialManager, "TutorialDirector");
            if (director != null)
            {
                TrySetObjectField(view, new[] { "director", "tutorialDirector" }, director);
            }
        }
    }

    /// <summary>
    /// Troca a fonte do desafio: o <c>AudioWordChallengeSource</c> da fase de escuta sai e o
    /// <c>ChasingLettersGameManager.challengeSource</c> passa a apontar para o
    /// <c>TutorialChallengeSource</c> do Tutorial Manager — é de lá que sai também o
    /// <c>game_mode: "tutorial"</c> da telemetria.
    ///
    /// O <c>Interaction_Button</c> da fase 3 é desativado junto: o UnityEvent dele chamava
    /// <c>AudioWordChallengeSource.PlayCurrentWord</c>, que acabou de deixar de existir na cena.
    /// A repetição do áudio no tutorial é do director (<c>ReplayLetterAudio</c>).
    /// </summary>
    private static void SwapChallengeSource(Scene scene, GameObject tutorialManager, List<string> log)
    {
        GameObject gameManager = FindInScene(scene, GameManagerPath);
        if (gameManager == null)
        {
            Debug.LogError($"[TutorialSceneBuilder] Game Manager não encontrado: {GameManagerPath}");
            return;
        }

        foreach (AudioWordChallengeSource old in gameManager.GetComponents<AudioWordChallengeSource>())
        {
            UnityEngine.Object.DestroyImmediate(old);
            log.Add("AudioWordChallengeSource removido do Game Manager.");
        }

        GameObject interactionButton = FindInScene(scene, InteractionButtonPath);
        if (interactionButton != null && interactionButton.activeSelf)
        {
            interactionButton.SetActive(false);
            log.Add("Interaction_Button desativado (o UnityEvent dele apontava para o componente removido).");
        }

        Component tutorialSource = tutorialManager != null
            ? FindComponentOnObjectByName(tutorialManager, "TutorialChallengeSource")
            : null;

        var manager = gameManager.GetComponent<ChasingLettersGameManager>();
        if (manager == null)
        {
            Debug.LogError("[TutorialSceneBuilder] ChasingLettersGameManager ausente no Game Manager.");
            return;
        }

        if (tutorialSource == null)
        {
            log.Add($"PASSO MANUAL: arraste o TutorialChallengeSource do '{TutorialManagerName}' para o campo " +
                    "'Challenge Source' do ChasingLettersGameManager (Game Managers/Game Manager). " +
                    "Sem isso a cena sobe sem palavra e o GameCycleManager loga erro.");
            return;
        }

        var so = new SerializedObject(manager);
        SerializedProperty prop = so.FindProperty("challengeSource");
        if (prop == null)
        {
            log.Add("PASSO MANUAL: campo 'challengeSource' não encontrado no ChasingLettersGameManager.");
            return;
        }

        prop.objectReferenceValue = tutorialSource;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("ChasingLettersGameManager.challengeSource -> TutorialChallengeSource.");
    }

    /// <summary>
    /// Sem a cena no Build Settings, <c>SceneManager.LoadScene("5-Tutorial")</c> falha em runtime
    /// e o menu leva a uma tela vazia. Idempotente.
    /// </summary>
    private static void RegisterInBuildSettings(List<string> log)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        int index = scenes.FindIndex(s => s.path == TargetScenePath);

        if (index >= 0)
        {
            if (!scenes[index].enabled)
            {
                scenes[index] = new EditorBuildSettingsScene(TargetScenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                log.Add("Build Settings: cena reabilitada.");
            }
            else
            {
                log.Add("Build Settings: cena já registrada.");
            }
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(TargetScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        log.Add($"Build Settings: '{TargetScenePath}' adicionada (posição {scenes.Count - 1}).");
    }

    // ================================================================ validação

    /// <summary>
    /// Confere a cena aberta contra tudo que o tutorial precisa, e loga um relatório. É o que
    /// deve rodar depois de abrir 5-Tutorial, antes de jogar a fase pelo menu.
    /// </summary>
    private static void ValidateOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        var problems = new List<string>();
        var report = new List<string>();

        // --- esteira parada
        int activeBelts = FindAll<TreadmillBehavior>().Count(b => b.enabled && b.gameObject.activeInHierarchy);
        if (activeBelts > 0) problems.Add($"{activeBelts} TreadmillBehavior ainda ativos — a letra andaria.");

        int activeSpawners = FindAll<LetterBoxSpawner>().Count(s => s.enabled && s.gameObject.activeInHierarchy);
        if (activeSpawners > 0) problems.Add($"{activeSpawners} LetterBoxSpawner ainda ativos — haveria spawn aleatório.");

        // --- nada de errar
        foreach (InteractableCL interactable in FindAll<InteractableCL>())
        {
            if ((interactable.type == InteractableType.Trash || interactable.type == InteractableType.Hint) &&
                interactable.gameObject.activeInHierarchy)
            {
                problems.Add($"'{interactable.name}' ({interactable.type}) está ativo — caminho para errar.");
            }
        }

        // --- as três letras
        Transform letters = FindInScene(scene, LettersGroupName)?.transform;
        if (letters == null || letters.childCount != RoundLetters.Length)
        {
            problems.Add($"'{LettersGroupName}' deveria ter {RoundLetters.Length} caixas.");
        }

        Transform anchors = FindInScene(scene, AnchorsGroupName)?.transform;
        if (anchors == null)
        {
            problems.Add($"Grupo '{AnchorsGroupName}' ausente.");
        }

        // --- âncoras: distância e, principalmente, quem é o interactable mais próximo
        if (letters != null && anchors != null)
        {
            for (int i = 0; i < letters.childCount; i++)
            {
                GameObject box = letters.GetChild(i).gameObject;
                Transform anchor = anchors.Find($"Anchor_Belt_{LetterOf(box)}");
                if (anchor == null)
                {
                    problems.Add($"Âncora de '{box.name}' não encontrada.");
                    continue;
                }

                ReportAnchor(anchor, box, report);
                CheckClosestInteractable(anchor, box, problems, report);
            }

            GameObject table = FindDeliverTable(scene, report);
            Transform tableAnchor = anchors.Find("Anchor_Table");
            if (table != null && tableAnchor != null) ReportAnchor(tableAnchor, table, report);
            else problems.Add("Âncora da mesa ('Anchor_Table') não encontrada.");
        }

        // --- scripts do tutorial
        foreach (string typeName in new[] { "TutorialDirector", "TutorialChallengeSource", "TutorialOverlayView" })
        {
            if (FindTypeByName(typeName) == null)
            {
                report.Add($"PENDENTE (integração): tipo '{typeName}' ainda não existe no projeto. " +
                           "Rode o builder outra vez depois do merge das outras sessões para ele ligar as referências.");
            }
            else if (FindComponentByTypeName(scene, typeName) == null)
            {
                problems.Add($"'{typeName}' existe no projeto mas não está em nenhum objeto da cena. Rode o builder outra vez.");
            }
        }

        // --- build settings
        if (!EditorBuildSettings.scenes.Any(s => s.path == TargetScenePath && s.enabled))
        {
            problems.Add($"'{TargetScenePath}' não está habilitada no Build Settings — LoadScene falharia em runtime.");
        }

        Debug.Log($"[TutorialSceneBuilder] Verificação de '{scene.name}':\n  " + string.Join("\n  ", report));

        if (problems.Count == 0)
        {
            Debug.Log("[TutorialSceneBuilder] Verificação OK: nenhum problema encontrado.");
        }
        else
        {
            Debug.LogError($"[TutorialSceneBuilder] {problems.Count} problema(s):\n  - " + string.Join("\n  - ", problems));
        }
    }

    /// <summary>
    /// Espelha <c>PlayerControllerCL.GetClosestInteractible()</c>: entre os interactables cujo
    /// collider cabe na esfera de raio <see cref="PlayerInteractionRadius"/> em volta do robô, o
    /// escolhido é o de <c>transform.position</c> mais perto. Se não for a letra da rodada, o
    /// passo pegaria o objeto errado.
    /// </summary>
    private static void CheckClosestInteractable(Transform anchor, GameObject expected, List<string> problems, List<string> report)
    {
        Vector3 playerPivot = anchor.position + Vector3.up * PlayerPivotHeight;

        GameObject closest = null;
        float closestDistance = float.MaxValue;
        var inRange = new List<string>();

        foreach (InteractableCL interactable in FindAll<InteractableCL>())
        {
            if (!interactable.gameObject.activeInHierarchy) continue;

            BoxCollider collider = FirstBoxCollider(interactable.gameObject);
            if (collider == null) continue;

            Bounds bounds = WorldBounds(collider);
            if (bounds.SqrDistance(playerPivot) > PlayerInteractionRadius * PlayerInteractionRadius) continue;

            float distance = Vector3.Distance(playerPivot, interactable.transform.position);
            inRange.Add($"{interactable.name}@{distance:F2}");

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = interactable.gameObject;
            }
        }

        report.Add($"Em '{anchor.name}' o alcance pega: {(inRange.Count == 0 ? "nada" : string.Join(", ", inRange))}.");

        if (closest != expected)
        {
            problems.Add($"Em '{anchor.name}' o interactable mais próximo é " +
                         $"'{(closest != null ? closest.name : "nenhum")}', não '{expected.name}'.");
        }
    }

    // =================================================================== helpers

    private static string LetterOf(GameObject box)
    {
        int underscore = box.name.LastIndexOf('_');
        return underscore >= 0 ? box.name.Substring(underscore + 1) : box.name;
    }

    private static AudioClip LoadLetterAudio(string letter, List<string> log)
    {
        string path = $"{LetterAudioFolder}/{letter.ToLowerInvariant()}.mp3";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) log.Add($"AVISO: áudio da letra não encontrado: {path}");
        return clip;
    }

    /// <summary>Altura do piso onde o robô anda, medida no collider do Floor.</summary>
    private static float FloorHeight(Scene scene)
    {
        GameObject floor = FindInScene(scene, FloorPath);
        if (floor == null)
        {
            Debug.LogError($"[TutorialSceneBuilder] '{FloorPath}' não encontrado; usando y = 0 para as âncoras.");
            return 0f;
        }

        Collider collider = floor.GetComponent<Collider>();
        if (collider != null && collider.Raycast(new Ray(floor.transform.position + Vector3.up * 500f, Vector3.down),
                out RaycastHit hit, 1000f))
        {
            return hit.point.y;
        }

        // O piso é um mesh Plane: a superfície está no plano Y do próprio transform.
        return floor.transform.position.y;
    }

    /// <summary>
    /// AABB mundial de um <see cref="BoxCollider"/> calculada pelos 8 cantos. Não usa
    /// <c>Collider.bounds</c> porque as mesas de entrega ficam *desativadas* na cena (o
    /// <c>DeliverTablesManager</c> as liga em runtime) e bounds de collider inativo não é
    /// confiável. Também aguenta as escalas aninhadas malucas dos prefabs deste projeto
    /// (esteira 1000 × 0.001, mesa × 500).
    /// </summary>
    private static Bounds WorldBounds(BoxCollider collider)
    {
        if (collider == null) return new Bounds();

        Vector3 extents = collider.size * 0.5f;
        var bounds = new Bounds(collider.transform.TransformPoint(collider.center), Vector3.zero);

        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);

            bounds.Encapsulate(collider.transform.TransformPoint(collider.center + corner));
        }

        return bounds;
    }

    private static Bounds WorldBounds(Collider collider)
    {
        if (collider is BoxCollider box) return WorldBounds(box);
        if (collider == null) return new Bounds();

        Debug.LogWarning($"[TutorialSceneBuilder] '{collider.name}' não é BoxCollider; usando bounds do collider.");
        return collider.bounds;
    }

    private static BoxCollider FirstBoxCollider(GameObject go)
    {
        if (go == null) return null;

        BoxCollider own = go.GetComponent<BoxCollider>();
        if (own != null) return own;

        return go.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault();
    }

    private static GameObject FindInScene(Scene scene, string path)
    {
        string[] parts = path.Split('/');

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;

            Transform current = root.transform;
            for (int i = 1; i < parts.Length && current != null; i++)
            {
                current = current.Find(parts[i]);
            }

            if (current != null) return current.gameObject;
        }

        return null;
    }

    private static IEnumerable<T> FindAll<T>() where T : Component
    {
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Procura um tipo pelo nome em todos os assemblies carregados. É assim que o builder
    /// referencia os scripts do tutorial sem depender deles em tempo de compilação.
    /// </summary>
    private static Type FindTypeByName(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        // Caminho rápido: nome sem namespace, que é o caso dos scripts deste projeto.
        foreach (Assembly assembly in assemblies)
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null) return type;
        }

        // Caminho lento: o script pode ter ganhado um namespace. GetTypes() pode explodir em
        // assembly com dependência faltando, então cada um é tentado isolado.
        foreach (Assembly assembly in assemblies)
        {
            try
            {
                Type type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }
            catch (ReflectionTypeLoadException)
            {
                // assembly parcialmente carregado: ignora e segue
            }
        }

        return null;
    }

    private static Component FindComponentByTypeName(Scene scene, string typeName)
    {
        Type type = FindTypeByName(typeName);
        if (type == null) return null;

        return UnityEngine.Object
            .FindObjectsByType(type, FindObjectsInactive.Include)
            .OfType<Component>()
            .FirstOrDefault();
    }

    private static Component FindComponentOnObjectByName(GameObject go, string typeName)
    {
        Type type = FindTypeByName(typeName);
        return type == null ? null : go.GetComponent(type);
    }

    private static Component AddComponentByName(GameObject go, string typeName, List<string> log)
    {
        Type type = FindTypeByName(typeName);

        if (type == null)
        {
            log.Add($"PASSO MANUAL: '{typeName}' ainda não existe no projeto. O GameObject '{go.name}' foi criado " +
                    "vazio; depois do merge das outras sessões, rode 'Tools/Tutorial/Gerar cena 5-Tutorial' de novo " +
                    $"(ou arraste '{typeName}' para '{go.name}' no Inspector).");
            return null;
        }

        Component component = go.AddComponent(type);
        log.Add($"'{typeName}' adicionado em '{go.name}'.");
        return component;
    }

    private static void TrySetObjectField(Component target, string[] fieldNames, UnityEngine.Object value)
    {
        if (target == null || value == null) return;

        var so = new SerializedObject(target);
        foreach (string name in fieldNames)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) continue;

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return;
        }
    }

    private static void SetSerializedEnum(Component target, string fieldName, int value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null) return;

        prop.enumValueIndex = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Escreve o texto da letra pelo <see cref="SerializedObject"/> do TMP_Text em vez de
    /// <c>.text =</c>: fora do play mode só a propriedade serializada (<c>m_text</c>) sobrevive ao
    /// salvamento da cena.
    /// </summary>
    private static void SetLetterText(InteractableCL interactable, string letter, string boxName)
    {
        TMP_Text text = interactable.LetterText;
        if (text == null)
        {
            Debug.LogError($"[TutorialSceneBuilder] '{boxName}': InteractableCL sem LetterText.");
            return;
        }

        var so = new SerializedObject(text);
        SerializedProperty prop = so.FindProperty("m_text");
        if (prop != null)
        {
            prop.stringValue = letter;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        text.text = letter;
        text.ForceMeshUpdate();
        EditorUtility.SetDirty(text);
    }

    private static void SetRelativeString(SerializedProperty round, string field, string value)
    {
        SerializedProperty prop = round.FindPropertyRelative(field);
        if (prop != null) prop.stringValue = value;
    }

    private static void SetRelativeObject(SerializedProperty round, string field, UnityEngine.Object value)
    {
        SerializedProperty prop = round.FindPropertyRelative(field);
        if (prop != null) prop.objectReferenceValue = value;
    }

    private static void SetRelativeEnum(SerializedProperty round, string field, int value)
    {
        SerializedProperty prop = round.FindPropertyRelative(field);
        if (prop != null) prop.intValue = value;
    }

    private static string Fmt(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
}
