using System.Collections;
using UnityEngine;

namespace Sardael
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EncontroDeCombate : MonoBehaviour
    {
        [SerializeField] ModoDeCombate modo;
        [SerializeField] AlvoDeCombate[] inimigos;
        [SerializeField] bool iniciarAoEntrar = true;

        public bool Iniciado { get; private set; }
        public bool Concluido { get; private set; }
        public bool Derrotado { get; private set; }
        public Collider Volume => GetComponent<Collider>();
        public Bounds LimitesMundiais => Volume == null
            ? new Bounds(transform.position, Vector3.zero)
            : Volume.bounds;
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

        public void Configurar(
            ModoDeCombate novoModo,
            AlvoDeCombate[] novosInimigos,
            bool novoIniciarAoEntrar = true)
        {
            modo = novoModo;
            inimigos = novosInimigos;
            iniciarAoEntrar = novoIniciarAoEntrar;
            var volume = GetComponent<Collider>();
            if (volume != null) volume.isTrigger = true;
        }

        void Awake()
        {
            if (!Iniciado) DefinirCombatentesAtivos(false);
        }

        void OnTriggerEnter(Collider outro)
        {
            if (!iniciarAoEntrar || Iniciado || Concluido || Derrotado || modo == null || outro == null)
                return;
            if (outro.GetComponentInParent<MovimentoDoHeroi>() == null) return;
            Iniciar();
        }

        public bool Iniciar()
        {
            if (Iniciado || Concluido || Derrotado || modo == null) return false;
            if (inimigos == null || inimigos.Length == 0)
            {
                Debug.LogError("EncontroDeCombate sem inimigos configurados.", this);
                return false;
            }

            DefinirCombatentesAtivos(true);
            if (QuantidadeVivos == 0)
            {
                Debug.LogError("EncontroDeCombate nao possui inimigo valido para iniciar.", this);
                DefinirCombatentesAtivos(false);
                return false;
            }

            if (modo.IniciarEncontro(this)) return true;
            DefinirCombatentesAtivos(false);
            return false;
        }

        public IEnumerator EsperarFim()
        {
            while (!Concluido && !Derrotado) yield return null;
        }

        public void MarcarIniciado() => Iniciado = true;

        public void MarcarConcluido()
        {
            Concluido = true;
            var volume = GetComponent<Collider>();
            if (volume != null) volume.enabled = false;
        }

        public void MarcarDerrotado()
        {
            Derrotado = true;
        }

        void DefinirCombatentesAtivos(bool ativos)
        {
            if (inimigos == null) return;
            for (int i = 0; i < inimigos.Length; i++)
            {
                var alvo = inimigos[i];
                if (alvo == null) continue;
                if (ativos && !alvo.gameObject.activeSelf) alvo.gameObject.SetActive(true);

                var motor = alvo.GetComponent<MotorDeCombateDoInimigo>();
                var controlador = alvo.GetComponent<CharacterController>();
                var barra = alvo.GetComponent<BarraDeVidaDoInimigo>();
                if (ativos)
                {
                    DesativarLegado(alvo.gameObject);
                    if (controlador != null) controlador.enabled = true;
                    alvo.enabled = true;
                    if (motor != null) motor.enabled = true;
                    if (barra != null) barra.Visivel = true;

                    var animator = alvo.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.speed = MovimentoDoHeroi.VELOCIDADE_DAS_ANIMACOES;
                        animator.Rebind();
                        animator.Update(0f);
                    }
                }
                else
                {
                    if (motor != null) motor.enabled = false;
                    alvo.enabled = false;
                    if (controlador != null) controlador.enabled = false;
                    if (barra != null) barra.Visivel = false;
                }
            }
        }

        static void DesativarLegado(GameObject raiz)
        {
            foreach (var tocador in raiz.GetComponentsInChildren<TocadorDeClipe>(true))
                tocador.enabled = false;
            foreach (var combatente in raiz.GetComponentsInChildren<Combatente>(true))
                combatente.enabled = false;
            foreach (var corpo in raiz.GetComponentsInChildren<CorpoDoOrc>(true))
                corpo.enabled = false;
            foreach (var duelo in raiz.GetComponentsInChildren<Duelo>(true))
                duelo.enabled = false;
        }
    }
}
