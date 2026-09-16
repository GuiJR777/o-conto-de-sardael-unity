using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// O duelo: quem bate, quem reage.
    ///
    /// A REGRA CENTRAL, E DE ONDE ELA VEIO
    /// ------------------------------------
    /// O pacote da lanca nao traz so' os golpes: traz a REACAO DO ALVO pareada com cada golpe
    /// (Attack4_Stage1_Complete_IP  <->  Attack4_Stage1_Complete_React_IP). As duas animacoes
    /// foram feitas juntas, quadro a quadro. Entao nao ha' nada a inventar sobre o impacto:
    /// basta comecar as duas NO MESMO INSTANTE, com os dois corpos no afastamento em que o
    /// criador as animou.
    ///
    /// Esse afastamento eu MEDI na cena de animacao que ele aprovou: 1,756 m a frente e 0,041 m
    /// pro lado, com o alvo NA MESMA ROTACAO do atacante. Parece errado que o alvo nao esteja
    /// virado pro atacante — mas o giro esta' dentro do clipe. Foi exatamente por girar o orc
    /// na mao que a primeira montagem saiu com "sardael batendo pra frente e o orc de costas".
    ///
    /// O QUE E' MEDIDO E O QUE E' MEU
    /// -------------------------------
    /// MEDIDO: ALCANCE, DESVIO, o instante do impacto do golpe do orc (o construtor amostra o
    ///         clipe e acha onde a arma chega mais longe), e a duracao de tudo (Exit Time).
    /// MEU:    <see cref="folga"/> — quanto de erro de posicionamento eu perdoo antes de
    ///         considerar que o golpe passou longe. Esta' aqui em cima, sozinho, pra voce
    ///         mexer.
    /// </summary>
    public class Duelo : MonoBehaviour
    {
        // parametros do heroi
        public const string P_ATACAR   = "atacar";
        public const string P_ATINGIDO = "atingido";
        public const string P_APARAR   = "aparar";
        /// <summary>Nome do estado de aparo no controller do heroi. Ver MontarAparo.</summary>
        public const string ESTADO_APARO = "Aparo";
        // parametros do orc
        public const string P_ORC_ATACA = "atacar";

        /// <summary>
        /// O afastamento PADRAO do pacote: 1,756 m a frente do atacante.
        ///
        /// Ele nao foi escolhido nem calculado — foi LIDO da cena Finishers_Sardael, onde cada
        /// clipe de ataque esta' montado com o Sardael e o alvo nas posicoes em que as duas
        /// animacoes casam. Quatro dos cinco pares usam exatamente esta distancia, com clipes
        /// completamente diferentes; e' o padrao com que o pacote foi animado.
        ///
        /// Serve de referencia geral (o golpe do orc, por exemplo). Para os golpes do heroi,
        /// quem manda e' <see cref="AlcanceDoElo"/>, porque um dos elos foge do padrao.
        /// </summary>
        public const float ALCANCE = 1.756f;

        /// <summary>LIDO da mesma cena: o alvo fica 0,041 m pro lado. Pequeno, mas e' o que o
        /// clipe espera, e bate igual nos cinco pares.</summary>
        public const float DESVIO = -0.041f;

        /// <summary>
        /// O afastamento de CADA elo da cadeia, lido da Finishers_Sardael par a par.
        ///
        ///   elo 1  Attack1_Stage1_Complete   1,756 m
        ///   elo 2  Attack3_Stage2_Complete   1,166 m   <- fora do padrao, 59 cm mais perto
        ///   elo 3  Attack6_Stage2_Complete   1,756 m
        ///   elo 4  Attack3_Stage3_Complete   1,756 m
        ///
        /// O elo 2 e' o motivo desta tabela existir. Com um numero so' pra todos, aquele golpe
        /// erraria sempre, e apareceria muito depois como "o segundo golpe do combo e' esquisito"
        /// — que e' o tipo de defeito que custa caro justamente por parecer pequeno.
        /// </summary>
        static readonly float[] ALCANCE_DO_ELO = { 1.756f, 1.166f, 1.756f, 1.756f };

        /// <summary>O afastamento do elo, com o padrao do pacote pra qualquer elo fora da lista.</summary>
        public static float AlcanceDoElo(int elo)
        {
            int i = elo - 1;
            return i >= 0 && i < ALCANCE_DO_ELO.Length ? ALCANCE_DO_ELO[i] : ALCANCE;
        }

        [Header("Quem luta")]
        public Animator doHeroi;
        public Animator doOrc;

        [Header("Numeros meus — sao dois")]
        [Tooltip("Quanto erro de posicao eu perdoo antes de dizer que o golpe passou longe.")]
        public float folga = 0.7f;

        [Tooltip("Quanto antes do impacto o aparo ainda vale. Todo o resto do aparo e' medido: " +
                 "o instante do impacto vem do clipe do orc e a duracao do aparo vem do clipe " +
                 "dele. Este e' o unico numero do aparo que e' escolha minha.")]
        public float janelaDoAparo = 0.30f;

        [Header("Orc")]
        [Tooltip("Segundos entre um ataque do orc e o proximo. MEU — mexa a vontade.")]
        public float descansoDoOrc = 3.5f;
        [Tooltip("Graus por segundo que o orc gira pra encarar. MEU.")]
        public float giroDoOrc = 720f;
        public bool orcAtacaSozinho = true;

        public static Duelo Atual { get; private set; }

        /// <summary>Se o orc esta' no meio de uma reacao pareada. O <see cref="CorpoDoOrc"/>
        /// usa isto pra decidir se o clipe pode desloca-lo.</summary>
        public bool OrcEmReacao { get { return emReacaoPareada; } }

        public string UltimoEvento { get; private set; }
        public int GolpesQueAcertaram { get; private set; }
        public int GolpesQuePassaramLonge { get; private set; }

        MovimentoDoHeroi movimento;
        float proximoAtaqueDoOrc;

        /// <summary>
        /// Enquanto uma reacao pareada roda, o orc fica na MESMA rotacao do heroi e eu nao
        /// encosto nele. Fora disso, ele encara o heroi. Ver <see cref="EncararOHeroi"/>.
        /// </summary>
        bool emReacaoPareada;
        bool jaEncarou;
        bool correnteEngatada;

        void Awake()
        {
            Atual = this;
            movimento = GetComponent<MovimentoDoHeroi>();
            if (doHeroi == null) doHeroi = GetComponent<Animator>();
        }

        void OnDestroy() { if (Atual == this) Atual = null; }

        void Update()
        {
            bool atacou = false;
            bool aparou = false;
#if ENABLE_INPUT_SYSTEM
            var t = MovimentoDoHeroi.SoInjecao ? null : Keyboard.current;
            if (t != null)
            {
                atacou = t.jKey.wasPressedThisFrame;
                aparou = t.kKey.wasPressedThisFrame;
            }
#endif
            if (atacou || ataqueInjetado) { doHeroi.SetTrigger(P_ATACAR); ataqueInjetado = false; }

            // O aparo NAO se resolve aqui. Aqui so' toca a animacao e fica anotado QUANDO o
            // jogador pediu. Quem decide se valeu e' o quadro do impacto, no GolpeDoOrc — porque
            // e' so' la' que a pergunta "chegou a tempo?" tem resposta.
            // A MESMA PORTEIRA DAS OUTRAS HABILIDADES, E NAO E' ENFEITE.
            //
            // Esquiva, rolamento e arranco nunca chegam a acender o gatilho enquanto o heroi
            // esta' no meio de outra acao: o MovimentoDoHeroi so' os aceita sob "!ocupado &&
            // NoChao". O aparo precisa da mesma guarda porque a transicao dele vem de AnyState
            // com hasExitTime 0 — ela nao respeita estado nenhum. Sem isto o K vira botao de
            // CANCELAR: cancela o golpe do proprio heroi no meio, cancela o tranco de ter
            // apanhado, e funciona no ar, em dialogo e com o jogo pausado.
            //
            // E tem um segundo motivo, mais escondido: gatilho que o Animator nao consome nao
            // se apaga sozinho. Um K recusado la' dentro ficaria pendurado e dispararia um
            // aparo fantasma quando o estado trocasse.
            bool podeAparar = movimento == null
                           || (!movimento.EmAcao && !movimento.Travado && movimento.NoChao);

            if (aparou || aparoInjetado)
            {
                if (podeAparar)
                {
                    doHeroi.SetTrigger(P_APARAR);
                    pediuOAparoEm = Time.time;
                }
                else AparosRecusados++;
                aparoInjetado = false;        // recusado tambem se gasta: nao fica na fila
            }

            // A INVARIANTE: o gatilho do aparo nunca sobrevive dentro de uma acao.
            //
            // O Unity so' apaga um Trigger quando uma transicao condicionada nele e' REALMENTE
            // tomada. Ha' um quadro em que isso escapa da porteira acima: o impacto do orc acende
            // "atingido" durante a fase de animacao, e no Update do quadro seguinte o heroi ainda
            // nao entrou no tranco — entao um K ali e' aceito. Na avaliacao daquele quadro os dois
            // gatilhos estao acesos, e a lista de AnyState poe "atingido" antes de "aparar":
            // o heroi apanha e o "aparar" fica pendurado, pra arrancar ele do tranco um quadro
            // depois. Apagar aqui, todo quadro, fecha isso sem depender da ordem da lista.
            if (!podeAparar) doHeroi.ResetTrigger(P_APARAR);

            EncararOHeroi();

            if (orcAtacaSozinho && Time.time >= proximoAtaqueDoOrc)
            {
                proximoAtaqueDoOrc = Time.time + descansoDoOrc;
                MandarOrcAtacar();
            }
        }

        bool ataqueInjetado, aparoInjetado;
        public void InjetarAtaque() { ataqueInjetado = true; }
        public void InjetarAparo() { aparoInjetado = true; }

        /// <summary>Instante em que o jogador pediu o aparo. SEM_PEDIDO = nao ha' pedido aberto.</summary>
        float pediuOAparoEm = SEM_PEDIDO;

        // -999 e nao 0: no Editor o Play comeca com Time.time = 0, e um K no primeiro quadro
        // carimbaria 0f. Com "> 0f" como teste de "houve pedido", esse aparo sumiria.
        const float SEM_PEDIDO = -999f;

        public int AparosCertos { get; private set; }
        public int AparosCedoDemais { get; private set; }
        /// <summary>Pedidos de aparo barrados na porteira (no meio de outra acao, travado, no ar).</summary>
        public int AparosRecusados { get; private set; }

        /// <summary>O heroi esta' aparando AGORA? Conta a transicao de entrada, como o EmAcao.</summary>
        public bool Aparando
        {
            get
            {
                if (doHeroi == null) return false;
                if (doHeroi.GetCurrentAnimatorStateInfo(0).IsName(ESTADO_APARO)) return true;
                return doHeroi.IsInTransition(0)
                    && doHeroi.GetNextAnimatorStateInfo(0).IsName(ESTADO_APARO);
            }
        }
        public void MandarOrcAtacar()
        {
            if (doOrc == null) return;
            doOrc.SetTrigger(P_ORC_ATACA);
        }

        /// <summary>
        /// Chamado pelo proprio Animator (ver <see cref="AvisoDoAnimator"/>), no quadro exato
        /// em que o estado comeca. Nada de perguntar todo quadro.
        /// </summary>
        public void AoComecarEstado(string aviso, Animator quem)
        {
            if (aviso == "golpe1") { GolpeDoHeroi(1); return; }
            if (aviso == "golpe2") { GolpeDoHeroi(2); return; }
            if (aviso == "golpe3") { GolpeDoHeroi(3); return; }
            if (aviso == "golpe4") { GolpeDoHeroi(4); return; }
            if (aviso == "impactoDoOrc") { GolpeDoOrc(); return; }
            if (aviso == "orcParado") { emReacaoPareada = false; return; }
        }

        void GolpeDoHeroi(int elo)
        {
            if (doOrc == null) { UltimoEvento = "golpe " + elo + ": sem orc"; return; }

            int olhando = movimento != null ? movimento.Olhando : (transform.forward.x >= 0f ? 1 : -1);
            float aFrente = (doOrc.transform.position.x - transform.position.x) * olhando;

            // SEM LIMITE INFERIOR: perto sempre acerta.
            //
            // A regra antiga exigia que o orc estivesse numa FAIXA (alcance +- folga), o que
            // significa que colar no inimigo tambem errava. Isso e' o contrario de como um golpe
            // funciona, e a cadeia expoe o defeito sozinha: cada elo avanca o Sardael mais do que
            // o tranco afasta o orc, entao a distancia encolhe golpe a golpe — 1,38 m, 1,00 m,
            // 0,57 m. Com faixa, o terceiro e o quarto elo estavam condenados a errar por estarem
            // PERTO DEMAIS de um alvo que a lanca alcanca de sobra.
            //
            // Agora a pergunta e' a certa: o orc esta' a frente, e dentro do alcance deste elo?
            // O que sobra do outro lado — atravessar o orc — nao se resolve aqui: se resolve
            // dando corpo a ele, pra que os dois nao ocupem o mesmo lugar.
            float alcance = AlcanceDoElo(elo);
            if (aFrente <= 0f || aFrente > alcance + folga)
            {
                GolpesQuePassaramLonge++;
                UltimoEvento = string.Format("golpe {0} PASSOU LONGE (orc a {1:F2} m, alcanca ate' {2:F2})",
                                             elo, aFrente, alcance + folga);
                return;
            }

            // NADA DE TELEPORTE E NADA DE COPIAR ROTACAO.
            //
            // Eu tinha os dois na mesma rotacao pra casar com o clipe pareado do pacote, que
            // carrega o giro da vitima dentro dos OSSOS. So' que dois corpos na mesma rotacao
            // e' um de costas pro outro, e quando eu forcava o transform a encarar, os ossos
            // giravam de novo e ele ficava de costas do mesmo jeito. Fundacao errada.
            //
            // Agora o orc apanha com o MESMO clipe que o Sardael usa quando apanha: Hit_Fw_RM,
            // feito pra quem esta' de frente pro agressor. Sem giro escondido, sem encaixe de
            // posicao, sem salto. Os dois sempre se encaram.
            // O CHUTE EMPURRA. A LANCA NAO.
            //
            // O elo 1 e' um chute giratorio: e' golpe de perto, e empurrar o orc um pouco e' o
            // que abre a distancia pros tres golpes de lanca que vem depois.
            //
            // Os elos 2, 3 e 4 sao lanca, e lanca NAO empurra — ela fura. Empurrar ali e' pior
            // que inutil: as animacoes de lanca foram feitas pra 1,756 m, e quanto mais perto o
            // orc fica, mais a ponta passa dele e quem encosta e' o CABO. Foi o que o dono viu
            // na tela, e os numeros do teste confirmam: os golpes de lanca estavam acertando a
            // 1,01 m, 0,58 m e 0,80 m.
            //
            // Quem manda no deslocamento e' este campo: o CorpoDoOrc so' aplica o root motion da
            // reacao quando ele esta' ligado. Com ele desligado a reacao TOCA (o orc reage), mas
            // o orc fica no lugar.
            emReacaoPareada = (elo == 1);

            // Levar e' o tranco curto (Hit_Bw_RM, 0,15 m). LevarForte e' o longo (Hit_Fw_RM,
            // 1,32 m) e segue disponivel pro dia em que um golpe precisar arremessar.
            doOrc.Play("Levar", 0, 0f);

            GolpesQueAcertaram++;
            UltimoEvento = string.Format("golpe {0} ACERTOU (orc a {1:F2} m)", elo, aFrente);
        }

        /// <summary>
        /// O orc encara o heroi.
        ///
        /// ATENCAO A DISTINCAO — foi aqui que a primeira montagem errou feio.
        ///
        /// No clipe PAREADO (a reacao), atacante e alvo ficam na MESMA rotacao, porque o giro
        /// do alvo esta' dentro da animacao. Girar o alvo na mao ali quebra o encaixe: foi o
        /// "sardael batendo pra frente e o orc de costas".
        ///
        /// Mas a guarda e o ataque do orc sao clipes do Kevin, feitos pra rodar com o corpo
        /// virado pra onde ele olha. Herdar a rotacao do heroi nesses deixa o orc de costas.
        /// Entao: encarar por padrao, e so' largar a rotacao durante a reacao pareada.
        /// </summary>
        void Encarar()
        {
            if (doOrc == null) return;
            float paraOHeroi = transform.position.x - doOrc.transform.position.x;
            doOrc.transform.rotation = Quaternion.Euler(0f, paraOHeroi >= 0f ? 90f : -90f, 0f);
            jaEncarou = true;
        }

        /// <summary>Pra que lado esta' o orc em relacao ao heroi: +1 se esta' a direita.</summary>
        public float ParaLonge
        {
            get
            {
                if (doOrc == null) return 1f;
                return doOrc.transform.position.x >= transform.position.x ? 1f : -1f;
            }
        }

        void EncararOHeroi()
        {
            if (doOrc == null) return;
            float paraOHeroi = transform.position.x - doOrc.transform.position.x;
            var encarando = Quaternion.Euler(0f, paraOHeroi >= 0f ? 90f : -90f, 0f);

            // gira em vez de saltar: a reacao pareada larga o orc num angulo qualquer, e voltar
            // pra guarda de um quadro pro outro seria um tranco na tela.
            doOrc.transform.rotation = jaEncarou
                ? Quaternion.RotateTowards(doOrc.transform.rotation, encarando, giroDoOrc * Time.deltaTime)
                : encarando;
            jaEncarou = true;
        }

        void GolpeDoOrc()
        {
            if (doHeroi == null) return;
            float distancia = Mathf.Abs(doOrc.transform.position.x - transform.position.x);

            // O PEDIDO DE APARO SE GASTA AQUI, ANTES DE QUALQUER SAIDA.
            //
            // Este era um buraco meu: eu gastava o pedido so' no caminho em que o orc esta' no
            // alcance. Quando o golpe passava longe, o carimbo ficava pra tras e era cobrado no
            // golpe SEGUINTE como "cedo demais" — num golpe em que o jogador nao encostou na
            // tecla. Ler as tres perguntas aqui em cima, e limpar, faz o pedido valer por um
            // balanco de machado e nao pelo resto da partida.
            bool pediu = pediuOAparoEm > SEM_PEDIDO;
            float faz = pediu ? Time.time - pediuOAparoEm : 0f;
            bool noTempo = pediu && faz <= janelaDoAparo;
            bool naGuarda = Aparando;
            pediuOAparoEm = SEM_PEDIDO;

            if (distancia > ALCANCE + folga)
            {
                UltimoEvento = string.Format("orc atacou e PASSOU LONGE (heroi a {0:F2} m)", distancia);
                return;
            }
            // O APARO SE DECIDE AQUI, E SO' AQUI.
            //
            // Este metodo e' chamado pelo AvisoNoImpacto no quadro MEDIDO do impacto do golpe do
            // orc (32,22% do clipe), e nao no comeco do ataque. Entao esta e' literalmente a hora
            // em que o golpe chega, e a pergunta certa e' "ha' quanto tempo ele pediu o aparo?".
            //
            // E tem que ser um return ANTES do SetTrigger, nunca um ResetTrigger depois: a
            // transicao pro Atingido vem de AnyState com hasExitTime 0, ou seja, dispara no MESMO
            // quadro em que o gatilho acende, vindo de qualquer estado — inclusive de dentro do
            // aparo. Setar e desfazer no quadro seguinte daria o heroi aparando e apanhando ao
            // mesmo tempo, que e' exatamente o defeito que este aparo existe pra nao ter.
            // Duas perguntas, e as DUAS tem que dar sim.
            //
            // O relogio sozinho nao basta: ele diria "aparou" com o heroi de bracos baixos,
            // andando — bastava tamborilar a tecla. Por isso a segunda pergunta e' feita ao
            // ANIMATOR: o corpo esta' mesmo na guarda neste quadro? E' ela que separa um aparo
            // de 0,30 s de invulnerabilidade numa tecla que nao anima nada.
            if (noTempo && naGuarda)
            {
                AparosCertos++;
                UltimoEvento = string.Format(
                    "APAROU (pediu {0:F2} s antes do impacto; a janela e' {1:F2} s)", faz, janelaDoAparo);
                return;
            }

            string porque = null;
            if (pediu)
            {
                AparosCedoDemais++;
                porque = !noTempo
                    ? string.Format(" — o aparo saiu {0:F2} s antes do impacto, fora da janela de {1:F2} s",
                                    faz, janelaDoAparo)
                    : " — pediu dentro da janela, mas o heroi nao estava na guarda no impacto";
            }

            doHeroi.SetTrigger(P_ATINGIDO);
            UltimoEvento = string.Format("ORC ACERTOU o heroi (a {0:F2} m)", distancia) + porque;
        }

        void LateUpdate()
        {
            if (doOrc == null) return;
            // O clipe pareado tambem anda PRO LADO (no Stage1 o atacante vai 1,06 m de lado).
            // Num jogo travado na linha isso viraria profundidade, e os dois sairiam do plano.
            // Aqui eu recolho so' o que interessa e devolvo o orc pra linha.
            var p = doOrc.transform.position;
            p.z = transform.position.z;
            p.y = 0f;
            doOrc.transform.position = p;
        }

        void OnGUI()
        {
            var e = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            e.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 140, 520, 74), GUIContent.none);
            GUI.Label(new Rect(20, 146, 510, 66),
                "J  atacar (emende apertando de novo durante o golpe)     K  aparar\n" +
                string.Format("acertou: {0}   passou longe: {1}   APAROU: {2}   aparo cedo: {3}   recusado: {4}",
                              GolpesQueAcertaram, GolpesQuePassaramLonge, AparosCertos,
                              AparosCedoDemais, AparosRecusados)
                + "\n" +
                (UltimoEvento == null ? "" : UltimoEvento), e);
        }
    }
}
