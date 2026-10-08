using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    public enum ModoMovimentoHeroi
    {
        Exploracao,
        Combate,
        RetornoALinha
    }

    /// <summary>
    /// Movimento do Sardael, travado na linha 2.5D. SO' MOVIMENTO — combate nao entra aqui.
    ///
    /// QUEM MANDA NA ANIMACAO E' O ANIMATOR, NAO ESTE SCRIPT.
    ///
    /// Este e' o ponto que mudou depois de dois dias perdidos. Antes eu tinha escrito meu
    /// proprio tocador de animacao (grafo de Playables) e minha propria maquina de estados —
    /// e a partir dali TODO defeito foi na minha camada: peso de mistura, tempo do clipe,
    /// ordem de execucao, previsao de fim. Coisas que o AnimatorController do Unity ja'
    /// resolve, testado por todo mundo.
    ///
    /// Agora a divisao e' limpa:
    ///   - este script LE a entrada, calcula velocidade e gravidade, e SETA parametros;
    ///   - o AnimatorController decide que animacao toca e quando ela acaba;
    ///   - quando uma habilidade esta' rodando, este script PERGUNTA ao Animator
    ///     (<see cref="EmAcao"/>) e tira as maos do volante.
    ///
    /// O "comeco e fim das habilidades" e' o Exit Time do Unity: a transicao de volta pra
    /// locomocao so' dispara quando a animacao termina. Nao ha' adivinhacao de duracao em
    /// lugar nenhum deste arquivo.
    ///
    /// QUEM DESLOCA: na locomocao, o codigo (velX). Nas habilidades, o proprio clipe — eles
    /// sao _RM e carregam 1,85 m de esquiva, 3,70 m de rolamento, 3,67 m de arranco. O codigo
    /// so' recolhe esse deslocamento em <see cref="OnAnimatorMove"/> e entrega ao
    /// CharacterController, pra colidir direito.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class MovimentoDoHeroi : MonoBehaviour
    {
        public const float VELOCIDADE_DAS_ANIMACOES = 1.5f;

        // nomes dos parametros do AnimatorController, num lugar so'
        public const string P_VELOCIDADE = "velocidade";
        public const string P_NO_CHAO    = "noChao";
        public const string P_PULAR      = "pular";
        public const string P_ESQUIVAR   = "esquivar";
        public const string P_ESQUIVA_HORIZONTAL = "esquivaHorizontal";
        public const string P_ESQUIVA_VERTICAL   = "esquivaVertical";
        public const string P_ROLAR      = "rolar";
        public const string P_ARRANCAR   = "arrancar";

        /// <summary>
        /// Enquanto ligado, o teclado FISICO e' ignorado: so' valem os Injetar*.
        ///
        /// POR QUE ISTO EXISTE: a entrada do jogador e a do teste entram pela mesma variavel de
        /// proposito — testar por um atalho que o jogador nao usa nao prova nada — mas elas se
        /// SOMAM, e durante um teste automatico isso torna a rodada irreproduzivel. Um Alt+Tab
        /// no meio dos 45 s de teste virou arranco (o arranco e' LeftAlt), o arranco tem root
        /// motion proprio, e o Sardael atravessou o orc: oito golpes reportados como "passou
        /// longe" que nao tinham nada a ver com o combate.
        ///
        /// A regra da casa continua de pe': o teste entra pelos MESMOS campos. Isto so' desliga
        /// a SOMA do teclado de verdade enquanto ninguem deveria estar jogando.
        /// </summary>
        public static bool SoInjecao;

        // Sem Domain Reload o estatico atravessa partidas. Um "true" esquecido aqui faria o jogo
        // abrir sem responder a tecla nenhuma, e sem erro no console.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoComecarAPartida() { SoInjecao = false; }
        public const string TAG_ACAO     = "acao";

        [Header("Andar — razao 1,875x herdada do tuning.js")]
        public float velocidadeAndar = 2.4f;
        public float velocidadeCorrer = 4.5f;
        [Tooltip("Quao rapido chega na velocidade. Alto = responde na hora.")]
        public float aceleracao = 16f;

        [Header("Pular")]
        public float alturaDoPulo = 1.1f;
        [Tooltip("Mais forte que a gravidade real: queda real em jogo parece flutuante.")]
        public float gravidade = -24f;
        [Tooltip("Perdao pra quem aperta pular logo depois de sair da borda.")]
        public float tempoDeCoiote = 0.12f;
        [Range(0f, 1f), Tooltip("Quanto do controle horizontal sobra no ar.")]
        public float controleNoAr = 0.7f;

        [Header("Linha 2.5D")]
        public float zDaLinha = 0f;
        public float giroPorSegundo = 480f;
        public bool usarLimites = true;
        public float xMinimo = -38f, xMaximo = 38f;   // o chao tem 80 m; a trava batia em 24 e ele parava no nada

        [Header("Free Flow 3D")]
        [SerializeField] EntradaDeCombate entrada;
        [SerializeField, Min(0.1f)] float velocidadeDeCombate = 4.2f;
        [SerializeField, Min(0.1f)] float aceleracaoDeCombate = 18f;
        [SerializeField, Min(0.1f)] float velocidadeDeRetornoALinha = 6f;
        [SerializeField, Min(0.001f)] float toleranciaDaLinha = 0.025f;
        [SerializeField, Min(1f)] float multiplicadorDistanciaDaEsquiva = 1.35f;

        [Header("Depuracao")]
        public bool mostrarPainel = true;

        /// <summary>
        /// Trava a entrada SEM desligar o componente — e' o que a conversa usa.
        /// Desligar o script pararia tambem a gravidade, e quem conversasse em cima de um
        /// degrau ficaria pendurado no ar ate' a conversa acabar.
        /// </summary>
        /// <summary>
        /// Trava manual, pra quem quer segurar o heroi sem se identificar. PREFIRA
        /// <see cref="Travar"/>: um booleano so' pertence a quem escreveu por ultimo.
        /// </summary>
        [System.NonSerialized] public bool bloqueado;

        /// <summary>
        /// Quem esta' segurando o heroi agora.
        ///
        /// E' um conjunto, e nao um booleano, porque mais de um sistema segura o heroi ao
        /// mesmo tempo: o diretor da cutscene e a <see cref="Interacao"/>. Com um booleano
        /// escrito todo quadro por cada um, quem roda por ultimo ganha — foi assim que o
        /// jogador saiu correndo no meio da fala da mensageira, com a Interacao zerando a
        /// trava do diretor a cada Update.
        /// </summary>
        readonly System.Collections.Generic.HashSet<Object> travas = new System.Collections.Generic.HashSet<Object>();

        /// <summary>Segura ou solta o heroi em nome de <paramref name="quem"/>.</summary>
        public void Travar(Object quem, bool quanto)
        {
            if (quem == null) return;
            if (quanto) travas.Add(quem); else travas.Remove(quem);
        }

        /// <summary>O heroi esta' preso por alguem?</summary>
        public bool Travado { get { return bloqueado || travas.Count > 0; } }

        public bool NoChao { get; private set; }
        public float VelocidadeAtual => modoAtual == ModoMovimentoHeroi.Exploracao
            ? Mathf.Abs(velX)
            : velocidadePlanar.magnitude;
        public int Olhando { get { return olhando; } }
        public ModoMovimentoHeroi ModoAtual => modoAtual;
        public bool EmModoCombate => modoAtual == ModoMovimentoHeroi.Combate;
        public bool EmEsquiva => esquivaSolicitada || EstadoDeEsquivaAtivo();
        public bool RetornoConcluido => modoAtual != ModoMovimentoHeroi.RetornoALinha ||
            Mathf.Abs(transform.position.z - zDeRetorno) <= toleranciaDaLinha;
        public Vector2 MovimentoLido { get; private set; }

        /// <summary>
        /// Uma habilidade esta' rodando? Quem responde e' o Animator, pelo TAG do estado.
        /// Conta tambem a transicao de entrada, senao ha' uns quadros em que a habilidade ja'
        /// comecou e o script ainda acha que pode mexer no corpo.
        /// </summary>
        public bool EmAcao
        {
            get
            {
                if (anim == null) return false;
                if (anim.GetCurrentAnimatorStateInfo(0).IsTag(TAG_ACAO)) return true;
                if (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag(TAG_ACAO)) return true;
                return false;
            }
        }

        public string EstadoDaAnimacao
        {
            get
            {
                if (anim == null) return "-";
                var e = anim.GetCurrentAnimatorStateInfo(0);
                foreach (var n in nomesDeEstado) if (e.IsName(n)) return n;
                return "?";
            }
        }

        // os de combate entraram porque o painel mostrava "?" justamente nos quadros que as
        // fotos dos testes precisam provar — nao da' pra ler de uma foto se o heroi apanhou ou aparou
        static readonly string[] nomesDeEstado = {
            "Locomocao", "PuloSaida", "PuloNoAr", "PuloQueda",
            "Esquiva", "Rolar", "Arranco",
            "Golpe1", "Golpe2", "Golpe3", "Golpe4", "Atingido", "Aparo",
            "Bloqueio", "ContraAtaque", "Morte" };

        CharacterController cc;
        Animator anim;
        CombateDoHeroi combate;
        float velX, velY;
        Vector3 velocidadePlanar;
        int olhando = 1;
        float saiuDoChaoEm = -99f;
        Vector3 deslocamentoDaAnimacao;
        Vector3 deslocamentoDeCombate;
        Vector3 direcaoDeCombate = Vector3.right;
        Transform alvoContextualDeCombate;
        ModoMovimentoHeroi modoAtual = ModoMovimentoHeroi.Exploracao;
        float zDeRetorno;
        RuntimeAnimatorController controladorComParametrosConhecidos;
        bool temVelocidade;
        bool temNoChao;
        bool temEsquivaHorizontal;
        bool temEsquivaVertical;
        bool esquivaSolicitada;
        bool esquivaVistaNoAnimator;
        float esquivaSolicitadaEm;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            anim = GetComponent<Animator>();
            combate = GetComponent<CombateDoHeroi>();
            if (entrada == null) entrada = GetComponent<EntradaDeCombate>();
            anim.applyRootMotion = true;
            anim.speed = VELOCIDADE_DAS_ANIMACOES;

            // LER O VETOR, NAO RECALCULAR O ANGULO.
            // cos(90 graus) em ponto flutuante da' -4,4e-8. Lendo o angulo, um corpo nascido
            // em Y=90 era interpretado como olhando pro lado oposto e girava 180 graus no
            // primeiro quadro, antes de qualquer tecla.
            olhando = transform.forward.x >= 0f ? 1 : -1;
        }

        void Start()
        {
            // Um passo de nada, so' para o CharacterController calcular isGrounded ANTES do
            // primeiro Update.
            //
            // Sem isto, no quadro 1 o cc.isGrounded vem falso (ele ainda nao se moveu), o
            // parametro noChao vai falso, o Animator cai na rede de seguranca "caiu de uma
            // borda" e Sardael COMECA O JOGO ATERRISSANDO. Foi o que o teste mostrou: 0,35 s
            // de animacao de queda sem ninguem ter encostado no teclado.
            cc.Move(new Vector3(0f, -0.02f, 0f));
            saiuDoChaoEm = Time.time;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            Vector2 eixo; bool correndo, pulou, esquivou, rolou, arrancou;
            LerEntrada(out eixo, out correndo, out pulou, out esquivou, out rolou, out arrancou);
            MovimentoLido = eixo;

            NoChao = cc.isGrounded;
            if (NoChao)
            {
                saiuDoChaoEm = Time.time;
                if (velY < 0f) velY = -2f;   // cola no chao em rampa
            }

            bool ocupado = EmAcao;
            AtualizarConhecimentoDosParametros();
            AtualizarEstadoDaEsquiva();

            // ---- habilidades: so' pedem, quem executa e' o Animator ----
            bool cancelouAtaqueParaEsquivar = false;
            if (esquivou && NoChao && ocupado && combate != null)
            {
                cancelouAtaqueParaEsquivar = combate.CancelarParaAcaoPrioritaria();
                if (cancelouAtaqueParaEsquivar) ocupado = false;
            }

            if (NoChao && esquivou && !ocupado)
            {
                IniciarEsquiva(eixo, cancelouAtaqueParaEsquivar);
                // O estado so' entra na avaliacao do Animator no fim do quadro. Sem isto,
                // a locomocao abaixo ainda usa o analogico da esquiva e gira o corpo,
                // fazendo um dodge lateral virar outro recuo em relacao ao novo facing.
                ocupado = true;
            }
            else if (!ocupado && NoChao)
            {
                if (rolou) { anim.SetTrigger(P_ROLAR); velX = 0f; }
                else if (arrancou) { anim.SetTrigger(P_ARRANCAR); velX = 0f; }
            }

            // ---- andar ----
            if (ocupado || modoAtual == ModoMovimentoHeroi.RetornoALinha)
            {
                velX = 0f;   // durante a habilidade quem desloca e' o clipe
                velocidadePlanar = Vector3.zero;
            }
            else if (EmModoCombate)
            {
                Vector3 intencao = Vector3.ClampMagnitude(new Vector3(eixo.x, 0f, eixo.y), 1f);
                float rapidez = correndo ? velocidadeCorrer : velocidadeDeCombate;
                velocidadePlanar = Vector3.MoveTowards(
                    velocidadePlanar, intencao * rapidez, aceleracaoDeCombate * dt);
                velX = 0f;
                if (alvoContextualDeCombate == null && intencao.sqrMagnitude > 0.01f)
                    DefinirDirecaoDeCombate(intencao);
            }
            else
            {
                float peso = NoChao ? 1f : controleNoAr;
                float alvo = eixo.x * (correndo ? velocidadeCorrer : velocidadeAndar);
                velX = Mathf.MoveTowards(velX, alvo, aceleracao * peso * dt);
                velocidadePlanar = Vector3.zero;
                if (Mathf.Abs(eixo.x) > 0.01f) olhando = eixo.x > 0f ? 1 : -1;
            }

            // ---- pular ----
            // O isGrounded do CharacterController PISCA: uma emenda no chao, uma rampa, e ele
            // responde "no ar" por um quadro. Se esse quadro chegar cru no Animator, dispara a
            // animacao de queda a toa. O mesmo tempo de coiote que perdoa o jogador filtra a
            // piscada — e um pulo de verdade fura o filtro, porque zera o saiuDoChaoEm.
            bool podePular = NoChao || (Time.time - saiuDoChaoEm) <= tempoDeCoiote;
            if (pulou && podePular && !ocupado)
            {
                velY = Mathf.Sqrt(2f * alturaDoPulo * -gravidade);
                saiuDoChaoEm = -99f;
                anim.SetTrigger(P_PULAR);
            }
            velY += gravidade * dt;

            // ---- contar pro Animator o que esta' acontecendo ----
            if (temVelocidade) anim.SetFloat(P_VELOCIDADE, VelocidadeAtual);
            if (temNoChao) anim.SetBool(P_NO_CHAO, podePular);

            Mover(dt);
        }

        /// <summary>
        /// Recolhe o deslocamento que a ANIMACAO produziu.
        ///
        /// Com AnimatorController, <c>animator.deltaPosition</c> e' valido — foi justamente o
        /// que faltava quando eu usava um grafo de Playables proprio, onde ele vinha zerado e
        /// o corpo derivava pra fora do personagem.
        ///
        /// Na exploracao so' o X importa. No Free Flow, o delta mundial X/Z acompanha a
        /// orientacao dada ao personagem antes da habilidade.
        /// </summary>
        void OnAnimatorMove()
        {
            if (anim == null) return;
            if (!EmAcao) return;                  // na locomocao quem anda e' velX
            if (EmModoCombate)
            {
                Vector3 delta = anim.deltaPosition;
                delta.y = 0f;
                if (EstadoDeEsquivaAtivo()) delta *= multiplicadorDistanciaDaEsquiva;
                deslocamentoDaAnimacao += delta;
            }
            else if (modoAtual == ModoMovimentoHeroi.Exploracao)
                deslocamentoDaAnimacao.x += anim.deltaPosition.x;
        }

        void Mover(float dt)
        {
            Vector3 passo = deslocamentoDeCombate;
            deslocamentoDeCombate = Vector3.zero;

            if (modoAtual == ModoMovimentoHeroi.Exploracao)
                passo += new Vector3(velX * dt + deslocamentoDaAnimacao.x, 0f, 0f);
            else if (modoAtual == ModoMovimentoHeroi.Combate)
                passo += velocidadePlanar * dt + new Vector3(
                    deslocamentoDaAnimacao.x, 0f, deslocamentoDaAnimacao.z);
            else
            {
                float erro = zDeRetorno - transform.position.z;
                float passoZ = Mathf.Clamp(
                    erro, -velocidadeDeRetornoALinha * dt, velocidadeDeRetornoALinha * dt);
                passo += new Vector3(0f, 0f, passoZ);
            }
            deslocamentoDaAnimacao = Vector3.zero;
            passo.y = velY * dt;
            cc.Move(passo);

            if (modoAtual == ModoMovimentoHeroi.Exploracao)
            {
                // Fora do encontro a regra 2.5D continua absoluta.
                var p = transform.position;
                p.z = zDaLinha;
                if (usarLimites) p.x = Mathf.Clamp(p.x, xMinimo, xMaximo);
                transform.position = p;
            }
            else if (usarLimites)
            {
                var p = transform.position;
                p.x = Mathf.Clamp(p.x, xMinimo, xMaximo);
                transform.position = p;
            }

            Quaternion rotacaoAlvo;
            if (modoAtual == ModoMovimentoHeroi.Exploracao)
                rotacaoAlvo = Quaternion.Euler(0f, olhando > 0 ? 90f : -90f, 0f);
            else
                rotacaoAlvo = Quaternion.LookRotation(direcaoDeCombate, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, rotacaoAlvo, giroPorSegundo * dt);
        }

        // ---------------------------------------------------------------- teclado de mentira
        //
        // O teste automatico entra por aqui, pelas MESMAS variaveis que a tecla de verdade.
        // Nao ha' caminho alternativo dentro do Update: se o teste passa, e' porque o
        // caminho real passa. Testar por um atalho que o jogador nao usa nao prova nada.
        //
        // Os gatilhos ficam ligados ate' serem lidos — assim nao se perdem se o script de
        // teste rodar depois deste no quadro.
        Vector2 eixoDeFora; bool correrDeFora;
        bool puloDeFora, esquivaDeFora, rolarDeFora, arrancoDeFora;

        public void InjetarEixo(float v)   { eixoDeFora.x = v; }
        public void InjetarMovimento(Vector2 v) { eixoDeFora = Vector2.ClampMagnitude(v, 1f); }
        public void InjetarCorrer(bool v)  { correrDeFora = v; }
        public void InjetarPulo()          { puloDeFora = true; }
        public void InjetarEsquiva()       { esquivaDeFora = true; }
        public void InjetarRolar()         { rolarDeFora = true; }
        public void InjetarArranco()       { arrancoDeFora = true; }

        /// <summary>
        /// Acrescenta uma correcao planar ao mesmo pipeline do root motion.
        /// O deslocamento e consumido por <see cref="CharacterController.Move(Vector3)"/>.
        /// </summary>
        public bool AdicionarDeslocamentoDeCombate(float deltaX)
        {
            return AdicionarDeslocamentoDeCombate(new Vector3(deltaX, 0f, 0f));
        }

        public bool AdicionarDeslocamentoDeCombate(Vector3 deltaPlanar)
        {
            deltaPlanar.y = 0f;
            if (Travado || !isActiveAndEnabled || deltaPlanar.sqrMagnitude <= 0.0000001f)
                return false;
            deslocamentoDeCombate += deltaPlanar;
            return true;
        }

        public void CancelarDeslocamentoDeCombate()
        {
            velX = 0f;
            velocidadePlanar = Vector3.zero;
            deslocamentoDaAnimacao = Vector3.zero;
            deslocamentoDeCombate = Vector3.zero;
        }

        /// <summary>Permite ao targeting orientar Sardael sem mover ou teleportar o corpo.</summary>
        public bool DefinirDirecaoDeCombate(int direcao)
        {
            if (direcao == 0) return false;
            olhando = direcao < 0 ? -1 : 1;
            direcaoDeCombate = new Vector3(olhando, 0f, 0f);
            return true;
        }

        public bool DefinirDirecaoDeCombate(Vector3 direcao)
        {
            direcao.y = 0f;
            if (direcao.sqrMagnitude <= 0.0001f) return false;
            direcaoDeCombate = direcao.normalized;
            if (Mathf.Abs(direcaoDeCombate.x) > 0.05f)
                olhando = direcaoDeCombate.x < 0f ? -1 : 1;
            return true;
        }

        public void DefinirAlvoContextualDeCombate(Transform alvo)
        {
            alvoContextualDeCombate = alvo;
        }

        /// <summary>
        /// Converte a entrada mundial X/Z para direita/frente do corpo em combate.
        /// Sardael continua encarando o alvo enquanto o Blend Tree escolhe a esquiva.
        /// Sem direcao, o comportamento tradicional continua sendo recuar.
        /// </summary>
        Vector2 CalcularDirecaoDaEsquiva(Vector2 eixo)
        {
            Vector3 intencao = new Vector3(eixo.x, 0f, eixo.y);
            if (intencao.sqrMagnitude < 0.04f) return Vector2.down;

            Vector3 frente = direcaoDeCombate;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.0001f) frente = transform.forward;
            frente.Normalize();

            Vector3 direita = Vector3.Cross(Vector3.up, frente).normalized;
            intencao.Normalize();
            return new Vector2(
                Vector3.Dot(intencao, direita),
                Vector3.Dot(intencao, frente)).normalized;
        }

        void IniciarEsquiva(Vector2 eixo, bool interrompeuAtaque)
        {
            Vector2 direcaoDaEsquiva = EmModoCombate
                ? CalcularDirecaoDaEsquiva(eixo)
                : Vector2.down;
            if (temEsquivaHorizontal)
                anim.SetFloat(P_ESQUIVA_HORIZONTAL, direcaoDaEsquiva.x);
            if (temEsquivaVertical)
                anim.SetFloat(P_ESQUIVA_VERTICAL, direcaoDaEsquiva.y);

            if (interrompeuAtaque) anim.CrossFade("Esquiva", 0.03f, 0, 0f);
            else anim.SetTrigger(P_ESQUIVAR);

            esquivaSolicitada = true;
            esquivaVistaNoAnimator = false;
            esquivaSolicitadaEm = Time.time;
            velX = 0f;
            velocidadePlanar = Vector3.zero;
        }

        void AtualizarEstadoDaEsquiva()
        {
            if (EstadoDeEsquivaAtivo())
            {
                esquivaVistaNoAnimator = true;
                return;
            }

            if (esquivaSolicitada &&
                (esquivaVistaNoAnimator || Time.time - esquivaSolicitadaEm > 0.5f))
            {
                esquivaSolicitada = false;
                esquivaVistaNoAnimator = false;
            }
        }

        bool EstadoDeEsquivaAtivo()
        {
            if (anim == null) return false;
            if (anim.GetCurrentAnimatorStateInfo(0).IsName("Esquiva")) return true;
            return anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsName("Esquiva");
        }

        public void EntrarEmCombate()
        {
            modoAtual = ModoMovimentoHeroi.Combate;
            velocidadePlanar = Vector3.zero;
            velX = 0f;
            if (transform.forward.sqrMagnitude > 0.01f)
                DefinirDirecaoDeCombate(transform.forward);
        }

        public void IniciarRetornoALinha(float zOriginal)
        {
            modoAtual = ModoMovimentoHeroi.RetornoALinha;
            zDeRetorno = zOriginal;
            velocidadePlanar = Vector3.zero;
            velX = 0f;
            CancelarDeslocamentoDeCombate();
        }

        public void EntrarEmExploracao(float zOriginal)
        {
            zDaLinha = zOriginal;
            zDeRetorno = zOriginal;
            modoAtual = ModoMovimentoHeroi.Exploracao;
            alvoContextualDeCombate = null;
            velocidadePlanar = Vector3.zero;
            var p = transform.position;
            p.z = zOriginal;
            transform.position = p;
        }

        void AtualizarConhecimentoDosParametros()
        {
            if (anim == null || anim.runtimeAnimatorController == controladorComParametrosConhecidos)
                return;
            controladorComParametrosConhecidos = anim.runtimeAnimatorController;
            temVelocidade = false;
            temNoChao = false;
            temEsquivaHorizontal = false;
            temEsquivaVertical = false;
            foreach (var parametro in anim.parameters)
            {
                if (parametro.name == P_VELOCIDADE) temVelocidade = true;
                else if (parametro.name == P_NO_CHAO) temNoChao = true;
                else if (parametro.name == P_ESQUIVA_HORIZONTAL) temEsquivaHorizontal = true;
                else if (parametro.name == P_ESQUIVA_VERTICAL) temEsquivaVertical = true;
            }
        }

        void LerEntrada(out Vector2 eixo, out bool correndo, out bool pulou,
                        out bool esquivou, out bool rolou, out bool arrancou)
        {
            if (Travado)
            {
                eixo = Vector2.zero; correndo = false; pulou = false;
                esquivou = false; rolou = false; arrancou = false;
                eixoDeFora = Vector2.zero; correrDeFora = false;
                puloDeFora = esquivaDeFora = rolarDeFora = arrancoDeFora = false;
                entrada?.DesarmarCorrida();

                // PARA NA HORA, nao por desaceleracao. Zerar so' a entrada deixa a velocidade
                // vigente escorrer pela aceleracao (16 m/s2), o que da' meio metro de deslize
                // depois do "pare" — e numa conversa o jogador anda por baixo do dialogo.
                // O deslocamento do clipe tambem morre aqui: habilidade em curso continuaria
                // empurrando o corpo mesmo com a entrada zerada.
                velX = 0f;
                velocidadePlanar = Vector3.zero;
                deslocamentoDaAnimacao = Vector3.zero;
                deslocamentoDeCombate = Vector3.zero;
                return;
            }

            eixo = eixoDeFora; correndo = correrDeFora;
            eixoDeFora = Vector2.zero;
            pulou    = puloDeFora;    puloDeFora = false;
            esquivou = esquivaDeFora; esquivaDeFora = false;
            rolou    = rolarDeFora;   rolarDeFora = false;
            arrancou = arrancoDeFora; arrancoDeFora = false;
            if (!SoInjecao && entrada != null)
            {
                eixo += entrada.Movimento;
                pulou |= entrada.ConsumirPulo();
                esquivou |= entrada.ConsumirEsquiva();
            }
            eixo = Vector2.ClampMagnitude(eixo, 1f);
            if (!SoInjecao && entrada != null)
                correndo |= entrada.AtualizarCorrida(eixo, false);
        }

        void OnGUI()
        {
            if (!mostrarPainel) return;
            var e = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            e.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 10, 420, 142), GUIContent.none);
            GUI.Label(new Rect(20, 16, 410, 130),
                "A / D  andar      SHIFT  correr      ESPACO  pular\n" +
                "CTRL  esquiva      C  rolar      ALT  arranco\n" +
                "E  falar / interagir\n" +
                "\n" +
                "modo: " + modoAtual + "   estado: " + EstadoDaAnimacao +
                (EmAcao ? "   [EM ACAO]" : "") + "\n" +
                string.Format("x/z = {0:F2}/{1:F2}    velocidade = {2:F2} m/s    no chao: {3}",
                              transform.position.x, transform.position.z, VelocidadeAtual, NoChao),
                e);
        }

        void OnDisable()
        {
            esquivaSolicitada = false;
            esquivaVistaNoAnimator = false;
        }
    }
}
