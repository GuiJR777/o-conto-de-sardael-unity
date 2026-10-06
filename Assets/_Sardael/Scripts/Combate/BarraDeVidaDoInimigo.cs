using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Barra de vida acima da cabeca do inimigo, montada em tempo de jogo com dois quads
    /// (fundo + preenchimento), sem Canvas: os quads viram pro olho da camera como os demais
    /// avisos da cena. A opcao "visivel" no Inspector liga e desliga a barra.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BarraDeVidaDoInimigo : MonoBehaviour
    {
        [SerializeField] AlvoDeCombate alvo;
        [Tooltip("Liga ou desliga a barra sobre a cabeca deste inimigo.")]
        [SerializeField] bool visivel = true;
        [SerializeField, Min(0.05f)] float largura = 1.5f;
        [SerializeField, Min(0.02f)] float espessura = 0.2f;
        [SerializeField, Min(0f)] float alturaAcimaDaCabeca = 0.5f;

        Transform raiz;
        Transform preenchimento;
        Renderer renderizadorDoFundo;
        Renderer renderizadorDoPreenchimento;
        Material materialDoFundo;
        Material materialDoPreenchimento;
        Animator animator;
        bool montado;

        public bool Visivel
        {
            get => visivel;
            set => visivel = value;
        }

        void Awake()
        {
            if (alvo == null) alvo = GetComponent<AlvoDeCombate>();
            animator = GetComponent<Animator>();
            if (visivel) GarantirVisual();
        }

        void Update()
        {
            if (alvo == null) return;
            if (!montado && visivel) GarantirVisual();
            if (!montado) return;

            bool mostrar = visivel && alvo.Vivo;
            if (raiz.gameObject.activeSelf != mostrar)
                raiz.gameObject.SetActive(mostrar);
            if (!mostrar) return;

            float fracao = alvo.VidaMaxima <= 0f
                ? 0f
                : Mathf.Clamp01(alvo.VidaAtual / alvo.VidaMaxima);
            Pintar(materialDoPreenchimento, Color.Lerp(
                new Color(0.95f, 0.22f, 0.12f), new Color(0.35f, 0.95f, 0.3f), fracao));
            preenchimento.localPosition = new Vector3(fracao * largura * 0.5f, 0f, 0f);
            preenchimento.localScale = new Vector3(
                Mathf.Max(0.001f, fracao * largura), espessura, 1f);

            Vector3 posicao = transform.position;
            if (animator != null && animator.isHuman)
            {
                var cabeca = animator.GetBoneTransform(HumanBodyBones.Head);
                if (cabeca != null) posicao = cabeca.position;
            }
            posicao.y += alturaAcimaDaCabeca;
            raiz.position = posicao;
            if (Camera.main != null) raiz.rotation = Camera.main.transform.rotation;
        }

        void OnDestroy()
        {
            if (materialDoFundo != null) Destroy(materialDoFundo);
            if (materialDoPreenchimento != null) Destroy(materialDoPreenchimento);
        }

        void GarantirVisual()
        {
            if (montado) return;
            var shader = AcharShader();
            if (shader == null) return;

            raiz = new GameObject("BarraDeVida").transform;
            raiz.SetParent(transform, false);
            raiz.localPosition = Vector3.zero;

            var fundo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fundo.name = "Fundo";
            Object.Destroy(fundo.GetComponent<Collider>());
            fundo.transform.SetParent(raiz, false);
            fundo.transform.localPosition = Vector3.zero;
            fundo.transform.localScale = new Vector3(largura, espessura, 1f);
            renderizadorDoFundo = fundo.GetComponent<MeshRenderer>();
            materialDoFundo = new Material(shader);
            Pintar(materialDoFundo, new Color(0.06f, 0.06f, 0.08f, 1f));
            PrepararMaterial(materialDoFundo);
            renderizadorDoFundo.sharedMaterial = materialDoFundo;
            renderizadorDoFundo.sortingOrder = 44;
            ConfigurarRenderizador(renderizadorDoFundo);

            var ancora = new GameObject("Ancora").transform;
            ancora.SetParent(raiz, false);
            ancora.localPosition = new Vector3(-largura * 0.5f, 0f, -0.01f);

            var vida = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vida.name = "Vida";
            Object.Destroy(vida.GetComponent<Collider>());
            preenchimento = vida.transform;
            preenchimento.SetParent(ancora, false);
            preenchimento.localPosition = new Vector3(largura * 0.5f, 0f, 0f);
            preenchimento.localScale = new Vector3(largura, espessura, 1f);
            renderizadorDoPreenchimento = vida.GetComponent<MeshRenderer>();
            materialDoPreenchimento = new Material(shader);
            PrepararMaterial(materialDoPreenchimento);
            renderizadorDoPreenchimento.sharedMaterial = materialDoPreenchimento;
            renderizadorDoPreenchimento.sortingOrder = 45;
            ConfigurarRenderizador(renderizadorDoPreenchimento);

            montado = true;
        }

        static void ConfigurarRenderizador(Renderer renderizador)
        {
            if (renderizador == null) return;
            renderizador.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderizador.receiveShadows = false;
        }

        static void PrepararMaterial(Material material)
        {
            if (material == null) return;
            // visivel dos dois lados, nao importa pra que lado a camera olhe
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        }

        /// <summary>
        /// material.color so' enxerga "_Color"; o Unlit do URP usa "_BaseColor".
        /// </summary>
        static void Pintar(Material material, Color cor)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", cor);
            else material.color = cor;
        }

        static Shader shaderEmCache;

        static Shader AcharShader()
        {
            if (shaderEmCache == null)
            {
                shaderEmCache = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Standard");
            }
            return shaderEmCache;
        }
    }
}
