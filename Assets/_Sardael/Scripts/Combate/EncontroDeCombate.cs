using UnityEngine;

namespace Sardael
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EncontroDeCombate : MonoBehaviour
    {
        [SerializeField] ModoDeCombate modo;
        [SerializeField] AlvoDeCombate[] inimigos;

        public bool Iniciado { get; private set; }
        public bool Concluido { get; private set; }
        public int QuantidadeVivos
        {
            get
            {
                if (inimigos == null) return 0;
                int vivos = 0;
                for (int i = 0; i < inimigos.Length; i++)
                    if (inimigos[i] != null && inimigos[i].Valido) vivos++;
                return vivos;
            }
        }
        public bool TodosMortos => Iniciado && QuantidadeVivos == 0;

        public void Configurar(ModoDeCombate novoModo, AlvoDeCombate[] novosInimigos)
        {
            modo = novoModo;
            inimigos = novosInimigos;
            var volume = GetComponent<Collider>();
            if (volume != null) volume.isTrigger = true;
        }

        void OnTriggerEnter(Collider outro)
        {
            if (Iniciado || Concluido || modo == null || outro == null) return;
            if (outro.GetComponentInParent<MovimentoDoHeroi>() == null) return;
            modo.IniciarEncontro(this);
        }

        public void MarcarIniciado() => Iniciado = true;

        public void MarcarConcluido()
        {
            Concluido = true;
            var volume = GetComponent<Collider>();
            if (volume != null) volume.enabled = false;
        }
    }
}
