using UnityEngine;

namespace Sardael
{
    public sealed class SistemaDeFlow : MonoBehaviour
    {
        [SerializeField, Min(1f)] float maxFlow = 100f;
        [SerializeField, Min(0f)] float flowAtual;
        [SerializeField, Min(0f)] float decayDelay = 3f;
        [SerializeField, Min(0f)] float decayRate = 6f;
        [SerializeField, Min(0f)] float damagePenalty = 25f;
        [SerializeField, Min(0f)] float bonusPorAlvoExtra = 2.5f;
        [SerializeField, Min(0f)] float multiplicadorDeParry = 1.25f;

        float ultimaOfensiva = float.NegativeInfinity;

        public float Atual => flowAtual;
        public float Maximo => maxFlow;
        public float Normalizado => maxFlow <= 0f ? 0f : flowAtual / maxFlow;
        public bool Cheio => flowAtual >= maxFlow - 0.001f;
        public float UltimoGanho { get; private set; }
        public float TotalGerado { get; private set; }

        void Update()
        {
            if (flowAtual <= 0f || Time.time - ultimaOfensiva < decayDelay) return;
            flowAtual = Mathf.MoveTowards(flowAtual, 0f, decayRate * Time.deltaTime);
        }

        public void RegistrarAcerto(DefinicaoDeAtaque ataque, int quantidadeDeAlvos)
        {
            if (ataque == null || quantidadeDeAlvos <= 0) return;
            float bonus = Mathf.Max(0, quantidadeDeAlvos - 1) * bonusPorAlvoExtra;
            Adicionar(ataque.flowGerado + bonus);
        }

        public void RegistrarParry(float baseFlow)
        {
            Adicionar(Mathf.Max(0f, baseFlow) * multiplicadorDeParry);
        }

        public void RegistrarDanoRecebido(float multiplicador = 1f)
        {
            flowAtual = Mathf.Max(0f, flowAtual - damagePenalty * Mathf.Max(0f, multiplicador));
        }

        public void Adicionar(float quantidade)
        {
            if (quantidade <= 0f) return;
            UltimoGanho = quantidade;
            TotalGerado += quantidade;
            flowAtual = Mathf.Clamp(flowAtual + quantidade, 0f, maxFlow);
            ultimaOfensiva = Time.time;
        }

        public bool ConsumirCheio()
        {
            if (!Cheio) return false;
            flowAtual = 0f;
            return true;
        }
    }
}
