using UnityEngine;
using UnityEngine.Events;

namespace Sardael
{
    /// <summary>
    /// A cena em que o Veu devolve Sardael.
    ///
    /// Fluxo: escuro e' quieto -> o veado entra pela direita andando -> para de frente pro
    /// corpo -> abaixa a cabeca e cheira, reconhecendo quem esta ali -> levanta -> a luz desce
    /// sobre Sardael -> a tela lava de branco -> chama quem vem depois.
    ///
    /// A ordem importa: o reconhecimento vem ANTES da luz. E' o veado que chama o Veu, nao o
    /// Veu que aponta o corpo pro veado.
    ///
    /// Tudo por tempo e por distancia, nao por evento de animacao: e' um plano fixo, e um
    /// corte de camera que depende de um Animation Event vira pesadelo de sincronia quando
    /// alguem mexe num clipe. Cada etapa tem um numero no Inspector.
    ///
    /// O passo do veado vem de <see cref="velocidadeDoVeado"/>, medido no clipe, e nao de um
    /// valor bonito: se o numero nao for o da passada, o bicho patina no espelho e a cena
    /// inteira perde o peso.
    /// </summary>
    public class CenaDaMorte : MonoBehaviour
    {
        public enum Etapa { Escuro, VeadoEntra, VeadoPara, Cheirando, Luz, Fade, Fim }

        [Header("Atores")]
        public Transform veado;
        public Animator animadorDoVeado;
        [Tooltip("Onde o veado para. Ele encosta ate' aqui e fica de frente pro corpo.")]
        public Transform pontoDeParada;
        public Transform sardael;

        [Header("Luz")]
        [Tooltip("O raio de luz. Ele JA fica no lugar desde o inicio - so esta invisivel.")]
        public Renderer pinturaDoFacho;
        public Light luzDoFacho;
        [Tooltip("Opacidade que o raio atinge no fim. 0 = le do proprio material.")]
        public float opacidadeFinal = 0f;

        [Header("Tempos (segundos)")]
        public float esperaInicial = 1.6f;
        [Tooltip("Quanto tempo o veado fica parado olhando antes de abaixar a cabeca.")]
        public float pausaAntesDeCheirar = 1.2f;
        [Tooltip("Duracao do cheirar. O clipe tem 5,5s; sobra pras duas transicoes.")]
        public float duracaoDoCheiro = 6.4f;
        [Tooltip("Respiro depois que ele levanta a cabeca, antes da luz descer.")]
        public float pausaAntesDaLuz = 1.1f;
        public float duracaoDaLuz = 2.6f;
        public float duracaoDoFade = 1.4f;
        public float esperaNoBranco = 0.8f;

        [Header("Veado")]
        [Tooltip("Medido na fase de apoio do casco. Mexer aqui sem remedir faz o pe deslizar.")]
        public float velocidadeDoVeado = 1.35f;
        public float desaceleracao = 1.1f;
        public string boolAndando = "andando";
        public string gatilhoCheirar = "cheirar";

        [Header("Fade")]
        public CanvasGroup telaBranca;

        [Header("Fim")]
        public UnityEvent aoAcabar;

        [Header("Depuracao")]
        public bool mostrarPainel = false;

        public Etapa Agora { get; private set; }

        float relogio;
        float brilhoMaximo;
        Material material;
        string propriedadeDaCor;
        Color corCheia = Color.white;
        bool chamou;

        void Start()
        {
            Agora = Etapa.Escuro;
            relogio = 0f;

            if (telaBranca != null) telaBranca.alpha = 0f;

            if (luzDoFacho != null) { brilhoMaximo = luzDoFacho.intensity; luzDoFacho.intensity = 0f; }

            // o raio NAO entra em cena: ele ja esta la, no lugar certo, com opacidade zero.
            // Ligar e desligar objeto denuncia o truque; opacidade subindo parece revelacao.
            if (pinturaDoFacho != null)
            {
                material = pinturaDoFacho.material;          // instancia propria, nao o asset

                // cada shader guarda a cor num nome diferente: o Legacy de particula usa
                // _TintColor, o URP usa _BaseColor, os antigos usam _Color. Procurar em vez
                // de supor foi o que resolveu - eu estava escrevendo em _BaseColor num
                // material que nem tem essa propriedade, e por isso nada apagava.
                foreach (var nome in new string[] { "_TintColor", "_BaseColor", "_Color" })
                    if (material.HasProperty(nome)) { propriedadeDaCor = nome; break; }

                if (propriedadeDaCor != null)
                {
                    var c = material.GetColor(propriedadeDaCor);
                    corCheia = new Color(c.r, c.g, c.b, 1f);
                    if (opacidadeFinal <= 0f) opacidadeFinal = c.a;
                }
                else Debug.LogWarning("CenaDaMorte: material do facho nao tem propriedade de cor conhecida");

                PintarOFacho(0f);
            }
            if (animadorDoVeado != null) animadorDoVeado.SetBool(boolAndando, false);
        }

        void Update()
        {
            relogio += Time.deltaTime;

            switch (Agora)
            {
                case Etapa.Escuro:
                    if (relogio >= esperaInicial) Trocar(Etapa.VeadoEntra);
                    break;

                case Etapa.VeadoEntra:
                    AndarOVeado();
                    break;

                case Etapa.VeadoPara:
                    if (relogio >= pausaAntesDeCheirar) Trocar(Etapa.Cheirando);
                    break;

                case Etapa.Cheirando:
                    // o veado reconhece o corpo antes de qualquer coisa acontecer.
                    // A luz so vem depois: quem chama o Veu e o reconhecimento, nao o contrario.
                    if (relogio >= duracaoDoCheiro) Trocar(Etapa.Luz);
                    break;

                case Etapa.Luz:
                    AcenderOFacho();
                    if (relogio >= duracaoDaLuz) Trocar(Etapa.Fade);
                    break;

                case Etapa.Fade:
                    if (telaBranca != null)
                        telaBranca.alpha = Mathf.Clamp01(relogio / Mathf.Max(0.01f, duracaoDoFade));
                    if (relogio >= duracaoDoFade + esperaNoBranco) Trocar(Etapa.Fim);
                    break;

                case Etapa.Fim:
                    if (!chamou)
                    {
                        chamou = true;
                        if (aoAcabar != null) aoAcabar.Invoke();
                    }
                    break;
            }
        }

        void Trocar(Etapa e)
        {
            Agora = e;
            relogio = 0f;
            if (e == Etapa.VeadoEntra && animadorDoVeado != null)
                animadorDoVeado.SetBool(boolAndando, true);
            if (e == Etapa.VeadoPara && animadorDoVeado != null)
            {
                animadorDoVeado.SetBool(boolAndando, false);
                // devolver a velocidade: a freada deixa o Animator em 0,18 e o cheirar
                // sairia cinco vezes mais lento que o clipe
                animadorDoVeado.speed = 1f;
            }
            if (e == Etapa.Cheirando && animadorDoVeado != null)
                animadorDoVeado.SetTrigger(gatilhoCheirar);

        }

        void AndarOVeado()
        {
            if (veado == null || pontoDeParada == null) { Trocar(Etapa.VeadoPara); return; }

            var alvo = pontoDeParada.position;
            var d = alvo - veado.position;
            d.y = 0f;
            float falta = d.magnitude;

            if (falta <= 0.05f)
            {
                veado.position = new Vector3(alvo.x, veado.position.y, alvo.z);
                Trocar(Etapa.VeadoPara);
                return;
            }

            // freia nos ultimos metros: um bicho que para de supetao denuncia o script
            float v = velocidadeDoVeado;
            if (falta < desaceleracao)
                v *= Mathf.Max(0.18f, falta / desaceleracao);

            veado.position += d.normalized * v * Time.deltaTime;

            // o passo do clipe acompanha a freada, senao o casco patina na chegada
            if (animadorDoVeado != null)
                animadorDoVeado.speed = Mathf.Max(0.18f, v / Mathf.Max(0.01f, velocidadeDoVeado));
        }

        /// <summary>
        /// Acende o raio subindo a opacidade, sem mover nada.
        ///
        /// A primeira versao fazia o facho DESCER do alto e crescer de escala. Na tela isso
        /// virou uma caixa branca caindo em cima do corpo: o objeto e um cubo, e cubo em
        /// movimento le como cubo. Parado e transparecendo, le como luz.
        /// </summary>
        void AcenderOFacho()
        {
            float t = Mathf.Clamp01(relogio / Mathf.Max(0.01f, duracaoDaLuz * 0.75f));
            float suave = t * t * (3f - 2f * t);
            PintarOFacho(suave);
            if (luzDoFacho != null) luzDoFacho.intensity = brilhoMaximo * suave;
        }

        /// <summary>
        /// Apaga o raio pela COR, nao so pelo alfa.
        ///
        /// O shader do Synty usa mistura pre-multiplicada: com _SrcBlend = One, o RGB entra
        /// em forca total mesmo com alfa zero. Baixar so o alfa deixava um cubo branco solido
        /// na tela desde o primeiro quadro. Escalar o RGB junto apaga em qualquer mistura.
        /// </summary>
        void PintarOFacho(float quanto)
        {
            if (material == null || propriedadeDaCor == null) return;
            var c = corCheia;
            material.SetColor(propriedadeDaCor,
                new Color(c.r * quanto, c.g * quanto, c.b * quanto, opacidadeFinal * quanto));
        }

        void OnGUI()
        {
            if (!mostrarPainel) return;
            var e = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            e.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 10, 300, 54), GUIContent.none);
            GUI.Label(new Rect(20, 16, 290, 44),
                "etapa: " + Agora + "\nrelogio: " + relogio.ToString("F2") + "s", e);
        }
    }
}
