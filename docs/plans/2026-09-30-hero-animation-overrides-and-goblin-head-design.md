# Configuracao de animacoes do heroi e cabeca do goblin

## Objetivo

Permitir que todas as animacoes usadas pelo heroi sejam substituidas pelo Inspector, sem alterar globalmente o Animator Controller e sem depender de nomes ambiguos de estados ou clipes. Corrigir tambem a finalizacao dos goblins para instanciar a cabeca do orc, e nao a cabeca de Sardael.

## Diagnostico confirmado

- O `Sardael_Combate.controller` possui estados de locomocao, pulo, esquiva, rolamento, arranco, quatro golpes, reacao a dano, aparo, bloqueio, contra-ataque e morte.
- Varios clipes diferentes importados de FBX possuem o mesmo nome visivel, `Unreal Take`.
- A tentativa atual chama o `AnimatorOverrideController` usando nomes de estados como `Aparo`. Overrides, contudo, sao identificados pelos clipes-base do controller. Isso torna a troca por string incorreta ou ambigua.
- Os goblins da cena de combate recebem `Cabeca_Sardael_Decepada.prefab` pelo montador da sandbox.
- `Cabeca_Decepada.prefab` referencia a malha `Cabeca_Orc.asset`; portanto ele e o prefab correto do goblin, apesar do nome generico.

## Solucao escolhida

### Componente do heroi

Criar um componente serializavel `ConfiguracaoDeAnimacoesDoHeroi` para ser colocado no mesmo objeto do `Animator` do protagonista. Seu Inspector personalizado tera:

- referencia ao `Animator`;
- botao `Sincronizar com Animator`;
- secoes organizadas por camada, estado e Blend Tree;
- para cada slot, clipe original somente leitura e um `AnimationClip` substituto selecionavel;
- botao para limpar overrides;
- avisos para controller ausente, controller alterado e clipes-base compartilhados por mais de um estado.

A sincronizacao percorrera o `AnimatorController` atual, inclusive Blend Trees aninhadas, e armazenara cada slot usando a referencia real do `AnimationClip` original. Assim, clipes diferentes chamados `Unreal Take` permanecem distintos.

### Aplicacao em runtime

No `Awake`/`OnEnable`, o componente criara um unico `AnimatorOverrideController` sobre o controller-base e aplicara todos os pares `clipe original -> clipe substituto` de uma vez. Campos vazios preservarao o clipe padrao.

O componente nao modificara o asset `.controller`; cada prefab ou instancia do heroi podera ter sua propria selecao. Se o Animator ja usar um `AnimatorOverrideController`, o sistema preservara o controller-base e combinara os overrides de modo deterministico.

Os scripts de combate continuarao apenas disparando parametros e triggers (`atacar`, `aparar`, `bloqueando`, etc.). Eles nao farao mais overrides isolados durante um golpe. A implementacao incompleta de `TrocadorDeAnimacoes` sera incorporada ou substituida sem descartar outras alteracoes existentes no projeto.

### Escopo das animacoes

O Inspector listara automaticamente tudo que existir no controller atual, incluindo:

- locomocao e os clipes internos de Blend Trees;
- saida do pulo, ar e queda;
- esquiva, rolamento e arranco;
- golpes 1 a 4 e futuros golpes adicionados ao controller;
- reacao a dano;
- aparo e bloqueio;
- contra-ataque;
- morte;
- futuros estados e execucoes adicionados ao controller.

Nao sera necessario adicionar um novo campo C# sempre que o Animator ganhar um estado.

### Cabeca decepada do goblin

Renomear `Cabeca_Decepada.prefab` para `Cabeca_Orc_Decepada.prefab` por meio do Unity Editor, preservando o GUID. Atualizar o montador da sandbox e as instancias de `EfeitoDeFinalizacao` dos goblins para usar esse prefab. `Cabeca_Sardael_Decepada.prefab` ficara reservado ao protagonista.

A correcao anterior de escala e seguranca de collider sera mantida. O prefab do orc tambem sera validado para garantir dimensoes fisicas humanas e impedir que a cabeca empurre ou lance o personagem.

## Tratamento de erros

- Controller ou Animator ausente: mostrar aviso no Inspector e nao aplicar overrides.
- Controller trocado depois da sincronizacao: mostrar aviso e exigir nova sincronizacao, sem perder silenciosamente os dados.
- Slot sem override: manter o clipe original.
- Clipe-base repetido em varios estados: mostrar que o override afetara todos os estados que compartilham exatamente a mesma referencia.
- Clipe incompativel: permitir que o Unity faça sua validacao normal e exibir aviso quando o tipo de rig nao for adequado, quando detectavel.

## Testes e validacao

- Teste EditMode confirma que dois clipes distintos com o mesmo nome sao substituidos pelas referencias corretas.
- Teste EditMode confirma que slots vazios mantem os clipes-base.
- Teste EditMode confirma sincronizacao de estados e Blend Trees.
- Teste EditMode confirma que a configuracao cria apenas um `AnimatorOverrideController` e pode reaplicar overrides.
- Teste EditMode confirma que o prefab de cabeca dos goblins usa `Cabeca_Orc.asset`, nunca `Cabeca_Sardael.asset`.
- Teste PlayMode/validacao ao vivo confirma que o parry selecionado no Inspector realmente toca.
- Validacao ao vivo da finalizacao confirma que a cabeca visual e a do goblin e que o protagonista nao sofre deslocamento anormal.

## Fora de escopo

- Alterar a logica de combos, janelas de aparo ou regras de dano.
- Substituir o sistema de Animator por Playables.
- Criar novas animacoes ou retargeting automatico de rigs incompatíveis.
