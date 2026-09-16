using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sardael
{
    /// <summary>
    /// A placa que diz pra onde ir: uma seta com a palavra dentro.
    ///
    /// Fica na beirada da tela, do lado pra onde o jogador deve seguir, e balanca NA DIRECAO
    /// apontada — placa que so' pisca chama atencao mas nao diz pra onde ir.
    ///
    /// Nao aponta pra um objeto do mundo de proposito: num jogo 2.5D so' existem dois
    /// caminhos, e uma seta presa a um alvo gira e confunde quando o alvo passa por tras da
    /// camera.
    ///
    /// A imagem vem de <c>Resources/Seta_Avancar</c> pra que a placa funcione mesmo quando
    /// ela se cria sozinha, sem ninguem pra preencher o campo no Inspector. Glifo de fonte
    /// nao serve: o atlas padrao do TMP nao tem os triangulos, e o que aparece e' o quadrado
    /// de caractere faltando.
    /// </summary>
    public class SetaDoCaminho : MonoBehaviour
    {
        public static SetaDoCaminho Instancia { get; private set; }

        // sem Domain Reload o estatico sobrevive entre partidas; limpa antes da cena carregar
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoComecarAPartida() { Instancia = null; }

        [Header("Imagem")]
        [Tooltip("Se vazio, carrega de Resources/Seta_Avancar.")]
        public Sprite imagem;
        public string palavra = "AVANÇAR";
        public Vector2 tamanho = new Vector2(300f, 94f);
        public int tamanhoDaLetra = 30;
        public Color corDaLetra = new Color(0.13f, 0.10f, 0.06f, 1f);

        [Header("Lugar")]
        [Tooltip("Distancia da beirada da tela, em pixels de referencia (1920x1080).")]
        public float margem = 70f;
        [Range(0f, 1f)] public float altura = 0.5f;

        [Header("Pulso")]
        public float balanco = 24f;
        public float porSegundo = 1.1f;
        public float suavidade = 0.3f;

        CanvasGroup grupo;
        RectTransform placa;
        RectTransform arte;
        TextMeshProUGUI texto;
        int lado = 1;
        float alvoDeOpacidade, fase;

        /// <summary>Mostra a placa apontando pra esquerda (-1) ou pra direita (+1).</summary>
        public static void Apontar(int paraOnde)
        {
            var s = Garantir();
            if (paraOnde != 0) s.lado = (int)Mathf.Sign(paraOnde);
            s.alvoDeOpacidade = paraOnde == 0 ? 0f : 1f;
            s.Assentar();
        }

        public static void Esconder() { if (Instancia != null) Instancia.alvoDeOpacidade = 0f; }

        static SetaDoCaminho Garantir()
        {
            if (Instancia != null) return Instancia;
            return new GameObject("SetaDoCaminho").AddComponent<SetaDoCaminho>();
        }

        void Awake()
        {
            if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
            Instancia = this;
            Montar();
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        void Montar()
        {
            if (imagem == null) imagem = Resources.Load<Sprite>("Seta_Avancar");
            if (imagem == null)
                Debug.LogWarning("SetaDoCaminho: nao achei Resources/Seta_Avancar. "
                               + "A placa vai sair sem desenho.", this);

            var go = new GameObject("Canvas_Seta");
            go.transform.SetParent(transform, false);
            var cv = go.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 400;                        // abaixo da fala e do carregamento
            var escala = go.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;

            grupo = go.AddComponent<CanvasGroup>();
            grupo.alpha = 0f;
            grupo.blocksRaycasts = false;

            var pgo = new GameObject("placa", typeof(RectTransform));
            pgo.transform.SetParent(go.transform, false);
            placa = (RectTransform)pgo.transform;
            placa.sizeDelta = tamanho;

            // a ARTE vira de lado; o TEXTO nao. Espelhar o pai deixaria a palavra ao contrario.
            var ago = new GameObject("arte", typeof(RectTransform), typeof(Image));
            ago.transform.SetParent(placa, false);
            arte = (RectTransform)ago.transform;
            arte.anchorMin = Vector2.zero; arte.anchorMax = Vector2.one;
            arte.offsetMin = Vector2.zero; arte.offsetMax = Vector2.zero;
            var img = ago.GetComponent<Image>();
            img.sprite = imagem;
            img.raycastTarget = false;
            img.preserveAspect = true;

            var tgo = new GameObject("palavra", typeof(RectTransform));
            tgo.transform.SetParent(placa, false);
            var rt = (RectTransform)tgo.transform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            // a ponta da seta come um pedaco de um dos lados; a palavra recua desse lado
            rt.offsetMin = new Vector2(18f, 0f); rt.offsetMax = new Vector2(-64f, 0f);
            texto = tgo.AddComponent<TextMeshProUGUI>();
            texto.text = palavra;
            texto.fontSize = tamanhoDaLetra;
            texto.fontStyle = FontStyles.Bold;
            texto.color = corDaLetra;
            texto.alignment = TextAlignmentOptions.Center;
            texto.raycastTarget = false;
            texto.enableWordWrapping = false;

            Assentar();
        }

        void Assentar()
        {
            if (placa == null) return;
            float x = lado < 0 ? 0f : 1f;
            placa.anchorMin = placa.anchorMax = new Vector2(x, altura);
            placa.pivot = new Vector2(0.5f, 0.5f);
            placa.anchoredPosition = new Vector2(lado < 0 ? margem + tamanho.x * 0.5f
                                                          : -margem - tamanho.x * 0.5f, 0f);
            arte.localScale = new Vector3(lado, 1f, 1f);
            var rt = (RectTransform)texto.transform;
            rt.offsetMin = new Vector2(lado < 0 ? 64f : 18f, 0f);
            rt.offsetMax = new Vector2(lado < 0 ? -18f : -64f, 0f);
        }

        void Update()
        {
            if (grupo == null) return;
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, alvoDeOpacidade,
                                            Time.unscaledDeltaTime / Mathf.Max(0.01f, suavidade));
            if (grupo.alpha <= 0.001f) return;

            fase += Time.unscaledDeltaTime * porSegundo * Mathf.PI * 2f;
            float d = (Mathf.Sin(fase) * 0.5f + 0.5f) * balanco * lado;
            float baseX = lado < 0 ? margem + tamanho.x * 0.5f : -margem - tamanho.x * 0.5f;
            placa.anchoredPosition = new Vector2(baseX + d, 0f);
        }
    }
}
