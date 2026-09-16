using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Prende um objeto a um osso de outro rig sem herdar a escala dele.
    ///
    /// Pendurar o cavaleiro direto como filho do osso parece o caminho obvio, mas o rig do
    /// cavalo carrega escala 18x NO OSSO e a animacao mexe nessa escala quadro a quadro.
    /// O filho entao muda de tamanho e o mesmo offset local vira distancias diferentes em
    /// cada quadro - o cavaleiro afunda no lombo numa pose e flutua na seguinte.
    ///
    /// Aqui o objeto fica fora da hierarquia do rig e so' copia POSICAO do osso (que e' onde
    /// mora o balanco da sela) e DIRECAO do corpo do cavalo (que e' estavel). Escala nao entra.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ColadoNoOsso : MonoBehaviour
    {
        [Tooltip("Osso que da' a posicao da sela, normalmente Back_Back.")]
        public Transform osso;
        [Tooltip("Corpo do animal, que da' a direcao. Sem ele o osso manda na direcao tambem.")]
        public Transform referencia;
        [Tooltip("Deslocamento em METROS a partir do osso, no referencial do corpo (X frente, Y cima, Z lado).")]
        public Vector3 deslocamento;
        [Tooltip("Giro extra em relacao ao corpo.")]
        public Vector3 giro;

        void LateUpdate()
        {
            if (osso == null) return;
            var baseRot = referencia != null ? referencia.rotation : osso.rotation;
            transform.position = osso.position + baseRot * deslocamento;
            transform.rotation = baseRot * Quaternion.Euler(giro);
        }
    }
}
