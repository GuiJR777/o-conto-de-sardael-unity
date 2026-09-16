using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Move o objeto em linha reta de A ate B e recomeca, em repeticao.
    ///
    /// Serve para vida de cenario que so' precisa atravessar o quadro: um grupo de cavaleiros
    /// passando por Halu, uma carroca subindo a estrada. Nao e' IA nem navegacao - e' um trilho.
    /// Quem anda junto (cavaleiro em cima do cavalo) deve ser filho do objeto que tem este script.
    ///
    /// O atraso existe para escalonar um grupo: o segundo cavalo entra 0,8 s depois do primeiro,
    /// senao os tres saem colados como um bloco so'.
    ///
    /// Cuidado com o olharParaFrente: ele gira ESTE objeto na direcao da marcha. Se as pecas
    /// filhas ja' foram posicionadas a mao olhando para a direcao certa, o giro entra por cima
    /// e a formacao inteira roda junto - a fila vira profundidade e todo mundo anda de lado.
    /// Nesse caso deixe desligado.
    /// </summary>
    public class PassagemEmLoop : MonoBehaviour
    {
        public Vector3 de;
        public Vector3 ate;
        [Tooltip("Metros por segundo.")]
        public float velocidade = 4f;
        [Tooltip("Segundos parado no inicio, antes da primeira passagem.")]
        public float atraso = 0f;
        [Tooltip("Segundos de espera fora de cena antes de repetir.")]
        public float esperaEntrePassagens = 3f;
        [Tooltip("Gira o objeto na direcao da marcha. Desligue se as pecas filhas ja' estao viradas a mao.")]
        public bool olharParaFrente = true;
        [Tooltip("Some da tela durante a espera. Sem isto o grupo aparece andando parado no ponto de partida e depois some pra tras num pulo.")]
        public bool esconderNaEspera = true;

        float t;
        float distancia;
        Renderer[] pinturas;
        bool visivel = true;

        void OnEnable()
        {
            pinturas = GetComponentsInChildren<Renderer>(true);
            visivel = true;
            t = -atraso;
            distancia = Vector3.Distance(de, ate);
            Posicionar();
        }

        void OnDisable() { Mostrar(true); }

        void Update()
        {
            t += Time.deltaTime;
            Posicionar();
        }

        void Mostrar(bool ligado)
        {
            if (visivel == ligado || pinturas == null) return;
            visivel = ligado;
            foreach (var r in pinturas) if (r != null) r.enabled = ligado;
        }

        void Posicionar()
        {
            if (distancia < 0.01f) return;
            float duracao = distancia / Mathf.Max(velocidade, 0.01f);
            float ciclo = duracao + esperaEntrePassagens;

            float local = Mathf.Repeat(t, ciclo);
            if (local < 0f) local += ciclo;

            bool andando = local <= duracao;
            // durante a espera, fica parado no ponto de partida
            float f = andando ? local / duracao : 0f;

            transform.position = Vector3.Lerp(de, ate, f);
            if (olharParaFrente)
            {
                var dir = ate - de;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
            }

            // o pulo de volta pro inicio nao pode ser visto: some enquanto espera
            Mostrar(!esconderNaEspera || andando);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(de, ate);
            Gizmos.DrawWireSphere(de, 0.5f);
            Gizmos.DrawWireSphere(ate, 0.5f);
        }
    }
}
