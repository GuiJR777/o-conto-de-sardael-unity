using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
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
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class MovimentoDoHeroi : MonoBehaviour
    {
        // nomes dos parametros do AnimatorController, num lugar so'
        public const string P_VELOCIDADE = "velocidade";
        public const string P_NO_CHAO    = "noChao";
        public const string P_PULAR      = "pular";
        public const string P_ESQUIVAR   = "esquivar";
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
        public float VelocidadeAtual { get { return Mathf.Abs(velX); } }
        public int Olhando { get { return olhando; } }

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
            "Golpe1", "Golpe2", "Golpe3", "Atingido", "Aparo" };

        CharacterController cc;
        Animator anim;
        float velX, velY;
        int olhando = 1;
        float saiuDoChaoEm = -99f;
        Vector3 deslocamentoDaAnimacao;
        float deslocamentoDeCombateX;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            anim = GetComponent<Animator>();
            anim.applyRootMotion = true;

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

            float eixo; bool correndo, pulou, esquivou, rolou, arrancou;
            LerEntrada(out eixo, out correndo, out pulou, out esquivou, out rolou, out arrancou);

            NoChao = cc.isGrounded;
            if (NoChao)
            {
                saiuDoChaoEm = Time.time;
                if (velY < 0f) velY = -2f;   // cola no chao em rampa
            }

            bool ocupado = EmAcao;

            // ---- habilidades: so' pedem, quem executa e' o Animator ----
            if (!ocupado && NoChao)
            {
                if (esquivou) { anim.SetTrigger(P_ESQUIVAR); velX = 0f; }
                else if (rolou) { anim.SetTrigger(P_ROLAR); velX = 0f; }
                else if (arrancou) { anim.SetTrigger(P_ARRANCAR); velX = 0f; }
            }

            // ---- andar ----
            if (ocupado)
            {
                velX = 0f;   // durante a habilidade quem desloca e' o clipe
            }
            else
            {
                float peso = NoChao ? 1f : controleNoAr;
                float alvo = eixo * (correndo ? velocidadeCorrer : velocidadeAndar);
                velX = Mathf.MoveTowards(velX, alvo, aceleracao * peso * dt);
                if (Mathf.Abs(eixo) > 0.01f) olhando = eixo > 0f ? 1 : -1;
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
            anim.SetFloat(P_VELOCIDADE, Mathf.Abs(velX));
            anim.SetBool(P_NO_CHAO, podePular);

            Mover(dt);
        }

        /// <summary>
        /// Recolhe o deslocamento que a ANIMACAO produziu.
        ///
        /// Com AnimatorController, <c>animator.deltaPosition</c> e' valido — foi justamente o
        /// que faltava quando eu usava um grafo de Playables proprio, onde ele vinha zerado e
        /// o corpo derivava pra fora do personagem.
        ///
        /// So' o X importa: o Z do clipe seria profundidade, e nao existe profundidade aqui.
        /// </summary>
        void OnAnimatorMove()
        {
            if (anim == null) return;
            if (!EmAcao) return;                  // na locomocao quem anda e' velX
            deslocamentoDaAnimacao.x += anim.deltaPosition.x;
        }

        void Mover(float dt)
        {
            float passoDaAnimacao = deslocamentoDaAnimacao.x;
            deslocamentoDaAnimacao.x = 0f;
            float passoDoCombate = deslocamentoDeCombateX;
            deslocamentoDeCombateX = 0f;

            cc.Move(new Vector3(velX * dt + passoDaAnimacao + passoDoCombate, velY * dt, 0f));

            // travar na linha: e' isto que faz o jogo ser 2.5D
            var p = transform.position;
            p.z = zDaLinha;
            if (usarLimites) p.x = Mathf.Clamp(p.x, xMinimo, xMaximo);
            transform.position = p;

            float grausAlvo = olhando > 0 ? 90f : -90f;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.Euler(0f, grausAlvo, 0f), giroPorSegundo * dt);
        }

        // ---------------------------------------------------------------- teclado de mentira
        //
        // O teste automatico entra por aqui, pelas MESMAS variaveis que a tecla de verdade.
        // Nao ha' caminho alternativo dentro do Update: se o teste passa, e' porque o
        // caminho real passa. Testar por um atalho que o jogador nao usa nao prova nada.
        //
        // Os gatilhos ficam ligados ate' serem lidos — assim nao se perdem se o script de
        // teste rodar depois deste no quadro.
        float eixoDeFora; bool correrDeFora;
        bool puloDeFora, esquivaDeFora, rolarDeFora, arrancoDeFora;

        public void InjetarEixo(float v)   { eixoDeFora = v; }
        public void InjetarCorrer(bool v)  { correrDeFora = v; }
        public void InjetarPulo()          { puloDeFora = true; }
        public void InjetarEsquiva()       { esquivaDeFora = true; }
        public void InjetarRolar()         { rolarDeFora = true; }
        public void InjetarArranco()       { arrancoDeFora = true; }

        /// <summary>
        /// Acrescenta a correcao horizontal do combate ao mesmo pipeline do root motion.
        /// O deslocamento e consumido por <see cref="CharacterController.Move(Vector3)"/>.
        /// </summary>
        public bool AdicionarDeslocamentoDeCombate(float deltaX)
        {
            if (Travado || !isActiveAndEnabled || Mathf.Approximately(deltaX, 0f)) return false;
            deslocamentoDeCombateX += deltaX;
            return true;
        }

        /// <summary>Permite ao targeting orientar Sardael sem mover ou teleportar o corpo.</summary>
        public bool DefinirDirecaoDeCombate(int direcao)
        {
            if (Travado || direcao == 0) return false;
            olhando = direcao < 0 ? -1 : 1;
            return true;
        }

        void LerEntrada(out float eixo, out bool correndo, out bool pulou,
                        out bool esquivou, out bool rolou, out bool arrancou)
        {
            if (Travado)
            {
                eixo = 0f; correndo = false; pulou = false;
                esquivou = false; rolou = false; arrancou = false;
                eixoDeFora = 0f; correrDeFora = false;
                puloDeFora = esquivaDeFora = rolarDeFora = arrancoDeFora = false;

                // PARA NA HORA, nao por desaceleracao. Zerar so' a entrada deixa a velocidade
                // vigente escorrer pela aceleracao (16 m/s2), o que da' meio metro de deslize
                // depois do "pare" — e numa conversa o jogador anda por baixo do dialogo.
                // O deslocamento do clipe tambem morre aqui: habilidade em curso continuaria
                // empurrando o corpo mesmo com a entrada zerada.
                velX = 0f;
                deslocamentoDaAnimacao = Vector3.zero;
                deslocamentoDeCombateX = 0f;
                return;
            }

            eixo = eixoDeFora; correndo = correrDeFora;
            pulou    = puloDeFora;    puloDeFora = false;
            esquivou = esquivaDeFora; esquivaDeFora = false;
            rolou    = rolarDeFora;   rolarDeFora = false;
            arrancou = arrancoDeFora; arrancoDeFora = false;
#if ENABLE_INPUT_SYSTEM
            if (SoInjecao) return;
            var t = Keyboard.current;
            if (t == null) return;
            if (t.dKey.isPressed || t.rightArrowKey.isPressed) eixo += 1f;
            if (t.aKey.isPressed || t.leftArrowKey.isPressed) eixo -= 1f;
            correndo |= t.leftShiftKey.isPressed;
            pulou    |= t.spaceKey.wasPressedThisFrame;
            esquivou |= t.leftCtrlKey.wasPressedThisFrame;
            rolou    |= t.cKey.wasPressedThisFrame;
            arrancou |= t.leftAltKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) eixo += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) eixo -= 1f;
            correndo |= Input.GetKey(KeyCode.LeftShift);
            pulou    |= Input.GetKeyDown(KeyCode.Space);
            esquivou |= Input.GetKeyDown(KeyCode.LeftControl);
            rolou    |= Input.GetKeyDown(KeyCode.C);
            arrancou |= Input.GetKeyDown(KeyCode.LeftAlt);
#endif
            eixo = Mathf.Clamp(eixo, -1f, 1f);
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
                "estado: " + EstadoDaAnimacao + (EmAcao ? "   [EM ACAO]" : "") + "\n" +
                string.Format("x = {0:F2}    velocidade = {1:F2} m/s    no chao: {2}",
                              transform.position.x, VelocidadeAtual, NoChao),
                e);
        }
    }
}
