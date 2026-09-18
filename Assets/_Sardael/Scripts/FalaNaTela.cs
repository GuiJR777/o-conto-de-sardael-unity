using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sardael
{
    /// <summary>
    /// A caixa de fala das cutscenes.
    ///
    /// Monta o proprio Canvas no Awake, de codigo. Nao ha' nada pra ligar na cena e nada pra
    /// quebrar quando alguem apaga um objeto sem querer — o unico jeito de a fala sumir e'
    /// apagar este componente.
    ///
    /// A caixa nao some sozinha no meio de uma frase: <see cref="Dizer"/> recebe os segundos,
    /// e quem chama e' o diretor da cena, que sabe o ritmo. Uma caixa que decide sozinha
    /// quanto tempo fica na tela sempre briga com o tempo da animacao.
    /// </summary>
    public class FalaNaTela : MonoBehaviour
    {
        public static FalaNaTela Instancia { get; private set; }

        // Com o Domain Reload desligado (que e' o que faz o Play abrir em 1 s em vez de 40),
        // os campos estaticos SOBREVIVEM entre uma partida e outra e apontam pra objetos ja'
        // destruidos. Isto limpa antes de a cena carregar.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoComecarAPartida() { Instancia = null; }

        [Header("Tamanho")]
        public int tamanhoDoNome = 26;
        public int tamanhoDaFala = 30;
        [Tooltip("Altura da caixa em pixels de referencia (tela de 1920x1080).")]
        public float alturaDaCaixa = 180f;
        public float margemDeBaixo = 60f;
        public float margemDosLados = 220f;

        [Header("Cores")]
        public Color corDoFundo = new Color(0.05f, 0.05f, 0.07f, 0.82f);
        public Color corDoNome = new Color(0.91f, 0.77f, 0.42f, 1f);
        public Color corDaFala = new Color(0.95f, 0.94f, 0.91f, 1f);

        [Header("Ritmo")]
        [Tooltip("Segundos pra caixa aparecer e sumir.")]
        public float suavidade = 0.22f;

        CanvasGroup grupo;
        TextMeshProUGUI textoDoNome, textoDaFala;
        float alvoDeOpacidade;

        public bool Falando { get { return alvoDeOpacidade > 0.5f; } }

        void Awake()
        {
            Instancia = this;
            Montar();
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        void Montar()
        {
            var go = new GameObject("Canvas_Fala");
            go.transform.SetParent(transform, false);
            go.layer = LayerMask.NameToLayer("UI");

            var cv = go.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 500;
            var escala = go.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            grupo = go.AddComponent<CanvasGroup>();
            grupo.alpha = 0f;
            grupo.blocksRaycasts = false;
            grupo.interactable = false;

            // --- fundo
            var fundo = new GameObject("fundo", typeof(RectTransform), typeof(Image));
            fundo.transform.SetParent(go.transform, false);
            var rf = (RectTransform)fundo.transform;
            rf.anchorMin = new Vector2(0f, 0f);
            rf.anchorMax = new Vector2(1f, 0f);
            rf.pivot = new Vector2(0.5f, 0f);
            rf.offsetMin = new Vector2(margemDosLados, margemDeBaixo);
            rf.offsetMax = new Vector2(-margemDosLados, margemDeBaixo + alturaDaCaixa);
            fundo.GetComponent<Image>().color = corDoFundo;

            textoDoNome = Texto("nome", fundo.transform, tamanhoDoNome, corDoNome,
                                new Vector2(0f, 1f), new Vector2(1f, 1f),
                                new Vector2(28f, -46f), new Vector2(-28f, -10f));
            textoDoNome.fontStyle = FontStyles.Bold;

            textoDaFala = Texto("fala", fundo.transform, tamanhoDaFala, corDaFala,
                                new Vector2(0f, 0f), new Vector2(1f, 1f),
                                new Vector2(28f, 16f), new Vector2(-28f, -50f));
            textoDaFala.textWrappingMode = TextWrappingModes.Normal;
        }

        TextMeshProUGUI Texto(string nome, Transform pai, int tamanho, Color cor,
                              Vector2 ancoraMin, Vector2 ancoraMax, Vector2 min, Vector2 max)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = ancoraMin; r.anchorMax = ancoraMax;
            r.offsetMin = min; r.offsetMax = max;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = tamanho;
            t.color = cor;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Poe a fala na tela. Quem apaga e' o diretor, chamando <see cref="Calar"/>.</summary>
        public void Dizer(string quem, string oQue)
        {
            if (textoDaFala == null) return;
            textoDoNome.text = quem;
            textoDaFala.text = oQue;
            alvoDeOpacidade = 1f;
        }

        public void Calar() { alvoDeOpacidade = 0f; }

        void Update()
        {
            if (grupo == null) return;
            float passo = Time.unscaledDeltaTime / Mathf.Max(0.01f, suavidade);
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, alvoDeOpacidade, passo);
        }
    }
}
