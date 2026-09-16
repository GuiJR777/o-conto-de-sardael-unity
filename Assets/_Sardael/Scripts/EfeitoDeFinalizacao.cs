using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Dispara sangue (e, opcionalmente, a decapitacao) num instante do clipe de finalizacao.
    /// Vai na VITIMA, nao no atacante.
    ///
    /// O instante e' uma fracao do clipe (0 a 1), nao segundos: assim o mesmo valor serve se
    /// a animacao for acelerada ou trocada.
    ///
    /// SOBRE O FX: o FX_Blood_Splatter_01 da Synty e' um respingo de ACERTO — 30 particulas
    /// de 1 a 10 cm com 0,8 s de vida. Pra finalizacao isso some. E nao adianta escalar o
    /// transform: o sistema simula em espaco de MUNDO, entao escala de objeto nao mexe em
    /// tamanho nem velocidade de particula. O jeito certo e' multiplicar os parametros do
    /// proprio ParticleSystem, que e' o que os campos abaixo fazem.
    ///
    /// A cabeca nao e' "escondida": o osso Head e' encolhido a quase zero e a malha da cabeca
    /// (extraida pelos pesos do esqueleto, em Gore/Cabeca_Orc) entra como objeto solto com
    /// fisica. O encaixe e' exato porque e' a mesma geometria.
    /// </summary>
    public class EfeitoDeFinalizacao : MonoBehaviour
    {
        [Header("Quando — fracao do clipe (0 a 1)")]
        [Range(0f, 1f)] public float instante = 0.74f;

        [Header("Sangue — o esguicho do golpe")]
        public GameObject fxSangue;
        public string ossoDoSangue = "Head";
        public Vector3 deslocamentoDoSangue = new Vector3(0f, -0.05f, 0f);
        [Tooltip("Multiplica a quantidade de particulas da rajada (original: 30).")]
        public float quantidade = 6f;
        [Tooltip("Multiplica o tamanho das gotas (original: 1 a 10 cm).")]
        public float tamanho = 2.5f;
        [Tooltip("Multiplica a velocidade, ou seja o quanto o sangue se espalha.")]
        public float alcance = 1.6f;
        [Tooltip("Multiplica quanto tempo cada gota dura no ar (original: 0,8 s).")]
        public float duracaoDasGotas = 2f;
        [Tooltip("Segundos ate' o objeto de sangue ser removido.")]
        public float duracaoDoSangue = 4f;

        [Header("Jato do pescoco — so' quando decapita")]
        [Tooltip("Um segundo emissor, presos ao pescoco, que continua jorrando depois do corte.")]
        public bool jatoContinuo = true;
        public float jatoDuracao = 1.6f;
        public float jatoPorSegundo = 120f;
        public float jatoTamanho = 1.8f;
        public float jatoAlcance = 1.1f;

        [Header("Decapitacao")]
        public bool decapitar = false;
        public GameObject prefabDaCabeca;
        public string ossoDaCabeca = "Head";
        [Tooltip("Direcao do arremesso, no espaco do personagem. Em 2.5D a cabeca tem que sair " +
                 "PELA LINHA DE JOGO (nao pro fundo), com pouca altura: arco curto e depois rola.")]
        public Vector3 impulso = new Vector3(0f, 0.6f, -1f);
        public float forca = 3.4f;
        [Tooltip("Giro. O eixo e' calculado pra ela ROLAR na direcao em que foi jogada.")]
        public float torque = 7f;

        [Header("Teste")]
        public bool repetirNoLoop = true;
        [Tooltip("Segundos que a cabeca fica no chao antes do efeito reiniciar.")]
        public float esperaAntesDeRefazer = 4f;

        Animator anim;
        Transform ossoCabeca, ossoSangue;
        Vector3 escalaOriginal = Vector3.one;
        GameObject cabecaSolta, sangueSolto, jatoSolto;
        bool disparado;
        float tempoAnterior, momentoDoCorte;

        void Awake()
        {
            anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>(true);
            ossoCabeca = Procurar(ossoDaCabeca);
            ossoSangue = Procurar(ossoDoSangue);
            if (ossoCabeca != null) escalaOriginal = ossoCabeca.localScale;
        }

        Transform Procurar(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t;
            return null;
        }

        void Update()
        {
            if (anim == null) return;
            float t = anim.GetCurrentAnimatorStateInfo(0).normalizedTime;
            t = t - Mathf.Floor(t);

            bool deuAVolta = t < tempoAnterior;
            tempoAnterior = t;

            if (deuAVolta && repetirNoLoop && disparado &&
                Time.time - momentoDoCorte >= esperaAntesDeRefazer)
                Desfazer();

            if (!disparado && t >= instante) Disparar();
        }

        // depois do Animator escrever as poses, senao o osso volta ao tamanho normal
        void LateUpdate()
        {
            if (disparado && decapitar && ossoCabeca != null)
                ossoCabeca.localScale = Vector3.one * 0.0001f;
        }

        /// <summary>
        /// Multiplica os parametros do ParticleSystem. Mexer aqui e' o unico jeito que
        /// funciona: o sistema simula em espaco de mundo, escala de transform nao pega.
        /// </summary>
        static void Temperar(GameObject fx, float qtd, float tam, float vel, float vida,
                             bool continuo, float porSegundo)
        {
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                m.loop = continuo;
                m.startSizeMultiplier *= tam;
                m.startSpeedMultiplier *= vel;
                m.startLifetimeMultiplier *= vida;

                var e = ps.emission;
                int total = 0;
                if (!continuo)
                {
                    e.rateOverTime = 0f;
                    for (int i = 0; i < e.burstCount; i++)
                    {
                        var b = e.GetBurst(i);
                        float n = Mathf.Max(1f, b.count.constant * qtd);
                        b.count = new ParticleSystem.MinMaxCurve(n);
                        e.SetBurst(i, b);
                        total += Mathf.CeilToInt(n);
                    }
                }
                else
                {
                    e.SetBursts(new ParticleSystem.Burst[0]);
                    e.rateOverTime = porSegundo;
                    total = Mathf.CeilToInt(porSegundo * m.startLifetimeMultiplier * m.duration);
                }

                // sem isso a rajada e' cortada no teto antigo (50) e nada aparece
                m.maxParticles = Mathf.Max(m.maxParticles, total + 64);

                ps.Clear(true);
                ps.Play(true);
            }
        }

        void Disparar()
        {
            disparado = true;
            momentoDoCorte = Time.time;

            var onde = ossoSangue != null ? ossoSangue : transform;

            if (fxSangue != null)
            {
                sangueSolto = Instantiate(fxSangue, onde.TransformPoint(deslocamentoDoSangue), onde.rotation);
                Temperar(sangueSolto, quantidade, tamanho, alcance, duracaoDasGotas, false, 0f);
                Destroy(sangueSolto, duracaoDoSangue);
            }

            if (!decapitar || prefabDaCabeca == null || ossoCabeca == null) return;

            // jato que continua jorrando do pescoco, preso ao osso pra acompanhar o corpo
            if (jatoContinuo && fxSangue != null)
            {
                jatoSolto = Instantiate(fxSangue, onde.TransformPoint(deslocamentoDoSangue), onde.rotation);
                jatoSolto.transform.SetParent(onde, true);
                Temperar(jatoSolto, 1f, jatoTamanho, jatoAlcance, duracaoDasGotas, true, jatoPorSegundo);
                var parar = jatoSolto.AddComponent<PararEmissao>();
                parar.depoisDe = jatoDuracao;
                Destroy(jatoSolto, jatoDuracao + duracaoDoSangue);
            }

            cabecaSolta = Instantiate(prefabDaCabeca, ossoCabeca.position, ossoCabeca.rotation);
            cabecaSolta.transform.localScale = ossoCabeca.lossyScale;

            var rb = cabecaSolta.GetComponent<Rigidbody>();
            if (rb != null)
            {
                var direcao = transform.TransformDirection(impulso.normalized);
                rb.linearVelocity = direcao * forca;

                var horizontal = new Vector3(direcao.x, 0f, direcao.z);
                var eixo = horizontal.sqrMagnitude > 0.0001f
                    ? Vector3.Cross(Vector3.up, horizontal.normalized)
                    : Vector3.right;
                rb.angularVelocity = eixo * torque + Random.onUnitSphere * (torque * 0.12f);
            }
        }

        void Desfazer()
        {
            disparado = false;
            if (cabecaSolta != null) Destroy(cabecaSolta);
            if (sangueSolto != null) Destroy(sangueSolto);
            if (jatoSolto != null) Destroy(jatoSolto);
            if (ossoCabeca != null) ossoCabeca.localScale = escalaOriginal;
        }

        void OnDisable()
        {
            if (ossoCabeca != null) ossoCabeca.localScale = escalaOriginal;
        }
    }

    /// <summary>Corta a emissao depois de N segundos, deixando as gotas ja' soltas cairem.</summary>
    public class PararEmissao : MonoBehaviour
    {
        public float depoisDe = 1.5f;
        float nascimento;

        void Start() { nascimento = Time.time; }

        void Update()
        {
            if (Time.time - nascimento < depoisDe) return;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            enabled = false;
        }
    }
}
