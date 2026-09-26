using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Avisa no instante do impacto DENTRO do clipe — nao no comeco dele.
    ///
    /// O instante nao foi escolhido por mim: o construtor amostra o clipe quadro a quadro e
    /// acha onde a mao da arma chega mais longe pra frente. E' ali que a lamina encosta.
    /// </summary>
    public class AvisoNoImpacto : StateMachineBehaviour
    {
        [Tooltip("Fracao do clipe (0..1) onde a arma chega mais longe. MEDIDO pelo construtor.")]
        public float quando = 0.5f;
        public string aviso;

        bool jaAvisou;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int camada)
        {
            jaAvisou = false;
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo info, int camada)
        {
            if (jaAvisou || info.normalizedTime < quando) return;
            jaAvisou = true;
            var combate = animator.GetComponentInParent<CombateDoHeroi>();
            if (combate != null && combate.AoAvisoDeImpacto(aviso)) return;
            var inimigo = animator.GetComponentInParent<MotorDeCombateDoInimigo>();
            if (inimigo != null && inimigo.AoAvisoDeImpacto(aviso)) return;
            if (Duelo.Atual != null) Duelo.Atual.AoComecarEstado(aviso, animator);
        }
    }
}
