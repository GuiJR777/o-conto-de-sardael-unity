using UnityEngine;

namespace Sardael
{
    public sealed class ResolvedorDeImpacto : MonoBehaviour
    {
        [SerializeField, Min(0f)] float tolerancia = 0.35f;

        public bool Resolver(DefinicaoDeAtaque ataque, Transform atacante, AlvoDeCombate alvo)
        {
            if (ataque == null || atacante == null || alvo == null || !alvo.Valido) return false;
            float distancia = Mathf.Abs(alvo.transform.position.x - atacante.position.x);
            if (distancia > ataque.alcance + tolerancia) return false;
            alvo.ReceberImpacto(ataque);
            return true;
        }
    }
}
