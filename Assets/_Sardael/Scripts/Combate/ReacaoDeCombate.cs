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

        AnimatorOverrideController sobrescrita;

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

        /// <summary>
        /// Toca o estado de morte. Se um clipe for passado, ele entra no lugar do clipe do
        /// estado so' nesta morte (via AnimatorOverrideController), sem mexer no controller.
        /// </summary>
        public void TocarMorte(AnimationClip clipe = null)
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) return;
            int hash = Animator.StringToHash(estadoDeMorte);
            if (!animator.HasState(0, hash)) return;

            if (clipe != null)
            {
                var atual = animator.runtimeAnimatorController;
                RuntimeAnimatorController baseDoAtual =
                    atual is AnimatorOverrideController herdeiro
                        ? herdeiro.runtimeAnimatorController
                        : atual;
                if (sobrescrita == null || sobrescrita.runtimeAnimatorController != baseDoAtual)
                {
                    sobrescrita = new AnimatorOverrideController(baseDoAtual);
                    sobrescrita.name = name + "_Morte";
                }
                sobrescrita[estadoDeMorte] = clipe;
                if (animator.runtimeAnimatorController != sobrescrita)
                {
                    animator.runtimeAnimatorController = sobrescrita;
                    animator.Rebind();
                    animator.Update(0f);
                }
            }

            animator.Play(hash, 0, 0f);
            ReacoesTocadas++;
            UltimoEstado = estadoDeMorte;
        }

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
