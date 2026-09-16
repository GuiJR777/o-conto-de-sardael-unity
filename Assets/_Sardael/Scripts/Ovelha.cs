using UnityEngine;
using System.Collections.Generic;

namespace Sardael
{
    /// <summary>
    /// Vida de ovelha: pasta, olha, cochila, caminha um pouco e de vez em quando dispara.
    ///
    /// A REGRA QUE MANDA AQUI: o rebanho foi posicionado a mao, e cada ovelha volta sempre pra
    /// perto de onde foi posta. O <see cref="casa"/> e' gravado no Awake e nunca muda, entao os
    /// grupos continuam sendo grupos depois de meia hora de jogo. Sem isso elas viram uma fila
    /// indiana atravessando Halu.
    ///
    /// Quem toca a animacao e' o AnimatorController. Este script so' informa a velocidade e liga
    /// dois bools. Nao ha' previsao de duracao de clipe em lugar nenhum deste arquivo.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class Ovelha : MonoBehaviour
    {
        public const string P_VELOCIDADE = "velocidade";
        public const string P_COMENDO    = "comendo";
        public const string P_DORMINDO   = "dormindo";
        public const string P_OLHAR      = "olhar";
        public const string P_SACUDIR    = "sacudir";

        public static readonly List<Ovelha> Todas = new List<Ovelha>();

        [Header("Territorio")]
        [Tooltip("Centro do pasto. Vazio = usa o ponto onde ela foi posta na cena.")]
        public Transform pasto;
        [Tooltip("Ate onde ela se afasta do centro do pasto.")]
        public float raio = 6f;
        [Tooltip("Recusa destino com desnivel maior que isto. E o que a mantem fora do lago e dos barrancos.")]
        public float desnivelMaximo = 1.4f;

        [Header("Escala real")]
        [Tooltip("Comprimento do bicho em metros. Ovelha adulta: 1,20 (ovelha) a 1,40 (carneiro).")]
        public float comprimentoReal = 1.10f;
        [Tooltip("Calcula a escala pelo comprimento da malha. Desligue pra usar a escala da cena.")]
        public bool escalaPeloReal = true;

        [Header("Velocidade")]
        public float velocidadeAndar = 0.64f;    // medida no clipe: e a que nao desliza
        public float velocidadeTrote = 1.40f;    // clipe acelerado 1,97x no blend tree
        public float velocidadeCorrer = 2.40f;   // clipe acelerado 2,61x no blend tree
        public float aceleracao = 4f;
        public float giroPorSegundo = 170f;

        [Header("Tempos (minimo, maximo)")]
        public Vector2 tempoPastando = new Vector2(7f, 18f);
        public Vector2 tempoParada = new Vector2(2f, 5f);
        public Vector2 tempoDormindo = new Vector2(14f, 34f);

        [Header("Chances a cada decisao")]
        [Range(0f, 1f)] public float chancePastar = 0.45f;
        [Range(0f, 1f)] public float chanceDormir = 0.10f;
        [Range(0f, 1f)] public float chanceOlhar = 0.12f;
        [Range(0f, 1f)] public float chanceSacudir = 0.06f;
        [Range(0f, 1f)] public float chanceTrote = 0.18f;

        [Header("Susto")]
        [Tooltip("Vazio = acha o heroi sozinho na cena.")]
        public Transform fugirDe;
        public float distanciaDeSusto = 5f;
        public float tempoFugindo = 3.5f;

        [Header("Rebanho")]
        [Tooltip("Empurrao leve pra nao ficarem uma dentro da outra.")]
        public float separacao = 1.15f;

        enum Estado { Pastando, Parada, Indo, Dormindo, Olhando, Sacudindo, Fugindo }

        Animator anim;
        Terrain chao;
        float escala = 1f;
        Vector3 casa;
        float alturaRelativa;
        Estado estado;
        float ateQuando;
        Vector3 destino;
        float alvoVelocidade, velocidade;

        void OnEnable() { if (!Todas.Contains(this)) Todas.Add(this); }
        void OnDisable() { Todas.Remove(this); }

        void Awake()
        {
            anim = GetComponent<Animator>();
            chao = Terrain.activeTerrain;

            if (escalaPeloReal) AplicarEscalaReal();
            escala = Mathf.Max(0.05f, transform.lossyScale.x);

            // o pasto e um objeto vazio no centro do grupo: e assim que cinco ovelhas
            // dividem UM cercado em vez de cada uma ter seu proprio circulo, que estouraria
            // a cerca por todos os lados
            casa = pasto != null ? pasto.position : transform.position;

            // guarda o desnivel de quem posicionou: se ela foi posta num cercado um palmo
            // acima do terreno, ela continua nesse palmo o jogo inteiro
            var aqui = transform.position;
            alturaRelativa = chao != null
                ? aqui.y - (chao.SampleHeight(aqui) + chao.transform.position.y)
                : 0f;

            // cada uma no seu ritmo, senao dezenove ovelhas mastigam no mesmo compasso
            anim.speed = Random.Range(0.94f, 1.06f);

            if (fugirDe == null)
            {
                var heroi = Object.FindFirstObjectByType<MovimentoDoHeroi>();
                if (heroi != null) fugirDe = heroi.transform;
            }
        }

        void Start()
        {
            Decidir();
            // espalha a primeira decisao no tempo pra nao comecarem todas juntas
            ateQuando = Time.time + Random.Range(0f, 6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (Assustada())
            {
                if (estado != Estado.Fugindo) ComecarFuga();
            }
            else if (Time.time >= ateQuando)
            {
                Decidir();
            }

            if (estado == Estado.Indo || estado == Estado.Fugindo)
            {
                var plano = destino - transform.position;
                plano.y = 0f;
                if (plano.magnitude < 0.45f)
                {
                    alvoVelocidade = 0f;
                    if (estado == Estado.Indo) Decidir();
                }
                else Virar(plano.normalized, dt);
            }
            else alvoVelocidade = 0f;

            velocidade = Mathf.MoveTowards(velocidade, alvoVelocidade, aceleracao * dt);
            if (velocidade > 0.01f)
                transform.position += transform.forward * velocidade * dt;

            Separar(dt);
            Colar();

            anim.SetFloat(P_VELOCIDADE, velocidade);
            anim.SetBool(P_COMENDO, estado == Estado.Pastando);
            anim.SetBool(P_DORMINDO, estado == Estado.Dormindo);
        }

        // ------------------------------------------------------------------- decisoes
        void Decidir()
        {
            float d = Random.value;

            if (d < chanceDormir)
            {
                estado = Estado.Dormindo;
                ateQuando = Time.time + Random.Range(tempoDormindo.x, tempoDormindo.y);
                alvoVelocidade = 0f;
                return;
            }
            d -= chanceDormir;

            if (d < chanceOlhar)
            {
                estado = Estado.Olhando;
                anim.SetTrigger(P_OLHAR);
                ateQuando = Time.time + Random.Range(3f, 5f);
                alvoVelocidade = 0f;
                return;
            }
            d -= chanceOlhar;

            if (d < chanceSacudir)
            {
                estado = Estado.Sacudindo;
                anim.SetTrigger(P_SACUDIR);
                ateQuando = Time.time + 2.4f;
                alvoVelocidade = 0f;
                return;
            }
            d -= chanceSacudir;

            if (d < chancePastar)
            {
                estado = Estado.Pastando;
                ateQuando = Time.time + Random.Range(tempoPastando.x, tempoPastando.y);
                alvoVelocidade = 0f;
                return;
            }

            // sobrou: ou fica de pe olhando o nada, ou caminha ate outro ponto do pasto
            if (Random.value < 0.3f)
            {
                estado = Estado.Parada;
                ateQuando = Time.time + Random.Range(tempoParada.x, tempoParada.y);
                alvoVelocidade = 0f;
                return;
            }

            if (EscolherDestino(out destino))
            {
                estado = Estado.Indo;
                alvoVelocidade = (Random.value < chanceTrote ? velocidadeTrote : velocidadeAndar) * escala;
                ateQuando = Time.time + 22f;   // trava de seguranca se o caminho travar
            }
            else
            {
                estado = Estado.Pastando;
                ateQuando = Time.time + Random.Range(tempoPastando.x, tempoPastando.y);
            }
        }

        /// <summary>
        /// Sorteia um ponto dentro do territorio e recusa o que tiver desnivel demais. Oito
        /// tentativas: se nenhuma serve, ela fica onde esta e pasta. E' isto que a mantem fora
        /// do lago sem eu precisar saber a altura da agua.
        /// </summary>
        bool EscolherDestino(out Vector3 ponto)
        {
            float alturaCasa = AlturaDoChao(casa);
            for (int i = 0; i < 8; i++)
            {
                var c = Random.insideUnitCircle * raio;
                var p = new Vector3(casa.x + c.x, 0f, casa.z + c.y);
                if (Mathf.Abs(AlturaDoChao(p) - alturaCasa) <= desnivelMaximo)
                {
                    p.y = transform.position.y;
                    ponto = p;
                    return true;
                }
            }
            ponto = transform.position;
            return false;
        }

        bool Assustada()
        {
            if (fugirDe == null) return false;
            var d = fugirDe.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude < distanciaDeSusto * distanciaDeSusto;
        }

        void ComecarFuga()
        {
            estado = Estado.Fugindo;
            var fuga = transform.position - fugirDe.position;
            fuga.y = 0f;
            if (fuga.sqrMagnitude < 0.01f) fuga = transform.forward;

            var p = transform.position + fuga.normalized * Random.Range(5f, 9f);

            // foge do susto, mas nunca pra fora do pasto
            var doCentro = p - casa;
            doCentro.y = 0f;
            if (doCentro.magnitude > raio * 1.6f) p = casa + doCentro.normalized * raio * 1.6f;

            p.y = transform.position.y;
            destino = p;
            alvoVelocidade = velocidadeCorrer * escala;
            ateQuando = Time.time + tempoFugindo;
        }

        // ---------------------------------------------------------------------- corpo
        void Virar(Vector3 dir, float dt)
        {
            var alvo = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, giroPorSegundo * dt);
        }

        void Separar(float dt)
        {
            if (separacao <= 0f) return;
            Vector3 empurrao = Vector3.zero;
            foreach (var o in Todas)
            {
                if (o == this || o == null) continue;
                var d = transform.position - o.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                float perto = separacao * escala;
                if (m > 0.001f && m < perto) empurrao += d / m * (perto - m);
            }
            if (empurrao != Vector3.zero) transform.position += empurrao * dt * 1.5f;
        }

        /// <summary>
        /// Escala tirada de medida real, nao de olho: pega o comprimento da malha em pose de
        /// bind e encolhe ate bater <see cref="comprimentoReal"/>. A velocidade acompanha,
        /// porque a passada do clipe encolhe junto com o corpo — se so a escala mudasse, o
        /// casco passaria a deslizar na mesma hora.
        /// </summary>
        void AplicarEscalaReal()
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) return;

            var tam = smr.sharedMesh.bounds.size;
            var s = smr.transform.lossyScale;
            float compAgora = Mathf.Max(Mathf.Abs(tam.x * s.x), Mathf.Abs(tam.z * s.z));
            float raizAtual = Mathf.Max(0.0001f, transform.lossyScale.x);
            float compNaEscala1 = compAgora / raizAtual;
            if (compNaEscala1 < 0.01f) return;

            float k = comprimentoReal / compNaEscala1;
            transform.localScale = Vector3.one * k;
        }

        float AlturaDoChao(Vector3 p)
        {
            if (chao == null) return p.y;
            return chao.SampleHeight(p) + chao.transform.position.y;
        }

        void Colar()
        {
            if (chao == null) return;
            var p = transform.position;
            p.y = AlturaDoChao(p) + alturaRelativa;
            transform.position = p;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.4f, 0.7f);
            Gizmos.DrawWireSphere(Application.isPlaying ? casa : transform.position, raio);
        }
    }
}
