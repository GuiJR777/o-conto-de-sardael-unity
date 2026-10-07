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
        Vector3 direcaoEscolhida = Vector3.right;
        float lungePercorrido;
        bool impactoResolvido;
        readonly string[] ultimosAlvosPorElo = new string[5];

        public AlvoDeCombate AlvoAtual => alvoAtual;
        public int EloAtual => eloAtual;
        public int LadoEscolhido => ladoEscolhido;
        public Vector3 DirecaoEscolhida => direcaoEscolhida;
        public float DistanciaDoAlvo => alvoAtual == null
            ? -1f
            : RegistroDeCombate.DistanciaPlanar(alvoAtual.transform.position, transform.position);
        public bool EmAtaque => eloAtual > 0 || eloPendente > 0 || EloDoEstadoAtual() > 0;
        public int CancelamentosDefensivos { get; private set; }
        public string UltimoAlvoDoElo(int elo) => elo >= 1 && elo < ultimosAlvosPorElo.Length
            ? (string.IsNullOrEmpty(ultimosAlvosPorElo[elo]) ? "-" : ultimosAlvosPorElo[elo])
            : "-";

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
            AtualizarOrientacao();
        }

        void SolicitarAtaque()
        {
            if (animator == null || movimento == null || movimento.Travado) return;

            int eloNoAnimator = EloDoEstadoAtual();
            int proximoElo = eloNoAnimator <= 0 ? 1 : eloNoAnimator + 1;
            if (proximoElo > ataques.Length || Definicao(proximoElo) == null) return;
            if (eloPendente == proximoElo) return;

            Vector2 movimentoLido = entrada == null ? Vector2.zero : entrada.Movimento;
            Vector3 direcaoDeEntrada = new Vector3(movimentoLido.x, 0f, movimentoLido.y);

            eloPendente = proximoElo;
            ataquePendente = Definicao(proximoElo);
            alvoPendente = EscolherAlvo(direcaoDeEntrada, alvoAtual);

            if (alvoPendente != null)
            {
                direcaoEscolhida = DirecaoPlanarAte(alvoPendente.transform.position);
                ladoEscolhido = direcaoEscolhida.x < 0f ? -1 : 1;
                if (eloNoAnimator <= 0) movimento.DefinirDirecaoDeCombate(direcaoEscolhida);
            }
            animator.SetTrigger(P_ATACAR);
        }

        void LimparComboEncerrado()
        {
            if (eloAtual <= 0 || EloDoEstadoAtual() > 0) return;
            eloAtual = 0;
            ataqueAtual = null;
            alvoAtual = null;
            movimento?.DefinirAlvoContextualDeCombate(null);
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
                Vector2 movimentoLido = entrada == null ? Vector2.zero : entrada.Movimento;
                alvoAtual = EscolherAlvo(
                    new Vector3(movimentoLido.x, 0f, movimentoLido.y), alvoAtual);
            }

            if (alvoAtual != null)
            {
                direcaoEscolhida = DirecaoPlanarAte(alvoAtual.transform.position);
                ladoEscolhido = direcaoEscolhida.x < 0f ? -1 : 1;
                movimento.DefinirDirecaoDeCombate(direcaoEscolhida);
                movimento.DefinirAlvoContextualDeCombate(alvoAtual.transform);
            }
            if (eloAtual >= 1 && eloAtual < ultimosAlvosPorElo.Length)
                ultimosAlvosPorElo[eloAtual] = alvoAtual == null ? "-" : alvoAtual.name;

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
            return CancelarParaDefesa(new Vector3(direcao, 0f, 0f));
        }

        public bool CancelarParaDefesa(Vector3 direcao)
        {
            direcao.y = 0f;
            if (!EmAtaque || direcao.sqrMagnitude <= 0.0001f || movimento == null) return false;

            eloAtual = 0;
            eloPendente = 0;
            ataqueAtual = null;
            ataquePendente = null;
            alvoAtual = null;
            alvoPendente = null;
            movimento.DefinirAlvoContextualDeCombate(null);
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

            Vector3 delta = alvo.transform.position - transform.position;
            delta.y = 0f;
            float distancia = delta.magnitude;
            if (distancia > ataque.alcanceMagnetismo || distancia <= ataque.distanciaDesejada) return;

            float maximoRestante = Mathf.Max(0f, ataque.alcanceMagnetismo - ataque.distanciaDesejada - lungePercorrido);
            float ateADistancia = distancia - ataque.distanciaDesejada;
            float passo = Mathf.Min(ataque.velocidadeAproximacao * Time.deltaTime, ateADistancia, maximoRestante);
            if (passo <= 0f) return;

            Vector3 direcao = delta / distancia;
            movimento.DefinirDirecaoDeCombate(direcao);
            if (movimento.AdicionarDeslocamentoDeCombate(direcao * passo)) lungePercorrido += passo;
        }

        void AtualizarOrientacao()
        {
            var alvo = alvoAtual != null && alvoAtual.Valido ? alvoAtual : alvoPendente;
            if (alvo == null || !alvo.Valido || movimento == null) return;
            direcaoEscolhida = DirecaoPlanarAte(alvo.transform.position);
            movimento.DefinirDirecaoDeCombate(direcaoEscolhida);
            movimento.DefinirAlvoContextualDeCombate(alvo.transform);
        }

        AlvoDeCombate EscolherAlvo(Vector3 direcaoDeEntrada, AlvoDeCombate anterior)
        {
            if (registro == null) return null;
            Vector3 frente = movimento != null && movimento.EmModoCombate
                ? transform.forward
                : new Vector3(movimento == null ? 1f : movimento.Olhando, 0f, 0f);
            if (direcaoDeEntrada.sqrMagnitude < zonaMortaDaDirecao * zonaMortaDaDirecao)
                direcaoDeEntrada = Vector3.zero;
            return registro.MelhorAlvoNaDirecao(
                transform.position, direcaoDeEntrada, frente, anterior);
        }

        Vector3 DirecaoPlanarAte(Vector3 destino)
        {
            Vector3 direcao = destino - transform.position;
            direcao.y = 0f;
            return direcao.sqrMagnitude <= 0.0001f ? transform.forward : direcao.normalized;
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
