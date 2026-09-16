using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Decide QUANDO o clipe pode mover o orc.
    ///
    /// O defeito que isto conserta: eu liguei o root motion no orc pra ele ser empurrado pelo
    /// golpe, e ele passou a ser empurrado tambem pelo clipe PARADO — que tem deslocamento e
    /// roda em loop. Resultado: o orc saia andando sozinho entre um golpe e outro, o par se
    /// afastava, e quando o combo seguinte comecava ele ja' estava fora de distancia. Foi isso
    /// que virou "acertou 1, passou longe 8", com a distancia chegando a -2,44 m.
    ///
    /// Regra: so' a reacao pareada desloca. Guarda e ataque do orc sao no lugar.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CorpoDoOrc : MonoBehaviour
    {
        Animator anim;

        void Awake()
        {
            anim = GetComponent<Animator>();
            anim.applyRootMotion = true;
        }

        void OnAnimatorMove()
        {
            if (anim == null) return;
            var d = Duelo.Atual;
            if (d == null || !d.OrcEmReacao) return;

            // O Hit_Fw_RM anda pra TRAS no referencial de quem apanha (-1,65 m/s no eixo
            // da frente). Como o orc esta' encarando o Sardael, tras dele e' longe do Sardael:
            // aplicar cru ja' da o sentido certo. Nao ha' sinal pra corrigir.
            var p = transform.position;
            p.x += anim.deltaPosition.x;
            transform.position = p;
        }
    }
}
