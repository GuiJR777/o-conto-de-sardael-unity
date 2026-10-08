using System.Collections;
using UnityEngine;

namespace Sardael
{
    [DefaultExecutionOrder(-50)]
    public sealed class DefesaDoHeroi : MonoBehaviour
    {
        [SerializeField] EntradaDeCombate entrada;
        [SerializeField] CombateDoHeroi combate;
        [SerializeField] MovimentoDoHeroi movimento;
        [SerializeField] Animator animator;
        [SerializeField] SistemaDeFlow flow;
        [SerializeField] DiretorDeCombate diretor;
        [SerializeField, Min(0.1f)] float janelaDeParry = 0.6f;
        [SerializeField, Min(0.1f)] float janelaDeContraAtaque = 0.9f;
        [SerializeField, Min(0f)] float danoDoContraAtaque = 4f;
        [SerializeField, Min(0f)] float poiseDoContraAtaque = 100f;
        [SerializeField, Min(0f)] float flowDoAparo = 8f;
        [SerializeField, Min(0.5f)] float alcanceDoParry = 3f;
        [SerializeField, Range(30f, 360f)] float anguloDoParry = 220f;

        AlvoDeCombate alvoDoContraAtaque;
        AlvoDeCombate alvoDoBloqueio;
        float contraAtaqueAte;
        bool bloqueioAplicado;
        bool cancelado;
        readonly JanelaDeParry estadoDoParry = new JanelaDeParry();

        static readonly int Bloqueando = Animator.StringToHash("bloqueando");
        static readonly int Aparar = Animator.StringToHash("aparar");
        static readonly int ContraAtacar = Animator.StringToHash("contraAtacar");

        public bool BloqueandoAgora => !cancelado && entrada != null && entrada.Bloqueando;
        public bool ContraAtaqueDisponivel => alvoDoContraAtaque != null &&
            alvoDoContraAtaque.Valido && Time.time <= contraAtaqueAte;
        public float DuracaoDaJanelaDeParry => janelaDeParry;
        public AlvoDeCombate AlvoDoBloqueio => alvoDoBloqueio;
        public int Aparos { get; private set; }
        public int ContraAtaques { get; private set; }

        public void Configurar(
            EntradaDeCombate novaEntrada,
            CombateDoHeroi novoCombate,
            MovimentoDoHeroi novoMovimento,
            Animator novoAnimator,
            SistemaDeFlow novoFlow,
            DiretorDeCombate novoDiretor = null,
            float novaJanelaDeParry = 0.6f)
        {
            entrada = novaEntrada;
            combate = novoCombate;
            movimento = novoMovimento;
            animator = novoAnimator;
            flow = novoFlow;
            diretor = novoDiretor;
            janelaDeParry = Mathf.Max(0.1f, novaJanelaDeParry);
        }

        void Awake()
        {
            if (entrada == null) entrada = GetComponent<EntradaDeCombate>();
            if (combate == null) combate = GetComponent<CombateDoHeroi>();
            if (movimento == null) movimento = GetComponent<MovimentoDoHeroi>();
            if (animator == null) animator = GetComponent<Animator>();
            if (diretor == null) diretor = FindAnyObjectByType<DiretorDeCombate>();
        }

        void Update()
        {
            bool querBloquear = BloqueandoAgora;
            if (querBloquear)
            {
                Vector2 movimentoLido = entrada == null ? Vector2.zero : entrada.Movimento;
                Vector3 direcaoDeEntrada = new Vector3(
                    movimentoLido.x, 0f, movimentoLido.y);
                alvoDoBloqueio = combate == null
                    ? null
                    : combate.EscolherAlvoDirecional(direcaoDeEntrada, alvoDoBloqueio);

                Vector3 direcao = direcaoDeEntrada.sqrMagnitude >= 0.04f
                    ? direcaoDeEntrada
                    : transform.forward;
                if (alvoDoBloqueio != null && alvoDoBloqueio.Valido)
                {
                    direcao = alvoDoBloqueio.transform.position - transform.position;
                    direcao.y = 0f;
                    movimento?.DefinirAlvoContextualDeCombate(alvoDoBloqueio.transform);
                }
                else alvoDoBloqueio = null;

                // Bloquear cancela o ataque mesmo sem haver um alvo selecionado.
                // Quando existe, o alvo direcional orienta o personagem como nos ataques.
                bool cancelouAtaque = !bloqueioAplicado && combate != null &&
                    combate.CancelarParaDefesa(direcao);
                if (!cancelouAtaque && direcao.sqrMagnitude > 0.0001f)
                    movimento?.DefinirDirecaoDeCombate(direcao);
            }
            if (querBloquear != bloqueioAplicado)
            {
                bloqueioAplicado = querBloquear;
                if (querBloquear) estadoDoParry.Abrir(Time.time, janelaDeParry);
                else LimparAlvoEJanelaDeBloqueio();
                movimento?.Travar(this, querBloquear);
                if (animator != null) animator.SetBool(Bloqueando, querBloquear);
            }

            if (alvoDoContraAtaque != null && Time.time > contraAtaqueAte)
                alvoDoContraAtaque = null;

            if (ContraAtaqueDisponivel && entrada != null && entrada.ConsumirAtaque())
                ExecutarContraAtaque();
        }

        public bool TentarBloquear(MotorDeCombateDoInimigo atacante)
        {
            if (!BloqueandoAgora || atacante == null || atacante.Alvo == null || movimento == null)
                return false;

            Vector3 direcao = atacante.transform.position - transform.position;
            direcao.y = 0f;
            float distancia = direcao.magnitude;
            if (distancia > alcanceDoParry || distancia <= 0.001f) return false;
            float angulo = Vector3.Angle(transform.forward, direcao / distancia);
            if (angulo > anguloDoParry * 0.5f) return false;

            movimento.DefinirDirecaoDeCombate(direcao);

            if (!estadoDoParry.TentarConsumir(Time.time)) return true;

            alvoDoContraAtaque = atacante.Alvo;
            contraAtaqueAte = Time.time + janelaDeContraAtaque;
            Aparos++;
            flow?.RegistrarParry(flowDoAparo);
            if (animator != null) animator.SetTrigger(Aparar);
            atacante.Alvo.ReceberAparo();
            return true;
        }

        public void Cancelar()
        {
            cancelado = true;
            alvoDoContraAtaque = null;
            LimparAlvoEJanelaDeBloqueio();
            bloqueioAplicado = false;
            movimento?.Travar(this, false);
            if (animator != null) animator.SetBool(Bloqueando, false);
        }

        public void Reativar()
        {
            cancelado = false;
            alvoDoContraAtaque = null;
            LimparAlvoEJanelaDeBloqueio();
            bloqueioAplicado = false;
            movimento?.Travar(this, false);
            if (animator != null) animator.SetBool(Bloqueando, false);
        }

        void ExecutarContraAtaque()
        {
            var alvo = alvoDoContraAtaque;
            alvoDoContraAtaque = null;
            if (alvo == null || !alvo.Valido) return;
            ContraAtaques++;
            Vector3 direcao = alvo.transform.position - transform.position;
            direcao.y = 0f;
            movimento?.DefinirDirecaoDeCombate(direcao);
            if (animator != null) animator.SetTrigger(ContraAtacar);
            alvo.ReceberContraAtaque(danoDoContraAtaque, poiseDoContraAtaque, transform);
            StartCoroutine(TravarContraAtaque(
                0.55f / MovimentoDoHeroi.VELOCIDADE_DAS_ANIMACOES));
        }

        IEnumerator TravarContraAtaque(float tempo)
        {
            movimento?.Travar(this, true);
            yield return new WaitForSeconds(tempo);
            if (!BloqueandoAgora) movimento?.Travar(this, false);
        }

        void LimparAlvoEJanelaDeBloqueio()
        {
            estadoDoParry.Fechar();
            alvoDoBloqueio = null;
            movimento?.DefinirAlvoContextualDeCombate(null);
        }

        void OnDisable() => Cancelar();
    }
}
