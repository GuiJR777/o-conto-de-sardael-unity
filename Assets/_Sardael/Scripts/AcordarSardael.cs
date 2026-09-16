using UnityEngine;
using UnityEngine.Events;

namespace Sardael
{
    /// <summary>
    /// O outro lado do fade branco: Sardael abre os olhos ao pe da grande arvore de Halu.
    ///
    /// A cena da morte termina lavando a tela de branco. Esta comeca branca e vai abrindo, entao
    /// o corte entre as duas nao existe - e' a mesma luz que fecha uma e abre a outra.
    ///
    /// Ele entra na MESMA pose em que caiu (ultimo quadro de HumanM@CombatDeath03, o clipe que a
    /// cena da morte usa), e levanta com esse clipe tocando DE TRAS PRA FRENTE. Nao ha animacao
    /// de levantar do chao em nenhum pacote do projeto - eu procurei nos 1134 arquivos com clipe.
    /// Desandar a queda entrega exatamente o movimento certo, e de brinde a pose inicial casa com
    /// a da cena anterior sem eu ter que alinhar nada na mao.
    ///
    /// A velocidade negativa mora no ESTADO do controller, nao em Animator.speed: mexer em
    /// Animator.speed viraria o tempo de todos os estados, e o idle do fim sairia de re'.
    /// </summary>
    public class AcordarSardael : MonoBehaviour
    {
        public enum Etapa { Branco, Caido, Levantando, Acordado }

        [Header("Ator")]
        public Animator animador;
        [Tooltip("Estado segurado enquanto ele ainda esta deitado.")]
        public string estadoCaido = "Caido";
        [Tooltip("Onde congelar o estado deitado. 0 = primeiro quadro do clipe de levantar.")]
        [Range(0f, 1f)] public float quadroDeitado = 0f;
        [Tooltip("Estado que toca o levantar.")]
        public string estadoLevanta = "Levanta";
        public string estadoParado = "Parado";

        [Header("Tela")]
        public CanvasGroup telaBranca;
        [Tooltip("Quanto tempo a tela leva pra sair do branco.")]
        public float duracaoDoFade = 2.2f;

        [Header("Tempos (segundos)")]
        [Tooltip("Quanto ele fica caido depois que a tela abre, antes de se mexer.")]
        public float esperaCaido = 2.0f;
        [Tooltip("Duracao do levante. Sai do comprimento do clipe dividido pela velocidade do estado.")]
        public float duracaoDoLevante = 3.4f;

        [Header("Desce da cama")]
        [Tooltip("Altura do corpo deitado (em cima do saco de dormir).")]
        public float alturaDeitado = 0.05f;
        [Tooltip("Altura do chao ao lado da cama. Ele desce ate' aqui enquanto levanta.")]
        public float alturaDePe = -0.19f;
        [Tooltip("Em que fracao do levante a descida comeca. 0,5 = so' na segunda metade.")]
        [Range(0f, 0.95f)] public float comecaADescerEm = 0.45f;

        [Header("Fim")]
        public UnityEvent aoAcordar;

        [Header("Depuracao")]
        public bool mostrarPainel = false;

        public Etapa Agora { get; private set; }

        float relogio;
        bool chamou;

        /// <summary>
        /// OnEnable e nao Start: esta cena roda MAIS DE UMA VEZ na mesma partida.
        ///
        /// Enquanto o combate de Bael nao existir, o orc mata Sardael toda vez que ele entra na
        /// floresta, e o Veu o devolve aqui sempre. Com Start, que roda uma vez so' na vida do
        /// componente, a segunda morte reacendia o palco com o estado da primeira: etapa ja' em
        /// "Acordado", tela sem branco e o boneco de pe' desde o primeiro quadro.
        /// </summary>
        void OnEnable()
        {
            Agora = Etapa.Branco;
            relogio = 0f;
            chamou = false;

            if (telaBranca != null) telaBranca.alpha = 1f;

            if (animador != null)
            {
                // PRIMEIRO quadro do clipe de levantar, congelado - a pose deitada. Ja errei isso
                // aqui: com 1f ele entra em cena no ULTIMO quadro, ou seja, ja de pe, e a cena
                // inteira roda com ele andando pra longe da cama.
                // Update(0) forca o Animator a avaliar agora: sem isso o primeiro quadro visivel
                // mostra o boneco em pose de bind, de pe.
                animador.Play(estadoCaido, 0, quadroDeitado);
                animador.Update(0f);
                animador.speed = 0f;
            }
        }

        void Update()
        {
            relogio += Time.deltaTime;

            switch (Agora)
            {
                case Etapa.Branco:
                    if (telaBranca != null)
                        telaBranca.alpha = 1f - Mathf.Clamp01(relogio / Mathf.Max(0.01f, duracaoDoFade));
                    if (relogio >= duracaoDoFade) Trocar(Etapa.Caido);
                    break;

                case Etapa.Caido:
                    if (relogio >= esperaCaido) Trocar(Etapa.Levantando);
                    break;

                case Etapa.Levantando:
                    DescerDaCama();
                    if (relogio >= duracaoDoLevante) Trocar(Etapa.Acordado);
                    break;

                case Etapa.Acordado:
                    if (!chamou) { chamou = true; if (aoAcordar != null) aoAcordar.Invoke(); }
                    break;
            }
        }

        /// <summary>
        /// Desce o corpo do saco de dormir ate' o chao durante o levante.
        ///
        /// O clipe foi animado pra quem esta deitado NO CHAO, entao a raiz e o chao sao a mesma
        /// altura nele. Em cima da cama a raiz sobe uns 25 cm, e sem isto ele terminaria de pe
        /// flutuando essa distancia. Descer no meio do movimento le como sair da cama; descer no
        /// fim seria um tranco visivel.
        /// </summary>
        void DescerDaCama()
        {
            if (animador == null) return;
            float t = Mathf.Clamp01(relogio / Mathf.Max(0.01f, duracaoDoLevante));
            float u = Mathf.Clamp01((t - comecaADescerEm) / Mathf.Max(0.05f, 1f - comecaADescerEm));
            u = u * u * (3f - 2f * u);
            var p = animador.transform.position;
            animador.transform.position = new Vector3(p.x, Mathf.Lerp(alturaDeitado, alturaDePe, u), p.z);
        }

        void Trocar(Etapa e)
        {
            Agora = e;
            relogio = 0f;
            if (animador == null) return;

            if (e == Etapa.Levantando)
            {
                animador.speed = 1f;
                animador.Play(estadoLevanta, 0, 0f);
            }
            else if (e == Etapa.Acordado)
            {
                animador.speed = 1f;
                animador.CrossFade(estadoParado, 0.3f);
            }
        }

        void OnGUI()
        {
            if (!mostrarPainel) return;
            var e = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            e.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 10, 300, 54), GUIContent.none);
            GUI.Label(new Rect(20, 16, 290, 44),
                "etapa: " + Agora + "\nrelogio: " + relogio.ToString("F2") + "s", e);
        }
    }
}
