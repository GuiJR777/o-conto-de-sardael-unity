using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// O ponto do mapa que leva pra outra cena.
    ///
    /// Fica num objeto vazio — o portao, a boca da estrada — e dispara quando o heroi chega
    /// a <see cref="distancia"/> dele. Nao usa trigger de fisica de proposito: o heroi anda
    /// por CharacterController numa linha fixa, e uma conta de distancia no eixo da marcha e'
    /// mais previsivel do que um collider que pode ser atravessado num quadro de lag.
    ///
    /// So' abre depois de <see cref="liberado"/> ficar verdadeiro. E' o que separa "o portao
    /// existe" de "a missao mandou ir ate' ele": sem isso o jogador cai na proxima fase por
    /// passear pro lado errado.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortaDeCena : MonoBehaviour
    {
        [Header("Pra onde")]
        [Tooltip("Nome exato da cena. Ela PRECISA estar no Build Settings, senao nao carrega.")]
        public string cenaDeDestino = "";
        [TextArea] public string recadoDaTela = "Carregando...";

        [Header("Quando")]
        [Tooltip("A PAREDE e' o gatilho: destrancada, ela vira passagem e atravessa-la carrega "
               + "a cena. Assim gatilho e parede sao o MESMO objeto e nao tem como um ficar num "
               + "lugar e o outro noutro — foi exatamente esse desencontro que deixou os dois "
               + "portoes de Halu inalcancaveis. Sem parede, cai na distancia abaixo.")]
        public bool aParedeEOGatilho = true;
        [Tooltip("A que distancia do ponto o heroi dispara a troca. So' vale quando nao ha' "
               + "parede, ou quando o gatilho pela parede esta' desligado.")]
        public float distancia = 3.5f;
        [Tooltip("Enquanto falso, passar por aqui nao faz nada. Quem liga e' a missao.")]
        public bool liberado = false;

        [Header("Quem")]
        [Tooltip("Se vazio, procura o MovimentoDoHeroi da cena ao comecar.")]
        public Transform heroi;

        [Header("A parede que fecha este caminho")]
        [Tooltip("A barreira invisivel que impede de chegar aqui. Sai do caminho junto com o "
               + "destrancar e volta se o portao for trancado de novo. Sem isto a seta aponta "
               + "pra um portao que o jogador nao alcanca — ele bate numa parede que nao ve.")]
        public GameObject paredeQueSai;

        [Tooltip("Meio corpo do heroi. O centro dele nunca entra na parede: para' a um raio "
               + "dela, e sem esta folga o gatilho pela parede nunca fecharia.")]
        public float folgaDoCorpo = 0.45f;

        [Tooltip("O que mais deixa de barrar quando o portao abre: o portao de madeira em si, "
               + "uma cerca, uma carroca atravessada. Portao destrancado com o modelo do portao "
               + "ainda solido para' o jogador meio metro antes do vao — ele ve' o caminho "
               + "aberto e bate no nada.")]
        public GameObject[] tambemAbrem = new GameObject[0];

        [Header("Depuracao")]
        public bool mostrarGizmo = true;

        bool disparou;

        void Start()
        {
            if (heroi == null)
            {
                var h = FindAnyObjectByType<MovimentoDoHeroi>();
                if (h != null) heroi = h.transform;
            }
            if (string.IsNullOrEmpty(cenaDeDestino))
                Debug.LogWarning("PortaDeCena em '" + name + "' sem cena de destino.", this);
        }

        void Update()
        {
            if (disparou || !liberado || heroi == null) return;
            if (TelaDeCarregamento.Carregando) return;
            if (!Chegou()) return;

            disparou = true;
            TelaDeCarregamento.Ir(cenaDeDestino, recadoDaTela);
        }

        bool Chegou()
        {
            var caixa = CaixaDaParede();
            if (caixa != null)
            {
                // encostou na faixa que a parede ocupava: o gatilho e' o vao dela
                float x = heroi.position.x;
                return x >= caixa.Value.min.x - folgaDoCorpo
                    && x <= caixa.Value.max.x + folgaDoCorpo;
            }
            var d = heroi.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= distancia * distancia;
        }

        Bounds? CaixaDaParede()
        {
            if (!aParedeEOGatilho || paredeQueSai == null) return null;
            var c = paredeQueSai.GetComponentInChildren<Collider>(true);
            if (c == null) return null;
            return c.bounds;
        }

        /// <summary>
        /// Liga ou desliga o portao.
        ///
        /// Trancado, a parede e' parede. Destrancado, ela vira VAO: o colisor continua no lugar
        /// mas em modo trigger, e atravessar esse vao e' o que carrega a cena. Antes eu apagava
        /// a parede e media distancia ate' um ponto solto — e bastava um outro colisor no
        /// caminho pra o jogador parar a cinco metros de um gatilho que ele nunca alcancava.
        /// </summary>
        public void Liberar(bool quanto)
        {
            liberado = quanto;
            disparou = false;

            foreach (var extra in tambemAbrem)
                if (extra != null)
                    foreach (var c in extra.GetComponentsInChildren<Collider>(true))
                        c.isTrigger = quanto;

            if (paredeQueSai == null) return;

            if (aParedeEOGatilho)
            {
                paredeQueSai.SetActive(true);              // ela precisa existir pra ser o vao
                foreach (var c in paredeQueSai.GetComponentsInChildren<Collider>(true))
                    c.isTrigger = quanto;
            }
            else paredeQueSai.SetActive(!quanto);
        }

        void OnDrawGizmos()
        {
            if (!mostrarGizmo) return;
            Gizmos.color = liberado ? new Color(0.4f, 0.95f, 0.5f, 0.8f)
                                    : new Color(0.95f, 0.4f, 0.35f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, distancia);
        }
    }
}
