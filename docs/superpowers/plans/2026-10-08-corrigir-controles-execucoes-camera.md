# Correcoes de controles, execucoes e camera Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aplicar os controles contextuais pedidos, eliminar a rotacao excessiva das finalizacoes em pe e tornar perceptivel o zoom suave durante a animacao de execucao.

**Architecture:** `EntradaDeCombate` concentra os pulsos do Input System e mantem o latch de corrida; `SistemaDeExecucao` consome o mesmo pulso de Interact antes da `Interacao` comum. As variantes em pe passam a usar pares IP cuja translacao e rotacao de raiz ficam abaixo dos limites testados. A camera e ligada explicitamente ao sistema de execucao e inicia o zoom depois do alinhamento, no inicio da coreografia.

**Tech Stack:** Unity 6000.6.1f1, C#, Input System, NUnit/EditMode, Unity Pipeline CLI.

## Global Constraints

- Interagir ou executar usa `E` e `buttonNorth` (Triangulo/Y).
- Bloqueio e parry usam botao direito do mouse e `rightShoulder` (R1/RB), sem `F` nem `leftShoulder`.
- Especial permanece em `rightTrigger` (R2/RT).
- Corrida usa um toque em Shift/L3, permanece enquanto houver movimento e desarma quando o movimento para ou o heroi e travado.
- Permanecem exatamente duas execucoes no chao e duas em pe.
- Assets e cena devem ser atualizados pelo Unity Editor conectado, nao por edicao manual de YAML.
- Preservar todas as alteracoes de combate ja pendentes no worktree.

---

### Task 1: Regressao dos controles e da rotacao das execucoes

**Files:**
- Create: `Assets/_Sardael/Scripts/Editor/ControlesDeCombateTests.cs`
- Modify: `Assets/_Sardael/Scripts/Editor/ExecucoesEmPeTests.cs`

**Interfaces:**
- Consumes: `InputActionAsset`, `EntradaDeCombate.AtualizarCorrida(Vector2, bool)` e `MontarCombateSandbox.CarregarVariantesDeExecucao()`.
- Produces: testes que falham com L1/Q/R1 antigos, corrida por botao segurado e rotacao de raiz acima de 60 graus.

- [x] **Step 1: Write the failing input and sprint tests**

Criar testes que carregam `Assets/Settings/InputSystem_Actions.inputactions`, comparam exatamente os paths das acoes e exercitam o latch por esta sequencia:

```csharp
entrada.RegistrarToqueDeCorrida();
Assert.That(entrada.AtualizarCorrida(Vector2.right, false), Is.True);
Assert.That(entrada.AtualizarCorrida(Vector2.right, false), Is.True);
Assert.That(entrada.AtualizarCorrida(Vector2.zero, false), Is.False);
Assert.That(entrada.AtualizarCorrida(Vector2.right, false), Is.False);
```

Os bindings esperados sao:

```csharp
Interact  = { "<Keyboard>/e", "<Gamepad>/buttonNorth" };
Execution = { };
Block     = { "<Mouse>/rightButton", "<Gamepad>/rightShoulder" };
Sprint    = { "<Keyboard>/leftShift", "<Gamepad>/leftStickPress" };
```

- [x] **Step 2: Extend the execution regression test**

Para cada variante em pe, amostrar `RootT.x/z` e `RootQ.x/y/z/w` ao longo do clipe inteiro. Manter `deslocamento <= 0.15 m` e adicionar `Quaternion.Angle(rotacaoInicial, rotacaoAmostrada) <= 60 graus`.

- [x] **Step 3: Run tests and verify RED**

Run:

```powershell
unity command --caller plugin --skill unity-cli --project-path '<project>' --format json run_tests --mode editor --timeout 300
```

Expected: os testes de binding, latch e rotacao falham; os dois testes anteriores continuam compilando.

---

### Task 2: Controles contextuais e corrida por toque

**Files:**
- Modify: `Assets/_Sardael/Scripts/Combate/EntradaDeCombate.cs`
- Modify: `Assets/_Sardael/Scripts/Combate/SistemaDeExecucao.cs`
- Modify: `Assets/_Sardael/Scripts/Interacao.cs`
- Modify: `Assets/_Sardael/Scripts/MovimentoDoHeroi.cs`
- Modify: `Assets/_Sardael/Scripts/Editor/MontarCombateSandbox.cs`
- Modify through live Editor: `Assets/Settings/InputSystem_Actions.inputactions`

**Interfaces:**
- Produces: `EntradaDeCombate.RegistrarToqueDeCorrida()`, `EntradaDeCombate.AtualizarCorrida(Vector2 movimento, bool travado)` e `EntradaDeCombate.DesarmarCorrida()`.
- Consumes: `SistemaDeExecucao.EmExecucao` em `Interacao` para impedir dialogo e execucao no mesmo quadro.

- [x] **Step 1: Implement the sprint latch**

Assinar `correr.performed`, armar um booleano no callback e desarma-lo quando `movimento.sqrMagnitude <= 0.0225f`, quando `travado` for verdadeiro e em `OnDisable`. `MovimentoDoHeroi.LerEntrada` combina o eixo e chama `AtualizarCorrida` depois de limitar a magnitude; o ramo travado chama `DesarmarCorrida`.

- [x] **Step 2: Unify contextual execution and interaction**

Remover o consumo funcional da acao `Execution`. Em `SistemaDeExecucao.Update`, um unico `ConsumirInteracao()` tenta primeiro `TentarExecutarAtordoado()`, depois `TentarExecutar()`. `Interacao.Apertou()` aceita apenas E ou `buttonNorth`, e retorna falso quando o `SistemaDeExecucao` do heroi esta executando.

- [x] **Step 3: Persist exact bindings through the builder**

Adicionar um helper em `MontarCombateSandbox` que apaga e recria os bindings de cada acao existente. Aplicar exatamente os quatro conjuntos definidos no Task 1, preservando FlowSpecial em R/R2 e Dodge em Ctrl/Circulo.

- [x] **Step 4: Update the Input Action asset in the live Editor**

Usar `InputActionSetupExtensions.ChangeBinding(...).Erase()` e `AddBinding(...)`, salvar `input.ToJson()` pelo proprio Editor, reimportar e confirmar os paths por leitura do asset carregado.

- [x] **Step 5: Run the focused tests and verify GREEN**

Expected: testes de controles e corrida passam; nenhum erro de compilacao.

---

### Task 3: Finalizacoes em pe sem giro e zoom perceptivel

**Files:**
- Modify: `Assets/_Sardael/Scripts/Editor/MontarCombateSandbox.cs`
- Modify: `Assets/_Sardael/Scripts/Combate/SistemaDeExecucao.cs`
- Modify: `Assets/_Sardael/Scripts/Combate/CameraDeCombate.cs`
- Modify through live Editor: `Assets/_Sardael/Cenas/Combate_Sandbox.unity`

**Interfaces:**
- `SistemaDeExecucao.Configurar(...)` recebe explicitamente `CameraDeCombate novaCameraDeCombate`.
- `CameraDeCombate.IniciarZoomDeExecucao(Transform alvo)` inicia o blend no comeco da coreografia, apos o alinhamento.

- [x] **Step 1: Select two low-rotation paired standing clips**

Trocar as variantes em pe para `Attack6_Stage3_Complete` e `Attack3_Stage1_Complete`, ambas IP com controllers de reacao correspondentes. Usar duracoes de `1.60f` e `1.10f`, e impactos de `1.00f` e `0.65f`, respectivamente.

- [x] **Step 2: Wire and retime execution zoom**

Passar `cameraCombate` ao configurar `SistemaDeExecucao`; manter o fallback de `Awake`. Mover `IniciarZoomDeExecucao` de antes do alinhamento para imediatamente depois de travar/orientar os personagens e antes da derrubada ou da aplicacao do controller de execucao.

- [x] **Step 3: Make the light zoom visually clear**

Usar `multiplicadorDoAfastamentoNaExecucao = 0.82f`, `campoDeVisaoDaExecucao = 48f` e manter blend exponencial suave. `EncerrarZoomDeExecucao` continua no fim e em cancelamentos para restaurar a camera normal.

- [x] **Step 4: Update the open scene through the live Editor**

Configurar a referencia `cameraDeCombate`, recarregar as quatro variantes via `CarregarVariantesDeExecucao()`, aplicar os tres parametros da camera, marcar a cena como alterada e salvar `Combate_Sandbox.unity`.

- [x] **Step 5: Run execution tests and inspect live state**

Expected: exatamente quatro variantes; as duas em pe ficam abaixo de `0.15 m` e `60 graus`; a referencia da camera nao e nula; parametros persistidos sao `0.82`, `48`, `7`.

---

### Task 4: Verificacao integrada

**Files:**
- Test: all changed C# files, input asset and `Combate_Sandbox.unity`.

**Interfaces:**
- Consumes: entregaveis dos Tasks 1-3.
- Produces: evidencia de compilacao, testes e persistencia sem regressao.

- [x] **Step 1: Run the complete EditMode suite**

Expected: todos os testes passam, zero falhas e zero erros de compilacao.

- [x] **Step 2: Re-read live Editor state**

Confirmar bindings, quatro variantes, controllers IP de baixo giro, camera ligada ao sistema e intensidade de zoom.

- [x] **Step 3: Review the diff**

Executar `git diff --check` apenas nos arquivos tocados nesta entrega, revisar `git diff --stat` e garantir que nenhuma mudanca pendente anterior foi descartada ou sobrescrita.

