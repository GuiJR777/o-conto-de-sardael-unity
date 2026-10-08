# Bloqueio direcional e janela de parry

## Objetivo

Separar bloqueio comum de parry e fazer a defesa orientar Sardael para o inimigo indicado pelo jogador, usando a mesma regra direcional dos ataques.

## Seleção direcional

`CombateDoHeroi` passa a expor a seleção de alvo que já utiliza para ataques. `DefesaDoHeroi` fornece a direção atual do movimento/analógico e reutiliza essa seleção enquanto o bloqueio estiver pressionado.

- Com direção acima da zona morta, escolher o inimigo mais coerente com a direção desejada.
- Sem direção, usar a regra existente do ataque para manter ou selecionar o alvo contextual mais adequado à frente.
- Enquanto houver alvo válido, orientar Sardael continuamente para ele.
- Se não houver alvo selecionável, manter a orientação atual.

Isso substitui a orientação atual baseada somente na ameaça mais urgente, que pode contrariar a direção escolhida pelo jogador.

## Bloqueio comum

Manter R1/RB ou o botão direito do mouse ativa a postura de bloqueio. Um golpe recebido dentro do alcance e do arco defensivo é bloqueado mesmo depois do fim da janela de parry.

O bloqueio comum:

- impede o dano conforme o comportamento defensivo atual;
- não dispara o gatilho `aparar`;
- não concede Flow de parry;
- não aplica reação de aparo ao inimigo;
- não abre oportunidade de contra-ataque.

## Janela de parry

Uma nova janela de `0,6 s` começa somente na transição de botão solto para botão pressionado.

- O primeiro golpe válido que atingir Sardael durante a janela realiza o parry.
- O parry dispara a animação `Aparo`, concede Flow, aplica a reação ao atacante e abre a janela de contra-ataque já existente.
- Depois de um parry, a janela é consumida imediatamente, impedindo múltiplos parries com o mesmo pressionamento.
- Se nenhum golpe chegar em `0,6 s`, a janela expira e a guarda continua como bloqueio comum.
- Soltar e pressionar o botão novamente abre uma nova janela.
- Cancelar ou desabilitar a defesa também encerra qualquer janela pendente.

## Alcance e arco

Os limites atuais continuam válidos para bloqueio e parry:

- alcance defensivo de `3 m`;
- arco frontal total de `220 graus`.

Golpes fora desses limites não são bloqueados e causam dano normalmente.

## Persistência

`MontarCombateSandbox` deve persistir a janela de parry de `0,6 s` ao reconstruir a cena. A cena `Combate_Sandbox.unity` deve ser atualizada pelo Unity Editor conectado, sem edição manual do YAML.

## Testes

- Confirmar que iniciar a guarda abre uma janela de `0,6 s`.
- Confirmar que um golpe dentro da janela produz parry e consome a janela.
- Confirmar que um golpe depois da janela é bloqueado sem produzir parry.
- Confirmar que soltar e pressionar novamente reabre a janela.
- Confirmar que a direção do analógico seleciona o mesmo alvo usado pelo ataque e orienta Sardael para ele.
- Executar a suíte EditMode completa e verificar a cena persistida no Editor conectado.
