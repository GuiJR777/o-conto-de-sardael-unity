using System.Collections;
using System.Collections.Generic;
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

        [Header("Morte")]
        [Tooltip("Animacoes de morte. Com uma so', ela toca sempre; com varias, uma e'\n"
               + "sorteada a cada morte. Vazio deixa o clipe do estado de morte do controller.")]
        [SerializeField] AnimationClip[] animacoesDeMorte = new AnimationClip[0];
        [Tooltip("O corpo some depois de um tempo apos a morte.")]
        [SerializeField] bool sumirAposMorte = true;
        [SerializeField, Min(0.5f)] float segundosAteSumir = 4f;

        float vidaAtual;
        float poiseAtual;
        bool vivo = true;
        Object reservadoPor;
        Coroutine sumico;

        public bool Valido => isActiveAndEnabled && vivo;
        public bool Vivo => vivo;
        public float VidaAtual => vidaAtual;
        public float VidaMaxima => vidaMaxima;
        public float PoiseAtual => poiseAtual;
        public int ColisoesRecebidas { get; private set; }
        public int AparosRecebidos { get; private set; }
        public MotorDeCombateDoInimigo Motor => motor;
        public bool Reservado => reservadoPor != null;
        public bool Atordoado => motor != null && motor.Atordoado;

        public AnimationClip[] AnimacoesDeMorte
        {
            get => animacoesDeMorte;
            set => animacoesDeMorte = value;
        }

        public bool SumirAposMorte
        {
            get => sumirAposMorte;
            set => sumirAposMorte = value;
        }

        public float SegundosAteSumir
        {
            get => segundosAteSumir;
            set => segundosAteSumir = Mathf.Max(0.5f, value);
        }

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
            reacao?.TocarMorte(SortearAnimacaoDeMorte());
            motor?.Interromper();
            foreach (var colisor in GetComponentsInChildren<Collider>()) colisor.enabled = false;
            if (sumirAposMorte && sumico == null) sumico = StartCoroutine(SumirDepois());
        }

        AnimationClip SortearAnimacaoDeMorte()
        {
            if (animacoesDeMorte == null || animacoesDeMorte.Length == 0) return null;
            var validos = new List<AnimationClip>(animacoesDeMorte.Length);
            for (int i = 0; i < animacoesDeMorte.Length; i++)
                if (animacoesDeMorte[i] != null) validos.Add(animacoesDeMorte[i]);
            if (validos.Count == 0) return null;
            return validos[Random.Range(0, validos.Count)];
        }

        IEnumerator SumirDepois()
        {
            yield return new WaitForSeconds(segundosAteSumir);
            sumico = null;
            Destroy(gameObject);
        }
    }
}
