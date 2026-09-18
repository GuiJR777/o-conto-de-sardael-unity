using UnityEngine;

namespace Sardael
{
    public enum PadraoDeAlvo
    {
        Single,
        FrontTwo,
        FrontThree,
        BothSides,
        Piercing
    }

    public enum TipoDeDeslocamento
    {
        Nenhum,
        Push,
        Pull,
        Launch,
        CrossSide,
        KnockThrough,
        PushPlayer
    }

    [CreateAssetMenu(menuName = "Sardael/Combate/Definicao de Ataque")]
    public sealed class DefinicaoDeAtaque : ScriptableObject
    {
        public string id;
        [Range(1, 4)] public int eloCombo = 1;
        [Min(0f)] public float alcance = 1.756f;
        [Min(0f)] public float alcanceMagnetismo = 3.2f;
        [Min(0f)] public float distanciaDesejada = 1.45f;
        [Min(0f)] public float velocidadeAproximacao = 7f;
        [Min(0f)] public float dano = 20f;
        [Min(0f)] public float poise = 20f;
        public PadraoDeAlvo padraoDeAlvo = PadraoDeAlvo.Single;
        public TipoDeDeslocamento deslocamento = TipoDeDeslocamento.Nenhum;
        [Min(0f)] public float flowGerado = 10f;
        [Range(0f, 1f), Tooltip("Momento normalizado do impacto no estado do Animator.")]
        public float momentoDoImpacto = 0.45f;
    }
}
