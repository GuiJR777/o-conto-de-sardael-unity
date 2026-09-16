using UnityEngine;
using System.Text;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// A vitrine das lancas: os retratos na tela, clicou trocou na mao.
    ///
    /// E' IMGUI de proposito. Isto e' ferramenta de oficina, nao HUD de jogo: IMGUI nao precisa
    /// de Canvas, nao precisa de EventSystem (que esta cena nao tem) e nao depende de qual
    /// backend de entrada esta ligado. Menos peca pra quebrar.
    ///
    /// O jogo continua rodando com o catalogo aberto — de proposito tambem. O retrato mostra o
    /// desenho, mas quem diz se a lanca presta e' ela na mao de Sardael no meio da animacao, e
    /// pra isso a animacao tem que estar tocando.
    ///
    /// A marca de "apagar" so' anota num arquivo de texto. Jogo rodando nao mexe em asset; quem
    /// apaga de verdade e' o menu Sardael > Lancas > Apagar as marcadas.
    /// </summary>
    public class CatalogoDeLancas : MonoBehaviour
    {
        [Header("Tecla")]
        public KeyCode teclaAbrir = KeyCode.Tab;
        public bool comecaAberto = false;

        [Header("Quem troca a lanca")]
        public LancaNaMao mao;

        const float ALT = 1080f;
        static readonly Color FUNDO    = new Color(0.106f, 0.114f, 0.141f, 0.97f);
        static readonly Color CELULA   = new Color(0.106f, 0.114f, 0.141f, 1f);
        static readonly Color OURO     = new Color(0.91f, 0.77f, 0.42f, 1f);
        static readonly Color VERMELHO = new Color(0.85f, 0.26f, 0.22f, 1f);
        static readonly Color CINZA    = new Color(0.72f, 0.73f, 0.77f, 1f);

        bool aberto;
        int selecionada = -1;
        Vector2 rolagem;
        bool[] marcadas;
        MovimentoDoHeroi heroi;
        GUIStyle eTitulo, eNome, eInfo, eCelula, eBotao, eRodape;
        Texture2D pixel;
        Camera visor;
        RenderTexture telaDoVisor;

        /// <summary>O menu de pausa olha isto pra nao abrir por cima do catalogo no ESC.</summary>
        public bool Aberto { get { return aberto; } }

        void Awake()
        {
            aberto = comecaAberto;
            if (mao == null) mao = Object.FindFirstObjectByType<LancaNaMao>();
            heroi = Object.FindFirstObjectByType<MovimentoDoHeroi>();
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        void Start()
        {
            int n = (mao != null && mao.Acervo != null) ? mao.Acervo.lancas.Length : 0;
            marcadas = new bool[n];
            if (aberto) { Travar(true); GarantirVisor(); }
        }

        void OnDestroy()
        {
            Travar(false);
            if (pixel != null) Destroy(pixel);
            if (visor != null) Destroy(visor.gameObject);
            if (telaDoVisor != null) { telaDoVisor.Release(); Destroy(telaDoVisor); }
        }

        /// <summary>
        /// O visor: uma segunda camera olhando Sardael, desenhada dentro do painel.
        ///
        /// Sem ele o catalogo tapa a tela inteira e esconde justamente o que interessa — a
        /// lanca na mao, no meio da animacao. O retrato mostra o desenho; o visor mostra se
        /// a arma cabe no personagem.
        /// </summary>
        void GarantirVisor()
        {
            if (visor != null) { visor.enabled = true; return; }
            if (mao == null) return;

            telaDoVisor = new RenderTexture(512, 648, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var go = new GameObject("Visor_Do_Catalogo");
            go.transform.SetParent(transform, false);
            visor = go.AddComponent<Camera>();
            visor.orthographic = true;
            visor.orthographicSize = 1.55f;
            visor.nearClipPlane = 0.05f;
            visor.farClipPlane = 40f;
            visor.clearFlags = CameraClearFlags.SolidColor;
            visor.backgroundColor = CELULA;
            visor.targetTexture = telaDoVisor;
            visor.depth = -20f;                 // nunca por cima da camera do jogo
        }

        void LateUpdate()
        {
            if (visor == null || !aberto || mao == null) return;
            // de lado, na altura do peito: e' o enquadramento da camera do jogo
            visor.transform.position = mao.transform.position + new Vector3(0f, 1.15f, -9f);
            visor.transform.rotation = Quaternion.identity;
        }

        void Update()
        {
            bool apertou = false;
#if ENABLE_INPUT_SYSTEM
            var t = Keyboard.current;
            if (t != null)
                apertou = t.tabKey.wasPressedThisFrame || (aberto && t.escapeKey.wasPressedThisFrame);
#else
            apertou = Input.GetKeyDown(teclaAbrir) || (aberto && Input.GetKeyDown(KeyCode.Escape));
#endif
            if (apertou)
            {
                aberto = !aberto;
                Travar(aberto);
                if (aberto) GarantirVisor();
                else if (visor != null) visor.enabled = false;
            }
        }

        void Travar(bool quanto)
        {
            if (heroi != null) heroi.Travar(this, quanto);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        void Estilos()
        {
            if (eTitulo != null) return;
            eTitulo = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            eTitulo.normal.textColor = OURO;
            eNome = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold };
            eNome.normal.textColor = Color.white;
            eInfo = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            eInfo.normal.textColor = CINZA;
            eCelula = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            eCelula.normal.textColor = CINZA;
            eBotao = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            eRodape = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            eRodape.normal.textColor = new Color(0.55f, 0.57f, 0.62f, 1f);
        }

        void Caixa(Rect r, Color c)
        {
            var antes = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, pixel); GUI.color = antes;
        }

        void Moldura(Rect r, Color c, float grossura)
        {
            Caixa(new Rect(r.x, r.y, r.width, grossura), c);
            Caixa(new Rect(r.x, r.yMax - grossura, r.width, grossura), c);
            Caixa(new Rect(r.x, r.y, grossura, r.height), c);
            Caixa(new Rect(r.xMax - grossura, r.y, grossura, r.height), c);
        }

        void OnGUI()
        {
            Estilos();
            float escala = Screen.height / ALT;
            float larguraVirtual = Screen.width / escala;
            var guardada = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));

            if (!aberto)
            {
                Caixa(new Rect(larguraVirtual - 430f, ALT - 60f, 400f, 36f), new Color(0f, 0f, 0f, 0.5f));
                GUI.Label(new Rect(larguraVirtual - 418f, ALT - 57f, 400f, 30f),
                          teclaAbrir + "  catalogo de lancas", eRodape);
                GUI.matrix = guardada;
                return;
            }

            if (mao == null || mao.Acervo == null || mao.Acervo.lancas.Length == 0)
            {
                Caixa(new Rect(24f, 24f, 900f, 70f), FUNDO);
                GUI.Label(new Rect(40f, 40f, 880f, 44f),
                    "Acervo vazio. Rode o menu  Sardael > Lancas > Montar o acervo.", eInfo);
                GUI.matrix = guardada;
                return;
            }

            var acervo = mao.Acervo;
            if (marcadas == null || marcadas.Length != acervo.lancas.Length)
                marcadas = new bool[acervo.lancas.Length];

            var painel = new Rect(24f, 24f, larguraVirtual - 48f, ALT - 48f);
            Caixa(painel, FUNDO);
            Moldura(painel, new Color(1f, 1f, 1f, 0.10f), 2f);

            GUI.Label(new Rect(painel.x + 26f, painel.y + 16f, 900f, 44f),
                      "CATALOGO DE LANCAS  -  " + acervo.lancas.Length + " modelos", eTitulo);

            // ---------------- coluna da esquerda: o visor ao vivo + a ficha
            var vitrine = new Rect(painel.x + 26f, painel.y + 72f, 372f, painel.height - 150f);
            Caixa(vitrine, CELULA);
            Moldura(vitrine, new Color(1f, 1f, 1f, 0.08f), 1f);
            DesenharVitrine(vitrine, acervo);

            // ---------------- grade
            var area = new Rect(vitrine.xMax + 24f, painel.y + 72f,
                                painel.xMax - vitrine.xMax - 50f, painel.height - 150f);

            const float LARG_CEL = 132f, ALT_CEL = 263f;
            int colunas = Mathf.Max(1, Mathf.FloorToInt((area.width - 20f) / LARG_CEL));
            int linhas = Mathf.CeilToInt(acervo.lancas.Length / (float)colunas);
            var dentro = new Rect(0f, 0f, colunas * LARG_CEL, linhas * ALT_CEL);

            rolagem = GUI.BeginScrollView(area, rolagem, dentro);
            for (int i = 0; i < acervo.lancas.Length; i++)
            {
                var cel = new Rect((i % colunas) * LARG_CEL, (i / colunas) * ALT_CEL,
                                   LARG_CEL - 8f, ALT_CEL - 8f);
                DesenharCelula(cel, acervo, i);
            }
            GUI.EndScrollView();

            int quantasMarcadas = 0;
            for (int i = 0; i < marcadas.Length; i++) if (marcadas[i]) quantasMarcadas++;
            GUI.Label(new Rect(painel.x + 26f, painel.yMax - 62f, painel.width - 52f, 30f),
                "clique escolhe e ja' poe na mao   -   botao direito marca pra apagar   -   "
              + "TAB ou ESC fecha   -   marcadas: " + quantasMarcadas, eRodape);

            GUI.matrix = guardada;
        }

        void DesenharVitrine(Rect r, AcervoDeLancas acervo)
        {
            bool temEscolha = selecionada >= 0 && selecionada < acervo.lancas.Length;
            var l = temEscolha ? acervo.lancas[selecionada] : null;

            GUI.Label(new Rect(r.x + 18f, r.y + 10f, r.width - 36f, 54f),
                      temEscolha ? l.nome : "original", eNome);

            // o visor ao vivo: Sardael com a lanca na mao, animacao rodando
            var quadro = new Rect(r.x + 14f, r.y + 66f, r.width - 28f, r.height - 262f);
            if (telaDoVisor != null)
            {
                float a = (float)telaDoVisor.width / telaDoVisor.height;
                float h = Mathf.Min(quadro.height, quadro.width / a);
                GUI.DrawTexture(new Rect(quadro.center.x - h * a * 0.5f, quadro.y, h * a, h), telaDoVisor);
            }
            else Caixa(quadro, new Color(0f, 0f, 0f, 0.25f));
            Moldura(quadro, new Color(1f, 1f, 1f, 0.10f), 1f);

            var sb = new StringBuilder();
            if (temEscolha)
            {
                sb.AppendLine("pacote " + l.pacote + "    modelo cru " + l.alturaCrua.ToString("F2") + " m");
                sb.AppendLine("na mao: " + acervo.comprimentoAlvo.ToString("F2")
                            + " m, o mesmo da lanca do pack de animacao");
            }
            else sb.AppendLine("A lanca do pack de animacao. E' a medida que todas as outras seguem.");
            GUI.Label(new Rect(r.x + 18f, quadro.yMax + 8f, r.width - 36f, 80f), sb.ToString(), eInfo);

            var b1 = new Rect(r.x + 18f, r.yMax - 112f, r.width - 36f, 42f);
            if (GUI.Button(b1, !temEscolha ? "escolha uma ao lado"
                             : (mao.Escolhida == selecionada ? "esta na mao" : "Equipar"), eBotao)
                && temEscolha) mao.Equipar(selecionada);

            var b2 = new Rect(r.x + 18f, r.yMax - 62f, (r.width - 44f) * 0.5f, 42f);
            if (GUI.Button(b2, "Original", eBotao)) { mao.VoltarAOriginal(); selecionada = -1; }

            var b3 = new Rect(b2.xMax + 8f, b2.y, b2.width, 42f);
            var corAntes = GUI.color;
            if (temEscolha && marcadas[selecionada]) GUI.color = VERMELHO;
            if (GUI.Button(b3, temEscolha && marcadas[selecionada] ? "Desmarcar" : "Apagar", eBotao)
                && temEscolha)
            {
                marcadas[selecionada] = !marcadas[selecionada];
                Salvar(acervo);
            }
            GUI.color = corAntes;
        }

        void DesenharCelula(Rect cel, AcervoDeLancas acervo, int i)
        {
            var l = acervo.lancas[i];
            Caixa(cel, CELULA);

            if (marcadas[i])
            {
                Caixa(cel, new Color(VERMELHO.r, VERMELHO.g, VERMELHO.b, 0.22f));
                Moldura(cel, VERMELHO, 2f);
            }
            else if (i == selecionada) Moldura(cel, OURO, 2f);
            else if (i == mao.Escolhida) Moldura(cel, new Color(1f, 1f, 1f, 0.35f), 1f);
            else Moldura(cel, new Color(1f, 1f, 1f, 0.07f), 1f);

            if (l.retrato != null)
                GUI.DrawTexture(new Rect(cel.x + 4f, cel.y + 3f, cel.width - 8f, cel.height - 28f),
                                l.retrato, ScaleMode.ScaleToFit);

            var corAntes = GUI.contentColor;
            GUI.contentColor = marcadas[i] ? VERMELHO : (i == selecionada ? OURO : CINZA);
            GUI.Label(new Rect(cel.x, cel.yMax - 24f, cel.width, 22f), l.nome, eCelula);
            GUI.contentColor = corAntes;

            var e = Event.current;
            if (e.type == EventType.MouseDown && cel.Contains(e.mousePosition))
            {
                if (e.button == 1) { marcadas[i] = !marcadas[i]; Salvar(acervo); }
                else { selecionada = i; mao.Equipar(i); }
                e.Use();
            }
        }

        /// <summary>Anota as marcadas num txt do projeto pro menu do editor apagar depois.</summary>
        void Salvar(AcervoDeLancas acervo)
        {
#if UNITY_EDITOR
            var sb = new StringBuilder();
            for (int i = 0; i < marcadas.Length; i++)
                if (marcadas[i]) sb.AppendLine(acervo.lancas[i].nome);
            System.IO.File.WriteAllText(
                "Assets/_Sardael/Externos/Lancas/marcadas_pra_apagar.txt", sb.ToString());
#endif
        }
    }
}
