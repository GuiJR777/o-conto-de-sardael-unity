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
        [SerializeField, Min(0.1f)] float janelaDeContraAtaque = 0.9f;
        [SerializeField, Min(0f)] float danoDoContraAtaque = 4f;
        [SerializeField, Min(0f)] float poiseDoContraAtaque = 100f;
        [SerializeField, Min(0f)] float flowDoAparo = 8f;

        AlvoDeCombate alvoDoContraAtaque;
        float contraAtaqueAte;
        bool bloqueioAplicado;
        bool cancelado;

        static readonly int Bloqueando = Animator.StringToHash("bloqueando");
        static readonly int Aparar = Animator.StringToHash("aparar");
        static readonly int ContraAtacar = Animator.StringToHash("contraAtacar");

        public bool BloqueandoAgora => !cancelado && entrada != null && entrada.Bloqueando;
        public bool ContraAtaqueDisponivel => alvoDoContraAtaque != null &&
            alvoDoContraAtaque.Valido && Time.time <= contraAtaqueAte;
        public int Aparos { get; private set; }
        public int ContraAtaques { get; private set; }

        public void Configurar(
            EntradaDeCombate novaEntrada,
            CombateDoHeroi novoCombate,
            MovimentoDoHeroi novoMovimento,
            Animator novoAnimator,
            SistemaDeFlow novoFlow)
        {
            entrada = novaEntrada;
            combate = novoCombate;
            movimento = novoMovimento;
            animator = novoAnimator;
            flow = novoFlow;
        }

        void Awake()
        {
            if (entrada == null) entrada = GetComponent<EntradaDeCombate>();
            if (combate == null) combate = GetComponent<CombateDoHeroi>();
            if (movimento == null) movimento = GetComponent<MovimentoDoHeroi>();
            if (animator == null) animator = GetComponent<Animator>();
        }

        void Update()
        {
            bool querBloquear = BloqueandoAgora;
            if (querBloquear && !bloqueioAplicado)
            {
                float direcao = entrada == null ? 0f : entrada.MoveX;
                if (Mathf.Abs(direcao) > 0.2f)
                {
                    int lado = direcao > 0f ? 1 : -1;
                    if (combate == null || !combate.CancelarParaDefesa(lado))
                        movimento?.DefinirDirecaoDeCombate(lado);
                }
            }
            if (querBloquear != bloqueioAplicado)
            {
                bloqueioAplicado = querBloquear;
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

            float delta = atacante.transform.position.x - transform.position.x;
            int lado = delta >= 0f ? 1 : -1;
            if (lado != movimento.Olhando) return false;

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
            bloqueioAplicado = false;
            movimento?.Travar(this, false);
            if (animator != null) animator.SetBool(Bloqueando, false);
        }

        public void Reativar()
        {
            cancelado = false;
            alvoDoContraAtaque = null;
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
            movimento?.DefinirDirecaoDeCombate(alvo.transform.position.x >= transform.position.x ? 1 : -1);
            if (animator != null) animator.SetTrigger(ContraAtacar);
            alvo.ReceberContraAtaque(danoDoContraAtaque, poiseDoContraAtaque, transform);
            StartCoroutine(TravarContraAtaque(0.55f));
        }

        IEnumerator TravarContraAtaque(float tempo)
        {
            movimento?.Travar(this, true);
            yield return new WaitForSeconds(tempo);
            if (!BloqueandoAgora) movimento?.Travar(this, false);
        }

        void OnDisable() => Cancelar();
    }
}
