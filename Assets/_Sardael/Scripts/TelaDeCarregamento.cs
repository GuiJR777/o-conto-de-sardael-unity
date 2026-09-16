using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Sardael
{
    /// <summary>
    /// A tela preta entre uma cena e outra.
    ///
    /// Monta o proprio Canvas e sobrevive a troca de cena (<c>DontDestroyOnLoad</c>), porque
    /// uma tela de carregamento que mora na cena de origem morre junto com ela — no meio do
    /// carregamento, que e' exatamente quando ela precisa estar na tela.
    ///
    /// Ninguem precisa por isto na cena: <see cref="Ir"/> cria a instancia se nao houver.
    /// Assim uma fase nova nao esquece de trazer a tela junto.
    ///
    /// O carregamento e' assincrono e SEGURADO ate' o fade acabar
    /// (<c>allowSceneActivation = false</c>): sem isso a cena nova entra por baixo da tela
    /// ainda transparente e o jogador ve' o corte.
    /// </summary>
    public class TelaDeCarregamento : MonoBehaviour
    {
        public static TelaDeCarregamento Instancia { get; private set; }

        // sem Domain Reload o estatico sobrevive entre partidas; limpa antes da cena carregar
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoComecarAPartida() { Instancia = null; }

        [Header("Ritmo")]
        public float tempoDeEscurecer = 0.45f;
        public float tempoDeClarear = 0.55f;
        [Tooltip("Tempo minimo com a tela preta. Cena leve carrega rapido demais e o preto "
               + "pisca, o que le' pior do que uma espera curta.")]
        public float tempoMinimo = 0.9f;

        [Header("Esperar a cena ficar jogavel")]
        [Tooltip("Quadros seguidos dentro do limite pra considerar que a cena esta' rodando. "
               + "Um so' nao basta: o primeiro quadro depois do engasgo sempre sai rapido.")]
        public int quadrosBonsSeguidos = 5;
        [Tooltip("Duracao maxima de um quadro, em segundos, pra ele contar como bom. O alvo e' "
               + "pular o ENGASGO da primeira imagem, nao esconder jogo lento: 0,25 e' bem "
               + "abaixo de qualquer quadro de abertura e bem acima de um jogo so' pesado.")]
        public float limiteDoQuadro = 0.25f;
        [Tooltip("Desiste de esperar depois disto e abre assim mesmo. Curto de proposito: se a "
               + "cena roda devagar de verdade, o lugar de descobrir isso e' jogando, nao "
               + "olhando pra um preto que nunca abre.")]
        public float esperaMaxima = 8f;
        [Tooltip("Escreve no Console quanto tempo foi carregar e quanto foi destravar.")]
        public bool contarOTempo = true;

        [Header("Cara")]
        public Color corDoFundo = new Color(0.04f, 0.04f, 0.05f, 1f);
        public Color corDoTexto = new Color(0.91f, 0.77f, 0.42f, 1f);
        [TextArea] public string recado = "Carregando...";

        CanvasGroup grupo;
        TextMeshProUGUI texto;
        Image barra;
        bool ocupada;

        public static bool Carregando { get { return Instancia != null && Instancia.ocupada; } }

        /// <summary>Vai pra outra cena com tela preta no meio. Cria a tela se ainda nao houver.</summary>
        public static void Ir(string nomeDaCena, string recadoDaVez = null)
        {
            var t = Garantir();
            if (t.ocupada) return;                       // dois gatilhos no mesmo quadro
            if (recadoDaVez != null) t.recado = recadoDaVez;
            t.StartCoroutine(t.Trocar(nomeDaCena));
        }

        static TelaDeCarregamento Garantir()
        {
            if (Instancia != null) return Instancia;
            var go = new GameObject("TelaDeCarregamento");
            DontDestroyOnLoad(go);
            return go.AddComponent<TelaDeCarregamento>();
        }

        void Awake()
        {
            if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
            Instancia = this;
            DontDestroyOnLoad(gameObject);
            Montar();
        }

        void Montar()
        {
            var go = new GameObject("Canvas_Carregamento");
            go.transform.SetParent(transform, false);
            var cv = go.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 900;                        // acima da caixa de fala
            var escala = go.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;

            grupo = go.AddComponent<CanvasGroup>();
            grupo.alpha = 0f;
            grupo.blocksRaycasts = false;

            var fundo = new GameObject("fundo", typeof(RectTransform), typeof(Image));
            fundo.transform.SetParent(go.transform, false);
            var rf = (RectTransform)fundo.transform;
            rf.anchorMin = Vector2.zero; rf.anchorMax = Vector2.one;
            rf.offsetMin = Vector2.zero; rf.offsetMax = Vector2.zero;
            fundo.GetComponent<Image>().color = corDoFundo;

            var tgo = new GameObject("recado", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)tgo.transform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(120f, 120f); rt.offsetMax = new Vector2(-120f, 190f);
            texto = tgo.AddComponent<TextMeshProUGUI>();
            texto.fontSize = 34; texto.color = corDoTexto;
            texto.alignment = TextAlignmentOptions.BottomRight;
            texto.raycastTarget = false;

            var bfundo = new GameObject("barra_fundo", typeof(RectTransform), typeof(Image));
            bfundo.transform.SetParent(go.transform, false);
            var rb = (RectTransform)bfundo.transform;
            rb.anchorMin = new Vector2(0f, 0f); rb.anchorMax = new Vector2(1f, 0f);
            rb.offsetMin = new Vector2(120f, 96f); rb.offsetMax = new Vector2(-120f, 102f);
            bfundo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var bgo = new GameObject("barra", typeof(RectTransform), typeof(Image));
            bgo.transform.SetParent(bfundo.transform, false);
            var rbb = (RectTransform)bgo.transform;
            rbb.anchorMin = Vector2.zero; rbb.anchorMax = new Vector2(0f, 1f);
            rbb.offsetMin = Vector2.zero; rbb.offsetMax = Vector2.zero;
            rbb.pivot = new Vector2(0f, 0.5f);
            barra = bgo.GetComponent<Image>();
            barra.color = corDoTexto;
        }

        IEnumerator Trocar(string nomeDaCena)
        {
            ocupada = true;
            texto.text = recado;
            grupo.blocksRaycasts = true;
            Encher(0f);

            yield return Pintar(0f, 1f, tempoDeEscurecer);

            float comecou = Time.unscaledTime;
            var carga = SceneManager.LoadSceneAsync(nomeDaCena);
            if (carga == null)
            {
                // nome errado ou cena fora do Build Settings: o jogo ficaria preto pra sempre
                Debug.LogError("TelaDeCarregamento: nao consegui carregar '" + nomeDaCena
                    + "'. A cena esta no Build Settings? (Arquivo > Build Settings)", this);
                yield return Pintar(1f, 0f, tempoDeClarear);
                grupo.blocksRaycasts = false; ocupada = false;
                yield break;
            }
            carga.allowSceneActivation = false;

            // o async para em 0,9 quando esta' pronto e espera a autorizacao
            while (carga.progress < 0.9f) { Encher(carga.progress / 0.9f); yield return null; }
            Encher(1f);
            while (Time.unscaledTime - comecou < tempoMinimo) yield return null;

            float leuODisco = Time.unscaledTime - comecou;
            carga.allowSceneActivation = true;
            while (!carga.isDone) yield return null;

            // ---- e agora a parte que faltava.
            //
            // isDone quer dizer "os objetos existem", e nao "da' pra jogar". Depois disso o
            // Unity ainda manda a primeira imagem: e' ai' que ele compila variante de shader,
            // sobe textura e acorda particula — dezenas de segundos numa cena grande, com o
            // jogo congelado. A tela abria em cima desse congelamento, e por isso ela mentia.
            //
            // O criterio de "pronto" nao e' tempo nem e' o isDone: sao quadros seguidos dentro
            // do limite. Enquanto o quadro estiver custando mais que isso, a cena ainda esta'
            // engasgando e o preto continua.
            texto.text = "Preparando a cena...";
            float comecouAPreparar = Time.unscaledTime;
            int bons = 0;
            while (bons < quadrosBonsSeguidos
                   && Time.unscaledTime - comecouAPreparar < esperaMaxima)
            {
                yield return null;
                if (Time.unscaledDeltaTime <= limiteDoQuadro) bons++;
                else bons = 0;                              // engasgou: a conta recomeca
                Encher(Mathf.Clamp01(bons / (float)Mathf.Max(1, quadrosBonsSeguidos)));
            }
            float preparou = Time.unscaledTime - comecouAPreparar;

            if (contarOTempo)
                Debug.Log("Carregamento de '" + nomeDaCena + "': disco " + leuODisco.ToString("F1")
                        + " s, destravar " + preparou.ToString("F1") + " s"
                        + (bons < quadrosBonsSeguidos ? "  (desisti de esperar)" : ""));

            yield return Pintar(1f, 0f, tempoDeClarear);
            grupo.blocksRaycasts = false;
            ocupada = false;
        }

        void Encher(float quanto)
        {
            if (barra == null) return;
            var r = (RectTransform)barra.transform;
            r.anchorMax = new Vector2(Mathf.Clamp01(quanto), 1f);
        }

        IEnumerator Pintar(float de, float ate, float quanto)
        {
            if (quanto <= 0f) { grupo.alpha = ate; yield break; }
            float t = 0f;
            while (t < quanto)
            {
                t += Time.unscaledDeltaTime;
                grupo.alpha = Mathf.Lerp(de, ate, t / quanto);
                yield return null;
            }
            grupo.alpha = ate;
        }
    }
}
