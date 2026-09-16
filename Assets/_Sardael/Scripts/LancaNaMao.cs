using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Troca a lanca da mao de Sardael.
    ///
    /// Este e' o pedaco que o jogo vai usar de verdade quando cair lanca de inimigo: recebe um
    /// modelo qualquer e o encaixa na mao ja' no tamanho certo. O catalogo por cima disto e' so'
    /// uma vitrine.
    ///
    /// COMO O ENCAIXE FUNCIONA. Nao da' pra sair colando FBX no osso: cada pack desenha a lanca
    /// no seu proprio tamanho e com o pivo onde quis. Entao:
    ///
    ///   1. Um SUPORTE nasce no osso Hand_R com a posicao e a rotacao medidas da lanca do pack
    ///      de animacao. A escala do suporte e' 1/escala-do-osso, entao dentro dele 1 unidade
    ///      volta a ser 1 metro — o codigo pensa em metros, nao em centimetros de rig.
    ///   2. O modelo entra no suporte e e' esticado pra ficar com o comprimento de referencia.
    ///      As animacoes foram feitas pra uma lanca de 2,24 m; uma de 2,05 m deixaria a mao no ar.
    ///   3. Ele desliza no proprio eixo ate' que o ponto de agarre (43% do comprimento, contado
    ///      de baixo) caia em cima da mao. Assim tanto faz se o pivo do FBX veio na base, no meio
    ///      ou na ponta.
    ///
    /// Por isso a conta usa o BOUNDS do modelo e nao numeros salvos por lanca: lanca nova que
    /// alguem jogar na pasta amanha ja' entra certa sem ninguem medir nada.
    /// </summary>
    [DisallowMultipleComponent]
    public class LancaNaMao : MonoBehaviour
    {
        [Header("Quem segura")]
        [Tooltip("O osso da mao. Se vazio, procura um filho chamado Hand_R.")]
        public Transform mao;
        [Tooltip("A lanca que ja' esta' montada na cena. Ela volta quando se escolhe 'original'.")]
        public Transform lancaOriginal;

        [Header("Acervo")]
        [Tooltip("Se vazio, carrega Resources/Acervo_De_Lancas.")]
        public AcervoDeLancas acervo;

        Transform suporte;
        GameObject posta;
        int escolhida = -1;

        /// <summary>Indice no acervo. -1 quer dizer a lanca original da cena.</summary>
        public int Escolhida { get { return escolhida; } }
        public AcervoDeLancas Acervo { get { return acervo; } }

        void Awake()
        {
            if (acervo == null) acervo = AcervoDeLancas.Carregar();

            if (mao == null)
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == "Hand_R") { mao = t; break; }

            if (lancaOriginal == null && mao != null)
            {
                var l = mao.Find("Lanca");
                if (l != null) lancaOriginal = l;
            }
        }

        /// <summary>Poe a lanca do acervo na mao. Indice fora da lista devolve a original.</summary>
        public void Equipar(int indice)
        {
            if (mao == null) return;

            if (posta != null) { Destroy(posta); posta = null; }

            bool ehOriginal = acervo == null || indice < 0 || indice >= acervo.lancas.Length
                           || acervo.lancas[indice].modelo == null;
            escolhida = ehOriginal ? -1 : indice;

            if (lancaOriginal != null) lancaOriginal.gameObject.SetActive(ehOriginal);
            if (ehOriginal) return;

            GarantirSuporte();

            posta = Instantiate(acervo.lancas[indice].modelo, suporte);
            posta.name = "Lanca_" + acervo.lancas[indice].nome;
            posta.transform.localPosition = Vector3.zero;
            posta.transform.localRotation = Quaternion.identity;
            posta.transform.localScale = Vector3.one;

            var b = Medir(posta.transform);
            if (b.size.y <= 0.0001f) return;

            float esc = acervo.comprimentoAlvo / b.size.y;
            posta.transform.localScale = Vector3.one * esc;
            // o agarre e' medido do fim de baixo; b.min.y desconta o pivo de onde quer que ele esteja
            posta.transform.localPosition =
                new Vector3(0f, -acervo.fracaoDeAgarre * acervo.comprimentoAlvo - b.min.y * esc, 0f);
        }

        public void VoltarAOriginal() { Equipar(-1); }

        void GarantirSuporte()
        {
            if (suporte != null) return;
            var go = new GameObject("Suporte_Lanca");
            suporte = go.transform;
            suporte.SetParent(mao, false);
            suporte.localPosition = acervo.posicaoNaMao;
            suporte.localRotation = Quaternion.Euler(acervo.rotacaoNaMao);
            // desfaz a escala do rig: dentro do suporte 1 unidade volta a ser 1 metro
            float e = mao.lossyScale.x;
            suporte.localScale = Vector3.one * (e > 0.00001f ? 1f / e : 1f);
        }

        /// <summary>Caixa que encerra o modelo, no espaco local da raiz dele e com escala 1.</summary>
        public static Bounds Medir(Transform raiz)
        {
            bool primeiro = true;
            var caixa = new Bounds();
            var paraLocal = raiz.worldToLocalMatrix;

            foreach (var f in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                Somar(ref caixa, ref primeiro, f.sharedMesh.bounds, paraLocal * f.transform.localToWorldMatrix);
            }
            foreach (var s in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.sharedMesh == null) continue;
                Somar(ref caixa, ref primeiro, s.sharedMesh.bounds, paraLocal * s.transform.localToWorldMatrix);
            }
            return caixa;
        }

        static void Somar(ref Bounds caixa, ref bool primeiro, Bounds b, Matrix4x4 m)
        {
            var c = b.center; var e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z)));
                if (primeiro) { caixa = new Bounds(p, Vector3.zero); primeiro = false; }
                else caixa.Encapsulate(p);
            }
        }
    }
}
