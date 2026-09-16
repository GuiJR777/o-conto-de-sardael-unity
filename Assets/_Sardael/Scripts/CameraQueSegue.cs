using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// Camera de rolagem lateral 2.5D: acompanha Sardael em X, com amortecimento, e da' um
    /// passo a frente na direcao pra onde ele olha.
    ///
    /// O olhar a frente existe porque camera centrada no personagem mostra tanto chao atras
    /// quanto pela frente — e quem esta' correndo precisa ver o que vem, nao o que passou.
    /// Ele e' amortecido a parte, senao a virada de lado joga a imagem de um lado pro outro.
    ///
    /// O Z fica preso: a linha 2.5D nao tem profundidade pra seguir. A roda do mouse so'
    /// afasta e aproxima.
    ///
    /// O CEU anda junto. O domo do Synty e' uma malha, nao um cubemap: parado no mundo ele
    /// ficaria pra tras quando a camera andasse, e a neblina de Halu (linear, acaba em 340 m)
    /// apagaria ele se fosse grande o bastante pra cobrir o mapa. Colado na camera, dentro do
    /// comeco da neblina, ele aparece limpo e nunca sai do lugar.
    /// </summary>
    [DefaultExecutionOrder(500)]   // depois do movimento, senao a camera persegue a posicao velha
    public class CameraQueSegue : MonoBehaviour
    {
        [Header("Alvo")]
        public Transform alvo;
        public MovimentoDoHeroi heroi;
        [Tooltip("Altura do ponto que a camera mira, acima do pe.")]
        public float alturaDoOlhar = 1.15f;

        [Header("Enquadramento")]
        [Tooltip("Distancia no eixo Z. A roda do mouse mexe nisto.")]
        public float distancia = 10.5f;
        public float distanciaMinima = 5f;
        public float distanciaMaxima = 20f;
        public float passoDaRoda = 1.4f;
        public float altura = 3.6f;
        [Tooltip("Inclinacao pra baixo, em graus.")]
        public float inclinacao = 11f;

        [Header("Suavidade")]
        [Tooltip("Tempo pra alcancar o alvo. Maior = mais preguicosa.")]
        public float tempoDeResposta = 0.18f;
        [Tooltip("Quanto ela olha a frente, em metros, no sentido em que ele anda.")]
        public float olharAFrente = 2.2f;
        public float tempoDoOlhar = 0.5f;

        [Header("Ceu")]
        [Tooltip("O domo. Anda junto com a camera, sem girar.")]
        public Transform ceu;

        Vector3 velocidadeDaCamera;
        float frenteAtual, velocidadeDaFrente;
        float distanciaAlvo;

        void Start()
        {
            distanciaAlvo = distancia;
            if (alvo != null) transform.position = Onde();
            Mirar();
        }

        void LateUpdate()
        {
            LerRoda();
            distancia = Mathf.Lerp(distancia, distanciaAlvo, 1f - Mathf.Exp(-10f * Time.deltaTime));

            if (alvo != null)
            {
                float querFrente = heroi != null ? heroi.Olhando * olharAFrente : 0f;
                frenteAtual = Mathf.SmoothDamp(frenteAtual, querFrente, ref velocidadeDaFrente,
                                               Mathf.Max(0.01f, tempoDoOlhar));
                transform.position = Vector3.SmoothDamp(transform.position, Onde(),
                                                        ref velocidadeDaCamera,
                                                        Mathf.Max(0.01f, tempoDeResposta));
            }
            Mirar();

            // o ceu acompanha, mas nao gira: girar o domo junto faria a textura escorregar
            if (ceu != null) ceu.position = transform.position;
        }

        Vector3 Onde()
        {
            var p = alvo.position;
            return new Vector3(p.x + frenteAtual, p.y + altura, p.z - distancia);
        }

        void Mirar()
        {
            transform.rotation = Quaternion.Euler(inclinacao, 0f, 0f);
        }

        void LerRoda()
        {
            float roda = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) roda = Mouse.current.scroll.ReadValue().y;
#elif ENABLE_LEGACY_INPUT_MANAGER
            roda = Input.mouseScrollDelta.y * 120f;
#endif
            if (Mathf.Abs(roda) < 0.01f) return;
            distanciaAlvo = Mathf.Clamp(distanciaAlvo - Mathf.Sign(roda) * passoDaRoda,
                                        distanciaMinima, distanciaMaxima);
        }
    }
}
