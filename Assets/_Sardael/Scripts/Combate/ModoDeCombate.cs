using UnityEngine;

namespace Sardael
{
    public enum EstadoDoEncontro
    {
        Exploracao,
        Combate,
        RetornandoALinha,
        RestaurandoCamera
    }

    [DefaultExecutionOrder(-90)]
    public sealed class ModoDeCombate : MonoBehaviour
    {
        [SerializeField] MovimentoDoHeroi movimento;
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] DiretorDeCombate diretor;
        [SerializeField] CameraDeCombate cameraDeCombate;

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
            CameraDeCombate novaCamera)
        {
            movimento = novoMovimento;
            registro = novoRegistro;
            diretor = novoDiretor;
            cameraDeCombate = novaCamera;
        }

        void Start()
        {
            if (movimento == null) movimento = FindAnyObjectByType<MovimentoDoHeroi>();
            if (registro == null) registro = GetComponent<RegistroDeCombate>();
            if (diretor == null) diretor = GetComponent<DiretorDeCombate>();
            zOriginal = movimento == null ? 0f : movimento.transform.position.z;
            movimento?.EntrarEmExploracao(zOriginal);
            diretor?.DefinirAtivo(false);
            cameraDeCombate?.ForcarLateral();
            Estado = EstadoDoEncontro.Exploracao;
        }

        void Update()
        {
            if (Estado == EstadoDoEncontro.Combate)
            {
                bool terminou = encontroAtual != null
                    ? encontroAtual.TodosMortos
                    : registro != null && registro.Quantidade == 0;
                if (terminou) IniciarSaida();
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
            movimento?.IniciarRetornoALinha(zOriginal);
            Estado = EstadoDoEncontro.RetornandoALinha;
        }
    }
}
