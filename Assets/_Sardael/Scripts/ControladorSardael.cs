using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// Movimento 2.5D travado na linha: o personagem so' anda no eixo X, Z e' forcado a cada
    /// frame. Pular e subir em caixote funciona; nao existe plataforma aerea.
    ///
    /// As proporcoes vem do tuning.js do projeto Construct, que foi balanceado JOGANDO:
    /// andar 160 e correr 300, ou seja correr e' 1,875x andar. Os valores absolutos aqui
    /// estao em metros por segundo (o Construct estava em pixels), mas a razao e' a mesma.
    ///
    /// Enquanto nao houver animacao o personagem desliza em pose de repouso. Isso e'
    /// proposital: deixa a falta escancarada em vez de esconder atras de um clipe errado.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ControladorSardael : MonoBehaviour
    {
        [Header("Andar")]
        public float velocidadeAndar = 2.4f;
        public float velocidadeCorrer = 4.5f;
        [Tooltip("Quao rapido ele atinge a velocidade. Alto = responde na hora, baixo = pesado.")]
        public float aceleracao = 16f;

        [Header("Pular")]
        [Tooltip("Altura em metros. O caixote da arena tem 0,82 m — 1,1 sobe com folga.")]
        public float alturaDoPulo = 1.1f;
        [Tooltip("Mais forte que a gravidade real: queda real em jogo parece flutuante.")]
        public float gravidade = -24f;
        [Tooltip("Perdao pra quem aperta pular logo depois de sair da borda.")]
        public float tempoDeCoiote = 0.12f;

        [Header("Linha 2.5D")]
        public float zDaLinha = 0f;
        public float giroPorSegundo = 1080f;

        [Header("Limites da fase")]
        public bool usarLimites = true;
        public float xMinimo = -32f;
        public float xMaximo = 36f;

        [Header("Depuracao")]
        public bool mostrarPainel = true;

        CharacterController cc;
        float velX, velY;
        float saiuDoChaoEm = -99f;
        int olhando = 1;          // +1 olhando pro +X, -1 pro -X

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            // cos(90 graus) em ponto flutuante da' -4,4e-8, entao um corpo nascido em Y=90
            // era lido como olhando pro lado errado e girava 180 graus no primeiro quadro.
            // O vetor forward nao tem essa borda.
            olhando = transform.forward.x >= 0f ? 1 : -1;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            float entrada; bool correndo, pulou;
            LerEntrada(out entrada, out correndo, out pulou);

            // ---- horizontal ----
            float alvo = entrada * (correndo ? velocidadeCorrer : velocidadeAndar);
            velX = Mathf.MoveTowards(velX, alvo, aceleracao * dt);

            if (Mathf.Abs(entrada) > 0.01f) olhando = entrada > 0f ? 1 : -1;

            // ---- vertical ----
            if (cc.isGrounded)
            {
                saiuDoChaoEm = Time.time;
                if (velY < 0f) velY = -2f;              // cola no chao em rampa
            }
            bool podePular = cc.isGrounded || (Time.time - saiuDoChaoEm) <= tempoDeCoiote;
            if (pulou && podePular)
            {
                velY = Mathf.Sqrt(2f * alturaDoPulo * -gravidade);
                saiuDoChaoEm = -99f;
            }
            velY += gravidade * dt;

            cc.Move(new Vector3(velX, velY, 0f) * dt);

            // ---- travar na linha: e' isto que faz o jogo ser 2.5D ----
            var p = transform.position;
            p.z = zDaLinha;
            if (usarLimites) p.x = Mathf.Clamp(p.x, xMinimo, xMaximo);
            transform.position = p;

            // ---- virar de frente pro lado que anda ----
            float grausAlvo = olhando > 0 ? 90f : -90f;
            var giro = Quaternion.Euler(0f, grausAlvo, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, giro, giroPorSegundo * dt);
        }

        void LerEntrada(out float eixo, out bool correndo, out bool pulou)
        {
            eixo = 0f; correndo = false; pulou = false;
#if ENABLE_INPUT_SYSTEM
            var tec = Keyboard.current;
            if (tec == null) return;
            if (tec.dKey.isPressed || tec.rightArrowKey.isPressed) eixo += 1f;
            if (tec.aKey.isPressed || tec.leftArrowKey.isPressed) eixo -= 1f;
            correndo = tec.leftShiftKey.isPressed;
            pulou = tec.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) eixo += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) eixo -= 1f;
            correndo = Input.GetKey(KeyCode.LeftShift);
            pulou = Input.GetKeyDown(KeyCode.Space);
#endif
        }

        void OnGUI()
        {
            if (!mostrarPainel) return;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            estilo.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 10, 330, 96), GUIContent.none);
            GUI.Label(new Rect(20, 16, 320, 86),
                "A / D  ou  setas  —  andar     SHIFT  correr     ESPACO  pular\n" +
                string.Format("x = {0:F2}      velocidade = {1:F2} m/s", transform.position.x, Mathf.Abs(velX)) + "\n" +
                string.Format("no chao: {0}      olhando: {1}", cc.isGrounded, olhando > 0 ? "direita" : "esquerda") + "\n" +
                "(sem animacao ainda — ele desliza, e' esperado)",
                estilo);
        }
    }
}
