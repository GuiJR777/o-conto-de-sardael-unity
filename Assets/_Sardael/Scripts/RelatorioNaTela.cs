using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Escreve na tela, em tempo real, os numeros que dizem se um rig esta' se empurrando sozinho.
    ///
    /// Discussao sobre "o cavalo teleporta" nao se resolve olhando: resolve vendo o numero da
    /// posicao andar. Aqui aparece a posicao do corpo, a do objeto que tem o Animator (e' onde
    /// o root motion apareceria) e a do osso Root_Motion, com o desvio acumulado desde o inicio.
    /// </summary>
    public class RelatorioNaTela : MonoBehaviour
    {
        public Transform corpo;
        public Animator animador;
        public Transform ossoRootMotion;
        public Transform pontoDaSela;
        public Transform cavaleiro;
        public Animator animadorDoCavaleiro;

        Vector3 corpoInicial, animInicial;
        bool guardou;
        float maiorDesvio;
        GUIStyle estilo;

        void LateUpdate()
        {
            if (!guardou && corpo != null && animador != null)
            {
                corpoInicial = corpo.position;
                animInicial = animador.transform.localPosition;
                guardou = true;
            }
            if (guardou && corpo != null)
            {
                float d = Vector3.Distance(corpo.position, corpoInicial);
                if (d > maiorDesvio) maiorDesvio = d;
            }
        }

        void OnGUI()
        {
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label);
                estilo.fontSize = 18;
                estilo.normal.textColor = Color.white;
            }

            var caixa = new Rect(12, 12, 620, 300);
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(caixa, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = 20f;
            void Linha(string t) { GUI.Label(new Rect(24, y, 600, 26), t, estilo); y += 24f; }

            if (corpo == null) { Linha("sem referencias"); return; }

            Linha("CAVALO PARADO - sem trilho, so' a animacao");
            Linha("");
            Linha("posicao do corpo        " + corpo.position.ToString("F4"));
            Linha("saiu do lugar           " + Vector3.Distance(corpo.position, corpoInicial).ToString("F4") + " m"
                + "   (maior ate agora " + maiorDesvio.ToString("F4") + " m)");
            if (animador != null)
            {
                Linha("objeto do Animator      local " + animador.transform.localPosition.ToString("F4"));
                Linha("   deslocou              " + Vector3.Distance(animador.transform.localPosition, animInicial).ToString("F4") + " m"
                    + "   applyRootMotion = " + animador.applyRootMotion);
            }
            if (ossoRootMotion != null)
                Linha("osso Root_Motion        local " + ossoRootMotion.localPosition.ToString("F4"));
            if (pontoDaSela != null && animadorDoCavaleiro != null)
            {
                var q = animadorDoCavaleiro.GetBoneTransform(HumanBodyBones.Hips);
                if (q != null)
                    Linha("quadril - ponto da sela " + (q.position.y - pontoDaSela.position.y).ToString("F3") + " m");
            }
            if (cavaleiro != null && corpo != null)
                Linha("cavaleiro <-> cavalo     " + Vector3.Distance(
                    new Vector3(cavaleiro.position.x, 0, cavaleiro.position.z),
                    new Vector3(corpo.position.x, 0, corpo.position.z)).ToString("F3") + " m no plano");
            Linha("");
            Linha("WASD anda, botao direito olha, Shift corre");
        }
    }
}
