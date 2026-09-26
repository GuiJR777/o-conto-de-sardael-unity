using UnityEngine;

namespace Sardael
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(MovimentoDoHeroi))]
    [RequireComponent(typeof(EntradaDeCombate))]
    public sealed class CombateDoHeroi : MonoBehaviour
    {
        const string P_ATACAR = "atacar";

        [SerializeField] MovimentoDoHeroi movimento;
        [SerializeField] EntradaDeCombate entrada;
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] ResolvedorDeImpacto resolvedor;
        [SerializeField] Animator animator;
        [SerializeField] DefinicaoDeAtaque[] ataques = new DefinicaoDeAtaque[4];
        [SerializeField, Range(0f, 1f)] float zonaMortaDaDirecao = 0.2f;

        AlvoDeCombate alvoAtual;
        AlvoDeCombate alvoPendente;
        DefinicaoDeAtaque ataqueAtual;
        DefinicaoDeAtaque ataquePendente;
        int eloAtual;
        int eloPendente;
        int ladoEscolhido = 1;
        float lungePercorrido;
        bool impactoResolvido;

        public AlvoDeCombate AlvoAtual => alvoAtual;
        public int EloAtual => eloAtual;
        public int LadoEscolhido => ladoEscolhido;
        public float DistanciaDoAlvo => alvoAtual == null
            ? -1f
            : Mathf.Abs(alvoAtual.transform.position.x - transform.position.x);
        public bool EmAtaque => eloAtual > 0 || eloPendente > 0 || EloDoEstadoAtual() > 0;
        public int CancelamentosDefensivos { get; private set; }

        public void Configurar(
            MovimentoDoHeroi novoMovimento,
            EntradaDeCombate novaEntrada,
            RegistroDeCombate novoRegistro,
            ResolvedorDeImpacto novoResolvedor,
            Animator novoAnimator,
            DefinicaoDeAtaque[] novasDefinicoes)
        {
            movimento = novoMovimento;
            entrada = novaEntrada;
            registro = novoRegistro;
            resolvedor = novoResolvedor;
            animator = novoAnimator;
            ataques = novasDefinicoes;
        }

        void Awake()
        {
            if (movimento == null) movimento = GetComponent<MovimentoDoHeroi>();
            if (entrada == null) entrada = GetComponent<EntradaDeCombate>();
            if (animator == null) animator = GetComponent<Animator>();
        }

        void Update()
        {
            if (entrada != null && entrada.ConsumirAtaque()) SolicitarAtaque();
            LimparComboEncerrado();
            AtualizarAtaqueAtivo();
            AtualizarLunge();
        }

        void SolicitarAtaque()
        {
            if (animator == null || movimento == null || movimento.Travado) return;

            int eloNoAnimator = EloDoEstadoAtual();
            int proximoElo = eloNoAnimator <= 0 ? 1 : eloNoAnimator + 1;
            if (proximoElo > ataques.Length || Definicao(proximoElo) == null) return;
            if (eloPendente == proximoElo) return;

            float moveX = entrada == null ? 0f : entrada.MoveX;
            ladoEscolhido = Mathf.Abs(moveX) > zonaMortaDaDirecao
                ? (moveX > 0f ? 1 : -1)
                : movimento.Olhando;

            eloPendente = proximoElo;
            ataquePendente = Definicao(proximoElo);
            alvoPendente = registro == null
                ? null
                : registro.PrimeiroNoLado(transform.position, ladoEscolhido);

            if (eloNoAnimator <= 0) movimento.DefinirDirecaoDeCombate(ladoEscolhido);
            animator.SetTrigger(P_ATACAR);
        }

        void LimparComboEncerrado()
        {
            if (eloAtual <= 0 || EloDoEstadoAtual() > 0) return;
            eloAtual = 0;
            ataqueAtual = null;
            alvoAtual = null;
            impactoResolvido = false;
            lungePercorrido = 0f;
        }

        public bool AoComecarEstado(string aviso)
        {
            int elo = EloDoAviso(aviso);
            if (elo <= 0) return false;

            eloAtual = elo;
            ataqueAtual = Definicao(elo);
            if (eloPendente == elo)
            {
                alvoAtual = alvoPendente != null && alvoPendente.Valido ? alvoPendente : null;
                eloPendente = 0;
                ataquePendente = null;
                alvoPendente = null;
            }
            else
            {
                alvoAtual = registro == null
                    ? null
                    : registro.PrimeiroNoLado(transform.position, movimento.Olhando);
            }

            if (alvoAtual != null)
            {
                ladoEscolhido = alvoAtual.transform.position.x >= transform.position.x ? 1 : -1;
                movimento.DefinirDirecaoDeCombate(ladoEscolhido);
            }

            lungePercorrido = 0f;
            impactoResolvido = false;
            return true;
        }

        public bool AoAvisoDeImpacto(string aviso)
        {
            if (aviso != "impactoDoHeroi") return false;
            ResolverImpacto();
            return true;
        }

        public bool CancelarParaDefesa(int direcao)
        {
            if (!EmAtaque || direcao == 0 || movimento == null || direcao == movimento.Olhando)
                return false;

            eloAtual = 0;
            eloPendente = 0;
            ataqueAtual = null;
            ataquePendente = null;
            alvoAtual = null;
            alvoPendente = null;
            impactoResolvido = true;
            lungePercorrido = 0f;
            entrada?.CancelarAtaquesPendentes();
            movimento.CancelarDeslocamentoDeCombate();
            movimento.DefinirDirecaoDeCombate(direcao);
            if (animator != null)
            {
                animator.ResetTrigger(P_ATACAR);
                animator.CrossFade("Bloqueio", 0.03f, 0, 0f);
            }
            CancelamentosDefensivos++;
            return true;
        }

        void AtualizarAtaqueAtivo()
        {
            if (eloAtual <= 0 || ataqueAtual == null || impactoResolvido || animator == null) return;
            if (alvoAtual != null && !alvoAtual.Valido) alvoAtual = null;

            var estado = animator.GetCurrentAnimatorStateInfo(0);
            if (!estado.IsName("Golpe" + eloAtual)) return;
            if (estado.normalizedTime >= ataqueAtual.momentoDoImpacto) ResolverImpacto();
        }

        void ResolverImpacto()
        {
            if (impactoResolvido) return;
            impactoResolvido = true;
            resolvedor?.Resolver(ataqueAtual, transform, alvoAtual);
        }

        void AtualizarLunge()
        {
            if (eloAtual > 0 && impactoResolvido) return;
            var alvo = eloAtual > 0 ? alvoAtual : (eloPendente == 1 ? alvoPendente : null);
            var ataque = eloAtual > 0 ? ataqueAtual : (eloPendente == 1 ? ataquePendente : null);
            if (alvo == null || ataque == null || !alvo.Valido || movimento == null) return;

            float delta = alvo.transform.position.x - transform.position.x;
            float distancia = Mathf.Abs(delta);
            if (distancia > ataque.alcanceMagnetismo || distancia <= ataque.distanciaDesejada) return;

            float maximoRestante = Mathf.Max(0f, ataque.alcanceMagnetismo - ataque.distanciaDesejada - lungePercorrido);
            float ateADistancia = distancia - ataque.distanciaDesejada;
            float passo = Mathf.Min(ataque.velocidadeAproximacao * Time.deltaTime, ateADistancia, maximoRestante);
            if (passo <= 0f) return;

            int direcao = delta > 0f ? 1 : -1;
            movimento.DefinirDirecaoDeCombate(direcao);
            if (movimento.AdicionarDeslocamentoDeCombate(passo * direcao)) lungePercorrido += passo;
        }

        int EloDoEstadoAtual()
        {
            if (animator == null) return 0;
            if (animator.IsInTransition(0))
            {
                var proximo = animator.GetNextAnimatorStateInfo(0);
                for (int i = 1; i <= ataques.Length; i++) if (proximo.IsName("Golpe" + i)) return i;
            }
            var atual = animator.GetCurrentAnimatorStateInfo(0);
            for (int i = 1; i <= ataques.Length; i++) if (atual.IsName("Golpe" + i)) return i;
            return 0;
        }

        DefinicaoDeAtaque Definicao(int elo)
        {
            int indice = elo - 1;
            return ataques != null && indice >= 0 && indice < ataques.Length ? ataques[indice] : null;
        }

        static int EloDoAviso(string aviso)
        {
            if (string.IsNullOrEmpty(aviso) || !aviso.StartsWith("golpe")) return 0;
            return int.TryParse(aviso.Substring(5), out int elo) ? elo : 0;
        }

    }
}
