using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Congela um Animator no ultimo quadro de um clipe. E' assim que Sardael entra na cena de
    /// morte ja' caido, sem eu ter que assar uma pose nova.
    ///
    /// Por que nao assar um clipe de um quadro so': o clipe de morte e' humanoide, e as curvas
    /// de um clipe humanoide importado sao musculares, nao de osso. Achatar elas na mao da'
    /// resultado errado. Tocar o clipe no tempo 1,0 com velocidade zero entrega a pose exata
    /// que o animador do pacote desenhou.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SegurarUltimoQuadro : MonoBehaviour
    {
        [Tooltip("Nome do estado no controller.")]
        public string estado = "Deitado";
        [Tooltip("Onde parar. 1 = ultimo quadro.")]
        [Range(0f, 1f)] public float tempo = 1f;

        void Start()
        {
            var anim = GetComponent<Animator>();
            if (anim.runtimeAnimatorController == null)
            {
                Debug.LogWarning("SegurarUltimoQuadro: sem controller em " + name);
                return;
            }
            anim.Play(estado, 0, tempo);
            // Update() forca o Animator a avaliar agora; sem isto o corpo aparece em pose de
            // bind no primeiro quadro visivel e o jogador ve o boneco em T por um piscar
            anim.Update(0f);
            anim.speed = 0f;
        }
    }
}
