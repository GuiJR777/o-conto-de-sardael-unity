using UnityEngine;

namespace Sardael
{
    public sealed class ReacaoDeCombate : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] string estadoAoReceberGolpe = "Levar";

        public void Configurar(Animator alvo) => animator = alvo;

        public void Tocar(DefinicaoDeAtaque ataque)
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator != null && animator.HasState(0, Animator.StringToHash(estadoAoReceberGolpe)))
                animator.Play(estadoAoReceberGolpe, 0, 0f);
        }
    }
}
