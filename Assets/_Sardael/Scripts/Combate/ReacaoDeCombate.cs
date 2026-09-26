using UnityEngine;

namespace Sardael
{
    public sealed class ReacaoDeCombate : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] string estadoAoReceberGolpe = "Levar";
        [SerializeField] string estadoAoReceberGolpeForte = "LevarForte";
        [SerializeField] string estadoAtordoado = "Atordoado";
        [SerializeField] string estadoDeMorte = "Morte";

        public int ReacoesTocadas { get; private set; }
        public string UltimoEstado { get; private set; } = "-";

        public void Configurar(Animator alvo) => animator = alvo;

        public void Tocar(DefinicaoDeAtaque ataque)
        {
            TocarEstado(estadoAoReceberGolpe);
        }

        public void TocarColisao(bool derrubado)
        {
            TocarEstado(derrubado ? estadoAoReceberGolpeForte : estadoAoReceberGolpe);
        }

        public void TocarAparo() => TocarEstado(estadoAoReceberGolpe);

        public void TocarAtordoado() => TocarEstado(estadoAtordoado);

        public void TocarMorte() => TocarEstado(estadoDeMorte);

        void TocarEstado(string estado)
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) return;
            int hash = Animator.StringToHash(estado);
            if (!animator.HasState(0, hash)) return;
            animator.Play(hash, 0, 0f);
            ReacoesTocadas++;
            UltimoEstado = estado;
        }
    }
}
