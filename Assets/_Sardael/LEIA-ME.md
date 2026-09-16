# A Lenda de Sardael — onde fica cada coisa

A regra é uma só: **`_Sardael/` é o jogo. `_Pacotes/` é o que foi comprado.**
Se um arquivo está em `_Pacotes/`, ninguém do projeto o editou — dá para apagar o pacote
inteiro e reimportar da loja sem perder trabalho. Se está em `_Sardael/`, é nosso.

```
Assets/
├── _Sardael/          O JOGO
│   ├── Animacoes/
│   │   ├── Clipes/            .anim e FBX de animação feitos aqui
│   │   └── Controladores/     os .controller (Sardael_Halu, Sardael_Combate, Orc_Combate...)
│   ├── Arte/
│   │   ├── Ceu/               a nuvem e o ToonShader dela
│   │   ├── Importados/        arte de terceiro que foi AJUSTADA: lanças, veado, trigo,
│   │   │                      ovelhas, camponeses, props da CraftPix
│   │   ├── Materiais/         materiais soltos de cenário
│   │   └── Menu/              a arte do menu principal
│   ├── Cenas/                 as 7 cenas + os materiais que só elas usam
│   ├── Personagens/           prefabs e materiais de personagem
│   │   ├── Gore/              cabeças decepadas
│   │   ├── Kael/              o mago, com atlas e material próprios
│   │   └── Minotauro/
│   ├── Resources/             o que é carregado por NOME em tempo de jogo
│   │                          (Acervo_De_Lancas, Menu_Placa, Seta_Avancar, StepDust)
│   ├── Scripts/               todo o código do jogo
│   │   └── Editor/            e todas as ferramentas de editor, num lugar só
│   └── Terrenos/              TerrainData e materiais de terreno
├── _Pacotes/          TERCEIROS, intocados
├── Settings/          URP e o mapa de entrada
└── TextMesh Pro/      fica aqui porque o TMP procura por caminho fixo
```

## As cenas, na ordem em que o jogo roda

| cena | o que é |
|---|---|
| `Menu_Principal` | entrada, os 3 pergaminhos, apagar partida |
| `Halu` | a aldeia. As duas missões e a conversa do sonho |
| `Tutorial_Fazenda` | o capitão de Valdória, o troll, o aviso de combate não feito |
| `Floresta_Trecho_01` | Bael. A emboscada do orc e o Kael |
| `Morte_Do_Veu` | a cutscene do veado e da luz |
| `Movimento_Teste` | oficina: teste de movimento e o catálogo de lanças (TAB) |
| `Finishers_Sardael` | oficina: as animações de finalização em par |

`Finishers_Sardael` e `Movimento_Teste` **não estão no Build Settings** de propósito: são
bancada, não fase.

## Onde procurar quando…

- **um diálogo está errado** → `Scripts/Diretor*.cs`. Todo texto é campo no Inspector do
  diretor da cena, não está dentro do código.
- **uma animação não toca** → `Scripts/TocadorDeClipe.cs`. Dois grafos no mesmo Animator dão
  T-pose: quem usa o tocador tem de estar sem AnimatorController.
- **o herói não anda** → `Scripts/MovimentoDoHeroi.cs`. A trava é um conjunto de donos
  (`Travar`), não um booleano — cada sistema solta só a própria chave.
- **um portão não abre** → `Scripts/PortaDeCena.cs`. A parede invisível **é** o gatilho:
  trancada é parede, destrancada vira vão.
- **o save** → `Scripts/Salvao.cs`. Três arquivos JSON em
  `AppData/LocalLow/DefaultCompany/o conto e sardael/`.

## A DOCUMENTAÇÃO MANDA — leia antes de tocar no projeto

Ela **não** fica dentro do Unity. Fica em `o conto de sardael/documentos/`, um nível acima
da pasta do projeto, com índice em `documentos/README.md`. Os `.html` abrem no navegador.

| documento | o que decide |
|---|---|
| **Contrato de Combate** (11/09) | **É o documento que o código obedece.** Estados do Sardael e do orc, o que interrompe o quê, qual clipe toca em cada estado, e a tabela de números inteira (vida, poise, dano, telegrafo, alcance, cadeia, janelas). Fecha com 8 passos de construção, cada um com uma prova. |
| **O Véu Não o Deixa Morrer** (11/09) | A narrativa em três atos, o elenco, e o clímax. A run é o **Ato II**, com os beats 06 a 11 já escritos. |
| **Ordem de Construção** (10/09) | As 18 fases da reconstrução em Unity, cada uma com prova. |

Três coisas que este mapa já afirmou errado por não ter lido a pasta, e que ficam registradas
para ninguém repetir:

- **O combate NÃO é indefinido.** Está especificado número por número no Contrato.
- **O jogo é LINHA, não pista.** "Travado na linha, só anda no eixo X" — Contrato, seção
  Sardael. Os trilhos de Bael são limite de cenário, não intenção contrariada.
- **Bael é procedural por cânone**, não por conveniência: "aqueles que entram na floresta sem
  propósito são absorvidos por ela". O `Floresta_Trecho_01` montado à mão é bancada, não a fase.

## O que ainda não existe

- **o combate em código**. Está desenhado e medido; falta escrever. O tutorial mostra um aviso
  e manda voltar; em Bael o orc mata sempre (`DiretorDeBael.matarSempre`). Os 8 passos e as
  provas de cada um estão no Contrato de Combate.
- **som**. Não há um AudioSource no projeto. As barras de Música e Efeitos já estão no menu
  de pausa e guardadas em `Configuracoes`, esperando. Por decisão do dono, som e FX entram
  **depois** de o combate estar gostoso de jogar e a narrativa sem furos.
