using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Mede, de dentro do jogo rodando, se um objeto salta de um quadro pro outro.
    ///
    /// Medir isso de fora com EditorApplication.Step() nao funciona: o Unity reclama de
    /// "PlayerLoop called recursively" e devolve zeros que parecem prova mas nao sao nada.
    /// Aqui o numero sai do proprio LateUpdate, no Play de verdade, e vai pro console.
    /// </summary>
    public class Vigia : MonoBehaviour
    {
        public string apelido = "cavalo";
        [Tooltip("Objeto que tem o Animator - e' onde root motion apareceria.")]
        public Transform objetoDoAnimator;
        [Tooltip("Raiz do cavalo.")]
        public Transform corpo;
        public Transform sela;
        public Transform cavaleiro;
        [Tooltip("Deslocamento num quadro acima disto e' considerado salto.")]
        public float limiteSalto = 0.20f;
        [Tooltip("Segundos entre resumos no console.")]
        public float intervaloResumo = 2f;

        Vector3 antAnim, antCorpo, antSela, antCav;
        bool primeiro = true;
        float t;
        int saltos;
        float maxAnim, maxCorpo, maxSela, maxCav;
        float distCavaleiroMin = 9999f, distCavaleiroMax = -9999f;

        void LateUpdate()
        {
            if (corpo == null) return;
            if (primeiro)
            {
                if (objetoDoAnimator != null) antAnim = objetoDoAnimator.localPosition;
                antCorpo = corpo.position;
                if (sela != null) antSela = sela.position;
                if (cavaleiro != null) antCav = cavaleiro.position;
                primeiro = false;
                return;
            }

            if (objetoDoAnimator != null)
            {
                float d = Vector3.Distance(objetoDoAnimator.localPosition, antAnim);
                if (d > maxAnim) maxAnim = d;
                antAnim = objetoDoAnimator.localPosition;
            }

            float dCorpo = Vector3.Distance(corpo.position, antCorpo);
            if (dCorpo > maxCorpo) maxCorpo = dCorpo;
            if (dCorpo > limiteSalto)
            {
                saltos++;
                Debug.Log("[VIGIA " + apelido + "] SALTO de " + dCorpo.ToString("F3") + " m em t=" + Time.time.ToString("F2")
                    + "s : " + antCorpo.ToString("F2") + " -> " + corpo.position.ToString("F2"));
            }
            antCorpo = corpo.position;

            if (sela != null)
            {
                float d = Vector3.Distance(sela.position, antSela);
                if (d > maxSela) maxSela = d;
                if (d > limiteSalto)
                    Debug.Log("[VIGIA " + apelido + "] SELA saltou " + d.ToString("F3") + " m em t=" + Time.time.ToString("F2") + "s");
                antSela = sela.position;
            }

            if (cavaleiro != null)
            {
                float d = Vector3.Distance(cavaleiro.position, antCav);
                if (d > maxCav) maxCav = d;
                if (d > limiteSalto)
                    Debug.Log("[VIGIA " + apelido + "] CAVALEIRO saltou " + d.ToString("F3") + " m em t=" + Time.time.ToString("F2") + "s");
                antCav = cavaleiro.position;
                float dist = Vector3.Distance(cavaleiro.position, corpo.position);
                if (dist < distCavaleiroMin) distCavaleiroMin = dist;
                if (dist > distCavaleiroMax) distCavaleiroMax = dist;
            }

            t += Time.deltaTime;
            if (t >= intervaloResumo)
            {
                t = 0f;
                Debug.Log("[VIGIA " + apelido + "] t=" + Time.time.ToString("F1") + "s"
                    + " | corpo em " + corpo.position.ToString("F2")
                    + " | maior passo do corpo num quadro " + maxCorpo.ToString("F4") + " m"
                    + " | objeto do Animator " + maxAnim.ToString("F5") + " m"
                    + " | sela " + maxSela.ToString("F4") + " m"
                    + " | cavaleiro " + maxCav.ToString("F4") + " m"
                    + " | cavaleiro<->corpo " + distCavaleiroMin.ToString("F3") + ".." + distCavaleiroMax.ToString("F3") + " m"
                    + " | saltos acima de " + limiteSalto.ToString("F2") + " m: " + saltos);
            }
        }
    }
}
