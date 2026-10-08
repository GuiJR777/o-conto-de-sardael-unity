# Controles contextuais e corrida por toque

## Objetivo

Unificar execução e interação no mesmo comando, mover bloqueio e parry para R1/clique direito e permitir que a corrida continue após um único toque enquanto houver movimento.

## Mapeamento de entrada

- Interagir ou executar: `E` no teclado e `buttonNorth` no controle (Triângulo no PlayStation, Y no Xbox).
- Bloquear ou aparar: botão direito do mouse e `rightShoulder` no controle (R1/RB).
- Correr: `Left Shift` no teclado e `leftStickPress` no controle (L3/LS).
- A ação antiga `Execution` permanece no asset apenas para compatibilidade de serialização, mas sem atalhos próprios.

## Resolução contextual

Um toque em Interact gera um único comando contextual. O sistema de execução, que atualiza antes da interação comum, tenta na seguinte ordem:

1. Executar um inimigo atordoado válido próximo ao herói.
2. Executar o alvo de combate selecionado quando a barra de Flow estiver cheia.
3. Se nenhuma execução começar, deixar o mesmo toque iniciar ou avançar uma interação comum.

Quando uma execução começa, a interação comum ignora aquele mesmo quadro. Assim, conversar com um NPC continua funcionando com `E`/Triângulo, mas uma execução válida tem prioridade e não dispara diálogo simultaneamente.

## Bloqueio e parry

Bloqueio e parry continuam sendo dois resultados do mesmo comando. Manter R1/RB ou o botão direito pressionado sustenta o bloqueio; iniciar o bloqueio dentro da janela de impacto produz o parry. Os atalhos antigos L1/LB e `F` são removidos para evitar comandos duplicados.

## Corrida por toque

Pressionar Shift ou L3 arma a corrida uma vez. O estado permanece ativo enquanto a magnitude da entrada de movimento estiver acima da zona morta. Ao soltar o movimento e o personagem parar, o estado é desarmado; o próximo deslocamento volta a ser caminhada até outro toque no botão de corrida.

A corrida também é desarmada quando o componente de entrada é desabilitado ou o herói fica sem movimento por causa de uma trava de gameplay.

## Arquivos e persistência

- Atualizar `InputSystem_Actions.inputactions` no Editor conectado.
- Atualizar `EntradaDeCombate`, `SistemaDeExecucao`, `Interacao` e `MovimentoDoHeroi`.
- Atualizar `MontarCombateSandbox` para que reconstruir a cena não restaure os atalhos antigos.
- Preservar as alterações de combate e de finalizações já presentes no worktree.

## Testes

- Teste EditMode confirma os atalhos de Interact, Block, Sprint e a ausência de atalhos em Execution.
- Teste unitário confirma que a corrida é armada por um toque, permanece durante movimento e é desarmada ao parar.
- Teste de comportamento confirma a prioridade da execução sobre a interação comum sem perder a interação quando nenhuma execução for válida.
- Executar a suíte EditMode completa e confirmar ausência de erros de compilação.
