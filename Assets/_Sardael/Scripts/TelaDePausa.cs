using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// A pausa: continuar, configuracoes, voltar ao menu, sair.
    ///
    /// Nasce sozinha depois que a primeira cena carrega e sobrevive as trocas de cena
    /// (<c>DontDestroyOnLoad</c>). E' o unico jeito sensato: pausa tem que existir em TODA cena
    /// de jogo, e por a mao em cada cena pra pendurar um Canvas e' trabalho que se perde na
    /// proxima fase que alguem criar. Aqui, fase nova ja' nasce com pausa.
    ///
    /// No <see cref="Salvao.CENA_DO_MENU"/> ela se esconde: pausar o menu principal nao quer
    /// dizer nada, e o ESC la' ja' serve pra voltar de painel.
    ///
    /// Pausar e' <c>Time.timeScale = 0</c>. Por isso toda animacao de UI do projeto usa
    /// <c>unscaledDeltaTime</c> — inclusive esta. Se algo continuar se mexendo com o jogo
    /// pausado, e' que aquele script esta' no relogio errado.
    ///
    /// Voltar ao menu NAO perde progresso: a partida grava a cada avanco de historia e a cada
    /// cena nova. O que se perde e' a posicao dentro da cena, e e' so' por isso que ha' uma
    /// confirmacao.
    /// </summary>
    public class TelaDePausa : MonoBehaviour
    {
        public static TelaDePausa Instancia { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoComecarAPartida() { Instancia = null; }

        /// <summary>
        /// Destrava o relogio no comeco de toda partida.
        ///
        /// <c>Time.timeScale</c> nao e' um campo de script: mora no TimeManager do projeto e
        /// fica gravado la'. Basta alguma coisa zerar ele e nao repor — um script que morreu no
        /// meio, uma ferramenta de editor que abortou — pra TODA partida seguinte abrir
        /// congelada, sem painel de pausa, sem erro no Console, sem nada. Foi exatamente o que
        /// aconteceu neste projeto: o jogo caia em Halu parado e so' destravava depois de
        /// pausar e despausar na mao.
        ///
        /// No comeco de uma partida nada pode estar legitimamente pausado, entao repor 1 aqui
        /// nunca atrapalha ninguem e fecha esse buraco de vez.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void DestravarORelogio()
        {
            if (Time.timeScale > 0f) return;
            Debug.LogWarning("Time.timeScale estava em " + Time.timeScale
                           + " ao abrir a partida. Repondo 1.");
            Time.timeScale = 1f;
        }

        /// <summary>Poe a pausa no jogo assim que a primeira cena sobe.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Nascer()
        {
            if (Instancia != null) return;
            var go = new GameObject("TelaDePausa");
            DontDestroyOnLoad(go);
            go.AddComponent<TelaDePausa>();
        }

        static readonly Color OURO   = new Color(0.91f, 0.77f, 0.42f, 1f);
        static readonly Color CLARO  = new Color(0.95f, 0.94f, 0.91f, 1f);
        static readonly Color FRACO  = new Color(0.74f, 0.71f, 0.64f, 1f);
        static readonly Color TRILHO = new Color(0.08f, 0.07f, 0.06f, 0.85f);

        Canvas lona;
        CanvasGroup grupo;
        GameObject painelPausa, painelOpcoes, painelConfirmar;
        TextMeshProUGUI rotuloResolucao, rotuloQualidade, rotuloTelaCheia;
        TextMeshProUGUI valorGeral, valorMusica, valorEfeitos;
        Sprite placa;

        Vector2Int[] resolucoes;
        int noQual;
        bool pausado;
        float relogioDeAntes = 1f;

        public bool Pausado { get { return pausado; } }

        void Awake()
        {
            if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
            Instancia = this;

            placa = Resources.Load<Sprite>("Menu_Placa");
            resolucoes = Configuracoes.Resolucoes();
            noQual = 0;
            var atual = Configuracoes.Resolucao;
            for (int i = 0; i < resolucoes.Length; i++) if (resolucoes[i] == atual) noQual = i;

            Montar();
            Fechar();
            SceneManager.sceneLoaded += AoTrocarDeCena;
            Conferir(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= AoTrocarDeCena;
            if (Instancia == this) Instancia = null;
        }

        void AoTrocarDeCena(Scene cena, LoadSceneMode modo)
        {
            if (pausado) Continuar();
            Conferir(cena);
        }

        void Conferir(Scene cena)
        {
            // no menu principal a pausa nao faz sentido
            bool serve = cena.name != Salvao.CENA_DO_MENU;
            if (lona != null) lona.gameObject.SetActive(serve);
            enabled = serve;

            // toda cena precisa do seu, e nao so' a primeira que subiu
            GarantirEventSystem();
        }

        void Update()
        {
            if (TelaDeCarregamento.Carregando) return;

            bool apertou = false;
#if ENABLE_INPUT_SYSTEM
            var t = Keyboard.current;
            if (t != null) apertou = t.escapeKey.wasPressedThisFrame;
#else
            apertou = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (!apertou) return;

            // o catalogo de lancas tambem fecha no ESC; nao abre pausa por cima dele
            var cat = Object.FindAnyObjectByType<CatalogoDeLancas>();
            if (!pausado && cat != null && cat.Aberto) return;

            if (!pausado) Pausar();
            else if (painelConfirmar.activeSelf) Mostrar(painelPausa);
            else if (painelOpcoes.activeSelf) Mostrar(painelPausa);
            else Continuar();
        }

        // ------------------------------------------------------------------ acoes

        public void Pausar()
        {
            pausado = true;
            relogioDeAntes = Time.timeScale <= 0f ? 1f : Time.timeScale;
            Time.timeScale = 0f;
            Travar(true);
            grupo.alpha = 1f; grupo.blocksRaycasts = true; grupo.interactable = true;
            Encher();
            Mostrar(painelPausa);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }

        public void Continuar()
        {
            pausado = false;
            Time.timeScale = relogioDeAntes;
            Travar(false);
            Fechar();
        }

        void Fechar()
        {
            grupo.alpha = 0f; grupo.blocksRaycasts = false; grupo.interactable = false;
            painelPausa.SetActive(false);
            painelOpcoes.SetActive(false);
            painelConfirmar.SetActive(false);
        }

        void Travar(bool quanto)
        {
            var h = Object.FindAnyObjectByType<MovimentoDoHeroi>();
            if (h != null) h.Travar(this, quanto);
        }

        void Mostrar(GameObject qual)
        {
            if (painelPausa == null) return;
            painelPausa.SetActive(qual == painelPausa);
            painelOpcoes.SetActive(qual == painelOpcoes);
            painelConfirmar.SetActive(qual == painelConfirmar);
        }

        void VoltarAoMenu()
        {
            Salvao.Gravar();                 // a posicao na cena se perde; a historia nao
            Time.timeScale = 1f;
            pausado = false;
            Travar(false);
            Fechar();
            TelaDeCarregamento.Ir(Salvao.CENA_DO_MENU, "Voltando ao menu...");
        }

        void SairDoJogo()
        {
            Salvao.Gravar();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ montagem

        void Montar()
        {
            var go = new GameObject("Canvas_Pausa");
            go.transform.SetParent(transform, false);
            lona = go.AddComponent<Canvas>();
            lona.renderMode = RenderMode.ScreenSpaceOverlay;
            lona.sortingOrder = 800;                      // acima da fala, abaixo do carregamento
            var escala = go.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            grupo = go.AddComponent<CanvasGroup>();

            var escuro = Novo("Escuro", go.transform); Esticar(escuro);
            escuro.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.72f);

            MontarPausa(go.transform);
            MontarOpcoes(go.transform);
            MontarConfirmar(go.transform);

            // o EventSystem nao entra aqui: quem cuida dele e' o Conferir, que roda logo depois
            // desta montagem e de novo a cada cena nova. Se ficasse so' aqui, valeria pra cena
            // em que a pausa nasceu e pra mais nenhuma.
        }

        /// <summary>
        /// Poe um EventSystem na cena que subiu, se ela nao tiver o seu.
        ///
        /// Sem EventSystem nenhum clique de UI existe: os botoes aparecem, mas o mouse nao chega
        /// neles — nem o realce de passar por cima. Era exatamente o estado da pausa dentro do
        /// jogo: so' o Menu_Principal tem EventSystem gravado na cena, e ele morre junto com ela
        /// quando a partida comeca. Dali em diante a pausa abria inteira e nenhum botao
        /// respondia; so' o ESC funcionava, porque o ESC e' lido no <see cref="Update"/> daqui e
        /// nao passa pela UI.
        ///
        /// O que se cria aqui morre junto com a cena, de proposito. Se sobrevivesse, ao voltar
        /// ao menu haveria dois EventSystem ao mesmo tempo — o Unity reclama e um dos dois para
        /// de processar, o que quebraria o menu pra consertar a pausa. Cada cena que precisar
        /// ganha o seu na hora em que sobe.
        /// </summary>
        static void GarantirEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            var tipo = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (tipo != null) es.AddComponent(tipo);
            else es.AddComponent<StandaloneInputModule>();
        }

        void MontarPausa(Transform pai)
        {
            painelPausa = Novo("Painel_Pausa", pai).gameObject;
            Esticar((RectTransform)painelPausa.transform);

            var titulo = Texto("Titulo", painelPausa.transform, "PAUSA", 64, OURO);
            Por((RectTransform)titulo.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(900f, 90f));
            titulo.fontStyle = FontStyles.Bold;

            float y = 140f;
            Botao(painelPausa.transform, "CONTINUAR", new Vector2(0f, y), new Vector2(460f, 104f), OURO, Continuar);
            Botao(painelPausa.transform, "CONFIGURAÇÕES", new Vector2(0f, y - 125f), new Vector2(460f, 104f), OURO,
                  delegate { Encher(); Mostrar(painelOpcoes); });
            Botao(painelPausa.transform, "VOLTAR AO MENU", new Vector2(0f, y - 250f), new Vector2(460f, 104f), OURO,
                  delegate { Mostrar(painelConfirmar); });
            Botao(painelPausa.transform, "SAIR DO JOGO", new Vector2(0f, y - 375f), new Vector2(460f, 104f),
                  new Color(0.93f, 0.72f, 0.66f, 1f), SairDoJogo);

            var dica = Texto("Dica", painelPausa.transform, "ESC continua o jogo", 22, FRACO);
            Por((RectTransform)dica.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(700f, 40f));
        }

        void MontarOpcoes(Transform pai)
        {
            painelOpcoes = Novo("Painel_Opcoes", pai).gameObject;
            Esticar((RectTransform)painelOpcoes.transform);

            var caixa = Novo("Caixa", painelOpcoes.transform);
            Por(caixa, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 800f));
            var img = caixa.gameObject.AddComponent<UnityEngine.UI.Image>();
            if (placa != null)
            {
                img.sprite = placa;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.5f;
            }
            else img.color = new Color(0.07f, 0.06f, 0.05f, 0.96f);

            var titulo = Texto("Titulo", caixa, "CONFIGURAÇÕES", 44, OURO);
            Por((RectTransform)titulo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(800f, 60f));
            titulo.fontStyle = FontStyles.Bold;

            // seis linhas de 78 em 78, com uma respirada antes do bloco de tela; o VOLTAR fica
            // no rodape da caixa e nao pode encostar na ultima linha
            float y = 180f;
            valorGeral   = Fatia(caixa, "Volume geral", y,       Configuracoes.VolumeGeral,
                                 delegate (float v) { Configuracoes.VolumeGeral = v; });
            valorMusica  = Fatia(caixa, "Música", y - 78f,       Configuracoes.VolumeDaMusica,
                                 delegate (float v) { Configuracoes.VolumeDaMusica = v; });
            valorEfeitos = Fatia(caixa, "Efeitos", y - 156f,     Configuracoes.VolumeDosEfeitos,
                                 delegate (float v) { Configuracoes.VolumeDosEfeitos = v; });

            rotuloResolucao = Seletor(caixa, "Resolução", y - 254f, TrocarResolucao);
            rotuloQualidade = Seletor(caixa, "Qualidade", y - 332f, TrocarQualidade);
            rotuloTelaCheia = Seletor(caixa, "Tela cheia", y - 410f, TrocarTelaCheia);

            Botao(caixa, "VOLTAR", new Vector2(0f, -332f), new Vector2(320f, 92f), OURO,
                  delegate { Mostrar(painelPausa); });
        }

        void MontarConfirmar(Transform pai)
        {
            painelConfirmar = Novo("Painel_Confirmar", pai).gameObject;
            Esticar((RectTransform)painelConfirmar.transform);

            var caixa = Novo("Caixa", painelConfirmar.transform);
            Por(caixa, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 380f));
            var img = caixa.gameObject.AddComponent<UnityEngine.UI.Image>();
            if (placa != null)
            {
                img.sprite = placa;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.5f;
            }
            else img.color = new Color(0.07f, 0.06f, 0.05f, 0.96f);

            var p = Texto("Pergunta", caixa,
                "Voltar ao menu principal?\n<size=22>O progresso da história está gravado. "
              + "Você recomeça do início desta cena.</size>", 32, CLARO);
            // tudo ancorado no CENTRO da caixa: Botao ancora assim, e misturar ancora de topo
            // com ancora de centro foi o que fez os botoes subirem em cima do texto
            Por((RectTransform)p.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(760f, 150f));
            p.textWrappingMode = TextWrappingModes.Normal;

            // "VOLTAR" e "FICAR": curtos e do mesmo tamanho. A pergunta em cima ja' diz pra
            // onde se volta, e rotulo comprido quebra em duas linhas dentro da placa
            Botao(caixa, "VOLTAR", new Vector2(-175f, -82f), new Vector2(300f, 88f), OURO, VoltarAoMenu);
            Botao(caixa, "FICAR", new Vector2(175f, -82f), new Vector2(300f, 88f), CLARO,
                  delegate { Mostrar(painelPausa); });
        }

        // ------------------------------------------------------------------ seletores

        void TrocarResolucao(int passo)
        {
            if (resolucoes.Length == 0) return;
            noQual = (noQual + passo + resolucoes.Length) % resolucoes.Length;
            Configuracoes.Resolucao = resolucoes[noQual];
            Encher();
        }

        void TrocarQualidade(int passo)
        {
            int n = QualitySettings.names.Length;
            if (n == 0) return;
            Configuracoes.Qualidade = (Configuracoes.Qualidade + passo + n) % n;
            Encher();
        }

        void TrocarTelaCheia(int passo)
        {
            Configuracoes.TelaCheia = !Configuracoes.TelaCheia;
            Encher();
        }

        /// <summary>Poe nos rotulos o que esta' valendo agora.</summary>
        void Encher()
        {
            if (rotuloResolucao != null && resolucoes.Length > 0)
                rotuloResolucao.text = resolucoes[noQual].x + " x " + resolucoes[noQual].y;
            if (rotuloQualidade != null)
            {
                var nomes = QualitySettings.names;
                int q = Mathf.Clamp(Configuracoes.Qualidade, 0, nomes.Length - 1);
                rotuloQualidade.text = nomes.Length > 0 ? nomes[q] : "-";
            }
            if (rotuloTelaCheia != null)
                rotuloTelaCheia.text = Configuracoes.TelaCheia ? "sim" : "não";
            if (valorGeral != null)   valorGeral.text   = Porcento(Configuracoes.VolumeGeral);
            if (valorMusica != null)  valorMusica.text  = Porcento(Configuracoes.VolumeDaMusica);
            if (valorEfeitos != null) valorEfeitos.text = Porcento(Configuracoes.VolumeDosEfeitos);
        }

        static string Porcento(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }

        // ------------------------------------------------------------------ pecas de tela

        static RectTransform Novo(string nome, Transform pai)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            return (RectTransform)go.transform;
        }

        static void Esticar(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        static void Por(RectTransform r, Vector2 ancora, Vector2 pos, Vector2 tam)
        {
            r.anchorMin = r.anchorMax = ancora;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = tam;
        }

        static TextMeshProUGUI Texto(string nome, Transform pai, string oQue, int tamanho, Color cor)
        {
            var r = Novo(nome, pai);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = oQue; t.fontSize = tamanho; t.color = cor;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        Button Botao(Transform pai, string rotulo, Vector2 pos, Vector2 tam, Color cor,
                     UnityEngine.Events.UnityAction aoClicar)
        {
            var r = Novo("Botao_" + rotulo, pai);
            Por(r, new Vector2(0.5f, 0.5f), pos, tam);
            var img = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            if (placa != null)
            {
                img.sprite = placa;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.5f;
            }
            else img.color = new Color(0.16f, 0.13f, 0.10f, 0.96f);

            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cores = b.colors;
            cores.highlightedColor = new Color(1.15f, 1.08f, 0.92f, 1f);
            cores.pressedColor = new Color(0.75f, 0.70f, 0.62f, 1f);
            cores.fadeDuration = 0.08f;
            b.colors = cores;
            b.onClick.AddListener(aoClicar);

            var t = Texto("texto", r, rotulo, Mathf.RoundToInt(tam.y * 0.30f), cor);
            Esticar((RectTransform)t.transform);
            t.fontStyle = FontStyles.Bold;
            // a placa tem moldura gravada dos dois lados: a palavra recua pra nao subir nela,
            // e encolhe sozinha se for comprida demais. Sem isto "VOLTAR AO MENU" invade o
            // entalhe — e qualquer rotulo novo, mais longo, invadiria tambem
            t.margin = new Vector4(34f, 6f, 34f, 6f);
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = t.fontSize * 0.55f;
            return b;
        }

        /// <summary>Linha de volume: nome, barra e o valor em porcento.</summary>
        TextMeshProUGUI Fatia(Transform pai, string nome, float y, float valor,
                              UnityEngine.Events.UnityAction<float> aoMudar)
        {
            var nomeTxt = Texto("nome_" + nome, pai, nome, 26, CLARO);
            Por((RectTransform)nomeTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(-300f, y), new Vector2(300f, 44f));
            nomeTxt.alignment = TextAlignmentOptions.MidlineLeft;

            var r = Novo("barra_" + nome, pai);
            Por(r, new Vector2(0.5f, 0.5f), new Vector2(60f, y), new Vector2(400f, 26f));
            var fundo = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            fundo.color = TRILHO;

            var area = Novo("cheio_area", r);
            area.anchorMin = new Vector2(0f, 0f); area.anchorMax = new Vector2(1f, 1f);
            area.offsetMin = new Vector2(3f, 3f); area.offsetMax = new Vector2(-17f, -3f);
            var cheio = Novo("cheio", area);
            cheio.anchorMin = Vector2.zero; cheio.anchorMax = Vector2.one;
            cheio.offsetMin = Vector2.zero; cheio.offsetMax = Vector2.zero;
            var imgCheio = cheio.gameObject.AddComponent<UnityEngine.UI.Image>();
            imgCheio.color = OURO;

            var areaAlca = Novo("alca_area", r);
            areaAlca.anchorMin = Vector2.zero; areaAlca.anchorMax = Vector2.one;
            areaAlca.offsetMin = new Vector2(10f, 0f); areaAlca.offsetMax = new Vector2(-10f, 0f);
            var alca = Novo("alca", areaAlca);
            alca.sizeDelta = new Vector2(26f, 34f);
            var imgAlca = alca.gameObject.AddComponent<UnityEngine.UI.Image>();
            imgAlca.color = new Color(0.98f, 0.93f, 0.80f, 1f);

            var s = r.gameObject.AddComponent<Slider>();
            s.fillRect = cheio; s.handleRect = alca; s.targetGraphic = imgAlca;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f; s.maxValue = 1f; s.value = valor;
            s.onValueChanged.AddListener(aoMudar);
            s.onValueChanged.AddListener(delegate (float v) { Encher(); });

            var valorTxt = Texto("valor_" + nome, pai, Porcento(valor), 24, FRACO);
            Por((RectTransform)valorTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(340f, y), new Vector2(120f, 44f));
            return valorTxt;
        }

        /// <summary>Linha de escolha: nome, seta pra tras, valor, seta pra frente.</summary>
        TextMeshProUGUI Seletor(Transform pai, string nome, float y, System.Action<int> aoTrocar)
        {
            var nomeTxt = Texto("nome_" + nome, pai, nome, 26, CLARO);
            Por((RectTransform)nomeTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(-300f, y), new Vector2(300f, 44f));
            nomeTxt.alignment = TextAlignmentOptions.MidlineLeft;

            // as setas ficam nas pontas da mesma faixa que as barras de volume ocupam,
            // pra a coluna do meio ler como uma coluna so'
            Seta(pai, "<", new Vector2(-115f, y), aoTrocar, -1);
            Seta(pai, ">", new Vector2(235f, y), aoTrocar, +1);

            var valor = Texto("valor_" + nome, pai, "-", 26, OURO);
            Por((RectTransform)valor.transform, new Vector2(0.5f, 0.5f), new Vector2(60f, y), new Vector2(280f, 44f));
            return valor;
        }

        void Seta(Transform pai, string desenho, Vector2 pos, System.Action<int> aoTrocar, int passo)
        {
            var r = Novo("seta_" + desenho + pos.y, pai);
            Por(r, new Vector2(0.5f, 0.5f), pos, new Vector2(50f, 50f));
            var img = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.14f, 0.11f, 0.08f, 0.9f);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(delegate { aoTrocar(passo); });
            var t = Texto("t", r, desenho, 30, OURO);
            Esticar((RectTransform)t.transform);
            t.fontStyle = FontStyles.Bold;
        }
    }
}
