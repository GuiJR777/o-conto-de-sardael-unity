using System.Collections;
using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// As duas missoes de Halu.
    ///
    /// PRIMEIRA (o tutorial). Sardael esta' parado e sem comando. A mensageira entra correndo,
    /// para' de frente pra ele e avisa que o chefe o convoca; depois vai embora andando. O
    /// comando e' liberado e a seta aponta pro centro. Chegando ao chefe, ele conta que a
    /// fazenda leste esta' sob ataque e manda encontrar o capitao de Valdoria la'. A seta vira
    /// pro outro lado e o portao oeste-norte destranca: passar por ele carrega o tutorial.
    ///
    /// SEGUNDA. Voltando do tutorial, a mesma cena de Halu abre ja' no chefe, que manda
    /// Sardael pra Floresta de Bael. A seta aponta pro sul e o portao oeste-sul destranca.
    ///
    /// QUAL DAS DUAS RODA quem decide e' o <see cref="Progresso"/>, nao um objeto da cena: a
    /// Halu e' carregada duas vezes na mesma partida e precisa saber qual das duas vezes e'
    /// esta. Guardado em PlayerPrefs, o teste tambem continua de onde parou depois de fechar
    /// o editor.
    ///
    /// Tudo por tempo e por distancia, com numero no Inspector, como a <see cref="CenaDaMorte"/>
    /// e o <see cref="DiretorDoCapitao"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiretorDeHalu : MonoBehaviour
    {
        public enum Etapa
        {
            Espera, MensageiraChega, Recado, MensageiraSai,
            RumoAoCentro, ConversaComOLider, RumoAoPortao,
            SegundaMissao, SonhoDoVeu, RumoAoSul, Fim
        }

        [Header("Atores")]
        public Transform sardael;
        public MovimentoDoHeroi comandoDoHeroi;
        public Transform mensageira;
        public Transform lider;
        public FalaNaTela caixaDeFala;

        [Header("Pontos")]
        public Transform pontoDoChefe;
        public PortaDeCena portaDoTutorial;     // Portao_Oeste_norte
        public PortaDeCena portaDaFloresta;     // Portao_Oeste_sul
        [Tooltip("Ate onde o Sardael pode andar depois que a segunda missao abre o caminho sul.")]
        public float xMaximoDepoisDeBael = 156f;

        [Header("Clipes da mensageira")]
        public AnimationClip mensageiraCorrendo;
        public AnimationClip mensageiraFalando;
        public AnimationClip mensageiraAndando;
        public AnimationClip mensageiraParada;
        [Tooltip("Passada do clipe de correr, medida no [RM] dele.")]
        public float velocidadeCorrendo = 3.8f;
        [Tooltip("Passada do clipe de andar, medida no [RM] dele.")]
        public float velocidadeAndando = 1.9f;

        [Header("1 - a mensageira")]
        public float esperaInicial = 1.0f;
        [Tooltip("De onde ela entra. Fora do quadro: a camera mostra +-7,2 m em torno do heroi.")]
        public float xDePartida = 14f;
        [Tooltip("A que distancia do Sardael ela para'.")]
        public float distanciaDaParada = 2.8f;
        public string nomeDaMensageira = "Mensageira";
        [TextArea(2, 4)] public string recado =
            "Sardael! O líder dos veados te convoca — agora. Ele está no centro da aldeia.";
        public float duracaoDoRecado = 4.5f;
        [Tooltip("Pra onde ela vai embora depois do recado, em metros a partir de onde parou.")]
        public float quantoElaSeAfasta = 16f;

        [Header("2 - o lider")]
        [Tooltip("A que distancia do ponto do chefe a conversa comeca.")]
        public float distanciaQueAbreAConversa = 3.2f;
        public string nomeDoLider = "Líder dos Veados";
        [TextArea(2, 4)] public string falaDoLider1 =
            "A fazenda leste está sob ataque. Orcs — muitos deles.";
        [TextArea(2, 4)] public string falaDoLider2 =
            "O capitão de Valdória já está lá. Encontre-o. Vá pelo portão oeste, e depressa.";
        public float duracaoDeCadaFala = 4.2f;

        [Header("3 - a segunda missao")]
        [TextArea(2, 4)] public string falaDeBael1 =
            "A fazenda resistiu. Mas o que veio de lá não veio sozinho.";
        [TextArea(2, 4)] public string falaDeBael2 =
            "A Floresta de Bael guarda a raiz disso. Siga para o sul, Sardael.";

        [Header("3b - volta do tutorial")]
        [Tooltip("Onde ele reaparece ao voltar da fazenda. Vazio = logo depois do portao norte, "
               + "por onde ele saiu. Ele NAO nasce no chefe: tem de caminhar ate' la'.")]
        public Transform pontoDeChegadaDoTutorial;
        [Tooltip("Quanto ele fica pra dentro do portao, se nao houver ponto marcado.")]
        public float folgaDoPortao = 3f;

        [Header("4 - de volta do Véu")]
        [Tooltip("O palco do acordar sob a arvore: camera, corpo deitado e diretor. Fica "
               + "desligado e so' acende quando ele volta do Veu.")]
        public GameObject grupoDeAcordar;
        public AcordarSardael diretorDeAcordar;
        [Tooltip("A camera de jogo, que sai de cena enquanto o acordar roda.")]
        public Camera cameraDoJogo;
        [Tooltip("Respiro depois que ele fica de pe', antes de cortar pro chefe.")]
        public float pausaDepoisDeAcordar = 1.2f;
        public string nomeDeSardael = "Sardael";
        [TextArea(2, 4)] public string falaDoSonho =
            "Chefe, tive um sonho estranho. Os povos místicos atacavam Halu.";
        [TextArea(2, 4)] public string respostaDoChefe =
            "Não foi sonho, Sardael. O que está esperando para ir para Bael?";
        [Tooltip("Da segunda morte em diante o chefe encurta — ele ja' ouviu esta história.")]
        [TextArea(2, 4)] public string falaDoSonhoDeNovo =
            "Chefe... o mesmo sonho. De novo.";
        [TextArea(2, 4)] public string respostaDoChefeDeNovo =
            "E continua não sendo sonho. Bael, Sardael. Agora.";

        [Header("Depuracao")]
        public bool mostrarPainel = true;
        [Tooltip("Liga pra forcar a primeira missao mesmo com o progresso salvo adiante. "
               + "E' o que permite repetir o fluxo sem apagar o PlayerPrefs na mao.")]
        public bool sempreDoComeco = false;

        public Etapa Agora { get; private set; }

        TocadorDeClipe tocadorDaMensageira;
        Terrain chao;
        Vector3 casaDaMensageira;
        bool travaDoRoteiro;

        void Start()
        {
            chao = Terrain.activeTerrain;
            if (caixaDeFala == null) caixaDeFala = FalaNaTela.Instancia;
            if (mensageira != null)
            {
                tocadorDaMensageira = mensageira.GetComponent<TocadorDeClipe>();
                casaDaMensageira = mensageira.position;
            }
            if (!Conferir()) return;
            if (sempreDoComeco) Progresso.Zerar();

            // a ordem importa: quem morreu em Bael volta com a conversa do sonho, e nao com a
            // segunda missao de novo — as duas etapas sao depois do tutorial
            if (Progresso.Onde == Progresso.Etapa.MorreuEmBael) StartCoroutine(DeVoltaDoVeu());
            else StartCoroutine(Progresso.Passou(Progresso.Etapa.VoltouDoTutorial)
                              ? SegundaMissao() : PrimeiraMissao());
        }

        bool Conferir()
        {
            string falta = "";
            if (sardael == null) falta += "sardael, ";
            if (comandoDoHeroi == null) falta += "comandoDoHeroi, ";
            if (mensageira == null) falta += "mensageira, ";
            if (lider == null) falta += "lider, ";
            if (pontoDoChefe == null) falta += "pontoDoChefe, ";
            if (portaDoTutorial == null) falta += "portaDoTutorial, ";
            if (falta == "") return true;
            Debug.LogWarning("DiretorDeHalu sem: " + falta.TrimEnd(' ', ',') + ". A missao nao vai rodar.", this);
            enabled = false;
            return false;
        }

        // ------------------------------------------------------------------ missao 1
        IEnumerator PrimeiraMissao()
        {
            Agora = Etapa.Espera;
            Travar(true);
            SetaDoCaminho.Esconder();
            if (portaDoTutorial != null) portaDoTutorial.Liberar(false);
            if (portaDaFloresta != null) portaDaFloresta.Liberar(false);

            // ela entra de fora do quadro, como o capitao na fazenda. O objeto que voce
            // posicionou nao muda de lugar no editor — quem a leva pra entrada e' isto aqui.
            int lado = xDePartida >= sardael.position.x ? +1 : -1;
            Plantar(mensageira, new Vector3(xDePartida, 0f, comandoDoHeroi.zDaLinha));
            mensageira.rotation = Quaternion.LookRotation(new Vector3(-lado, 0f, 0f));
            Passo(mensageiraParada, 0f, 0f);
            yield return new WaitForSeconds(esperaInicial);

            Agora = Etapa.MensageiraChega;
            float xParada = sardael.position.x + lado * distanciaDaParada;
            yield return Ir(mensageira, xParada, velocidadeCorrendo, mensageiraCorrendo);
            Encarar(mensageira, sardael);

            Agora = Etapa.Recado;
            Passo(mensageiraFalando, 0f, 0f);
            Dizer(nomeDaMensageira, recado);
            yield return new WaitForSeconds(duracaoDoRecado);
            Calar();

            // ---- comando liberado; ela vai embora andando enquanto o jogador anda
            Agora = Etapa.MensageiraSai;
            Travar(false);
            SetaDoCaminho.Apontar(pontoDoChefe.position.x > sardael.position.x ? +1 : -1);
            StartCoroutine(ElaVaiEmbora(lado));

            Agora = Etapa.RumoAoCentro;
            while (Mathf.Abs(sardael.position.x - pontoDoChefe.position.x) > distanciaQueAbreAConversa)
                yield return null;

            Agora = Etapa.ConversaComOLider;
            Travar(true);
            SetaDoCaminho.Esconder();
            Encarar(lider, sardael);
            yield return Falar(nomeDoLider, falaDoLider1);
            yield return Falar(nomeDoLider, falaDoLider2);
            Progresso.Onde = Progresso.Etapa.FalouComOChefe;

            Agora = Etapa.RumoAoPortao;
            Travar(false);
            if (portaDoTutorial != null)
            {
                portaDoTutorial.Liberar(true);
                SetaDoCaminho.Apontar(portaDoTutorial.transform.position.x > sardael.position.x ? +1 : -1);
                AbrirCaminhoAte(portaDoTutorial.transform.position.x);
            }
            Agora = Etapa.Fim;
        }

        // ------------------------------------------------------------------ missao 2
        IEnumerator SegundaMissao()
        {
            Agora = Etapa.SegundaMissao;
            Travar(true);
            SetaDoCaminho.Esconder();
            if (portaDoTutorial != null) portaDoTutorial.Liberar(false);
            if (portaDaFloresta != null) portaDaFloresta.Liberar(false);

            // ele volta PELO PORTAO por onde saiu, e nao no colo do chefe. O dialogo comecava
            // sozinho antes de o jogador andar um passo — o chefe falava com o horizonte.
            float xChegada = pontoDeChegadaDoTutorial != null
                ? pontoDeChegadaDoTutorial.position.x
                : portaDoTutorial.transform.position.x
                  + (pontoDoChefe.position.x > portaDoTutorial.transform.position.x ? folgaDoPortao : -folgaDoPortao);
            Plantar(sardael, new Vector3(xChegada, 0f, comandoDoHeroi.zDaLinha));
            Encarar(sardael, pontoDoChefe);
            if (mensageira != null) Plantar(mensageira, casaDaMensageira);
            yield return new WaitForSeconds(esperaInicial);

            // caminho livre ate' o chefe, com a seta apontando, igual a primeira missao
            Travar(false);
            AbrirCaminhoAte(pontoDoChefe.position.x);
            SetaDoCaminho.Apontar(pontoDoChefe.position.x > sardael.position.x ? +1 : -1);
            while (Mathf.Abs(sardael.position.x - pontoDoChefe.position.x) > distanciaQueAbreAConversa)
                yield return null;

            Travar(true);
            SetaDoCaminho.Esconder();
            Encarar(sardael, lider);
            Encarar(lider, sardael);
            yield return Falar(nomeDoLider, falaDeBael1);
            yield return Falar(nomeDoLider, falaDeBael2);
            Progresso.Onde = Progresso.Etapa.IndoParaBael;
            AbrirORumoAoSul();
        }

        // ------------------------------------------------------------------ de volta do Veu
        /// <summary>
        /// O outro lado da morte em Bael: ele abre os olhos diante do chefe, no mesmo lugar da
        /// conversa, achando que sonhou.
        ///
        /// A cena da morte ja' fecha em branco; aqui a Halu abre normalmente e a unica coisa
        /// que denuncia o que houve e' o dialogo. Essa e' a intencao: pro jogador tem que
        /// parecer sonho, e e' o chefe quem estraga a ilusao.
        /// </summary>
        IEnumerator DeVoltaDoVeu()
        {
            Agora = Etapa.SonhoDoVeu;
            Travar(true);
            SetaDoCaminho.Esconder();
            if (portaDoTutorial != null) portaDoTutorial.Liberar(false);
            if (portaDaFloresta != null) portaDaFloresta.Liberar(false);

            yield return Acordar();

            Plantar(sardael, new Vector3(pontoDoChefe.position.x, 0f, comandoDoHeroi.zDaLinha));
            Encarar(sardael, lider);
            Encarar(lider, sardael);
            if (mensageira != null) Plantar(mensageira, casaDaMensageira);
            yield return new WaitForSeconds(esperaInicial);

            bool primeiraVez = Salvao.MortesNoVeu <= 1;
            yield return Falar(nomeDeSardael, primeiraVez ? falaDoSonho : falaDoSonhoDeNovo);
            yield return Falar(nomeDoLider, primeiraVez ? respostaDoChefe : respostaDoChefeDeNovo);

            Progresso.Onde = Progresso.Etapa.VoltouDoVeu;
            AbrirORumoAoSul();
        }

        /// <summary>
        /// A cutscene do acordar, que ja' estava montada em <c>10_Acordar</c> e so' faltava
        /// alguem acender.
        ///
        /// O palco fica num canto da propria Halu (z perto de 90), longe da linha de jogo, com
        /// camera propria e um Sardael deitado sobre o saco de dormir. Enquanto ele roda, o
        /// heroi de jogo e a camera de jogo saem de cena — dois Sardael visiveis ao mesmo tempo
        /// entregariam o truque na hora.
        ///
        /// Espero pelo ESTADO do <see cref="AcordarSardael"/> em vez de assinar o evento dele:
        /// assinatura feita em corrotina sobrevive a cena e acumula a cada morte.
        /// </summary>
        IEnumerator Acordar()
        {
            if (grupoDeAcordar == null || diretorDeAcordar == null)
            {
                Debug.LogWarning("DiretorDeHalu sem o palco de acordar; pulando a cutscene.", this);
                yield break;
            }

            var branco = diretorDeAcordar.telaBranca;
            if (cameraDoJogo != null) cameraDoJogo.gameObject.SetActive(false);
            if (sardael != null) sardael.gameObject.SetActive(false);
            grupoDeAcordar.SetActive(true);

            while (diretorDeAcordar.Agora != AcordarSardael.Etapa.Acordado) yield return null;
            yield return new WaitForSeconds(pausaDepoisDeAcordar);

            grupoDeAcordar.SetActive(false);
            // o branco mora DENTRO do grupo: desligar o objeto dele quebraria a segunda morte,
            // entao so' zero a opacidade e deixo o grupo esconder o resto
            if (branco != null) branco.alpha = 0f;
            if (sardael != null) sardael.gameObject.SetActive(true);
            if (cameraDoJogo != null) cameraDoJogo.gameObject.SetActive(true);
            yield return null;                       // um quadro pra camera se reassentar
        }

        /// <summary>Solta o comando, destranca o portao sul e aponta a seta pra la'.</summary>
        void AbrirORumoAoSul()
        {
            Agora = Etapa.RumoAoSul;
            Travar(false);
            if (portaDaFloresta == null) { Agora = Etapa.Fim; return; }

            portaDaFloresta.Liberar(true);
            AbrirCaminhoAte(portaDaFloresta.transform.position.x);
            SetaDoCaminho.Apontar(portaDaFloresta.transform.position.x > sardael.position.x ? +1 : -1);
            Agora = Etapa.Fim;
        }

        // ------------------------------------------------------------------ peças
        /// <summary>
        /// Abre a trava do heroi ate' alcancar um X. O portao sul fica em 153 e a trava vem de
        /// fabrica em 38: sem isto a seta aponta pra um lugar onde ele nao consegue chegar.
        /// </summary>
        void AbrirCaminhoAte(float x)
        {
            // a faixa tem de conter o destino E O LUGAR ONDE ELE ESTA AGORA. Halu comeca sem
            // limite nenhum, e o jogador chega ao chefe la' em x=55; ligar a trava com o teto
            // de fabrica em 38 arrancava ele 17 m pra tras no mesmo quadro.
            float onde = sardael != null ? sardael.position.x : x;
            float teto = Mathf.Max(x, onde) + 3f;
            float piso = Mathf.Min(x, onde) - 3f;

            comandoDoHeroi.usarLimites = true;
            comandoDoHeroi.xMaximo = Mathf.Max(comandoDoHeroi.xMaximo, teto);
            comandoDoHeroi.xMinimo = Mathf.Min(comandoDoHeroi.xMinimo, piso);
        }

        IEnumerator ElaVaiEmbora(int lado)
        {
            float destino = mensageira.position.x + lado * quantoElaSeAfasta;
            mensageira.rotation = Quaternion.LookRotation(new Vector3(lado, 0f, 0f));
            yield return Ir(mensageira, destino, velocidadeAndando, mensageiraAndando);
            Passo(mensageiraParada, 0f, 0f);
        }

        /// <summary>Leva alguem ate' um X na linha de jogo, no ritmo do clipe.</summary>
        IEnumerator Ir(Transform quem, float xDestino, float velocidade, AnimationClip passo)
        {
            if (quem == null) yield break;
            Passo(passo, velocidade, velocidade);
            int dir = xDestino >= quem.position.x ? +1 : -1;
            quem.rotation = Quaternion.LookRotation(new Vector3(dir, 0f, 0f));
            while ((xDestino - quem.position.x) * dir > 0.06f)
            {
                float anda = Mathf.Min(velocidade * Time.deltaTime, Mathf.Abs(xDestino - quem.position.x));
                Plantar(quem, new Vector3(quem.position.x + dir * anda, 0f, quem.position.z));
                yield return null;
            }
            Plantar(quem, new Vector3(xDestino, 0f, quem.position.z));
        }

        /// <summary>Troca o clipe da mensageira e acerta o ritmo pela velocidade real.</summary>
        void Passo(AnimationClip clipe, float velocidadeReal, float passadaDoClipe)
        {
            if (tocadorDaMensageira == null || clipe == null) return;
            if (tocadorDaMensageira.clipe != clipe)
            {
                tocadorDaMensageira.congelarNoFim = false;
                tocadorDaMensageira.Trocar(clipe);
            }
            tocadorDaMensageira.Ritmo(passadaDoClipe > 0.01f && velocidadeReal > 0.01f
                ? Mathf.Clamp(velocidadeReal / passadaDoClipe, 0.55f, 1.6f) : 1f);
        }

        IEnumerator Falar(string quem, string oQue)
        {
            Dizer(quem, oQue);
            yield return new WaitForSeconds(duracaoDeCadaFala);
            Calar();
            yield return new WaitForSeconds(0.15f);
        }

        void Encarar(Transform quem, Transform paraQuem)
        {
            if (quem == null || paraQuem == null) return;
            var d = paraQuem.position - quem.position; d.y = 0f;
            if (d.sqrMagnitude < 0.0004f) return;
            quem.rotation = Quaternion.LookRotation(d.normalized);
        }

        void Plantar(Transform quem, Vector3 xz)
        {
            if (quem == null) return;
            float y = chao != null ? chao.SampleHeight(xz) + chao.transform.position.y : quem.position.y;
            quem.position = new Vector3(xz.x, y, xz.z);
        }

        // texto na tela TAMBEM trava o comando: assim uma fala nova no roteiro ja' nasce
        // travando o jogador, sem depender de eu lembrar de chamar isto em cada etapa
        void Travar(bool quanto) { travaDoRoteiro = quanto; AplicarTrava(); }

        void AplicarTrava()
        {
            if (comandoDoHeroi == null) return;
            comandoDoHeroi.Travar(this, travaDoRoteiro
                                     || (caixaDeFala != null && caixaDeFala.Falando));
        }

        void Update() { AplicarTrava(); }

        void Dizer(string quem, string oQue) { if (caixaDeFala != null) caixaDeFala.Dizer(quem, oQue); }
        void Calar() { if (caixaDeFala != null) caixaDeFala.Calar(); }

        void OnGUI()
        {
            if (!mostrarPainel) return;
            GUI.Box(new Rect(10, 10, 300, 44), "");
            GUI.Label(new Rect(20, 18, 290, 30), "Halu: " + Agora + "   (progresso: " + Progresso.Onde + ")");
        }
    }
}
