using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Camera de jogo 2.5D: segue o alvo so' no eixo X, com Y e Z fixos. Nao gira nunca —
    /// girar quebra a leitura lateral do combate, que e' onde a distancia entre os dois
    /// corpos precisa ser lida de relance.
    ///
    /// Os valores que ja' estao aqui foram medidos na Arena_Teste: a 10,5 m de distancia
    /// com campo de visao de 42 graus, a faixa visivel na linha de jogo tem 8,1 m de altura,
    /// entao um personagem de 1,8 m ocupa 22% da tela. E' a proporcao de beat-em-up.
    /// </summary>
    public class CameraLateral : MonoBehaviour
    {
        [Header("Alvo")]
        public Transform alvo;
        [Tooltip("Se vazio, procura por um objeto com ControladorSardael ao iniciar.")]
        public bool acharSozinho = true;

        [Header("Posicao")]
        public float altura = 3.6f;
        public float distancia = 10.5f;
        public float inclinacao = 11f;
        [Tooltip("Quanto a camera se adianta na direcao em que ele anda. Da' visao do que vem.")]
        public float olharAFrente = 1.6f;

        [Header("Suavidade")]
        [Tooltip("Segundos pra alcancar o alvo. 0 = gruda, 0.25 = macio.")]
        public float suavidade = 0.18f;
        [Tooltip("Segundos pra suavizar a velocidade medida do alvo. Alto = a camera demora " +
                 "pra se adiantar e pra voltar; baixo = ela reage a cada passo.")]
        public float suavidadeDoLado = 0.35f;
        [Tooltip("Velocidade em que o adiantamento chega ao maximo. Igual a velocidade de " +
                 "corrida: andando ele se adianta pela metade, parado nao se adianta nada.")]
        public float velocidadeDeReferencia = 4.5f;

        [Header("Limites (pra camera nao sair da fase)")]
        public bool usarLimites = true;
        public float xMinimo = -26f;
        public float xMaximo = 30f;

        float xAtual, velocidade;
        float xAnterior, velocidadeMedida, aceleracaoMedida;
        ControladorSardael controlador;

        void Start()
        {
            if (alvo == null && acharSozinho)
            {
                controlador = FindAnyObjectByType<ControladorSardael>();
                if (controlador != null) alvo = controlador.transform;
            }
            if (alvo != null) xAtual = alvo.position.x;
            Posicionar(true);
        }

        void LateUpdate()
        {
            Posicionar(false);
        }

        void Posicionar(bool imediato)
        {
            if (alvo == null) return;

            // O ADIANTAMENTO SEGUE PRA ONDE ELE ANDA, NAO PRA ONDE OLHA.
            //
            // Era aqui o "teleporte ao mudar de lado". Antes o adiantamento vinha do lado pra
            // onde o corpo olhava: parado, so' girando, o alvo da camera pulava 2x
            // olharAFrente — 2,8 m. Mesmo suavizado em 0,3 s isso e' 9,3 m/s, mais rapido do
            // que ele corre. A camera disparava com o personagem PARADO, e o olho le' isso
            // como o personagem voando pela tela. Suavizar nao resolvia porque o errado era o
            // gatilho, nao a velocidade.
            //
            // Medindo a velocidade real, girar no lugar da zero: a camera nao mexe um
            // milimetro. Ela so' se adianta quando ele esta' de fato indo pra algum lado, que
            // e' a unica hora em que adiantar ajuda a ver o que vem.
            float agora = alvo.position.x;
            if (imediato) { xAnterior = agora; velocidadeMedida = 0f; }
            else if (Time.deltaTime > 0.0001f)
            {
                float bruta = (agora - xAnterior) / Time.deltaTime;
                xAnterior = agora;
                velocidadeMedida = Mathf.SmoothDamp(velocidadeMedida, bruta,
                                                    ref aceleracaoMedida, suavidadeDoLado);
            }

            float fracao = velocidadeDeReferencia <= 0.01f ? 0f
                         : Mathf.Clamp(velocidadeMedida / velocidadeDeReferencia, -1f, 1f);
            float x = agora + olharAFrente * fracao;

            if (usarLimites) x = Mathf.Clamp(x, xMinimo, xMaximo);

            xAtual = imediato || suavidade <= 0f
                ? x
                : Mathf.SmoothDamp(xAtual, x, ref velocidade, suavidade);

            transform.position = new Vector3(xAtual, altura, -distancia);
            transform.rotation = Quaternion.Euler(inclinacao, 0f, 0f);
        }
    }
}
