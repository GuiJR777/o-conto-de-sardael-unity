using UnityEngine;

namespace Sardael
{
    public sealed class AlvoDeCombate : MonoBehaviour
    {
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] ReacaoDeCombate reacao;
        [SerializeField, Min(1f)] float vidaMaxima = 100f;
        [SerializeField, Min(0f)] float poiseMaximo = 100f;

        float vidaAtual;
        float poiseAtual;
        bool vivo = true;

        public bool Valido => isActiveAndEnabled && vivo;
        public bool Vivo => vivo;
        public float VidaAtual => vidaAtual;

        public void Configurar(RegistroDeCombate novoRegistro, ReacaoDeCombate novaReacao)
        {
            if (registro != null) registro.Desregistrar(this);
            registro = novoRegistro;
            reacao = novaReacao;
            if (isActiveAndEnabled && registro != null) registro.Registrar(this);
        }

        void Awake()
        {
            vidaAtual = vidaMaxima;
            poiseAtual = poiseMaximo;
        }

        void Start()
        {
            if (registro != null) registro.Registrar(this);
        }

        void OnEnable()
        {
            if (registro != null) registro.Registrar(this);
        }

        void OnDisable()
        {
            if (registro != null) registro.Desregistrar(this);
        }

        public void ReceberImpacto(DefinicaoDeAtaque ataque)
        {
            if (!Valido || ataque == null) return;
            vidaAtual -= ataque.dano;
            poiseAtual -= ataque.poise;
            reacao?.Tocar(ataque);
            if (vidaAtual <= 0f) Morrer();
        }

        public void Morrer()
        {
            if (!vivo) return;
            vivo = false;
            registro?.Desregistrar(this);
            foreach (var colisor in GetComponentsInChildren<Collider>()) colisor.enabled = false;
        }
    }
}
