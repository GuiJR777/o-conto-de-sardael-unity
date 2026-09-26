using UnityEngine;

namespace Sardael
{
    public sealed class AlvoDeCombate : MonoBehaviour
    {
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] ReacaoDeCombate reacao;
        [SerializeField] MotorDeCombateDoInimigo motor;
        [SerializeField, Min(1f)] float vidaMaxima = 100f;
        [SerializeField, Min(0f)] float poiseMaximo = 100f;
        [SerializeField, Min(0.1f)] float duracaoDoStun = 2.2f;

        float vidaAtual;
        float poiseAtual;
        bool vivo = true;
        Object reservadoPor;

        public bool Valido => isActiveAndEnabled && vivo;
        public bool Vivo => vivo;
        public float VidaAtual => vidaAtual;
        public float PoiseAtual => poiseAtual;
        public int ColisoesRecebidas { get; private set; }
        public int AparosRecebidos { get; private set; }
        public MotorDeCombateDoInimigo Motor => motor;
        public bool Reservado => reservadoPor != null;
        public bool Atordoado => motor != null && motor.Atordoado;

        public void Configurar(
            RegistroDeCombate novoRegistro,
            ReacaoDeCombate novaReacao,
            MotorDeCombateDoInimigo novoMotor = null)
        {
            if (registro != null) registro.Desregistrar(this);
            registro = novoRegistro;
            reacao = novaReacao;
            motor = novoMotor;
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

        public void ReceberImpacto(DefinicaoDeAtaque ataque, Transform atacante = null)
        {
            if (!Valido || ataque == null) return;
            AplicarDanoEPoise(ataque.dano, ataque.poise, atacante, ataque);
            if (!Valido) return;
            motor?.AplicarDeslocamento(ataque, atacante);
        }

        public void ReceberContraAtaque(float dano, float poise, Transform atacante)
        {
            if (!Valido) return;
            AplicarDanoEPoise(dano, poise, atacante, null);
        }

        public void ReceberAparo()
        {
            if (!Valido) return;
            AparosRecebidos++;
            motor?.PerderVezDeAtaque();
            reacao?.TocarAparo();
        }

        void AplicarDanoEPoise(float dano, float poise, Transform atacante, DefinicaoDeAtaque ataque)
        {
            motor?.PerderVezDeAtaque();
            vidaAtual = Mathf.Max(0f, vidaAtual - Mathf.Max(0f, dano));
            poiseAtual = Mathf.Max(0f, poiseAtual - Mathf.Max(0f, poise));
            if (vidaAtual <= 0f)
            {
                Morrer();
                return;
            }

            if (poiseAtual <= 0f)
            {
                reacao?.TocarAtordoado();
                motor?.Atordoar(duracaoDoStun);
            }
            else if (ataque != null) reacao?.Tocar(ataque);
            else reacao?.TocarColisao(false);
        }

        public void RecuperarPoise() => poiseAtual = poiseMaximo;

        public void ReceberColisaoEmCadeia(bool derrubado)
        {
            if (!Valido) return;
            ColisoesRecebidas++;
            reacao?.TocarColisao(derrubado);
        }

        public bool TentarReservar(Object solicitante)
        {
            if (!Valido || solicitante == null || (reservadoPor != null && reservadoPor != solicitante))
                return false;
            reservadoPor = solicitante;
            return true;
        }

        public void LiberarReserva(Object solicitante)
        {
            if (reservadoPor == solicitante) reservadoPor = null;
        }

        public void Morrer()
        {
            if (!vivo) return;
            vivo = false;
            reservadoPor = null;
            registro?.Desregistrar(this);
            reacao?.TocarMorte();
            motor?.Interromper();
            foreach (var colisor in GetComponentsInChildren<Collider>()) colisor.enabled = false;
        }
    }
}
