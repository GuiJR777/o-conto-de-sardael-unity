# PLANO DE TRABALHO — de "cenário pronto" até combate jogável

**A regra deste plano:** nenhuma onda começa sem que a anterior tenha uma prova gravada. Cada item tem arquivo, número e um "PRONTO QUANDO" que se verifica rodando o jogo, não lendo código.

**A leitura de fundo, em três linhas.** O movimento do herói já está bem construído para receber combate: `MovimentoDoHeroi.cs` tem trava por dono (`travas`, linha 95-101), tem `EmAcao` decidido pela TAG do estado do Animator (linha 108-121), tem root motion aplicado só durante a ação (`deslocamentoDaAnimacao`, linha 240-243) e tem `Injetar*` para o teste entrar pelas mesmas variáveis da tecla (linha 275-280). Isso é o trilho do combate e está de pé. O que falta não é arquitetura de movimento — é **decisão**, **entidade de inimigo** e **bancada**.

---

## STATUS ATUAL — COMBATE FREE FLOW (2026-09-21)

> Este bloco registra o estado executável atual e prevalece sobre recomendações históricas conflitantes nas ondas abaixo. A decisão vigente é combate lateral em uma única linha: movimento físico em X, altura em Y e Z fixo.

Cena de desenvolvimento: `Assets/_Sardael/Cenas/Combate_Sandbox.unity`.

### Concluído

- [x] **Fase A — Inspeção:** scripts legados, Animator Controllers, Input Actions, cenas e baseline de compilação revisados.
- [x] **Fase B — Sandbox:** cena isolada com Sardael real, câmera lateral, chão, limites e três inimigos de cada lado.
- [x] **Fase C — Input:** `EntradaDeCombate` usa as actions existentes `Player/Move` e `Player/Attack` do New Input System.
- [x] **Fase D — Targets:** `AlvoDeCombate` e `RegistroDeCombate` usam posição física e ordenação por distância em X.
- [x] **Fase E — Ataques:** quatro assets `DefinicaoDeAtaque`, preservando os alcances medidos dos quatro golpes.
- [x] **Fase F — Combate do herói:** escolha contextual esquerda/direita, alvo estável durante o golpe, dano e reação básica.
- [x] **Fase G — Lunge:** aproximação suave pelo `MovimentoDoHeroi` e `CharacterController.Move`, sem teleporte e sem deslocamento em Z.
- [x] **Fase H — Troca de alvo:** combo ping-pong E3 → E1 → E3 validado em Play Mode. A fila preserva o primeiro comando pendente de cada elo sob spam.
- [x] **Fase I — Multi-target:** o combo comum usa somente `Single`, `FrontTwo` e `FrontThree` à frente da direção escolhida. `BothSides` e `Piercing` permanecem disponíveis apenas para habilidades futuras.
- [x] **Fase J — Displacement:** o combo comum aplica somente `Push` leve. `Pull`, `Launch`, `CrossSide`, `KnockThrough` e `PushPlayer` permanecem disponíveis apenas para habilidades futuras.
- [x] **Fase K — Collision chain:** inimigo lançado contra vizinho encerra o deslocamento, recebe reação forte e aplica stagger previsível no atingido.
- [x] **Fase L — Combat Director:** um único turno global de ataque; as duas filas mantêm pressão, mas somente um inimigo pode avisar ou golpear por vez.
- [x] **Fase M — Flow:** ganho confirmado, bônus multi-hit, delay/decay, penalidade de dano, multiplicador de parry e consumo explícito. Flow cheio não dispara nada sozinho.
- [x] **Fase N — Execution:** `Player/Execution` reserva, alinha e trava o par; reutiliza controladores pareados e `EfeitoDeFinalizacao`, mata o alvo e libera o estado. `Player/FlowSpecial` oferece Crowd Knockdown como escolha alternativa.
- [x] **Fase O — Fila viva:** todos os inimigos recebem uma posição por lado e se reorganizam quando o herói anda, um inimigo morre ou a ordem física muda.
- [x] **Fase P — Ataques inimigos:** um único portador de turno global exibe aviso visual antes do golpe e aplica dano no frame de impacto medido da animação.
- [x] **Fase Q — Defesa e contra-ataque:** `Player/Block` permite bloqueio direcional; o atacante reage imediatamente ao aparo, que abre uma janela curta em que Attack executa contra-ataque de 4 de vida e 100 de poise.
- [x] **Fase R — Vida, stun e morte:** vida/dano/morte do herói, quebra e recuperação de poise, stun e animação de morte dos inimigos.
- [x] **Fase S — Execução contextual:** inimigo atordoado dentro do alcance exibe `E`; `Player/Interact` reserva o par e executa a animação de finalização sem consumir Flow.
- [x] **Fase T — Perda de turno:** o portador do turno atingido interrompe aviso/golpe, perde o token e fica temporariamente inelegível enquanto outro inimigo assume.
- [x] **Fase U — Cancelamento defensivo:** direção oposta + Block cancela combo, impacto e root motion pendentes, vira o herói e entra imediatamente em `Bloqueio`.
- [x] **Fase V — Locomoção da fila:** reposicionamento usa `AndarFrente`/`AndarTras`, voltando a `Parado` ao alcançar a posição.
- [x] **Fase W — Finalizações no chão:** `Takedown_DoubleLeg_Start` derruba o alvo e uma entre quatro finalizações do setor `Chao — FINALIZACOES (Full Mount)` é sorteada sem repetição imediata: KnifeStab, SlashNeck, AxeToFace ou HeadSmash.

### Validação concluída na sandbox

- [x] Ataque sem inimigos próximos.
- [x] Um inimigo à direita e um à esquerda, isoladamente.
- [x] Dois inimigos de cada lado.
- [x] Inimigo fora do alcance magnético.
- [x] Troca de lado durante o combo.
- [x] Alvo morto/inválido antes do impacto.
- [x] Inimigos muito próximos.
- [x] Lunge bloqueado por collider.
- [x] Spam de Attack sem sobrescrever o alvo pendente.
- [x] Multi-target 1/2/2/3 e Piercing com três inimigos na frente.
- [x] Combo frontal D-D-D-D: nenhum inimigo atrás recebeu dano, poise, reação ou deslocamento; os alvos à frente receberam dano, perda de poise, reação e `Push` leve.
- [x] Os seis tipos de deslocamento, reordenação física da fila e colisão em cadeia.
- [x] Diretor limitado a 1 turno global; mesmo cercado, nunca há dois avisos ou golpes inimigos simultâneos.
- [x] Combo completo gerando 50 Flow; penalidade de dano reduzindo 40 → 15.
- [x] Execution via Input Action: 1 alvo reservado, efeito disparado, registro 6 → 5 e herói liberado.
- [x] Catálogo de execução contém quatro pares Full Mount sincronizados; o finalizador usado fica exposto no painel e um toque curto em `E` é aceito mesmo durante o fim de uma ação do herói.
- [x] Crowd Knockdown via Input Action: cinco alvos restantes atingidos e Flow consumido.
- [x] Herói e todos os inimigos permaneceram em Z = 0 nos testes das fases I–N.
- [x] Fila dos dois lados convergiu para posições ordenadas e somente um inimigo recebeu o turno global de ataque.
- [x] Ataque inimigo mostrou telegraph, disparou o estado `Golpe` e retirou 18 de vida no impacto.
- [x] Bloqueio frontal anulou dano, fez o atacante reagir ao aparo e abriu contra-ataque; o contra retirou somente 4 de vida, zerou poise e disparou `Atordoado`.
- [x] Mortes do herói e do inimigo entraram no estado `Morte` e interromperam movimento/ataques.
- [x] Inimigo atordoado exibiu um único indicador `E`; a tecla real iniciou a execução pareada, matou 1 alvo e ocultou o prompt.
- [x] Portador atingido registrou 1 turno perdido, ficou sem token e o Diretor entregou o único token global a outro inimigo.
- [x] Durante `Golpe1`, A+F cancelou o ataque, mudou o lado de +1 para -1 e entrou em `Bloqueio` antes do impacto.
- [x] Cinco inimigos entraram simultaneamente em movimento de fila e os ciclos de caminhada foram confirmados visualmente.
- [x] Compilação final e Console sem erros ou warnings novos do combate.

### Próximas integrações — fora da sandbox

- [ ] Ligar `RegistrarParry` e `RegistrarDanoRecebido` aos futuros caminhos definitivos de defesa/vida do herói.
- [ ] Fazer rodada de tuning humano de deslocamentos, cadência do Diretor, decay e custo de Flow.
- [x] Escolher e montar quatro pares de execução da `Finishers_Sardael`, preservando reação, distância e duração de cada par.
- [ ] Integrar o sistema em um encontro real somente depois do aceite de feeling na sandbox.
- [ ] Criar HUD definitivo de vida/Flow e substituir o painel IMGUI de laboratório.

### Próximo gate

Jogar a sequência completa na sandbox e ajustar humanamente `alcanceMagnetismo = 4,75 m`, `velocidadeAproximacao = 10 m/s`, deslocamentos, distância de pressão, decay e timings da execução. Não integrar em cenas narrativas antes desse aceite.

---

## ONDA 0 — DECIDIR (meio dia, zero linha de código)

Estas quatro decisões são as que custam caro depois. Nenhuma delas é código; todas são uma página de texto e dois campos no Inspector.

### D1. Linha ou pista? (o eixo Z)

Hoje: `MovimentoDoHeroi.Mover` (linha 250-256) faz `cc.Move(new Vector3(velX*dt + passo, velY*dt, 0f))` e logo em seguida `p.z = zDaLinha`. O Z está zerado em dois lugares. Ao mesmo tempo, `Floresta_Trecho_01` tem `Trilho_Fundo` em z=+2,2 e `Trilho_Frente` em z=−2,2, **os dois com BoxCollider sólido**, formando um corredor de 4,4 m que ninguém pode encostar. O cenário foi montado para pista; o código é linha.

**Minha recomendação: PISTA, faixa estreita (z de −2,2 a +2,2), crescendo para o lado da câmera.** Motivos concretos, não de gosto:
- Três das 15 missões da run só existem com Z: *A Vigia* (esconder-se atrás de tronco), *O Rebanho do Véu* (imobilizar-se enquanto os bichos passam), *A Corrida do Fôlego* (desviar sem revidar). Em 1-D elas viram "ande para trás".
- Em 1-D, 4 orcs viram fila: o alcance de 1,756 m do `Duelo` passa a ser o jogo inteiro, a esquiva só pode ser para trás, e a câmera lateral não tem nada para enquadrar. Isso é jogo de luta, não beat-em-up.
- O lado da pista que dá para a câmera está **vazio**: dos 227 objetos de cenário de Bael, 5 estão em z entre 0 e 2, e **zero** entre −2,2 e 0. Há 2,2 m livres esperando.

**Se escolher pista, o custo é pequeno e está localizado:** `Mover` passa a somar `velZ*dt` no Vector3 e o clamp vira `p.z = Mathf.Clamp(p.z + velZ*dt, zMinimo, zMaximo)`; `LerEntrada` ganha W/S no mesmo padrão de A/D. É meia hora. **Se escolher linha, apague os dois trilhos de Bael hoje**, porque eles documentam uma intenção que o código nega e vão enganar você daqui a três meses.

### D2. O mapa de botões único — teclado, gamepad e toque na MESMA tabela

Este é o item que a pergunta do porte para mobile realmente cobra. Hoje `MovimentoDoHeroi.LerEntrada` (linha 305-315) lê `Keyboard.current` direto, tecla por tecla. Se o combate nascer do mesmo jeito, cada verbo novo (atacar, aparar, encadear, executar) fica soldado ao teclado, e o porte para mobile e o suporte a gamepad viram reescrita do combate inteiro.

Três números a fixar hoje, numa tabela de uma página:

| decisão | por que agora |
|---|---|
| **Quantos verbos, no máximo** | Um polegar direito confortável tem **3 botões**, quatro no limite. Hoje o painel já anuncia 7 teclas (A/D, Shift, Espaço, Ctrl, C, Alt, E). Se o combate somar golpe + aparar + executar, são 10. Decida quais verbos são **contextuais** (mesmo botão, ação diferente conforme a situação — aparar e esquivar podem ser o mesmo botão; executar pode ser o golpe apertado sobre inimigo cambaleante). Cortar verbo depois de animado é jogar trabalho fora. |
| **Janela mínima de tempo, em SEGUNDOS** | Nunca em quadros. O toque em celular tem 40-80 ms de latência a mais que o teclado. Fixe o piso: **nenhuma janela de acerto abaixo de 0,20 s**. Se o aparar da missão 11 nascer com janela de 6 quadros (0,10 s), ele funciona no seu PC e é impossível no celular — e você só descobre no porte. |
| **1 eixo ou 2** | Sai direto de D1. Define se o controle de toque é um botão-de-direção ou um analógico. |

Ferramenta: `Assets/Settings/InputSystem_Actions.inputactions` já existe, com 42 bindings de gamepad, e está **órfão** — nenhum script o usa. É ele que deve virar a única porta de entrada, com `MovimentoDoHeroi` chamando os `Injetar*` que já existem. Você não precisa reescrever o movimento: precisa trocar o leitor.

### D3. Uma página escrita: `Assets/_Sardael/COMBATE.md`

Hoje o `LEIA-ME.md` tem uma seção "O que ainda não existe" com **dois itens: combate e som**. Não existe uma linha no projeto dizendo como o combate funciona. A maior etapa do projeto ia começar sem parágrafo de especificação.

Preencha estes campos. Todos têm ou já um número medido, ou uma escolha de uma linha:

- **Alcance do golpe:** 1,756 m (já medido, `Duelo.cs:41`), folga 0,70 m (`Duelo.cs:51`).
- **Vida:** do herói, do orc comum, do alfa, do troll. Em número de golpes, não em pontos abstratos ("o orc comum morre em 3 golpes" é especificação; "100 HP" não é).
- **Dano por elo do combo:** o encadeamento da missão 12 precisa que o 3º golpe valha mais que o 1º, senão não há motivo para encadear.
- **Quantos inimigos podem atacar ao mesmo tempo:** a regra do anel do beat-em-up. Sugestão de partida: 6 na arena, **2 autorizados a atacar**, o resto circula.
- **Janela de encadeamento** e **janela de aparar**, em segundos, respeitando o piso de D2.
- **Invencibilidade da esquiva:** `Dodge_Bw_RM` dura 0,600 s. Quantos desses segundos são intocáveis? (Sugestão: 0,25 s começando em 0,08 s.)
- **O que a morte custa:** em segundos até estar de pé lutando de novo. Hoje custa ~55 s de cutscene mais dois carregamentos. Num roguelike o número aceitável é **menos de 5 segundos**.
- **Quando entra o finisher:** limiar de vida, ou último elo do combo, ou golpe em inimigo cambaleante (`Stun01` já está levantado na bancada).

### D4. O item de 5 minutos que vai junto (e só custa mais caro depois)

`ProjectSettings.asset:15-16`: `companyName: DefaultCompany`, `productName: o conto e sardael` (falta o "d"). Isso decide o `Application.persistentDataPath`, ou seja, a pasta onde os saves moram. Existem hoje `sardael_partida_0.json` e `sardael_partida_1.json` em `AppData/LocalLow/DefaultCompany/o conto e sardael/`. **No dia em que você corrigir o nome, esses saves ficam órfãos e somem sem erro** (`Salvao.cs:80` devolve ficha vazia em silêncio — o menu desenha três pergaminhos em branco).

Enquanto só existem saves de teste, é um campo no Player Settings. Depois do primeiro jogador externo, é código de migração.

Decida também **qual dos três nomes é o nome**: a arte do menu (`Arte/Menu/Menu_Fundo.png`) diz *"o conto de SARDAEL — as crônicas de valdória"*; o `productName` diz "o conto e sardael"; o briefing diz "A Lenda de Sardael". O título só existe como pixels dentro de um PNG — a cena `Menu_Principal` não tem nenhum texto de título. Trocar o nome hoje é editar um campo; depois é repintar arte.

> **PRONTO QUANDO:** existe `Assets/_Sardael/COMBATE.md` com os 9 números preenchidos e a tabela de botões nas três entradas; o `LEIA-ME.md` aponta para ele; e Player Settings mostra o nome e a empresa definitivos.

---

## ONDA 1 — LIMPAR A BANCADA (1 a 2 dias)

Tudo aqui é ferramenta para você, não conteúdo para o jogador. É o que faz a Onda 2 andar rápido em vez de travar.

### 1.1 O herói vira um prefab de verdade
`Personagens/Sardael.prefab` tem 110 objetos, 0 MonoBehaviour e `m_Controller: {fileID: 0}`. Todo o herói é override de instância, refeito à mão em 4 cenas — e as cápsulas **não coincidem**: Halu e Fazenda são `height 1,7716 / radius 0,30 / step 0,35`; a Floresta é `1,8246 / 0,28 / 0,42`; a bancada é `1,75 / 0,32 / 0,35`. São três corpos para um herói.

A cápsula é a unidade do combate: é o espaçamento entre corpos, é o que o golpe alcança, é o que empurra. **Ajustar distância de golpe na Floresta (raio 0,28) e mandar para Halu (raio 0,30) é afinar três jogos.** Ponha no prefab: CharacterController com UMA medida, Animator com o controller, `MovimentoDoHeroi`, `Interacao`, `PoeiraNoPe`, `LancaNaMao`. As cenas só instanciam e posicionam.

### 1.2 Um controller de herói, não três
`Sardael_Halu.controller` (3 cenas jogáveis) declara `esquivar`, `rolar` e `arrancar` mas **não tem os estados** — CTRL, C e ALT não fazem nada, e o painel na tela anuncia as três. `Sardael_Movimento.controller` tem os três estados prontos, com os clipes medidos (Dodge_Bw_RM 0,600 s / Roll_Fw_RM 0,933 s / Dash_Fw_RM 0,767 s) e está só na bancada. `Sardael_Combate.controller` tem os três mais `atacar` e `atingido`, e **não está em cena nenhuma** — e lhe falta o parâmetro `interagir`, que `Interacao.cs:99` seta.

Funda os três num só, com `interagir` incluído, e use-o nas três cenas. Enquanto houver dois controllers de herói, toda habilidade nova é feita duas vezes e vai faltar numa delas.

Atenção a um obstáculo de caminho: `Scripts/Editor/MontarMovimento.cs:37-38` aponta para `"Assets/SpearCombatAnimationV2/..."` e o pacote está em `"Assets/_Pacotes/SpearCombatAnimationV2/..."` — rodar o montador hoje só imprime "nao achei". Corrija o prefixo antes de usar a ferramenta.

### 1.3 A bancada de combate salva em disco
`Scripts/Editor/MontarCombate.cs:28` aponta para `Cenas/Combate_Duelo.unity`. **Esse arquivo não existe.** O protótipo inteiro só existe como cena gerada por menu, que some ao fechar o projeto. `Duelo.cs`, `TesteDeCombate.cs`, `AvisoDoAnimator.cs`, `AvisoNoImpacto.cs`, `CorpoDoOrc.cs`: GUID em nenhuma cena, nenhum prefab.

Salve `Combate_Duelo.unity` de verdade em `Cenas/` (fora do Build Settings, como as outras bancadas), com **R para reiniciar o encontro**, dois orcs, e o `RelatorioNaTela` ligado. Sem isso, cada ajuste de número custa atravessar Halu ou pagar a volta do laço do Véu.

### 1.4 A tecla de pular fala — ferramenta antes de ser conforto
Grep por `Keyboard` em `FalaNaTela.cs`, `CenaDaMorte.cs`, `AcordarSardael.cs` e nos três diretores: **zero**. Uma volta do laço custa ~55 s de tela travada (6,7 s em Bael + ~25 s do Véu + ~23,6 s do acordar), mais dois carregamentos, mais ~98 m de caminhada de x=55 até o portão sul (x=152,98).

Você vai pagar esse pedágio dezenas de vezes por dia testando combate. Duas peças pequenas:
- Um `EsperarOuApertar(float segundos)` em `FalaNaTela` que sai no tempo **ou** no Espaço, substituindo os `WaitForSeconds(duracaoDeCadaFala)` dos três diretores.
- `CenaDaMorte` lê ESC/Espaço no Update e chama `aoAcabar.Invoke()` — o evento já está ligado a `VoltaDoVeu.Voltar` na cena. Mostrar "ESPAÇO para pular" a partir da segunda vez, usando `Salvao.MortesNoVeu`, que já é gravado.

### 1.5 As três limpezas de 10 minutos que sujam toda gravação sua
Sua regra é rodar, gravar e assistir. Estas três estragam exatamente a gravação:
- **`FotografoDaCena` está LIGADO** em `Morte_Do_Veu.unity:1018`, cena de índice 4 do Build Settings, gravando 6 PNGs em `C:/Users/Desktop/AppData/Local/Temp/claude/.../scratchpad/`. Sem nenhum `#if UNITY_EDITOR` no arquivo. Cada instante faz RenderTexture 1280x720 com MSAA 4x + ReadPixels síncrono + EncodeToPNG na thread principal. `m_Enabled: 0` e embrulhe a classe em `#if UNITY_EDITOR`.
- **Sete painéis de depuração IMGUI ligados** nas 4 cenas jogáveis do build, dois por cena empilhados no mesmo canto (10,10). Trocar a guarda de cada `OnGUI` por `if (!mostrarPainel || !Debug.isDebugBuild) return;`.
- **`Application.targetFrameRate` não existe** em lugar nenhum, `vSyncCount: 0`, `runInBackground: 1`. O jogo desenha o mais rápido que a placa aguenta, inclusive minimizado. Uma linha.

### 1.6 O EventSystem — o bug que só aparece no build
`GarantirEventSystem()` (`TelaDePausa.cs:239`) só é chamada no `Montar()`, que roda uma vez, no `Awake`, **depois da primeira cena**. No build a primeira cena é `Menu_Principal`, que tem EventSystem próprio — então a checagem acha um e não cria nada. Quando o menu descarrega para entrar em Halu, esse EventSystem morre junto e ninguém cria outro. **Nenhuma cena de jogo tem EventSystem** (contei: Halu=0, Fazenda=0, Floresta=0, Véu=0).

Resultado no build: a partir do momento em que o jogador sai do menu, o painel de pausa aparece e **nada nele responde a clique** — nem VOLTAR AO MENU, nem SAIR DO JOGO. Só Alt+F4. E isso nunca apareceu nos seus testes porque abrir Halu direto no Editor faz o caminho **inverso**: não acha EventSystem, cria um, e tudo funciona.

Conserto: chamar `GarantirEventSystem()` também em `AoTrocarDeCena` (linha 112-116).

> **PRONTO QUANDO:** um build do Windows roda, você entra em Halu pelo menu, aperta ESC e **clica** em VOLTAR AO MENU; uma gravação da volta do Véu sai limpa, sem painel de debug e sem PNG novo na pasta; `Combate_Duelo.unity` abre do disco, R reinicia, e arrastar a cápsula do prefab do herói muda o alcance nas três cenas de uma vez.

---

## ONDA 2 — O ESQUELETO DO COMBATE (a semana grande)

Ordem obrigatória. Cada peça depende da anterior.

### 2.1 `Vitalidade` — a peça de raiz
Hoje não existe uma única variável de vida, dano ou morte nos 49 scripts de runtime. `Duelo.cs:163` resolve um acerto fazendo `GolpesQueAcertaram++`. `Combatente.cs:16` declara no cabeçalho: *"Ninguem morre: nao existe clipe de morte nesta lista, de proposito."*

Sem vida nenhum encontro pode terminar: o jogador bate para sempre, o orc bate para sempre, não há motivo para desviar.

Componente: `vidaMaxima`, `vidaAtual`, `Levar(dano, direcao)`, `Morreu`, evento `AoMorrer`. **E `AoMorrer` nasce com DOIS ramos desde a primeira linha** — morte comum e execução. Não deixe o finisher para depois: ele é o pagamento do gênero e já está pronto (ver 2.4).

### 2.2 `Inimigo` — inverter a posse
Hoje `Duelo` mora no **herói**, guarda UM `doOrc`, e se registra em `Duelo.Atual` (estático, sem proteção no `Awake`). `AvisoDoAnimator.OnStateEnter` roteia todo aviso de animação para `Duelo.Atual` — passa o Animator de quem avisou, mas `Duelo.AoComecarEstado` **descarta esse parâmetro**. Com dois orcs na cena, o último a acordar vira o único árbitro e os avisos dos dois caem nele.

O árbitro sai do herói e vira um componente **por boneco**: alvo, alcance, vitalidade, e uma máquina simples de aproximar / telegrafar / golpear / recuar. `AvisoDoAnimator` passa o próprio Animator como remetente — o parâmetro `quem` já chega, só não é usado.

**O orc também não tem para onde andar:** `Orc_Combate.controller` tem 4 estados — `Parado`, `Golpe`, `Levar`, `LevarForte`. Não há andar nem correr. Mesmo com IA pronta amanhã, ele desliza. Acrescente locomoção ao controller junto com a IA, não depois. (O que está certo e ninguém registrou: as reações dele são `Hit_Fw_RM` e `Hit_Bw_RM` **do pack da lança**, pareadas com os golpes de Sardael. Isso é bom trabalho, mantenha.)

### 2.3 `Encontro` — a peça que diz quando a jaula abre
Hoje a arena da Fazenda abre e fecha **por relógio**: `TravarACamera()`, `AoLiberarOsOrcs()`, `WaitForSeconds(2,2)`, aviso de 5,5 s, fim. Entre abrir e fechar não há nenhuma condição.

`Encontro`: lista de inimigos, condição de vitória (todos mortos), condição de derrota, eventos `AoVencer` / `AoPerder`. A jaula da Fazenda e a saída de Bael passam a ser `AoVencer`, não `WaitForSeconds`. Aqui mora a regra do anel definida em D3 (quantos atacam ao mesmo tempo).

### 2.4 A ponte com os finishers — o contrato, não o código
`Cenas/Finishers_Sardael.unity` é a **segunda maior cena do projeto** (153.287 linhas, maior que a Fazenda): 54 `TocadorDeClipe`, 52 clipes distintos já levantados — 1H, 2H, duas armas, lança, escudo, desarmado, `Parry2H01`/`ParryDW01`/`ParryPolearm01`, `Stun01`, `CombatDamage01/02`, `CombatDeath01/03`, `IdleWounded01`, mais `Hit_Fw`/`Hit_Bw`/`HitDeath_Fw`/`HitDeath_Bw`. Há 4 `EfeitoDeFinalizacao` montados, um com `decapitar: 1`, com a cabeça saindo pela linha de jogo.

**Isso é a maior peça de P&D de combate do projeto e está encostada fora do Build Settings.** O `instante` fracionário do `EfeitoDeFinalizacao` já resolve o sincronismo. Não precisa de código novo de animação — precisa que `AoMorrer` saiba chamar.

Duas armadilhas suas, já conhecidas, que valem aqui: os `.fbx` pareados vêm com `loopTime` ligado, e **travar no fim de clipe em loop mostra o quadro zero — o morto levanta**. Use `SegurarUltimoQuadro.cs` (que já roda na Morte_Do_Veu) e desligue `loopTime` no import, **antes** de montar a cena. E `Combatente.cs` avisa: dois PlayableGraph no mesmo Animator dão T-pose.

### 2.5 A morte do herói já está animada
`HitDeath_Fw_RM` e `HitDeath_Bw_RM` são direcionais e estão levantados na bancada. `Sardael_Combate.controller` tem 11 estados incluindo `Atingido`, mas **nenhum de morte**. `DiretorDeBael.Matar()` zera o `runtimeAnimatorController` justamente porque não há estado para onde ir. A peça que falta é menor do que parece: um estado, direcional.

> **PRONTO QUANDO:** em `Combate_Duelo`, três orcs acordam, se aproximam andando (não deslizando), dois atacam por vez, você mata os três, o último morre por execução com a cabeça saindo pela linha de jogo, e a tela mostra VENCEU. R reinicia. Existe uma gravação disso, assistida, não uma foto do Editor.

---

## ONDA 3 — O COMBATE ENTRA NO JOGO

### 3.1 Consertar a jaula da Fazenda ANTES de ligar o gancho
O `LEIA-ME:66` manda ligar o combate em `DiretorDoCapitao.AoLiberarOsOrcs`. No dia em que ligar, o jogador fica preso numa caixa de 12,4 m **onde não há um único orc**.

A conta: a marcha para quando `dist(Sardael, troll) <= 11`; o troll está em (35,78 / z 18,42) e o herói anda na linha z=18, então ele congela em **x = 46,77**. `TravarACamera` monta a jaula em torno do X **onde a câmera parou** — `46,77 − 2,2 (olharAFrente) = 44,57` — logo a jaula é `[38,4 ; 50,8]`. Os três `orcsDoDesafio` estão em x=33,66, 33,74 e 31,77: o mais próximo fica **4,63 m além da parede oeste**. E nenhum deles está na linha de jogo (z 16,41 / 16,89 / 19,62 contra `zDaLinha: 18`). Varrendo a cena, o único objeto dentro da jaula é `Forcado_largado`.

Conserto de números, não de arquitetura: centrar a jaula na média dos X dos `orcsDoDesafio` (ou no troll caído) em vez de no X da câmera, e plantar os três em z=18. Enquanto a arena for "onde a câmera por acaso parou", ela muda de lugar toda vez que alguém mexer em `olharAFrente` ou em `distanciaQueChamaOTroll`.

Nota: `AoLiberarOsOrcs()` hoje só faz `SetActive(true)` e os três já estão ativos — **a função não faz nada**. É ela que passa a montar o `Encontro`.

### 3.2 Morte de combate ≠ morte narrativa
Separe os dois desde já. A morte de Bael é roteiro e deve continuar. A morte de combate precisa de: tela de derrota curta, "tentar de novo", e um ponto de retorno. Meta de D3: **menos de 5 segundos** entre morrer e estar lutando de novo.

### 3.3 O save ganha posição
`FichaDeJogo` (`Salvao.cs:9-19`) tem 7 campos: existe, etapa, cena, segundos, mortesNoVeu, salvoEm, criadoEm. **Nenhuma posição.** O próprio código admite a consequência em `TelaDePausa.cs:195`: *"a posicao na cena se perde; a historia nao"*. Sair do jogo no meio de uma luta devolve o jogador ao ponto de nascimento da cena com a luta reiniciada do zero.

Acrescente `x` (e `z`, se D1 for pista) e o ponto de retorno do encontro.

### 3.4 HUD — o primeiro Canvas de jogo do projeto
Nenhuma das três cenas jogáveis tem Canvas de jogo. A palavra "Canvas" não aparece em `Tutorial_Fazenda.unity` nem em `Floresta_Trecho_01.unity`; em Halu o único é o `Canvas_Branco` do sonho. Não há UI Toolkit em lugar nenhum (zero `.uxml`, `.uss`, `UIDocument`).

Um prefab HUD no padrão dos outros canvases do projeto (ScaleWithScreenSize, 1920x1080, match 0,5), auto-instanciado por `RuntimeInitializeOnLoadMethod` como a `TelaDePausa`, escondendo-se no menu e nas cutscenes: barra de vida do herói, barra do inimigo mirado. Desenhe-o já com a área segura em mente (ver Onda 6).

> **PRONTO QUANDO:** você entra na Fazenda pelo menu, marcha, vê o duelo do capitão, e **luta com os três orcs dentro da jaula**; morre de propósito e volta a lutar em menos de 5 s; fecha o jogo no meio da luta, reabre pelo pergaminho e volta para a mesma luta, não para a entrada da fase.

---

## ONDA 4 — O FLUXO FECHA O LAÇO

Agora que o combate existe, o laço infinito tem para onde ir.

**4.1 Bael deixa de ser beco.** A Floresta tem **ZERO `PortaDeCena`** — nem solta, nem em prefab. A única linha que carrega outra cena é `DiretorDeBael.cs:148`, dentro do `Roteiro()`, que só roda se `matarSempre` for verdadeiro. **Desligue `matarSempre` hoje e o jogo vira um corredor de 100 m sem saída**, com ESC > voltar ao menu como única porta. Ponha a `PortaDeCena` no fim da faixa (a parede `Fim_Direita` já está em x=102 e serve de `paredeQueSai`), liberada pelo `Encontro.AoVencer`.

**4.2 A etapa que falta no enum.** `Progresso.Etapa` acaba em `VoltouDoVeu = 50`, e o grep `Progresso.Onde =` devolve 5 escritas, nenhuma acima de 50. **Não existe estado que signifique "sobreviveu a Bael".** Acrescente `VenceuEmBael = 60` e o ramo correspondente no `Start` do `DiretorDeHalu` (hoje as linhas 143-145 só distinguem `==40` e `>=20`).

**4.3 A regressão da etapa 50.** Tanto 30 quanto 50 caem em `SegundaMissao()`. Quem já morreu no Véu e fechou o jogo volta a ser um Sardael que nunca morreu: o chefe dá o briefing de primeira vez, o herói é replantado em `x = −26,19 + 3 = −23,19` (porque `pontoDeChegadaDoTutorial` está `{fileID: 0}` na cena), **179 m do portão sul onde ele estava**, e a linha 253 reescreve a etapa de 50 para 30. Pior: `mortesNoVeu` não é zerado, então na morte seguinte o chefe responde *"o mesmo sonho. De novo."* logo depois de ter dado a fala de primeira vez. A contradição fica na tela.

**4.4 O save não deve gravar cutscene.** `AoCarregarCena` grava `ficha.cena = cena.name` em toda cena que não é o menu — inclusive `Morte_Do_Veu`. Alt+F4 durante a cutscene basta. Continuar devolve o jogador para dentro de 25 s de vídeo. Filtre a cena do Véu.

**4.5 Bael passa a ser honesta enquanto não estiver pronta.** Se a emboscada continuar matando sempre por um tempo, copie o padrão que já funciona na Fazenda: `DiretorDoCapitao.cs:351` diz *"— ainda em construção — O combate ainda não foi criado."* Bael não diz nada. O jogador morre sem barra de vida e sem tecla de ataque e conclui que **ele** errou. Amarre o aviso ao mesmo `matarSempre`: quando ele for a 0, o aviso some junto.

**4.6 O chicote da câmera.** Em `DiretorDeHalu.Acordar()` a câmera é reacendida (linha 323) **um quadro antes** de o herói ser teleportado (linha 276). Os dois estavam em x=4; o chefe está em x=55. O branco já sumiu. O jogador vê a aldeia vazia por um quadro e a câmera varre 51 m. Inverta a ordem e dê à `CameraQueSegue` um `Assentar()` que faça `transform.position = Onde()` de estalo. Acontece em 100% das voltas.

**4.7 O `Interagivel` que não existe.** O GUID `eb538df404a2dae469718c0c2056d01f` aparece **uma vez** em todo o `Assets/`: no próprio `.cs.meta`. Zero instâncias. E `Interacao.cs` desenha exatamente a UI que falta — balão com `"E   " + verbo`, nome em dourado, caixa com rodapé "1 / 3 — E para continuar", avanço por E/Enter/gamepad. Está montado no Sardael de Halu e da Fazenda varrendo uma lista permanentemente vazia. Ponha `Interagivel` em 3 ou 4 alvos de Halu e a peça inteira acende: balão, contador, o E do painel, e o estado `Interagir` do controller.

> **PRONTO QUANDO:** uma partida do zero vai do menu até vencer em Bael, sair pela porta e voltar a Halu com o chefe reconhecendo a vitória — sem nenhuma etapa andando para trás, sem teleporte de 179 m, e com a gravação da volta do Véu abrindo já enquadrada no chefe.

---

## ONDA 5 — A RUN DE BAEL

Aqui entra a trilha das 15 missões. Antes de escrever qualquer uma, três medições que mudam o plano:

**5.1 Você montou 122 m e o jogador vê 11.** SARDAEL nasce em x=2 e morre em x≈12: **9% do que foi montado à mão**. E os 122 m são uniformes — os 207 chãos vão de x=−5 a x=105, 18 por faixa de 10 m, iguais do começo ao fim; os outros 227 objetos, entre 11 e 23 por faixa, densidade constante. Não há uma clareira, uma ponte, uma ruína, uma subida. Os únicos props narrativos do pacote (3 `Skulls_Hanging`, 3 `Weapon_Hanging`, 3 `Food_Hanging`) estão em z entre 7,7 e 15,1 — **atrás do Trilho_Fundo**, inalcançáveis.

Antes de qualquer missão: marque 4 ou 5 **beats** no eixo X e traga os props para eles. Run não é distância, é sequência de acontecimentos.

**5.2 O Kael.** É a peça da qual a run inteira depende — é ele quem acompanha e é ele quem mata Sardael no fim. Hoje é `SM_Chr_Wizard_01` em (7,20 / 0,10 / **5,59**), com **um único componente**: `TocadorDeClipe`. Sem colisor, sem nome, sem uma linha em nenhum `.cs` (a string "Kael" não existe no código). E ele está **3,4 m além do Trilho_Fundo**: como `Mover` faz `p.z = zDaLinha` todo quadro, a distância de 5,59 m nunca diminui, faça o jogador o que fizer.

Traga-o para a linha, acrescente um campo `kael` ao `DiretorDeBael`, e faça-o andar atrás do herói — o `LiderarAFrente` do `DiretorDoCapitao`, invertido, já resolve a escolta.

As pistas que custam zero código e estão prontas para usar: **(a)** Kael não recebe `PoeiraNoPe` (a pista é a ausência de um componente — há 36 instâncias em 3 cenas, ele não tem nenhuma); **(b)** `Ovelha.cs:63-64` já expõe `fugirDe` (Transform) e `distanciaDeSusto` (5f) — dois campos no Inspector apontados para o Kael e os bichos fogem dele; **(c)** o contador de mortes já existe em disco e nunca serviu para nada — é ele que Kael diz em voz alta.

**5.3 A cena da traição, corrigida.** Confira antes de montar: `Full_Mount_Attacks` é **inteiro luta de chão** (16 pares: Takedown, ArmBar, KnifeStab, SlashNeck...). Um finalizador de pé com um clipe em que a vítima está no chão não existe no pacote. A cena honesta é `Takedown_DoubleLeg_Start_Att/_Vic` seguido de `1HM_KnifeStab_Att/_Vic` — e é ela que paga a missão 11: a quarta guarda que Kael nunca ensinou não é um ângulo de lâmina, é a **derrubada**. Não existe guarda contra derrubada, e por isso ele não podia mostrá-la sem entregar o plano. É a diferença entre tirar o controle do jogador no clímax e o jogo trapaceando.

**5.4 Trecho_02: decida o formato antes do oitavo trecho.** O arquivo se chama `Trecho_01`, os 11 terrenos formam 9.460 m e a arte cobre **1,3%**. Não existe no projeto nenhuma peça para haver um Trecho_02 — nenhum spawner, nenhum sistema de segmento, nenhuma ferramenta de espalhar em `Scripts/Editor/` (as 9 ferramentas são de personagem, lança, vídeo, gravação e montagem de bancada). Os 434 objetos foram postos à mão. Se trecho é cena nova, tudo bem; **se for pedaço encaixado, o sistema de segmento nasce agora**, não depois de 8 trechos feitos à mão.

**5.5 O inventário órfão — decida um a um.** `Minotauro.prefab`, `Cavaleiro_Montado.prefab`, `Carroca_Puxada.prefab`, `Cabeca_Decepada.prefab`: **zero ocorrências dos GUIDs nas 7 cenas e em todos os prefabs**. O Minotauro é o chefe de meio de run que você já construiu. O cavaleiro custou a saga inteira do RootT e do retarget do Horse Anim Set. Entra, vira bancada, ou é apagado — e cada decisão vira uma linha no `LEIA-ME`.

**5.6 O texto sai das cenas.** Hoje são **23 frases**, cada uma um valor serializado dentro do arquivo da cena. Mover para um `ScriptableObject` com chave + texto custa uma tarde. **Faça no dia em que a run levar isso de 23 para 60 frases** — nem antes (é trabalho sem retorno hoje) nem depois (com 300 frases, custa reabrir todas as cenas).

> **PRONTO QUANDO:** existem 5 beats marcados na floresta, Kael anda atrás do jogador na linha z=0 e fala em pelo menos dois deles, e uma gravação mostra as ovelhas se afastando dele sem que você tenha escrito uma linha de código para isso.

---

## ONDA 6 — MOBILE

Nada aqui é para fazer antes. Estas são as **consequências** das decisões da Onda 0 — se D1 e D2 foram feitas direito, esta onda é trabalho mecânico.

**6.1 A textura de 64 MB.** `Personagens/Kael/Kael_Albedo.png`: 4096x4096, RGB24, **Uncompressed**, mipmaps, e `isReadable: 1` (segunda cópia idêntica na CPU). São 64 MB exatos de VRAM. É a maior textura do projeto por uma margem de 3x — a segunda é `PolygonGoblinWarCamp_01_Normals.png` com 21,3 MB, e já comprimida. Os blocos Standalone/Server do `.meta` existem mas estão `overridden: 0`, então o DefaultTexturePlatform manda em toda plataforma, **inclusive Android**, onde ela iria como RGB24 cru em 4096.

No PC é o maior desperdício unitário do projeto. No celular é bloqueante: sozinha, é o orçamento de uma cena inteira. Desmarcar Read/Write, Max Size 2048, compressão ligada: de 64 MB para a casa de 2 a 5 MB, sem tocar em arte. **Este é o único item de desempenho que vale fazer hoje**, porque é um checkbox.

(De quebra, `M_Kael` está em TransparentCutout com `_Cutoff: 0.5` e o PNG **não tem canal alfa** — está pagando shader de recorte que nunca recorta nada.)

**6.2 O trigo.** `Trigo_Alto.prefab` tem **1.070 triângulos por pé, sem LODGroup**. Em Halu é camada de detalhe do terreno: 2.788 instâncias = **2,98 M de triângulos**, contra 1,05 M de LOD0 de tudo que foi montado à mão na aldeia. Na Fazenda são 617 objetos soltos = 612.454 triângulos, **79,2% da geometria da cena**. Para comparar: o tufo de grama tem 144 triângulos e a árvore mais usada tem 688.

É a resposta concreta para "onde está o custo de quadro de Halu": não são os 13.771 objetos, é a lavoura do terreno.

**6.3 Distâncias de desenho.** Na Floresta a névoa satura em 55 m e os terrenos desenham grama até 90 m e árvore de malha cheia até 110 m — dentro de uma parede de névoa que já as esconde por completo. Nenhum protótipo de árvore tem LODGroup (`lodCount = 0` nos 11 da Floresta e nos 3 de Halu e da Fazenda), então `m_TreeBillboardDistance` é campo morto. **Valores seguros, medidos com o FOV e a razão de canto de tela: ~70 m de detalhe e ~80 m de árvore** — cortar direto para 55 raspa grama visível na borda. Halu **não** entra: lá a grama morre em 130 m contra névoa de 340, então encolher deixa a borda do tapete à vista.

**6.4 Qualidade.** `QualitySettings.asset` tem **UM nível**, chamado "PC", com `excludedTargetPlatforms` de Android e iPhone — ou seja, **zero níveis de qualidade no dia do porte**. E o seletor de Qualidade do menu de pausa faz módulo por n=1: as duas setas sempre voltam para "PC". É um controle morto na cara do jogador hoje. Os assets URP-Performant/Balanced/HighFidelity já existem em `Assets/Settings` e não estão ligados a nível nenhum.

**6.5 O que o HUD da Onda 3 precisa ter nascido sabendo:** área segura (notch), e os botões da tabela de D2 nas posições onde um polegar alcança.

**6.6 O `applicationIdentifier`.** `com.Unity-Technologies.com.unity.template.urp-blank`, gravado deliberadamente (`overrideDefaultApplicationIdentifier: 1`). Não atrapalha o build Windows, mas é um namespace que não te pertence e tem de mudar antes de qualquer submissão — e mudá-lo **também muda o persistentDataPath no Android**.

> **PRONTO QUANDO:** existem 3 níveis de qualidade sem exclusão de plataforma, o Kael pesa menos de 6 MB, e uma luta de 4 orcs roda no alvo de quadros escolhido com o contador de triângulos abaixo do orçamento que você escreveu em `COMBATE.md`.

---

## ONDA 7 — STEAM

**7.1 Foco de teclado e gamepad.** O `EventSystem` de `Menu_Principal` tem `m_FirstSelected: {fileID: 0}` e **nenhum script do projeto chama `SetSelectedGameObject`** (zero ocorrências em todo o `Assets/`). Quem abrir com o controle na mão não consegue apertar JOGAR nem escolher um pergaminho.

O importante: **os bindings já existem e já estão ligados** — o módulo de UI do menu aponta para o `DefaultInputActions` do pacote, com Navigate/Submit/Cancel em setas, WASD, dpad e sticks. Eles disparam e não chegam a ninguém, porque `InputSystemUIInputModule` só executa move/submit sobre `currentSelectedGameObject`, sem auto-seleção. **O conserto é preencher `m_FirstSelected` e chamar `SetSelectedGameObject` no `Mostrar()` dos dois scripts.** Não é criar binding.

**7.2 Configurações antes da partida.** A tela de opções mora dentro da `TelaDePausa`, que se desliga no menu. Para trocar resolução é preciso começar um jogo, esperar Halu carregar e apertar ESC. `Painel_Inicio` só tem JOGAR e SAIR.

**7.3 A resolução salva que não tem volta.** `Configuracoes.Aplicar()` é `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` e faz `Screen.SetResolution(l, a, cheia)` **sem conferir contra `Screen.resolutions`** — a lista existe em `Resolucoes()` e só é usada para desenhar o seletor. Quem jogou num ultrawide 3440x1440 e abre num notebook 1366x768 recebe a resolução errada antes da primeira cena. E como roda `BeforeSceneLoad`, ela **sobrescreve os argumentos `-screen-width`/`-screen-height`**, anulando a recuperação padrão via opções de inicialização da Steam. Sobra apagar chaves do registro. Valide contra a lista, e dê ao seletor a confirmação de 15 s com reversão.

(No mesmo arquivo: `TrocarTelaCheia` ignora o parâmetro `passo` — as setas `<` e `>` fazem a mesma coisa.)

**7.4 Tela de controles e legenda honesta.** O único lugar que ensina teclas hoje é o painel de depuração, ele anuncia 7 e **4 não funcionam**, e o painel do diretor cai por cima das duas primeiras linhas — justamente onde está "A / D andar". ESC não é ensinado em lugar nenhum: a dica "ESC continua o jogo" só aparece depois que a pausa já está aberta.

**7.5 Ícone e identidade.** `m_BuildTargetIcons: []` — nenhum ícone de Standalone. O executável nasce com o nome do `productName` e o ícone do Unity na barra de tarefas. (O nome no overlay da Steam vem do cadastro no Steamworks, não daqui.)

**7.6 A promessa da primeira tela.** O menu é ilustração 2D em pixel art vista de cima; o jogo é 3D low-poly Synty visto de lado. As duas são boas e não conversam. Não é polimento — é a promessa que a loja faz. Decida antes de a página da Steam existir.

> **PRONTO QUANDO:** você joga do menu até Bael inteiro **sem tocar no mouse**, com um controle; e um build numa segunda máquina, com resolução de desktop diferente, abre jogável.

---

## O QUE **NÃO** FAZER AGORA — e por quê

1. **Áudio, música, SFX, partículas e VFX polidos.** Sua decisão, e ela está certa: som entra depois que o combate estiver gostoso. Antes disso, cada ajuste de janela de golpe invalidaria o som feito para ela.

2. **Otimizar o trigo, as árvores e o bake de Halu.** Fica na Onda 6, com **uma exceção**: o Kael_Albedo, porque é literalmente um checkbox. O resto não se toca agora por dois motivos — o combate vai mudar o orçamento de desenho (4 orcs + gore + câmera de arena), e otimizar antes de saber o orçamento é chutar. Além disso nada disso bloqueia nenhuma onda anterior.

3. **Montar o Trecho_02 à mão.** Os 434 objetos do Trecho_01 renderam 9% de jogo visto. Repetir o processo antes de decidir 5.4 é comprar o mesmo problema de novo, em dobro.

4. **Rebind de teclas e tradução.** Rebind é conforto; a **listagem** de teclas (7.4) é obrigação. Tabela de textos: só quando passar de 23 frases (5.6).

5. **Apagar `Duelo.cs`, `TesteDeCombate.cs`, `AvisoNoImpacto.cs`.** A arquitetura deles está errada (árbitro estático no herói), mas os **números** são ouro medido: `ALCANCE = 1.756`, `DESVIO = −0.041`, o impacto em 0,322 do clipe do orc, os 25 pares Attack*_Complete/_React. Colha, não apague. Apague sim o `ControladorSardael.cs` (5,7 KB, zero cenas, segundo controlador de herói morto) ou marque-o como morto no LEIA-ME.

6. **Trazer Minotauro e Cavaleiro_Montado para o jogo antes da Onda 5.** Eles são a resposta para "o que a run precisa", e essa pergunta só se responde depois que o combate for jogável.

7. **Qualquer coisa de Steamworks** — conquistas, save em nuvem, cartas. Nada disso antes de haver um build que roda numa segunda máquina.

8. **Unificar as duas câmeras agora.** Vale fazer, mas é Onda 3/4: `CameraLateral` já conserta o teleporte que `CameraQueSegue` ainda tem (troca de direção joga o alvo 4,4 m de lado, amortecido em 0,5 s, enquanto o herói corre a 4,5 m/s) — o comentário da primeira documenta o bug da segunda. Uma câmera só, com `Enquadrar(xMin, xMax)` para a arena **em vez de desligar o script**, que é de onde vêm os 4,6 m de erro da jaula.

---

## ONDE CADA PERGUNTA SUA FOI RESPONDIDA

| categoria | ondas |
|---|---|
| **(a) fluxo e narrativa** | 1.4 (pular fala) · **4 inteira** · 5.2/5.3 (Kael e a traição) |
| **(b) o jogo como um todo** | 1.5, 1.6 (build honesto) · 3.4 (HUD) · 4.7 (interação) · 5.1/5.4/5.5 (a floresta, o órfão) · 7.4/7.6 (o jogo se apresentar) |
| **(c) preparo do combate** | **0 inteira** · 1.1, 1.2, 1.3 · **2 inteira** · 3.1, 3.2 |
| **(d) mobile** | decidido em **0 (D1, D2)**, executado em **6** |
| **(e) Steam** | D4 hoje (5 min) · 1.5, 1.6 · **7 inteira** |

**A ordem, em uma linha:** decidir (0) → limpar a bancada (1) → vida, inimigo, encontro (2) → o combate entra na Fazenda (3) → o laço fecha (4) → a run (5) → mobile (6) → Steam (7).

**O único item que vale sair da ordem e fazer hoje, em 20 minutos:** o `FotografoDaCena` desligado (1.5), o `EventSystem` em `AoTrocarDeCena` (1.6) e o Read/Write do Kael_Albedo (6.1). Os três são checkbox, e os dois primeiros contaminam toda gravação que você vai fazer a partir de amanhã.
