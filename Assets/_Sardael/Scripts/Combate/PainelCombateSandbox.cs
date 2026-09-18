using UnityEngine;

namespace Sardael
{
    public sealed class PainelCombateSandbox : MonoBehaviour
    {
        [SerializeField] CombateDoHeroi combate;
        [SerializeField] RegistroDeCombate registro;

        public void Configurar(CombateDoHeroi novoCombate, RegistroDeCombate novoRegistro)
        {
            combate = novoCombate;
            registro = novoRegistro;
        }

        void OnGUI()
        {
            if (combate == null) return;
            string alvo = combate.AlvoAtual == null ? "-" : combate.AlvoAtual.name;
            string distancia = combate.DistanciaDoAlvo < 0f ? "-" : combate.DistanciaDoAlvo.ToString("F2") + " m";
            GUI.Box(new Rect(10, 164, 330, 116), GUIContent.none);
            GUI.Label(new Rect(20, 174, 310, 100),
                "COMBATE SANDBOX\n" +
                "alvo: " + alvo + "\n" +
                "elo: " + combate.EloAtual + "   lado: " + (combate.LadoEscolhido < 0 ? "esquerda" : "direita") + "\n" +
                "distancia: " + distancia + "\n" +
                "inimigos registrados: " + (registro == null ? 0 : registro.Quantidade));
        }
    }
}
