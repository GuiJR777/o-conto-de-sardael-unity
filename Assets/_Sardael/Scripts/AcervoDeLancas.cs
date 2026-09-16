using UnityEngine;

namespace Sardael
{
    /// <summary>Uma lanca do acervo: o modelo, o retrato do catalogo e a medida crua dela.</summary>
    [System.Serializable]
    public class LancaDoAcervo
    {
        public string nome;            // "A01", "B17"... e' o que aparece na celula
        public string pacote;          // de qual pack veio, pra saber o que apagar junto
        public GameObject modelo;      // o FBX. Vazio = a lanca que ja' esta' na mao na cena
        public Texture2D retrato;      // desenhado no catalogo
        public float alturaCrua;       // metros do mesh sem escala nenhuma, so' informativo
    }

    /// <summary>
    /// A lista de todas as lancas que o jogo conhece.
    ///
    /// Fica num asset em Resources pra que qualquer cena carregue com uma linha, sem precisar
    /// arrastar 48 campos no Inspector e sem quebrar quando alguem apaga um objeto da cena.
    ///
    /// As medidas de encaixe (<see cref="posicaoNaMao"/>, <see cref="rotacaoNaMao"/>,
    /// <see cref="fracaoDeAgarre"/>) foram tiradas da lanca do pack de animacao — a
    /// SM_Wep_Spear_01 que ja' estava na mao de Sardael. Toda lanca nova e' ajustada PRA ELA,
    /// porque sao as animacoes que mandam: se a lanca nova for mais curta a mao erra o cabo.
    /// </summary>
    public class AcervoDeLancas : ScriptableObject
    {
        [Header("Medida de referencia (da lanca das animacoes)")]
        [Tooltip("Comprimento real, em metros, que toda lanca vai ter depois do ajuste.")]
        public float comprimentoAlvo = 2.2427f;
        [Tooltip("Onde a mao pega, contado da ponta de baixo. 0,43 = a mao fica a 43% do "
               + "comprimento, que e' onde a mao de Sardael pega a lanca do pack.")]
        [Range(0f, 1f)] public float fracaoDeAgarre = 0.4310f;

        [Header("Encaixe no osso Hand_R (unidades locais do osso)")]
        public Vector3 posicaoNaMao = new Vector3(9.28287f, 7.41498f, -11.86496f);
        public Vector3 rotacaoNaMao = new Vector3(344.698f, 78.331f, 290.765f);

        [Header("O acervo")]
        public LancaDoAcervo[] lancas = new LancaDoAcervo[0];

        /// <summary>Carrega o acervo de Resources. Devolve nulo se ninguem montou ainda.</summary>
        public static AcervoDeLancas Carregar()
        {
            return Resources.Load<AcervoDeLancas>("Acervo_De_Lancas");
        }
    }
}
