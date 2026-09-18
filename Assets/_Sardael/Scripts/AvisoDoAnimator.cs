using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Quem avisa que um golpe comecou e' o ANIMATOR, nao o meu codigo.
    ///
    /// Antes eu perguntava todo quadro "ja' entrou no ataque? ja' saiu?", comparando nomes de
    /// estado. Isso erra: perde o quadro, dispara duas vezes, ou pega a transicao no meio. O
    /// Unity ja' tem o gancho certo — OnStateEnter — e ele e' exato.
    ///
    /// O aviso vai para o <see cref="Duelo"/> junto com QUEM o mandou, porque os dois lados
    /// (heroi e orc) usam este mesmo aviso.
    /// </summary>
    public class AvisoDoAnimator : StateMachineBehaviour
    {
        [Tooltip("Nome entregue ao Duelo quando este estado comecar.")]
        public string aviso;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int camada)
        {
            var combate = animator.GetComponentInParent<CombateDoHeroi>();
            if (combate != null && combate.AoComecarEstado(aviso)) return;
            if (Duelo.Atual != null) Duelo.Atual.AoComecarEstado(aviso, animator);
        }
    }
}
