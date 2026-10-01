# Reproducao inversa individual no inspetor de animacoes

## Objetivo

Permitir que cada animacao listada no inspetor de `TrocadorDeAnimacoes` seja tocada de tras para frente por meio de um booleano individual, sem alterar globalmente o Animator Controller e sem interferir nas escolhas dos outros slots ou de outras instancias do heroi.

Esta extensao substitui o tratamento especial de parry invertido por um mecanismo generico, valido para aparo, ataques, locomocao, execucoes e quaisquer estados ou clipes de Blend Trees sincronizados pelo componente.

## Experiencia no inspetor

Cada entrada sincronizada exibira:

- caminho e nome do estado;
- clipe original somente leitura;
- campo `Usar no lugar` para escolher um clipe substituto;
- booleano `Tocar ao contrario`;
- referencia somente leitura ao clipe reverso gerado, quando aplicavel;
- aviso local quando a geracao nao puder ser concluida.

O booleano pertence ao proprio slot. Marcar o aparo nao afetara nenhum ataque, e duas instancias do heroi poderao ter configuracoes diferentes.

O botao `Limpar substituicoes` restaurara todos os slots ao comportamento padrao: clipe original, reproducao para frente e nenhuma referencia derivada ativa.

## Solucao escolhida

### Clipe reverso gerado no editor

Quando `Tocar ao contrario` estiver marcado, o editor determinara o clipe-fonte efetivo:

1. `clipeSubstituto`, quando preenchido;
2. caso contrario, `clipeOriginal`.

Em seguida, criara ou atualizara uma copia `.anim` com o conteudo temporalmente invertido. O clipe gerado sera salvo sob `Assets/_Sardael/Animacoes/Geradas/Reversas/` e identificado de forma estavel pelo GUID e file ID do clipe-fonte. A mesma origem podera reutilizar o mesmo asset reverso.

O clipe gerado continuara sendo reproduzido para frente pelo Animator. Seu conteudo, contudo, estara invertido. Isso evita depender de velocidade negativa, offset de entrada ou alteracoes no controller compartilhado.

### Conteudo invertido

O gerador copiara as configuracoes relevantes do clipe e invertera:

- todas as curvas de valores, espelhando cada keyframe para `duracao - tempo`;
- tangentes de entrada e saida, trocando seus lados e sinais conforme o espelhamento temporal;
- curvas de referencias a objetos, espelhando e reordenando seus keyframes;
- Animation Events, usando `duracao - tempo` e ordenando-os novamente;
- configuracoes de loop e wrap que sejam preservaveis pela API do editor.

Curvas constantes, propriedades musculares e root motion expostas pela API serao tratadas do mesmo modo. A implementacao validara o clipe importado usado no aparo para confirmar que as curvas necessarias ficam disponiveis no asset gerado.

### Resolucao em runtime

Cada `SubstituicaoDeAnimacao` armazenara:

- `clipeOriginal`;
- `clipeSubstituto`;
- `tocarAoContrario`;
- `clipeReversoGerado`;
- `assinaturaDaFonteReversa`, usada pelo editor para detectar reimportacoes.

Ao montar a lista do `AnimatorOverrideController`, `TrocadorDeAnimacoes` escolhera:

1. `clipeReversoGerado`, se `tocarAoContrario` estiver ativo e o asset for valido;
2. `clipeSubstituto`, se estiver preenchido;
3. o clipe original.

A aplicacao continuara em lote com um unico `AnimatorOverrideController`. O fluxo de combate permanecera responsavel apenas por parametros e triggers.

## Sincronizacao e regeneracao

O inspetor regenerara o derivado quando:

- o booleano for marcado;
- o clipe substituto do slot for alterado;
- o clipe-fonte tiver sido reimportado ou sua assinatura mudar;
- o usuario executar `Sincronizar com Animator`.

A assinatura combinara a identidade local do subasset com o hash de dependencia retornado pelo `AssetDatabase`. A comparacao sera feita quando o componente for desenhado ou sincronizado no editor; assim, uma reimportacao do FBX invalida o cache sem exigir alteracoes no runtime.

Desmarcar o booleano faz o runtime voltar imediatamente ao clipe normal. O asset em cache pode permanecer para reutilizacao; ele nao sera apagado automaticamente, evitando operacoes destrutivas e referencias quebradas.

A sincronizacao preservara `clipeSubstituto` e `tocarAoContrario` nos slots que ainda apontem para o mesmo clipe original.

## Compatibilidade com o parry reverso antigo

O mecanismo especifico `aparoAoContrario` nao deve participar do fluxo normal depois desta mudanca. O heroi sempre acionara o estado normal de aparo, e o novo slot individual decidira se o conteudo sera invertido.

O estado `AparoReverso` existente no controller podera permanecer temporariamente para compatibilidade com cenas antigas, mas nao sera disparado pelo codigo atualizado. Isso impede inversao dupla caso o clipe do aparo ja esteja marcado no novo inspetor.

Os montadores de cena e de aparo deixarao de configurar o booleano especial. Se ainda precisarem criar o estado legado por compatibilidade, ele nao sera usado no caminho padrao.

## Tratamento de erros

- Fonte ausente: desabilitar a opcao ou exibir aviso, sem aplicar um override quebrado.
- Falha ao criar ou atualizar o asset: manter a reproducao para frente e exibir o erro no slot e no Console.
- Clipe gerado desatualizado: tentar regenerar antes da aplicacao; se falhar, usar o clipe normal.
- Curva nao suportada: nao substituir silenciosamente. Registrar qual binding falhou e manter o slot no modo normal.
- Controller trocado: manter o comportamento atual de solicitar nova sincronizacao.
- Asset importado somente leitura: gerar a copia dentro da pasta propria do projeto, sem modificar o FBX original.

## Alternativas rejeitadas

### Velocidade negativa no AnimatorState

`AnimatorState.speed = -1` e suportado pela Unity, mas exigiria alterar ou duplicar estados do controller, iniciar a transicao no fim do clipe e coordenar offsets. Como o controller e compartilhado, isso tambem dificulta configuracoes diferentes por instancia.

### Playables em runtime

Controlar o tempo com Playables evitaria assets derivados, mas passaria a competir com a state machine existente. Eventos, root motion, transicoes e temporizacao do combate ganhariam complexidade desnecessaria para este escopo.

## Validacao pos-implementacao

Nao sera usado TDD, conforme solicitado. A validacao sera feita depois da implementacao com testes basicos:

- recompilar os scripts e confirmar que o Console nao possui novos erros;
- confirmar que todos os slots exibem o booleano individual;
- selecionar outro clipe para o aparo, marcar `Tocar ao contrario` e confirmar que o derivado e criado;
- comparar a pose inicial do reverso com a pose final do clipe-fonte e vice-versa;
- entrar em Play Mode e confirmar que o estado `Aparo` usa o clipe reverso;
- desmarcar o booleano e confirmar que o mesmo slot volta a tocar para frente;
- manter outro slot desmarcado e confirmar que ele nao foi afetado;
- verificar que Animation Events continuam ocorrendo em ordem temporal invertida;
- confirmar que o protagonista nao recebe deslocamento ou impulso inesperado durante aparo e execucao;
- salvar e reabrir a cena, confirmando a persistencia da configuracao.

## Fora de escopo

- Retargeting automatico de rigs incompativeis.
- Edicao manual dos assets FBX originais.
- Substituicao do Animator por Playables.
- Alteracoes nas regras de dano, combo, janela de parry ou execucao.
- Remocao automatica de clipes reversos que nao estejam mais em uso.
