using UnityEngine;

namespace Sardael
{
    public enum EstadoDoEncontro
    {
        Exploracao,
        Combate,
        RetornandoALinha,
        RestaurandoCamera,
        Derrotado
    }

    [DefaultExecutionOrder(-90)]
    public sealed class ModoDeCombate : MonoBehaviour
    {
        [SerializeField] MovimentoDoHeroi movimento;
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] DiretorDeCombate diretor;
        [SerializeField] CameraDeCombate cameraDeCombate;
        [SerializeField] CombateDoHeroi combateDoHeroi;
        [SerializeField] DefesaDoHeroi defesaDoHeroi;
        [SerializeField] SistemaDeExecucao execucao;
        [SerializeField] VidaDoHeroi vidaDoHeroi;

        EncontroDeCombate encontroAtual;
        float zOriginal;

        public EstadoDoEncontro Estado { get; private set; } = EstadoDoEncontro.Exploracao;
        public bool EmCombate => Estado == EstadoDoEncontro.Combate;
        public float ZOriginal => zOriginal;
        public EncontroDeCombate EncontroAtual => encontroAtual;

        public void Configurar(
            MovimentoDoHeroi novoMovimento,
            RegistroDeCombate novoRegistro,
            DiretorDeCombate novoDiretor,
            CameraDeCombate novaCamera,
            VidaDoHeroi novaVida = null,
            SistemaDeExecucao novaExecucao = null,
            CombateDoHeroi novoCombate = null,
            DefesaDoHeroi novaDefesa = null)
        {
            movimento = novoMovimento;
            registro = novoRegistro;
            diretor = novoDiretor;
            cameraDeCombate = novaCamera;
            vidaDoHeroi = novaVida;
            execucao = novaExecucao;
            combateDoHeroi = novoCombate;
            defesaDoHeroi = novaDefesa;
        }

        void Start()
        {
            if (movimento == null) movimento = FindAnyObjectByType<MovimentoDoHeroi>();
            if (registro == null) registro = GetComponent<RegistroDeCombate>();
            if (diretor == null) diretor = GetComponent<DiretorDeCombate>();
            if (movimento != null)
            {
                if (combateDoHeroi == null) combateDoHeroi = movimento.GetComponent<CombateDoHeroi>();
                if (defesaDoHeroi == null) defesaDoHeroi = movimento.GetComponent<DefesaDoHeroi>();
                if (execucao == null) execucao = movimento.GetComponent<SistemaDeExecucao>();
                if (vidaDoHeroi == null) vidaDoHeroi = movimento.GetComponent<VidaDoHeroi>();
            }
            zOriginal = movimento == null ? 0f : movimento.transform.position.z;
            movimento?.EntrarEmExploracao(zOriginal);
            diretor?.DefinirAtivo(false);
            cameraDeCombate?.ForcarLateral();
            DefinirSistemasDoHeroiAtivos(false);
            Estado = EstadoDoEncontro.Exploracao;
        }

        void Update()
        {
            if (Estado == EstadoDoEncontro.Combate)
            {
                if (vidaDoHeroi != null && !vidaDoHeroi.Vivo)
                {
                    InterromperPorDerrota();
                    return;
                }
                bool terminou = encontroAtual != null
                    ? encontroAtual.TodosMortos
                    : registro != null && registro.Quantidade == 0;
                if (terminou && (execucao == null || !execucao.EmExecucao)) IniciarSaida();
                return;
            }

            if (Estado == EstadoDoEncontro.RetornandoALinha &&
                movimento != null && movimento.RetornoConcluido)
            {
                movimento.EntrarEmExploracao(zOriginal);
                cameraDeCombate?.IniciarSaida();
                Estado = EstadoDoEncontro.RestaurandoCamera;
                return;
            }

            if (Estado == EstadoDoEncontro.RestaurandoCamera &&
                (cameraDeCombate == null || cameraDeCombate.SaidaConcluida))
            {
                DefinirSistemasDoHeroiAtivos(false);
                movimento?.Travar(this, false);
                encontroAtual?.MarcarConcluido();
                encontroAtual = null;
                Estado = EstadoDoEncontro.Exploracao;
            }
        }

        public bool IniciarEncontro(EncontroDeCombate encontro)
        {
            if (Estado != EstadoDoEncontro.Exploracao || encontro == null || encontro.Concluido)
                return false;
            encontroAtual = encontro;
            encontroAtual.MarcarIniciado();
            zOriginal = movimento == null ? 0f : movimento.transform.position.z;
            DefinirSistemasDoHeroiAtivos(true);
            movimento?.DefinirLimitesDeCombate(encontro.LimitesMundiais);
            movimento?.EntrarEmCombate();
            cameraDeCombate?.Ativar();
            diretor?.DefinirAtivo(true);
            Estado = EstadoDoEncontro.Combate;
            return true;
        }

        public void IniciarSaida()
        {
            if (Estado != EstadoDoEncontro.Combate) return;
            diretor?.DefinirAtivo(false);
            movimento?.Travar(this, true);
            movimento?.LimparLimitesDeCombate();
            movimento?.IniciarRetornoALinha(zOriginal);
            Estado = EstadoDoEncontro.RetornandoALinha;
        }

        void InterromperPorDerrota()
        {
            diretor?.DefinirAtivo(false);
            cameraDeCombate?.EncerrarZoomDeExecucao();
            movimento?.Travar(this, true);
            movimento?.LimparLimitesDeCombate();
            DefinirSistemasDoHeroiAtivos(false);
            encontroAtual?.MarcarDerrotado();
            Estado = EstadoDoEncontro.Derrotado;
        }

        void DefinirSistemasDoHeroiAtivos(bool ativos)
        {
            if (combateDoHeroi != null) combateDoHeroi.enabled = ativos;
            if (execucao != null) execucao.enabled = ativos;
            if (defesaDoHeroi != null)
            {
                defesaDoHeroi.enabled = ativos;
                if (ativos) defesaDoHeroi.Reativar();
                else defesaDoHeroi.Cancelar();
            }
        }
    }
}
