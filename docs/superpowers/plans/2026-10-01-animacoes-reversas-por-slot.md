# Animacoes reversas por slot no inspetor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar a cada animacao sincronizada do heroi um booleano individual que permita reproduzir o clipe escolhido de tras para frente, preservando o Animator Controller compartilhado.

**Architecture:** O editor gera e mantem em cache um `.anim` temporalmente invertido para o clipe efetivo de cada slot marcado. O runtime continua usando um unico `AnimatorOverrideController`, escolhendo entre clipe reverso, substituto ou original. O mecanismo especial `AparoReverso` deixa de ser disparado, e a cena atual migra sua escolha legada para o novo slot de `Aparo`.

**Tech Stack:** Unity 6.6 (`6000.6.1f1`), C#, UnityEditor IMGUI, `AnimationUtility`, `AssetDatabase`, `AnimatorOverrideController`, Unity Pipeline CLI.

## Global Constraints

- Nao usar TDD; executar somente testes basicos de funcionamento depois da implementacao, conforme solicitado.
- Nao modificar clipes FBX de origem nem o controller compartilhado para inverter animacoes.
- O booleano `Tocar ao contrario` deve ser individual por slot e por instancia do heroi.
- Preservar as alteracoes nao relacionadas que ja existem no worktree.
- Alterar e salvar cenas/assets somente pelo Unity Editor conectado via Pipeline/MCP.
- Se a Pipeline continuar indisponivel, recuperar a conexao antes de editar `Combate_Sandbox.unity` ou gerar `.anim`.

---

### Task 1: Resolucao runtime do clipe efetivo

**Files:**
- Modify: `Assets/_Sardael/Scripts/Combate/TrocadorDeAnimacoes.cs:8-18,53-93`

**Interfaces:**
- Produces: `SubstituicaoDeAnimacao.TocarAoContrario : bool`
- Produces: `SubstituicaoDeAnimacao.ClipeReversoGerado : AnimationClip`
- Produces: `SubstituicaoDeAnimacao.ClipeEfetivo : AnimationClip`
- Consumes later: campos serializados `tocarAoContrario`, `clipeReversoGerado`, `assinaturaDaFonteReversa`, `erroDoClipeReverso`

- [ ] **Step 1: Adicionar os dados serializados de cada slot**

Inserir em `SubstituicaoDeAnimacao`:

```csharp
[SerializeField] bool tocarAoContrario;
[SerializeField] AnimationClip clipeReversoGerado;
[SerializeField, HideInInspector] string assinaturaDaFonteReversa;
[SerializeField, HideInInspector] string erroDoClipeReverso;

public bool TocarAoContrario => tocarAoContrario;
public AnimationClip ClipeReversoGerado => clipeReversoGerado;
public AnimationClip ClipeEfetivo => tocarAoContrario && clipeReversoGerado != null
    ? clipeReversoGerado
    : (clipeSubstituto != null ? clipeSubstituto : clipeOriginal);
```

- [ ] **Step 2: Usar `ClipeEfetivo` ao aplicar/restaurar overrides**

Trocar as duas resolucoes duplicadas por:

```csharp
pares[i] = new KeyValuePair<AnimationClip, AnimationClip>(
    pares[i].Key,
    slot.ClipeEfetivo != null ? slot.ClipeEfetivo : pares[i].Key);
```

e, em `Sobrescrever`:

```csharp
sobrescrita[slot.ClipeOriginal] = clipe != null
    ? clipe
    : (slot.ClipeEfetivo != null ? slot.ClipeEfetivo : slot.ClipeOriginal);
```

- [ ] **Step 3: Conferir o diff do arquivo runtime**

Run:

```powershell
git diff --check -- Assets/_Sardael/Scripts/Combate/TrocadorDeAnimacoes.cs
```

Expected: nenhuma mensagem de erro; nenhuma referencia a `UnityEditor` no arquivo runtime.

---

### Task 2: Gerador editor-only de AnimationClip reverso

**Files:**
- Create: `Assets/_Sardael/Scripts/Editor/GeradorDeClipeReverso.cs`
- Create automatically through Unity: `Assets/_Sardael/Scripts/Editor/GeradorDeClipeReverso.cs.meta`
- Create automatically through Unity: `Assets/_Sardael/Animacoes/Geradas/Reversas/*.anim`

**Interfaces:**
- Produces: `GeradorDeClipeReverso.ObterAssinatura(AnimationClip) : string`
- Produces: `GeradorDeClipeReverso.TryCriarOuAtualizar(AnimationClip, out AnimationClip, out string, out string) : bool`
- Consumes: `AnimationUtility.GetCurveBindings`, `GetObjectReferenceCurveBindings`, `GetAnimationEvents`

- [ ] **Step 1: Criar a estrutura do gerador e a identidade estavel**

Criar `GeradorDeClipeReverso` como `internal static class` no namespace `SardaelEditor`, com:

```csharp
const string PastaRaiz = "Assets/_Sardael/Animacoes/Geradas";
const string PastaReversas = PastaRaiz + "/Reversas";

public static string ObterAssinatura(AnimationClip fonte)
{
    if (fonte == null) return string.Empty;
    string caminho = AssetDatabase.GetAssetPath(fonte);
    if (string.IsNullOrEmpty(caminho)) return string.Empty;
    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(fonte, out string guid, out long fileId))
        return string.Empty;
    return guid + ":" + fileId + ":" + AssetDatabase.GetAssetDependencyHash(caminho);
}
```

O caminho de saida deve ser `PastaReversas + "/" + guid + "_" + fileId + "_Reverso.anim"`. Criar as duas pastas, quando ausentes, com `AssetDatabase.CreateFolder`.

- [ ] **Step 2: Implementar a inversao de curvas escalares**

Para cada binding de `AnimationUtility.GetCurveBindings(fonte)`, criar uma nova curva com as chaves em ordem reversa:

```csharp
static AnimationCurve Inverter(AnimationCurve origem, float duracao)
{
    var antigas = origem.keys;
    var novas = new Keyframe[antigas.Length];
    for (int i = 0; i < antigas.Length; i++)
    {
        Keyframe antiga = antigas[antigas.Length - 1 - i];
        var nova = new Keyframe(
            Mathf.Max(0f, duracao - antiga.time),
            antiga.value,
            -antiga.outTangent,
            -antiga.inTangent,
            antiga.outWeight,
            antiga.inWeight)
        {
            weightedMode = TrocarPesos(antiga.weightedMode)
        };
        novas[i] = nova;
    }

    return new AnimationCurve(novas)
    {
        preWrapMode = origem.postWrapMode,
        postWrapMode = origem.preWrapMode
    };
}
```

`TrocarPesos` deve trocar somente os flags `In` e `Out`, preservando `None` e `Both`:

```csharp
static WeightedMode TrocarPesos(WeightedMode modo)
{
    if (modo == WeightedMode.In) return WeightedMode.Out;
    if (modo == WeightedMode.Out) return WeightedMode.In;
    return modo;
}
```

- [ ] **Step 3: Inverter referencias a objetos e Animation Events**

Para cada object binding, espelhar tempos e reordenar:

```csharp
static ObjectReferenceKeyframe[] Inverter(
    ObjectReferenceKeyframe[] origem,
    float duracao)
{
    var resultado = new ObjectReferenceKeyframe[origem.Length];
    for (int i = 0; i < origem.Length; i++)
    {
        var antiga = origem[origem.Length - 1 - i];
        resultado[i] = new ObjectReferenceKeyframe
        {
            time = Mathf.Max(0f, duracao - antiga.time),
            value = antiga.value
        };
    }
    return resultado;
}
```

Copiar cada evento para uma nova instancia, preservando todos os parametros e usando `time = Mathf.Clamp(duracao - evento.time, 0f, duracao)`. Ordenar o array final por `time` antes de `AnimationUtility.SetAnimationEvents`.

- [ ] **Step 4: Persistir sem trocar o GUID do clipe gerado**

Construir primeiro um `AnimationClip` em memoria, copiar `frameRate`, `legacy`, `wrapMode`, `localBounds`, clip settings, curvas, object curves e eventos, e chamar `EnsureQuaternionContinuity()`.

Se o asset ainda nao existir, usar `AssetDatabase.CreateAsset(novo, caminho)`. Se existir, usar:

```csharp
EditorUtility.CopySerialized(novo, existente);
UnityEngine.Object.DestroyImmediate(novo);
EditorUtility.SetDirty(existente);
AssetDatabase.SaveAssetIfDirty(existente);
```

Envolver a operacao em `try/catch`. Em falha, retornar `false`, `reverso = null`, assinatura calculada e uma mensagem iniciada por `Nao foi possivel gerar o clipe reverso:`. Nenhum asset parcial deve ser aplicado ao slot.

- [ ] **Step 5: Conferir o diff do gerador**

Run:

```powershell
git diff --check -- Assets/_Sardael/Scripts/Editor/GeradorDeClipeReverso.cs
```

Expected: nenhuma mensagem; o arquivo inteiro permanece sob a pasta `Editor`.

---

### Task 3: Integracao IMGUI e preservacao durante sincronizacao

**Files:**
- Modify: `Assets/_Sardael/Scripts/Editor/TrocadorDeAnimacoesEditor.cs:18-198`

**Interfaces:**
- Consumes: `GeradorDeClipeReverso.ObterAssinatura`
- Consumes: `GeradorDeClipeReverso.TryCriarOuAtualizar`
- Produces: `TrocadorDeAnimacoesEditor.DefinirReversao(TrocadorDeAnimacoes, string, bool) : bool`

- [ ] **Step 1: Adicionar rotulos e controles individuais ao slot**

Adicionar `GUIContent` cacheados para `Tocar ao contrario` e `Clipe reverso gerado`. Em `DesenharSlot`, localizar:

```csharp
var tocarAoContrario = slot.FindPropertyRelative("tocarAoContrario");
var reverso = slot.FindPropertyRelative("clipeReversoGerado");
var erro = slot.FindPropertyRelative("erroDoClipeReverso");
```

Depois de `Usar no lugar`, desenhar:

```csharp
EditorGUILayout.PropertyField(tocarAoContrario, TituloTocarAoContrario);
if (tocarAoContrario.boolValue)
{
    using (new EditorGUI.DisabledScope(true))
        EditorGUILayout.ObjectField(
            TituloReverso,
            reverso.objectReferenceValue,
            typeof(AnimationClip),
            false);
    if (!string.IsNullOrEmpty(erro.stringValue))
        EditorGUILayout.HelpBox(erro.stringValue, MessageType.Error);
}
```

- [ ] **Step 2: Gerar somente quando a configuracao ou assinatura exigir**

Apos `serializedObject.ApplyModifiedProperties()`, percorrer slots ativos. Para cada um, calcular `fonte = substituto ?? original` e comparar `assinaturaDaFonteReversa` com `GeradorDeClipeReverso.ObterAssinatura(fonte)`.

Quando a referencia, booleano ou assinatura mudar, chamar o gerador e gravar por `SerializedObject`:

```csharp
reverso.objectReferenceValue = sucesso ? clipeGerado : null;
assinatura.stringValue = sucesso ? assinaturaNova : string.Empty;
erro.stringValue = sucesso ? string.Empty : erroDaGeracao;
```

Usar `Undo.RecordObject`, `ApplyModifiedProperties` e `EditorUtility.SetDirty`. Em Play Mode, chamar `componente.AplicarConfiguracao()` depois da atualizacao.

- [ ] **Step 3: Limpar todos os campos relacionados no botao existente**

Para cada slot, o botao `Limpar substituicoes` deve definir:

```csharp
clipeSubstituto = null;
tocarAoContrario = false;
clipeReversoGerado = null;
assinaturaDaFonteReversa = string.Empty;
erroDoClipeReverso = string.Empty;
```

Os assets em cache nao devem ser apagados.

- [ ] **Step 4: Preservar toda a configuracao ao sincronizar**

Substituir o dicionario atual por um registro local que guarde substituto, booleano, gerado, assinatura e erro, indexado pela referencia de `clipeOriginal`. Restaurar todos esses campos no novo slot correspondente e chamar a garantia dos reversos ativos ao final.

- [ ] **Step 5: Expor migracao/configuracao programatica por estado**

Implementar:

```csharp
public static bool DefinirReversao(
    TrocadorDeAnimacoes componente,
    string nomeDoEstado,
    bool tocarAoContrario)
```

O metodo deve atualizar todos os slots com o mesmo nome de estado, aplicar as propriedades, gerar os clipes quando `true`, limpar referencia/assinatura/erro quando `false`, marcar o componente como dirty e retornar se encontrou pelo menos um slot.

- [ ] **Step 6: Conferir o diff do inspetor**

Run:

```powershell
git diff --check -- Assets/_Sardael/Scripts/Editor/TrocadorDeAnimacoesEditor.cs
```

Expected: nenhuma mensagem; todos os campos editaveis usam `SerializedProperty` e Undo.

---

### Task 4: Remover o caminho especial de parry reverso e migrar a sandbox

**Files:**
- Modify: `Assets/_Sardael/Scripts/Combate/DefesaDoHeroi.cs:14-17,28-33,104-108,150-156`
- Modify: `Assets/_Sardael/Scripts/Editor/MontarAparo.cs:49-50,119-160`
- Modify: `Assets/_Sardael/Scripts/Editor/MontarCombateSandbox.cs:131-134,432-461`
- Modify through Unity Editor: `Assets/_Sardael/Cenas/Combate_Sandbox.unity`

**Interfaces:**
- Consumes: `TrocadorDeAnimacoesEditor.DefinirReversao`
- Removes: `DefesaDoHeroi.P_APARAR_REVERSO`
- Removes: trigger runtime `apararReverso`

- [ ] **Step 1: Fazer a defesa disparar sempre o estado normal de aparo**

Remover `aparoAoContrario`, `P_APARAR_REVERSO`, `ApararReverso` e `TemParametro`. Simplificar o disparo para:

```csharp
if (animator != null) animator.SetTrigger(Aparar);
```

- [ ] **Step 2: Parar de criar o estado reverso nos montadores**

Em `MontarAparo`, remover `ESTADO_REVERSO` e todo o bloco que cria parametro, estado e transicoes reversas. O estado `Aparo` normal permanece inalterado.

Em `MontarCombateSandbox`, remover o override de `AparoReverso` e o bloco que cria estado/trigger/transicoes reversas. Depois de definir o substituto de `Aparo`, preservar o comportamento atual da cena chamando:

```csharp
TrocadorDeAnimacoesEditor.DefinirReversao(trocador, "Aparo", true);
```

- [ ] **Step 3: Recuperar a Unity Pipeline antes de tocar a cena**

Run:

```powershell
unity status
unity pipeline list --format json
```

Expected: uma instancia do projeto com servidor Pipeline `ready/reachable`. Se continuar `isRunning: true` e `isReachable: false`, reiniciar o Editor pelo fluxo autorizado e repetir, sem editar o YAML da cena.

- [ ] **Step 4: Recompilar e migrar a cena ativa pelo Editor**

Usar `unity command` para descobrir `eval`, `refresh` e `save_scene`. Pelo `eval`, localizar o `TrocadorDeAnimacoes` do Sardael, executar `Sincronizar`, manter `HumanM@ParryPolearm01 - Hit` como substituto de `Aparo`, chamar `DefinirReversao(..., "Aparo", true)` e salvar `Combate_Sandbox.unity`.

Expected: o slot `Aparo` referencia um `.anim` sob `Animacoes/Geradas/Reversas`, `tocarAoContrario == true`, e a cena salva nao contem mais o campo legado `aparoAoContrario` depois da reserializacao.

---

### Task 5: Testes basicos pos-implementacao

**Files:**
- Verify: `Assets/_Sardael/Scripts/Combate/TrocadorDeAnimacoes.cs`
- Verify: `Assets/_Sardael/Scripts/Editor/GeradorDeClipeReverso.cs`
- Verify: `Assets/_Sardael/Scripts/Editor/TrocadorDeAnimacoesEditor.cs`
- Verify: `Assets/_Sardael/Cenas/Combate_Sandbox.unity`

**Interfaces:**
- Consumes: Unity Pipeline `eval`, `console_get`, `editor_play`, `editor_stop`
- Produces: evidencia de compilacao, cache reverso, override individual e ausencia de regressao fisica

- [ ] **Step 1: Confirmar compilacao e Console limpo**

Forcar refresh/recompile pela Pipeline e consultar o Console.

Expected: zero erros novos de compilacao e nenhuma excecao do gerador.

- [ ] **Step 2: Validar o asset reverso por amostragem**

Por `eval`, obter o clipe-fonte e o gerado do slot `Aparo`. Para cada binding escalar compartilhado, comparar com tolerancia `0.0001f`:

```csharp
origem.Evaluate(0f) == reversa.Evaluate(duracao)
origem.Evaluate(duracao) == reversa.Evaluate(0f)
```

Tambem confirmar que os eventos satisfazem `tempoReverso = duracao - tempoOriginal` em ordem crescente.

Expected: nenhuma divergencia acima da tolerancia.

- [ ] **Step 3: Validar o override individual no Play Mode**

Entrar em Play Mode e consultar o `AnimatorOverrideController` do Sardael.

Expected:

- `Aparo` usa o clipe gerado reverso;
- pelo menos um slot desmarcado continua usando substituto/original normal;
- o trigger executado por `DefesaDoHeroi` e `aparar`, nunca `apararReverso`.

- [ ] **Step 4: Repetir a verificacao fisica da execucao**

Registrar a posicao vertical do heroi, executar a finalizacao de teste existente e medir novamente ao terminar.

Expected: nenhuma subida/impulso anormal; tolerancia maxima de `0.05` unidade em Y fora do ajuste normal do CharacterController.

- [ ] **Step 5: Testar desligamento e persistencia**

Em Edit Mode, desmarcar temporariamente `Tocar ao contrario`, aplicar e confirmar que `ClipeEfetivo` volta ao substituto normal; restaurar o valor `true`, salvar a cena, fecha-la/reabri-la e confirmar que o booleano e o asset gerado persistem.

- [ ] **Step 6: Revisar o diff final**

Run:

```powershell
git diff --check
git status --short
rg -n "aparoAoContrario|P_APARAR_REVERSO|ApararReverso" Assets/_Sardael
```

Expected: `git diff --check` limpo; nenhuma referencia de codigo ao mecanismo legado; somente arquivos planejados e mudancas preexistentes aparecem no status.

