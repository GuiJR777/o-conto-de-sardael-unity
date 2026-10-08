# Substituir execução em pé com giro — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** substituir `Attack3_Stage1_Complete` por `Attack11`, preservando duas execuções no chão e duas em pé sem órbita visual ao redor da vítima.

**Architecture:** a lista canônica continua em `MontarCombateSandbox`, que carrega os controllers IP pareados e fornece as variantes para a cena. O teste EditMode passa a verificar os nomes exatos e um limite mais rígido de rotação de raiz; a cena é atualizada pela API do Editor para manter o estado em memória e o YAML sincronizados.

**Tech Stack:** Unity 6, C#, NUnit/EditMode, Unity CLI/Pipeline.

## Global Constraints

- Manter `Attack6_Stage3_Complete` como a primeira execução em pé.
- Usar `Attack11` como a segunda execução em pé, com impacto em 1,5 s e duração total de 2,4 s no tempo original do clipe.
- Manter as duas execuções no chão e a lógica de sorteio inalteradas.
- Exigir no máximo 40° de rotação e 0,15 m de deslocamento planar nas execuções em pé.
- Não editar manualmente o YAML da cena enquanto o Unity Editor estiver conectado.

---

### Task 1: substituir a variante e endurecer a regressão

**Files:**
- Modify: `Assets/_Sardael/Scripts/Editor/MontarCombateSandbox.cs`
- Modify: `Assets/_Sardael/Scripts/Editor/ExecucoesEmPeTests.cs`

**Interfaces:**
- Consumes: `MontarCombateSandbox.CarregarVariantesDeExecucao()` e `VarianteDeExecucao.Nome`.
- Produces: exatamente duas variantes em pé chamadas `Attack6_Stage3_Complete` e `Attack11`.

- [ ] **Step 1: atualizar a configuração canônica**

```csharp
static readonly string[] ExecucoesEmPe =
{
    "Attack6_Stage3_Complete",
    "Attack11"
};
static readonly float[] ImpactosEmPe = { 1.00f, 1.50f };
static readonly float[] DuracoesEmPe = { 1.60f, 2.40f };
```

- [ ] **Step 2: tornar a verificação de rotação mais rígida e conferir os nomes**

```csharp
const float RotacaoDaRaizMaxima = 40f;

Assert.That(
    emPe.Select(variante => variante.Nome).ToArray(),
    Is.EqualTo(new[] { "Attack6_Stage3_Complete", "Attack11" }));
```

- [ ] **Step 3: executar o teste focado**

Run:

```powershell
unity command --caller plugin --skill unity-cli --project-path 'C:\Users\MSI\Desktop\RamiresTech\Games\Freelas\o conto de sardael unity' --format json run_tests --mode editor --filter 'Sardael.Tests.ExecucoesEmPeTests' --timeout 300
```

Expected: `Total: 1`, `Passed: 1`, `Failed: 0`.

### Task 2: atualizar a cena pelo Editor

**Files:**
- Modify via Unity Editor: `Assets/_Sardael/Cenas/Combate_Sandbox.unity`

**Interfaces:**
- Consumes: `MontarCombateSandbox.AtualizarFinalizacoesNaCenaAberta()`.
- Produces: `SistemaDeExecucao` serializado com `Attack6_Stage3_Complete` e `Attack11` nas duas posições em pé.

- [ ] **Step 1: recompilar sem erros**

Run `recompile` e consultar `recompile_status` até `status: completed`, `failed: false`.

- [ ] **Step 2: atualizar e salvar as quatro variantes**

Run:

```csharp
SardaelEditor.MontarCombateSandbox.AtualizarFinalizacoesNaCenaAberta();
return true;
```

pela operação `unity command eval` contra o Editor conectado.

- [ ] **Step 3: confirmar os nomes serializados**

Ler `SistemaDeExecucao.NomeDaUltimaVariante` não altera a seleção; em EditMode, consultar o `SerializedObject` e confirmar que `variantes[2].nome` é `Attack6_Stage3_Complete` e `variantes[3].nome` é `Attack11`.

### Task 3: validar regressões e execução

**Files:**
- Verify: `Assets/_Sardael/Scripts/Editor/*.cs`

**Interfaces:**
- Consumes: projeto compilado e cena atualizada.
- Produces: evidência de suíte verde e Play Mode sem erros novos.

- [ ] **Step 1: executar toda a suíte EditMode**

Run:

```powershell
unity command --caller plugin --skill unity-cli --project-path 'C:\Users\MSI\Desktop\RamiresTech\Games\Freelas\o conto de sardael unity' --format json run_tests --mode editor --timeout 300
```

Expected: todos os testes passam, zero falhas.

- [ ] **Step 2: validar em Play Mode**

Entrar em Play Mode, limpar o Console, confirmar que `SistemaDeExecucao.QuantidadeDeVariantesDisponiveis == 4`, aguardar alguns segundos e consultar `console_status`.

Expected: quatro variantes disponíveis, `compilationFailed: false`, `consoleErrors: 0` e `consoleWarnings: 0`.

- [ ] **Step 3: sair do Play Mode e revisar o diff**

Confirmar que a alteração desta tarefa se limita à configuração/teste da variante e aos valores correspondentes salvos na cena. Não commitar nem enviar sem solicitação explícita.
