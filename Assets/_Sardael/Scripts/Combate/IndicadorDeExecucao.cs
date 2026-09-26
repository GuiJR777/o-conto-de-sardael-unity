using UnityEngine;

namespace Sardael
{
    public sealed class IndicadorDeExecucao : MonoBehaviour
    {
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
                jogador != null && Mathf.Abs(jogador.position.x - transform.position.x) <= alcance;
            if (texto.gameObject.activeSelf != mostrar) texto.gameObject.SetActive(mostrar);
            if (!mostrar) return;

            float pulso = 1f + Mathf.Sin(Time.time * 8f) * 0.08f;
            texto.transform.localScale = Vector3.one * pulso;
            texto.transform.localPosition = new Vector3(0f, 2.55f + Mathf.Sin(Time.time * 5f) * 0.05f, 0f);
            if (Camera.main != null) texto.transform.rotation = Camera.main.transform.rotation;
        }

        void GarantirVisual()
        {
            if (texto != null) return;
            var objeto = new GameObject("IndicadorDeExecucao");
            objeto.transform.SetParent(transform, false);
            objeto.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            texto = objeto.AddComponent<TextMesh>();
            texto.text = "E";
            texto.anchor = TextAnchor.MiddleCenter;
            texto.alignment = TextAlignment.Center;
            texto.fontSize = 82;
            texto.characterSize = 0.045f;
            texto.fontStyle = FontStyle.Bold;
            texto.color = new Color(0.3f, 1f, 0.45f);
            var renderizador = objeto.GetComponent<MeshRenderer>();
            if (renderizador != null) renderizador.sortingOrder = 55;
            objeto.SetActive(false);
        }
    }
}
