# Hero Animation Inspector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expor todos os clipes do Animator principal do heroi em um unico componente no Inspector e corrigir a cabeca decepada usada pelos goblins.

**Architecture:** `TrocadorDeAnimacoes` sera o componente de runtime responsavel por manter um unico `AnimatorOverrideController` por heroi, indexado por referencias reais de `AnimationClip`. Um Custom Inspector percorrera o `AnimatorController`, estados e Blend Trees e gravara os slots serializados. O Unity Editor conectado sera usado para renomear o prefab do orc, atualizar a cena e validar o resultado.

**Tech Stack:** Unity 6.6, C#, Animator/AnimatorOverrideController, UnityEditor.Animations, IMGUI CustomEditor, Unity Pipeline CLI.

## Global Constraints

- Nao usar TDD; executar apenas testes basicos depois da implementacao.
- Nao modificar globalmente os motions do `Sardael_Combate.controller` ao escolher um override.
- Identificar overrides por referencia de `AnimationClip`, nunca pelos nomes repetidos `Unreal Take`.
- Preservar alteracoes nao relacionadas ja existentes no worktree.
- Trabalhar na branch atual `combat-system`; o projeto aberto e as alteracoes nao commitadas impedem mover esta tarefa para outro worktree sem perder o estado vivo do Unity.
- Nao criar commits de implementacao que misturem alteracoes anteriores do usuario; deixar a revisao final no worktree.

---

### Task 1: Consolidar o componente de overrides

**Files:**
- Modify: `Assets/_Sardael/Scripts/Combate/TrocadorDeAnimacoes.cs`
- Modify: `Assets/_Sardael/Scripts/Combate/DefesaDoHeroi.cs`
- Modify: `Assets/_Sardael/Scripts/Combate/CombateDoHeroi.cs`

**Interfaces:**
- Produces: `SubstituicaoDeAnimacao` com caminho do estado, nome do estado, clipe original e clipe substituto.
- Produces: `TrocadorDeAnimacoes.Configurar(Animator)`, `AplicarConfiguracao()` e `Sobrescrever(string, AnimationClip)`.
- Consumes: `Animator.runtimeAnimatorController` e os slots serializados pelo Inspector da Task 2.

- [ ] **Step 1: Remodelar os dados serializados**

Adicionar uma classe serializavel por slot e manter no componente a referencia ao Animator, ao controller sincronizado e a lista de substituicoes. O slot deve preservar a identidade do clipe-base:

```csharp
[Serializable]
public sealed class SubstituicaoDeAnimacao
{
    [SerializeField] string caminhoDoEstado;
    [SerializeField] string nomeDoEstado;
    [SerializeField] AnimationClip clipeOriginal;
    [SerializeField] AnimationClip clipeSubstituto;

    public string CaminhoDoEstado => caminhoDoEstado;
    public string NomeDoEstado => nomeDoEstado;
    public AnimationClip ClipeOriginal => clipeOriginal;
    public AnimationClip ClipeSubstituto => clipeSubstituto;
}
```

- [ ] **Step 2: Aplicar todos os slots em um unico controller**

Em `AplicarConfiguracao()`, desembrulhar um `AnimatorOverrideController` existente, criar ou reutilizar uma unica instancia e aplicar pares pelo objeto original:

```csharp
var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
sobrescrita.GetOverrides(pares);
for (int i = 0; i < pares.Count; i++)
{
    var slot = EncontrarPorClipeOriginal(pares[i].Key);
    if (slot != null)
        pares[i] = new KeyValuePair<AnimationClip, AnimationClip>(
            pares[i].Key, slot.ClipeSubstituto != null ? slot.ClipeSubstituto : pares[i].Key);
}
sobrescrita.ApplyOverrides(pares);
animator.runtimeAnimatorController = sobrescrita;
```

- [ ] **Step 3: Corrigir a API de troca por estado**

`Sobrescrever(string nomeDoEstado, AnimationClip clipe)` deve resolver primeiro o slot sincronizado e só então substituir `slot.ClipeOriginal`. Nao usar `sobrescrita[nomeDoEstado]` nem `Animator.HasState` com hash parcial.

- [ ] **Step 4: Remover a tentativa conflitante dos scripts de combate**

Remover de `DefesaDoHeroi` a troca pontual de `Aparo`/`AparoReverso` e de `CombateDoHeroi` a troca pontual de `GolpeN`. Esses scripts continuarao apenas disparando triggers; o componente central passa a ser a fonte das escolhas feitas no Inspector.

---

### Task 2: Criar o Inspector sincronizado com estados e Blend Trees

**Files:**
- Create: `Assets/_Sardael/Scripts/Editor/TrocadorDeAnimacoesEditor.cs`
- Create: `Assets/_Sardael/Scripts/Editor/TrocadorDeAnimacoesEditor.cs.meta` (gerado pelo Unity)

**Interfaces:**
- Consumes: `TrocadorDeAnimacoes` e seu Animator.
- Produces: `TrocadorDeAnimacoesEditor.Sincronizar(TrocadorDeAnimacoes)` para uso pelo montador da cena.

- [ ] **Step 1: Implementar a varredura do controller**

Percorrer cada layer, `AnimatorStateMachine`, estados filhos e Blend Trees aninhadas. Para cada `AnimationClip`, registrar `layer/estado/motion`, o nome do estado e a referencia do clipe. Deduplicar pela referencia do clipe original, porque esse e o nivel real de granularidade do `AnimatorOverrideController`.

- [ ] **Step 2: Preservar escolhas durante nova sincronizacao**

Antes de reconstruir a lista, guardar `clipeOriginal -> clipeSubstituto`; depois restaurar o substituto quando a mesma referencia continuar no controller. Envolver a alteracao em `Undo.RecordObject`, `EditorUtility.SetDirty` e atualizacao do `SerializedObject`.

- [ ] **Step 3: Desenhar o Custom Inspector**

Mostrar a referencia ao Animator, controller detectado, botao `Sincronizar com Animator`, botao `Limpar substituicoes` e uma linha por slot:

```csharp
EditorGUILayout.LabelField(caminho.stringValue, EditorStyles.boldLabel);
using (new EditorGUI.DisabledScope(true))
    EditorGUILayout.ObjectField("Original", original.objectReferenceValue,
        typeof(AnimationClip), false);
EditorGUILayout.PropertyField(substituto, new GUIContent("Usar no lugar"));
```

Mostrar `HelpBox` quando nao houver Animator/controller, quando o controller atual diferir do sincronizado ou quando o mesmo clipe-base alimentar varios caminhos.

---

### Task 3: Integrar o componente ao heroi da sandbox

**Files:**
- Modify: `Assets/_Sardael/Scripts/Editor/MontarCombateSandbox.cs`
- Modify through Unity Editor: `Assets/_Sardael/Cenas/Combate_Sandbox.unity`

**Interfaces:**
- Consumes: `TrocadorDeAnimacoes.Configurar(Animator)` e `TrocadorDeAnimacoesEditor.Sincronizar`.
- Produces: heroi `Sardael` com o componente visivel e todos os slots preenchidos no Inspector.

- [ ] **Step 1: Atualizar o montador**

Depois de configurar o Animator do heroi, obter ou adicionar `TrocadorDeAnimacoes`, chamar `Configurar(animatorHeroi)` e sincronizar os slots. Remover as atribuicoes antigas `defesa.AnimacaoDeAparo` e `combate.AnimacoesDosGolpes`.

- [ ] **Step 2: Recompilar pelo Unity conectado**

Run: `unity command recompile --format json`

Poll: `unity command recompile_status --format json`

Expected: `completed` com `success: true` e zero erros novos.

- [ ] **Step 3: Atualizar a instancia existente sem reconstruir a cena inteira**

Pelo Unity Editor conectado, adicionar `TrocadorDeAnimacoes` a `/Sardael`, atribuir o Animator, executar a sincronizacao e definir para o slot `Aparo` o clipe `HumanM@ParryPolearm01 - Hit.fbx`. Salvar a cena.

---

### Task 4: Corrigir definitivamente a cabeca do goblin

**Files:**
- Rename through Unity Editor: `Assets/_Sardael/Personagens/Gore/Cabeca_Decepada.prefab` -> `Assets/_Sardael/Personagens/Gore/Cabeca_Orc_Decepada.prefab`
- Modify: `Assets/_Sardael/Scripts/Editor/MontarCombateSandbox.cs`
- Modify through Unity Editor: `Assets/_Sardael/Cenas/Combate_Sandbox.unity`

**Interfaces:**
- Consumes: prefab cuja malha e `Cabeca_Orc.asset`.
- Produces: todos os `EfeitoDeFinalizacao` dos goblins apontando para o prefab do orc.

- [ ] **Step 1: Renomear o prefab pelo Unity**

Usar `move_asset` para preservar o GUID, verificar que o `MeshFilter.sharedMesh` continua sendo `Cabeca_Orc.asset` e manter collider/rigidbody existentes.

- [ ] **Step 2: Atualizar o caminho do montador**

Alterar `CabecaDecepada` para `Assets/_Sardael/Personagens/Gore/Cabeca_Orc_Decepada.prefab`.

- [ ] **Step 3: Corrigir os seis goblins da cena aberta**

Pelo Unity Editor conectado, atribuir o novo prefab ao campo `prefabDaCabeca` de cada `EfeitoDeFinalizacao` nos objetos `E1` a `E6`, marcar e salvar a cena.

---

### Task 5: Validacao funcional pos-implementacao

**Files:**
- Inspect only: Unity Console, `Combate_Sandbox.unity`, `Sardael_Combate.controller` e prefabs de gore.

**Interfaces:**
- Consumes: implementacao das Tasks 1-4.
- Produces: evidencia de funcionamento basico, sem suite TDD nova.

- [ ] **Step 1: Confirmar compilacao e serializacao**

Verificar pelo Unity que `/Sardael` possui `TrocadorDeAnimacoes`, que a lista tem slots para estados simples e clipes de Blend Trees e que o Console nao possui erros de compilacao.

- [ ] **Step 2: Confirmar o override de parry**

Entrar em Play Mode, acionar o fluxo de aparo existente e confirmar que o Animator toca o clipe selecionado no slot `Aparo`. Confirmar tambem que pelo menos um slot vazio continua tocando o clipe original.

- [ ] **Step 3: Confirmar a finalizacao**

Executar uma variante com `decapitar = true`; confirmar visualmente que a cabeca instanciada usa a malha do orc e registrar a maior coordenada Y do heroi durante a sequencia. Expected: sem salto/impulso anormal e cabeca do protagonista ausente.

- [ ] **Step 4: Revisar o diff final**

Executar `git diff --check` e `git status --short`, garantindo que nenhum arquivo fora do escopo tenha sido alterado pela implementacao.
