using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Gira uma roda na medida exata do chao que o veiculo percorreu, sem patinar.
    ///
    /// Nao recebe velocidade de fora: mede quanto o proprio objeto andou desde o quadro
    /// anterior e converte em angulo pelo raio. Assim a roda para sozinha quando a carroca
    /// para, acompanha se a velocidade do trilho mudar, e nao precisa ser reajustada.
    ///
    /// O trilho volta pro inicio de um quadro pro outro quando fecha a passagem. Esse pulo
    /// nao e' chao percorrido - por isso o salto e' descartado acima de um limite.
    /// </summary>
    public class RodaQueGira : MonoBehaviour
    {
        [Tooltip("Raio da roda em metros, no mundo.")]
        public float raio = 0.69f;
        [Tooltip("Eixo da roda, em coordenadas locais dela.")]
        public Vector3 eixo = Vector3.forward;
        [Tooltip("Para que lado o veiculo aponta, em coordenadas locais do veiculo.")]
        public Vector3 frenteDoVeiculo = Vector3.right;
        [Tooltip("Marque se a roda girar ao contrario.")]
        public bool inverter = false;
        [Tooltip("Deslocamento num unico quadro acima disto e' o trilho reiniciando, nao rolagem.")]
        public float saltoMaximo = 2f;

        Vector3 anterior;
        bool temAnterior;

        void OnEnable() { temAnterior = false; }

        void LateUpdate()
        {
            var agora = transform.position;
            if (!temAnterior) { anterior = agora; temAnterior = true; return; }

            var d = agora - anterior;
            anterior = agora;
            if (d.magnitude > saltoMaximo) return;          // reinicio do trilho: nao rolou nada

            var veiculo = transform.parent != null ? transform.parent : transform;
            float avanco = Vector3.Dot(d, veiculo.TransformDirection(frenteDoVeiculo).normalized);
            if (raio < 0.01f || Mathf.Abs(avanco) < 1e-6f) return;

            float graus = avanco / raio * Mathf.Rad2Deg;
            transform.Rotate(eixo, inverter ? graus : -graus, Space.Self);
        }
    }
}
