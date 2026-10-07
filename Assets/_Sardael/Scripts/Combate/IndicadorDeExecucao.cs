using UnityEngine;

namespace Sardael
{
    public sealed class IndicadorDeExecucao : MonoBehaviour
    {
        const string Caveira = "\u2620";

        [SerializeField] AlvoDeCombate alvo;
        [SerializeField] Transform jogador;
        [SerializeField, Min(0.5f)] float alcance = 4.5f;

        TextMesh texto;

        public bool Visivel => texto != null && texto.gameObject.activeSelf;

        public void Configurar(AlvoDeCombate novoAlvo, Transform novoJogador)
        {
            alvo = novoAlvo;
            jogador = novoJogador;
            GarantirVisual();
        }

        void Awake()
        {
            if (alvo == null) alvo = GetComponent<AlvoDeCombate>();
            GarantirVisual();
        }

        void Update()
        {
            if (jogador == null)
            {
                var vida = FindFirstObjectByType<VidaDoHeroi>();
                if (vida != null) jogador = vida.transform;
            }

            bool mostrar = alvo != null && alvo.Valido && alvo.Atordoado && !alvo.Reservado &&
                jogador != null && RegistroDeCombate.DistanciaPlanar(
                    jogador.position, transform.position) <= alcance;
            if (texto.gameObject.activeSelf != mostrar) texto.gameObject.SetActive(mostrar);
            if (!mostrar) return;

            float pulso = 1f + Mathf.Sin(Time.time * 8f) * 0.08f;
            texto.transform.localScale = Vector3.one * pulso;
            texto.transform.localPosition = new Vector3(0f, 2.55f + Mathf.Sin(Time.time * 5f) * 0.05f, 0f);
            if (Camera.main != null) texto.transform.rotation = Camera.main.transform.rotation;
        }

        void GarantirVisual()
        {
            bool acabouDeCriar = false;
            if (texto == null)
            {
                Transform existente = transform.Find("IndicadorDeExecucao");
                if (existente != null) texto = existente.GetComponent<TextMesh>();
            }
            if (texto == null)
            {
                var objeto = new GameObject("IndicadorDeExecucao");
                objeto.transform.SetParent(transform, false);
                texto = objeto.AddComponent<TextMesh>();
                acabouDeCriar = true;
            }

            texto.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            texto.text = Caveira;
            texto.anchor = TextAnchor.MiddleCenter;
            texto.alignment = TextAlignment.Center;
            texto.fontSize = 96;
            texto.characterSize = 0.05f;
            texto.fontStyle = FontStyle.Bold;
            texto.color = new Color(1f, 0.32f, 0.24f);
            var renderizador = texto.GetComponent<MeshRenderer>();
            if (renderizador != null) renderizador.sortingOrder = 55;
            if (acabouDeCriar) texto.gameObject.SetActive(false);
        }
    }
}
