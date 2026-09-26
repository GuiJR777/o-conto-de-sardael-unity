using UnityEngine;

namespace Sardael
{
    public sealed class AvisoDeAtaqueInimigo : MonoBehaviour
    {
        TextMesh texto;
        float inicio;
        float duracao;

        public bool Visivel => texto != null && texto.gameObject.activeSelf;
        public float Progresso => !Visivel || duracao <= 0f
            ? 0f
            : Mathf.Clamp01((Time.time - inicio) / duracao);

        void Awake() => GarantirVisual();

        public void Mostrar(float novaDuracao)
        {
            GarantirVisual();
            inicio = Time.time;
            duracao = Mathf.Max(0.05f, novaDuracao);
            texto.gameObject.SetActive(true);
        }

        public void Ocultar()
        {
            if (texto != null) texto.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Visivel) return;
            float pulso = 1f + Mathf.Sin(Time.time * 18f) * 0.14f;
            texto.transform.localScale = Vector3.one * pulso;
            texto.color = Color.Lerp(new Color(1f, 0.85f, 0.05f), Color.red, Progresso);
            if (Camera.main != null) texto.transform.rotation = Camera.main.transform.rotation;
        }

        void GarantirVisual()
        {
            if (texto != null) return;
            var objeto = new GameObject("AvisoDeAtaque");
            objeto.transform.SetParent(transform, false);
            objeto.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            texto = objeto.AddComponent<TextMesh>();
            texto.text = "!";
            texto.anchor = TextAnchor.MiddleCenter;
            texto.alignment = TextAlignment.Center;
            texto.fontSize = 96;
            texto.characterSize = 0.055f;
            texto.fontStyle = FontStyle.Bold;
            texto.color = new Color(1f, 0.85f, 0.05f);
            var renderizador = objeto.GetComponent<MeshRenderer>();
            if (renderizador != null) renderizador.sortingOrder = 50;
            objeto.SetActive(false);
        }
    }
}
