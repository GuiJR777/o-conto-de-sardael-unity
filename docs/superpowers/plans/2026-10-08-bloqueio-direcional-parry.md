# Bloqueio direcional e janela de parry — plano de implementação

**Objetivo:** fazer o bloqueio mirar com a mesma seleção direcional dos ataques e separar bloqueio comum de parry, reservando o parry para o primeiro impacto recebido nos 0,6 s após pressionar o botão.

**Arquitetura:** `CombateDoHeroi` expõe sua seleção direcional existente para que a defesa não duplique critérios. `DefesaDoHeroi` mantém o alvo de bloqueio enquanto o botão está pressionado e usa uma pequena classe de estado, controlada por tempo explícito, para abrir, expirar e consumir a janela de parry. A geometria atual de defesa (3 m e cone de 220°) continua decidindo se o golpe é bloqueável; somente os efeitos de parry ficam condicionados à janela.

**Tecnologias:** Unity 6, C#, NUnit/EditMode, Unity CLI.

---

## Tarefa 1: especificar a janela de parry com testes

**Arquivos:**
- Criar: `Assets/_Sardael/Scripts/Editor/DefesaDoHeroiTests.cs`
- Criar: `Assets/_Sardael/Scripts/Combate/JanelaDeParry.cs`

1. Escrever testes EditMode para uma janela aberta por 0,6 s, expiração depois do limite e consumo único no primeiro impacto.
2. Executar somente `DefesaDoHeroiTests` e confirmar a falha por ausência de `JanelaDeParry`.
3. Implementar a menor classe de estado possível, recebendo o tempo atual como argumento para manter os testes determinísticos.
4. Reexecutar os testes e confirmar que passam.

## Tarefa 2: compartilhar a seleção direcional entre ataque e defesa

**Arquivos:**
- Modificar: `Assets/_Sardael/Scripts/Combate/CombateDoHeroi.cs`
- Modificar: `Assets/_Sardael/Scripts/Editor/DefesaDoHeroiTests.cs`

1. Escrever um teste com dois inimigos em direções opostas e comprovar que a API pública desejada escolhe o inimigo apontado pela entrada.
2. Executar o teste e confirmar a falha por ausência da API.
3. Expor a seleção já usada pelo ataque como `EscolherAlvoDirecional`, sem alterar sua pontuação ou zona morta, e migrar as chamadas internas.
4. Reexecutar o teste e confirmar que passa.

## Tarefa 3: separar bloqueio comum de parry

**Arquivos:**
- Modificar: `Assets/_Sardael/Scripts/Combate/DefesaDoHeroi.cs`
- Modificar: `Assets/_Sardael/Scripts/Editor/DefesaDoHeroiTests.cs`
- Modificar: `Assets/_Sardael/Scripts/Editor/MontarCombateSandbox.cs`

1. Escrever testes para os invariantes observáveis: duração padrão de 0,6 s, janela reiniciada somente por novo pressionamento e parry consumido uma vez por pressão.
2. Executar os testes e confirmar a falha esperada no comportamento atual.
3. Ao pressionar bloqueio, abrir a janela e selecionar o alvo pela mesma entrada direcional do ataque; enquanto mantido, orientar o herói para esse alvo.
4. Em `TentarBloquear`, manter o retorno `true` para qualquer golpe dentro do alcance/cone, mas executar gatilho `aparar`, Flow, reação do inimigo e contra-ataque apenas quando a janela puder ser consumida.
5. Ao soltar, cancelar ou desabilitar, fechar a janela e limpar o alvo contextual. Configurar explicitamente 0,6 s no montador do sandbox.
6. Reexecutar os testes focados e confirmar que passam.

## Tarefa 4: persistir a cena e validar regressões

**Arquivos:**
- Modificar via Unity Editor: `Assets/_Sardael/Scenes/CombateSandbox.unity`

1. Aguardar recompilação sem erros.
2. Usar a API do Editor para gravar `janelaDeParry = 0.6` no componente existente da cena e salvar.
3. Rodar toda a suíte EditMode e confirmar zero falhas.
4. Entrar em Play Mode e verificar no runtime que o componente inicia com 0,6 s e não gera exceções; sair do Play Mode.
5. Revisar `git diff` e confirmar que somente arquivos relacionados foram modificados por esta implementação.
