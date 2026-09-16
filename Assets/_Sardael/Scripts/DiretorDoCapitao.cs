using System.Collections;
using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// O fluxo do Capitao de Valdoria no tutorial da fazenda.
    ///
    /// 1. Sardael entra no mapa e o comando fica travado. O capitao entra andando pela
    ///    direita, passa por ele, para' de frente e avisa que o lider do bando esta' adiante.
    /// 2. O comando e' liberado. Sardael avanca e o capitao vem atras. Quando eles chegam
    ///    perto do troll, o capitao passa na frente, os dois trocam palavra e duelam.
    /// 3. Passados os segundos de duelo, o capitao derruba o troll e corta o pescoco dele
    ///    com o par de clipes do Full Mount. Depois manda Sardael cuidar do resto: a camera
    ///    trava e Sardael fica preso na faixa que ela mostra.
    ///
    /// O COMBATE DE SARDAEL NAO ESTA AQUI de proposito. Este script entrega o palco montado
    /// — camera travada, trilho fechado, orcs em pe' — e para. Quando os comandos de combate
    /// existirem, e' so' chamar <see cref="AoLiberarOsOrcs"/>.
    ///
    /// Tudo por tempo e por distancia, com numero no Inspector, igual a <see cref="CenaDaMorte"/>:
    /// cutscene presa a evento de animacao vira pesadelo de sincronia quando alguem mexe num
    /// clipe.
    ///
    /// O PAR DA FINALIZACAO tem encaixe proprio. Os clipes _Att e _Vic sao escritos no mesmo
    /// espaco: a vitima fica NA FRENTE do atacante, virada 180 graus, a uma distancia que veio
    /// medida da cena de demonstracao do pacote (0,991 m na queda, 0,080 m no golpe). Errar
    /// esse numero faz o capitao acertar o ar.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiretorDoCapitao : MonoBehaviour
    {
        public enum Etapa
        {
            Espera, CapitaoEntra, Briefing, MarchaLivre, CapitaoPassaNaFrente,
            Conversa, Duelo, Queda, Golpe, Ordem, Arena, SemCombate, Fim
        }

        [Header("Atores")]
        public Transform capitao;
        public Transform sardael;
        public Transform troll;
        public MovimentoDoHeroi comandoDoHeroi;
        public CameraQueSegue cameraDoJogo;
        public FalaNaTela caixaDeFala;

        [Header("Clipes do capitao")]
        public AnimationClip parado;
        public AnimationClip andando;
        public AnimationClip correndo;
        public AnimationClip falando;
        public AnimationClip ordenando;
        [Tooltip("Passada do clipe de andar, em metros por segundo, medida no [RM] dele. "
               + "Mexer aqui sem remedir faz o pe deslizar.")]
        public float velocidadeAndando = 1.898f;
        [Tooltip("Passada do clipe de correr, medida no [RM] dele.")]
        public float velocidadeCorrendo = 3.17f;

        [Header("1 - entrada")]
        public float esperaInicial = 0.9f;
        [Tooltip("De onde ele entra. Fica fora do quadro, A ESQUERDA: ele vem da esquerda "
               + "pra direita e para' de frente pro Sardael, sem passar por tras dele.")]
        public Vector2 partidaXZ = new Vector2(118f, 18f);
        [Tooltip("Onde ele para', de frente pro Sardael.")]
        public Vector2 paradaXZ = new Vector2(129.5f, 18f);

        [Header("1 - fala da chegada")]
        public string nomeDoCapitao = "Capitão de Valdória";
        [TextArea(2, 4)] public string falaDaChegada =
            "Você chegou bem a tempo, Sardael. Vamos depressa — o líder desse bando está "
          + "adiante. Precisamos matá-lo para expulsar esses monstros!";
        public float duracaoDaFala = 5.5f;

        [Header("2 - marcha (o capitao vai NA FRENTE)")]
        [Tooltip("A que distancia a frente do Sardael, no sentido da marcha, o capitao anda. "
               + "Quem guia e' ele; Sardael e' que segue.")]
        public float distanciaNaFrente = 3.2f;
        [Tooltip("Zona morta. O capitao so' sai do lugar quando o erro passa disto, e so' "
               + "para quando volta a menos de um terco disto. E' o que impede o vai-e-vem "
               + "de um quadro que travava a animacao.")]
        public float folga = 0.55f;
        [Tooltip("Em quantos segundos ele fecharia o erro inteiro. Maior = mais preguicoso.")]
        public float tempoDeResposta = 0.45f;
        public float aceleracao = 7f;
        [Tooltip("Tempo minimo num clipe antes de poder trocar. Trocar de clipe remonta o "
               + "grafo e reinicia a animacao no quadro zero: trocar a cada quadro e' o que "
               + "fazia o capitao saltar em vez de andar.")]
        public float trocaMinima = 0.3f;
        [Tooltip("Quando Sardael chega a esta distancia do troll, a cutscene retoma.")]
        public float distanciaQueChamaOTroll = 11f;

        [Header("2 - o troll vem ao encontro")]
        public AnimationClip trollAndando;
        public float velocidadeDoTroll = 1.6f;

        [Header("2 - encontro")]
        [Tooltip("A que distancia do troll o capitao para' pra encarar.")]
        public float distanciaDoDuelo = 2.3f;
        public string nomeDoTroll = "Troll";
        [TextArea(2, 3)] public string falaDoTroll = "Mais um homem de Valdória... bom. Estava com fome.";
        [TextArea(2, 3)] public string respostaDoCapitao = "Sardael, fique atrás. Este é meu.";
        public float duracaoDaConversa = 3.2f;

        [Header("2 - duelo")]
        public float duracaoDoDuelo = 10f;
        public AnimationClip guardaDoCapitao;
        public AnimationClip[] golpesDoCapitao;
        public AnimationClip defesaDoCapitao;
        public AnimationClip guardaDoTroll;
        public AnimationClip[] golpesDoTroll;
        public AnimationClip defesaDoTroll;

        [Header("3 - finalizacao (par do Full Mount)")]
        public AnimationClip quedaAtacante;
        public AnimationClip quedaVitima;
        [Tooltip("Medido na cena de demonstracao do pacote: a vitima fica a esta distancia "
               + "NA FRENTE do atacante, virada 180 graus.")]
        public float distanciaDaQueda = 0.991f;
        public AnimationClip golpeAtacante;
        public AnimationClip golpeVitima;
        public float distanciaDoGolpe = 0.080f;
        [Tooltip("O capitao se erguendo de cima do corpo. Use o HumanM@SitGround01 - Stop do "
               + "Kevin: e' o que comeca mais perto da pose em que o golpe acaba (peito a "
               + "0,40 m contra 0,67 m; o Knockdown01 - StandUp comeca a 0,10 m, deitado). "
               + "NAO use o Paired_FullMount_PushOff_Att: nele quem empurra e' a VITIMA e o "
               + "atacante e' arremessado pra tras — o pacote nao tem atacante levantando "
               + "por vontade propria. Vazio, ele passa do golpe direto pra guarda.")]
        public AnimationClip levantandoAtacante;

        [Header("3 - emendas (medidas, nao chutadas)")]
        [Tooltip("Cada clipe pareado tem o proprio ponto de origem. Estes vetores, no eixo do "
               + "CAPITAO, sao o quanto cada raiz tem de andar na troca pra que o CORPO fique "
               + "parado na tela. Sem eles o boneco teleporta ~1,9 m a cada troca. Medidos "
               + "pelo peito do capitao e pelo quadril do troll; o y e' ignorado porque o pe "
               + "volta pro chao sozinho.")]
        public Vector3 emendaQuedaGolpeCapitao = new Vector3(0.056f, 0f, 1.923f);
        // 0,976 e nao 1,887: a medicao crua deu 1,887 porque as duas poses do troll foram
        // lidas com a raiz dele em lugares diferentes (0,991 contra 0,080), e essa diferenca
        // de 0,911 entrou no numero. A emenda e' so' o quanto o CORPO anda dentro do clipe.
        public Vector3 emendaQuedaGolpeTroll   = new Vector3(0.098f, 0f, 0.976f);
        public Vector3 emendaGolpeLevantar     = Vector3.zero;
        public Vector3 emendaLevantarParado    = new Vector3(-0.092f, 0f, -1.793f);
        [Tooltip("Usada quando nao ha' clipe de levantar: do fim do golpe direto pra guarda.")]
        public Vector3 emendaGolpeParado       = Vector3.zero;
        [Tooltip("Quanto tempo ele fica ajoelhado sobre o corpo antes de se erguer.")]
        public float pausaSobreOCorpo = 0.8f;
        [Tooltip("Mistura do golpe pra guarda. Aqui a mistura ajuda: e' o que faz ele parecer "
               + "se erguendo em vez de cortar de ajoelhado pra em pe'.")]
        public float suavidadeDeLevantar = 0.45f;
        public float pausaAntesDoGolpe = 0.1f;
        public float pausaDepoisDoGolpe = 1.2f;

        [Header("3 - ordem e arena")]
        [TextArea(2, 3)] public string ordemFinal = "O chefe caiu. Os outros são seus, Sardael. Acabe com eles!";
        public float duracaoDaOrdem = 4f;
        [Tooltip("Onde o capitao fica assistindo, atras do Sardael.")]
        public float recuoDoCapitao = 4.5f;
        [Tooltip("Os orcs que sobram pro jogador. Eles so' ficam de pe' — o combate nao esta feito.")]
        public Transform[] orcsDoDesafio;
        [Tooltip("Meia-largura da jaula: Sardael nao sai desta faixa em torno da camera.")]
        public float meiaLarguraDaArena = 6.2f;

        [Header("4 - enquanto o combate nao existe")]
        [Tooltip("Quanto tempo a jaula fica fechada, como se ele estivesse lutando, antes do aviso.")]
        public float pausaNaArena = 2.2f;
        public string nomeDoAviso = "— ainda em construção —";
        [TextArea(2, 4)] public string avisoSemCombate =
            "O combate ainda não foi criado. Considere os orcs derrotados: volte para Halu "
          + "e fale com o líder.";
        public float duracaoDoAviso = 5.5f;
        [Tooltip("O caminho de volta pra Halu. Destrancado junto com o aviso.")]
        public PortaDeCena portaDeVolta;

        [Header("Depuracao")]
        public bool mostrarPainel = true;
        public bool comecarSozinho = true;

        public Etapa Agora { get; private set; }

        bool jaulaGuardada, limitesDeAntes;
        float xMinimoDeAntes, xMaximoDeAntes;

        Animator animCapitao, animTroll;
        TocadorDeClipe tocadorCapitao, tocadorTroll;
        Terrain chao;
        bool travaDoRoteiro;
        float velocidadeDoCapitao, quandoTrocouDeClipe;
        bool emMarcha;
        AnimationClip clipeDoCapitao;
        Vector3 ultimaDoHeroi;
        float passoDoHeroi;

        void Start()
        {
            chao = Terrain.activeTerrain;
            if (capitao != null) { animCapitao = capitao.GetComponent<Animator>(); tocadorCapitao = capitao.GetComponent<TocadorDeClipe>(); }
            if (troll != null) { animTroll = troll.GetComponent<Animator>(); tocadorTroll = troll.GetComponent<TocadorDeClipe>(); }
            if (caixaDeFala == null) caixaDeFala = FalaNaTela.Instancia;

            if (!Conferir()) return;
            if (comecarSozinho) StartCoroutine(Roteiro());
        }

        bool Conferir()
        {
            string falta = "";
            if (capitao == null) falta += "capitao, ";
            if (sardael == null) falta += "sardael, ";
            if (troll == null) falta += "troll, ";
            if (comandoDoHeroi == null) falta += "comandoDoHeroi, ";
            if (quedaAtacante == null || quedaVitima == null) falta += "par da queda, ";
            if (golpeAtacante == null || golpeVitima == null) falta += "par do golpe, ";
            if (falta == "") return true;
            Debug.LogWarning("DiretorDoCapitao sem: " + falta.TrimEnd(' ', ',') + ". A cena nao vai rodar.", this);
            enabled = false;
            return false;
        }

        // ------------------------------------------------------------------ o roteiro
        IEnumerator Roteiro()
        {
            // ---------- 1: o capitao entra e avisa
            Agora = Etapa.Espera;
            Travar(true);
            Pousar(capitao, parado);
            Plantar(capitao, new Vector3(partidaXZ.x, 0f, partidaXZ.y));
            capitao.rotation = Quaternion.Euler(0f, 270f, 0f);      // olhando pro oeste
            yield return new WaitForSeconds(esperaInicial);

            Agora = Etapa.CapitaoEntra;
            yield return AndarAte(capitao, new Vector3(paradaXZ.x, 0f, paradaXZ.y), velocidadeAndando, andando, parado);
            Encarar(capitao, sardael);

            Agora = Etapa.Briefing;
            Pousar(capitao, falando != null ? falando : parado);
            Dizer(nomeDoCapitao, falaDaChegada);
            yield return new WaitForSeconds(duracaoDaFala);
            Calar();
            Pousar(capitao, parado);

            // ---------- 2: comando liberado. O CAPITAO VAI NA FRENTE e Sardael e' quem segue.
            Agora = Etapa.MarchaLivre;
            Travar(false);
            velocidadeDoCapitao = 0f; passoDoHeroi = 0f; ultimaDoHeroi = Plano(sardael.position);
            while (Vector3.Distance(Plano(sardael.position), Plano(troll.position)) > distanciaQueChamaOTroll)
            {
                LiderarAFrente();
                yield return null;
            }
            Travar(true);

            // ---------- 2b: os dois caminham um pro outro e se encontram no meio.
            // Sardael para onde estiver — quem fecha a distancia sao o capitao e o troll.
            Agora = Etapa.CapitaoPassaNaFrente;
            var eixo = Plano(troll.position - capitao.position).normalized;
            var meio = (Plano(capitao.position) + Plano(troll.position)) * 0.5f;
            var postoDoCapitao = meio - eixo * (distanciaDoDuelo * 0.5f);
            var postoDoTroll   = meio + eixo * (distanciaDoDuelo * 0.5f);
            yield return AndarOsDois(postoDoCapitao, postoDoTroll);
            Encarar(capitao, troll);
            Encarar(troll, capitao);

            Agora = Etapa.Conversa;
            Pousar(troll, guardaDoTroll);
            Dizer(nomeDoTroll, falaDoTroll);
            yield return new WaitForSeconds(duracaoDaConversa);
            Pousar(capitao, falando != null ? falando : parado);
            Dizer(nomeDoCapitao, respostaDoCapitao);
            yield return new WaitForSeconds(duracaoDaConversa);
            Calar();

            // ---------- 2c: o duelo
            Agora = Etapa.Duelo;
            var aliado = LigarCombatente(capitao, tocadorCapitao, guardaDoCapitao, golpesDoCapitao, defesaDoCapitao);
            var inimigo = LigarCombatente(troll, tocadorTroll, guardaDoTroll, golpesDoTroll, defesaDoTroll);
            var arbitro = new GameObject("Duelo_do_Capitao");
            arbitro.transform.SetParent(transform, false);
            var duelo = arbitro.AddComponent<DueloEncenado>();
            duelo.aliado = aliado; duelo.inimigo = inimigo; duelo.encararNoInicio = true;
            yield return new WaitForSeconds(duracaoDoDuelo);
            Destroy(arbitro);
            DesligarCombatente(capitao, tocadorCapitao);
            DesligarCombatente(troll, tocadorTroll);

            // ---------- 3: a queda
            Agora = Etapa.Queda;
            Encarar(capitao, troll);
            Assentar(distanciaDaQueda);
            PousarUmaVez(capitao, quedaAtacante);
            PousarUmaVez(troll, quedaVitima);
            yield return new WaitForSeconds(quedaAtacante.length + pausaAntesDoGolpe);

            // ---------- 3b: o golpe de misericordia.
            // CADA UM anda a propria emenda. Reassentar o troll em relacao ao capitao, como
            // eu fazia antes, obriga um dos dois a saltar: os dois clipes tem pontos de
            // origem diferentes, e a diferenca do fim de um pro comeco do outro chega a 1,9 m.
            Agora = Etapa.Golpe;
            Emendar(capitao, emendaQuedaGolpeCapitao);
            Emendar(troll,   emendaQuedaGolpeTroll);
            PousarUmaVez(capitao, golpeAtacante);
            PousarUmaVez(troll, golpeVitima);
            yield return new WaitForSeconds(golpeAtacante.length);

            // ---------- 3c: ele se desmonta do corpo. O troll fica congelado no ultimo
            // quadro do golpe — esta' morto, nao se mexe mais.
            yield return new WaitForSeconds(pausaSobreOCorpo);
            var toc = capitao.GetComponent<TocadorDeClipe>();
            if (levantandoAtacante != null)
            {
                // AQUI A MISTURA AJUDA, ao contrario do resto da cadeia. Os clipes pareados
                // emendam pose com pose e por isso vao em corte seco; este nao e' pareado,
                // comeca sentado com o peito a 0,40 m e o golpe acaba ajoelhado a 0,67 m —
                // os 0,26 m de diferenca somem na mistura.
                Emendar(capitao, emendaGolpeLevantar);
                if (toc != null) { toc.congelarNoFim = true; toc.Trocar(levantandoAtacante, suavidadeDeLevantar); }
                yield return new WaitForSeconds(levantandoAtacante.length);
                Emendar(capitao, emendaLevantarParado);
            }
            else
            {
                Emendar(capitao, emendaGolpeParado);
                if (toc != null) { toc.congelarNoFim = false; toc.Trocar(parado, suavidadeDeLevantar); }
            }
            yield return new WaitForSeconds(pausaDepoisDoGolpe);

            // ---------- 3d: a ordem
            Agora = Etapa.Ordem;
            capitao.rotation = Quaternion.Euler(0f, capitao.eulerAngles.y, 0f);
            Plantar(capitao, Plano(capitao.position));
            clipeDoCapitao = parado;
            Pousar(capitao, parado);
            if (tocadorCapitao != null) tocadorCapitao.Ritmo(1f);
            var recuo = Plano(sardael.position) + Plano(sardael.position - troll.position).normalized * recuoDoCapitao;
            yield return AndarAte(capitao, recuo, velocidadeAndando, andando, parado);
            Encarar(capitao, troll);
            Pousar(capitao, ordenando != null ? ordenando : falando);
            Dizer(nomeDoCapitao, ordemFinal);
            yield return new WaitForSeconds(duracaoDaOrdem);
            Calar();
            Pousar(capitao, parado);

            // ---------- 3d: a jaula
            Agora = Etapa.Arena;
            TravarACamera();
            Travar(false);
            AoLiberarOsOrcs();

            // ---------- 4: nao ha' combate, entao o tutorial se da' por cumprido
            Agora = Etapa.SemCombate;
            yield return new WaitForSeconds(pausaNaArena);
            Dizer(nomeDoAviso, avisoSemCombate);          // a caixa de fala ja' trava o comando
            yield return new WaitForSeconds(duracaoDoAviso);
            Calar();

            Progresso.Onde = Progresso.Etapa.VoltouDoTutorial;
            AbrirAVolta();
            Agora = Etapa.Fim;
        }

        /// <summary>
        /// Solta a jaula e abre o caminho de volta.
        ///
        /// A camera volta a seguir e os limites voltam ao que eram ANTES da arena: a jaula
        /// tinha sido apertada em torno do ponto onde a camera parou, e sem desfazer isso o
        /// jogador bate numa parede invisivel a seis metros do portao.
        /// </summary>
        void AbrirAVolta()
        {
            if (cameraDoJogo != null) cameraDoJogo.enabled = true;
            if (comandoDoHeroi != null && jaulaGuardada)
            {
                comandoDoHeroi.usarLimites = limitesDeAntes;
                comandoDoHeroi.xMinimo = xMinimoDeAntes;
                comandoDoHeroi.xMaximo = xMaximoDeAntes;
            }
            if (portaDeVolta != null)
            {
                portaDeVolta.Liberar(true);

                // a trava do heroi tem de ir ATE' o vao, senao ele para' a meio metro do
                // gatilho e a fazenda vira uma armadilha sem saida
                float ateOnde = portaDeVolta.transform.position.x;
                var parede = portaDeVolta.paredeQueSai != null
                    ? portaDeVolta.paredeQueSai.GetComponentInChildren<Collider>(true) : null;
                if (parede != null) ateOnde = parede.bounds.center.x;
                if (comandoDoHeroi != null && comandoDoHeroi.usarLimites)
                {
                    comandoDoHeroi.xMaximo = Mathf.Max(comandoDoHeroi.xMaximo, ateOnde + 1.5f);
                    comandoDoHeroi.xMinimo = Mathf.Min(comandoDoHeroi.xMinimo, ateOnde - 1.5f);
                }
                SetaDoCaminho.Apontar(ateOnde > sardael.position.x ? +1 : -1);
            }
            else Debug.LogWarning("DiretorDoCapitao sem portaDeVolta: o jogador fica preso na "
                                + "fazenda depois do aviso.", this);
        }

        /// <summary>
        /// Chamado quando o palco esta' montado e os orcs sao do jogador. O combate ainda nao
        /// existe: ligue aqui o que for feito depois, sem mexer no resto do roteiro.
        /// </summary>
        public virtual void AoLiberarOsOrcs()
        {
            if (orcsDoDesafio == null) return;
            foreach (var o in orcsDoDesafio)
                if (o != null && !o.gameObject.activeSelf) o.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ peças
        /// <summary>
        /// Trava ou solta o comando do Sardael.
        ///
        /// A trava e' recalculada todo quadro no <see cref="Update"/> porque TEXTO NA TELA
        /// TAMBEM TRAVA: se o controle ficasse por conta de cada etapa lembrar de chamar
        /// isto, bastaria uma fala nova entrar no roteiro pra o jogador sair andando no meio
        /// do dialogo. Assim a regra vale sozinha pra qualquer fala que apareca.
        /// </summary>
        void Travar(bool quanto) { travaDoRoteiro = quanto; AplicarTrava(); }

        void AplicarTrava()
        {
            if (comandoDoHeroi == null) return;
            comandoDoHeroi.Travar(this, travaDoRoteiro
                                     || (caixaDeFala != null && caixaDeFala.Falando));
        }

        void Update() { AplicarTrava(); }

        void TravarACamera()
        {
            if (cameraDoJogo == null) return;
            cameraDoJogo.enabled = false;                       // ela para onde estiver

            // guarda a faixa de antes: e' o que <see cref="AbrirAVolta"/> repoe depois
            limitesDeAntes = comandoDoHeroi.usarLimites;
            xMinimoDeAntes = comandoDoHeroi.xMinimo;
            xMaximoDeAntes = comandoDoHeroi.xMaximo;
            jaulaGuardada = true;

            float centro = cameraDoJogo.transform.position.x;
            comandoDoHeroi.usarLimites = true;
            comandoDoHeroi.xMinimo = centro - meiaLarguraDaArena;
            comandoDoHeroi.xMaximo = centro + meiaLarguraDaArena;
        }

        Combatente LigarCombatente(Transform quem, TocadorDeClipe tocador,
                                   AnimationClip guarda, AnimationClip[] golpes, AnimationClip defesa)
        {
            // o tocador tem de sair primeiro: dois grafos no mesmo Animator se anulam e o
            // boneco fica em T
            if (tocador != null) tocador.enabled = false;
            var c = quem.GetComponent<Combatente>();
            if (c == null) c = quem.gameObject.AddComponent<Combatente>();
            c.guarda = guarda; c.golpes = golpes; c.defesa = defesa;
            c.enabled = true;
            return c;
        }

        void DesligarCombatente(Transform quem, TocadorDeClipe tocador)
        {
            var c = quem.GetComponent<Combatente>();
            if (c != null) c.enabled = false;
            if (tocador != null) tocador.enabled = true;
        }

        /// <summary>
        /// O CAPITAO GUIA. Ele anda no eixo da marcha, <see cref="distanciaNaFrente"/> metros
        /// a frente do Sardael: se Sardael avanca ele avanca, se Sardael para ou volta ele
        /// para ou volta junto.
        ///
        /// O jeito ingenuo — colar o capitao num ponto fixo atras do heroi e correr atras
        /// dele todo quadro — nao funciona. Um passo do Sardael ja' move a ancora, a
        /// distancia fica oscilando em torno do limiar, o clipe troca de andar pra parado e
        /// de volta a cada quadro, e como trocar clipe REMONTA O GRAFO, a animacao reinicia
        /// no quadro zero sem parar. Da' o boneco travado e saltando.
        ///
        /// Por isso tres travas: zona morta com histerese (<see cref="folga"/>), velocidade
        /// com aceleracao em vez de degrau, e tempo minimo de permanencia no clipe
        /// (<see cref="trocaMinima"/>).
        /// </summary>
        void LiderarAFrente()
        {
            var eixo = Plano(troll.position - sardael.position).normalized;   // sentido da marcha
            if (eixo.sqrMagnitude < 0.0001f) eixo = capitao.forward;
            var alvo = Plano(sardael.position) + eixo * distanciaNaFrente;
            float erro = Vector3.Dot(alvo - Plano(capitao.position), eixo);   // + adianta, - recua

            if (emMarcha) { if (Mathf.Abs(erro) < folga * 0.34f) emMarcha = false; }
            else          { if (Mathf.Abs(erro) > folga)         emMarcha = true;  }

            // O teto TEM de passar da corrida do Sardael. Se ficar na passada gravada do
            // clipe (3,80) o heroi corre a 4,5 e ultrapassa o guia: medi isso e a distancia
            // caiu de 3,2 m pra 0,46 m no trecho de corrida. O ritmo do clipe cobre a
            // diferenca — e' pra isso que ele existe.
            float corridaDoHeroi = comandoDoHeroi != null ? comandoDoHeroi.velocidadeCorrer : 0f;
            float tetoDeVelocidade = Mathf.Max(velocidadeCorrendo, corridaDoHeroi) * 1.2f;

            // A velocidade do heroi no eixo, medida do deslocamento dele — e' o termo que
            // ANTECIPA. So' corrigir o erro nao basta: a correcao e' proporcional ao buraco,
            // entao o capitao equilibra num buraco fixo (medi 1,3 m em vez de 3,2). Somando
            // o passo do heroi, em regime o erro vai a zero e a distancia se mantem.
            var ondeEleEsta = Plano(sardael.position);
            float doHeroi = Vector3.Dot(ondeEleEsta - ultimaDoHeroi, eixo) / Mathf.Max(Time.deltaTime, 0.0001f);
            ultimaDoHeroi = ondeEleEsta;
            passoDoHeroi = Mathf.Lerp(passoDoHeroi, doHeroi, 1f - Mathf.Exp(-12f * Time.deltaTime));

            float desejada = emMarcha
                ? Mathf.Clamp(passoDoHeroi + erro / Mathf.Max(0.05f, tempoDeResposta),
                              -tetoDeVelocidade, tetoDeVelocidade)
                : Mathf.Clamp(passoDoHeroi, -tetoDeVelocidade, tetoDeVelocidade);
            velocidadeDoCapitao = Mathf.MoveTowards(velocidadeDoCapitao, desejada, aceleracao * Time.deltaTime);

            if (Mathf.Abs(velocidadeDoCapitao) > 0.01f)
                Plantar(capitao, Plano(capitao.position) + eixo * velocidadeDoCapitao * Time.deltaTime);

            // andando pra tras ele vira de costas pro troll, que e' o certo: esta voltando
            var olhar = Mathf.Abs(velocidadeDoCapitao) > 0.12f
                      ? eixo * Mathf.Sign(velocidadeDoCapitao) : eixo;
            capitao.rotation = Quaternion.RotateTowards(capitao.rotation,
                Quaternion.LookRotation(olhar), 420f * Time.deltaTime);

            EscolherPasso(Mathf.Abs(velocidadeDoCapitao));
        }

        /// <summary>Escolhe andar/correr/parado pela velocidade, com tempo minimo no clipe.</summary>
        void EscolherPasso(float v)
        {
            var querido = v < 0.30f ? parado
                        : v < (velocidadeAndando + velocidadeCorrendo) * 0.5f ? andando
                        : (correndo != null ? correndo : andando);

            if (querido != clipeDoCapitao && Time.time - quandoTrocouDeClipe >= trocaMinima)
            {
                clipeDoCapitao = querido;
                quandoTrocouDeClipe = Time.time;
                Pousar(capitao, querido);
            }

            if (tocadorCapitao == null) return;
            float passada = clipeDoCapitao == correndo ? velocidadeCorrendo
                          : clipeDoCapitao == andando  ? velocidadeAndando : 0f;
            tocadorCapitao.Ritmo(passada > 0.01f ? Mathf.Clamp(v / passada, 0.55f, 1.6f) : 1f);
        }

        /// <summary>Os dois caminham ao mesmo tempo, cada um pro seu posto, e se encontram.</summary>
        IEnumerator AndarOsDois(Vector3 destinoDoCapitao, Vector3 destinoDoTroll)
        {
            clipeDoCapitao = andando;
            Pousar(capitao, andando);
            if (tocadorCapitao != null) tocadorCapitao.Ritmo(1f);
            if (trollAndando != null) Pousar(troll, trollAndando);

            bool chegouCap = false, chegouTroll = false;
            while (!chegouCap || !chegouTroll)
            {
                chegouCap   = Aproximar(capitao, destinoDoCapitao, velocidadeAndando);
                chegouTroll = Aproximar(troll,   destinoDoTroll,   velocidadeDoTroll);
                yield return null;
            }
            clipeDoCapitao = parado;
            Pousar(capitao, parado);
            Pousar(troll, guardaDoTroll);
        }

        bool Aproximar(Transform quem, Vector3 destino, float velocidade)
        {
            var falta = Plano(destino) - Plano(quem.position);
            float d = falta.magnitude;
            if (d < 0.06f) return true;
            quem.rotation = Quaternion.RotateTowards(quem.rotation,
                Quaternion.LookRotation(falta.normalized), 420f * Time.deltaTime);
            Plantar(quem, Plano(quem.position) + falta.normalized * Mathf.Min(velocidade * Time.deltaTime, d));
            return false;
        }

        IEnumerator AndarAte(Transform quem, Vector3 destino, float velocidade,
                             AnimationClip passo, AnimationClip fim)
        {
            Pousar(quem, passo);
            destino = Plano(destino);
            while (true)
            {
                var falta = destino - Plano(quem.position);
                float d = falta.magnitude;
                if (d < 0.06f) break;
                quem.rotation = Quaternion.RotateTowards(quem.rotation,
                    Quaternion.LookRotation(falta.normalized), 420f * Time.deltaTime);
                float anda = Mathf.Min(velocidade * Time.deltaTime, d);
                Plantar(quem, Plano(quem.position) + falta.normalized * anda);
                yield return null;
            }
            Plantar(quem, destino);
            Pousar(quem, fim);
        }

        /// <summary>
        /// Anda a raiz pela emenda medida entre dois clipes pareados, no eixo do capitao,
        /// mantendo o pe no chao. E' o conserto do teleporte na troca de animacao.
        ///
        /// Os dois personagens usam o eixo do CAPITAO de proposito: o troll esta' virado 180
        /// graus, entao passar a emenda pelo eixo dele inverteria o vetor.
        /// </summary>
        void Emendar(Transform quem, Vector3 emenda)
        {
            if (quem == null || emenda.sqrMagnitude < 0.000001f) return;
            var mundo = capitao.TransformVector(new Vector3(emenda.x, 0f, emenda.z));
            Plantar(quem, Plano(quem.position) + mundo);
        }

        /// <summary>Poe a vitima na frente do atacante, virada 180 graus. E' o encaixe do par.</summary>
        void Assentar(float distancia)
        {
            var frente = capitao.forward; frente.y = 0f; frente.Normalize();
            var p = Plano(capitao.position) + frente * distancia;
            Plantar(troll, p);
            troll.rotation = Quaternion.LookRotation(-frente);
        }

        void Encarar(Transform quem, Transform paraQuem)
        {
            var d = Plano(paraQuem.position - quem.position);
            if (d.sqrMagnitude < 0.0004f) return;
            quem.rotation = Quaternion.LookRotation(d.normalized);
        }

        void Pousar(Transform quem, AnimationClip clipe)
        {
            if (quem == null || clipe == null) return;
            var t = quem.GetComponent<TocadorDeClipe>();
            if (t == null || t.clipe == clipe) return;
            t.congelarNoFim = false;
            t.Trocar(clipe);
        }

        void PousarUmaVez(Transform quem, AnimationClip clipe)
        {
            if (quem == null || clipe == null) return;
            var t = quem.GetComponent<TocadorDeClipe>();
            if (t == null) return;
            t.enabled = true;
            t.congelarNoFim = true;
            // CORTE SECO, sem mistura: a raiz anda a emenda no mesmo instante da troca, e
            // misturar as duas poses faria o corpo escorregar durante a transicao.
            t.Trocar(clipe, 0f);
        }

        Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Poe o objeto em XZ e encosta o pe no chao.</summary>
        void Plantar(Transform quem, Vector3 xz)
        {
            float y = chao != null ? chao.SampleHeight(xz) + chao.transform.position.y : quem.position.y;
            quem.position = new Vector3(xz.x, y, xz.z);
        }

        void Dizer(string quem, string oQue) { if (caixaDeFala != null) caixaDeFala.Dizer(quem, oQue); }
        void Calar() { if (caixaDeFala != null) caixaDeFala.Calar(); }

        void OnGUI()
        {
            if (!mostrarPainel) return;
            GUI.Box(new Rect(10, 10, 250, 44), "");
            GUI.Label(new Rect(20, 18, 240, 30), "cena do capitao: " + Agora);
        }
    }
}
