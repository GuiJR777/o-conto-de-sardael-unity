using UnityEngine;
using UnityEngine.InputSystem;

namespace Sardael
{
    /// <summary>Traduz Input Actions em intencoes de gameplay.</summary>
    public sealed class EntradaDeCombate : MonoBehaviour
    {
        [SerializeField] InputActionAsset acoes;
        [SerializeField] string mapa = "Player";
        [SerializeField] string acaoMover = "Move";
        [SerializeField] string acaoAtacar = "Attack";
        [SerializeField] string acaoInteragir = "Interact";
        [SerializeField] string acaoExecutar = "Execution";
        [SerializeField] string acaoEspecial = "FlowSpecial";
        [SerializeField] string acaoBloquear = "Block";
        [SerializeField] string acaoEsquivar = "Dodge";
        [SerializeField] string acaoPular = "Jump";
        [SerializeField] string acaoCorrer = "Sprint";

        InputAction mover;
        InputAction atacar;
        InputAction interagir;
        InputAction executar;
        InputAction especial;
        InputAction bloquear;
        InputAction esquivar;
        InputAction pular;
        InputAction correr;
        int ataquesPendentes;
        int interacoesPendentes;
        int execucoesPendentes;
        int especiaisPendentes;
        int esquivasPendentes;
        int pulosPendentes;
        bool habilitouMover;
        bool habilitouAtacar;
        bool habilitouInteragir;
        bool habilitouExecutar;
        bool habilitouEspecial;
        bool habilitouBloquear;
        bool habilitouEsquivar;
        bool habilitouPular;
        bool habilitouCorrer;

        public Vector2 Movimento => mover == null ? Vector2.zero : mover.ReadValue<Vector2>();
        public float MoveX => Movimento.x;
        public bool Bloqueando => bloquear != null && bloquear.IsPressed();
        public bool Correndo => correr != null && correr.IsPressed();

        public void Configurar(InputActionAsset asset)
        {
            acoes = asset;
            ResolverAcoes();
        }

        void Awake() => ResolverAcoes();

        void ResolverAcoes()
        {
            if (acoes == null) return;
            mover = acoes.FindAction(mapa + "/" + acaoMover, false);
            atacar = acoes.FindAction(mapa + "/" + acaoAtacar, false);
            interagir = acoes.FindAction(mapa + "/" + acaoInteragir, false);
            executar = acoes.FindAction(mapa + "/" + acaoExecutar, false);
            especial = acoes.FindAction(mapa + "/" + acaoEspecial, false);
            bloquear = acoes.FindAction(mapa + "/" + acaoBloquear, false);
            esquivar = acoes.FindAction(mapa + "/" + acaoEsquivar, false);
            pular = acoes.FindAction(mapa + "/" + acaoPular, false);
            correr = acoes.FindAction(mapa + "/" + acaoCorrer, false);
        }

        void OnEnable()
        {
            ResolverAcoes();
            if (mover != null && !mover.enabled) { mover.Enable(); habilitouMover = true; }
            if (atacar != null)
            {
                atacar.performed += AoAtacar;
                if (!atacar.enabled) { atacar.Enable(); habilitouAtacar = true; }
            }
            if (interagir != null)
            {
                // Player/Interact usa a interacao Hold no asset geral. A execucao contextual,
                // porem, deve responder ao toque em E, nao exigir que a tecla seja segurada.
                interagir.started += AoInteragir;
                if (!interagir.enabled) { interagir.Enable(); habilitouInteragir = true; }
            }
            if (executar != null)
            {
                executar.performed += AoExecutar;
                if (!executar.enabled) { executar.Enable(); habilitouExecutar = true; }
            }
            if (especial != null)
            {
                especial.performed += AoEspecial;
                if (!especial.enabled) { especial.Enable(); habilitouEspecial = true; }
            }
            if (bloquear != null && !bloquear.enabled) { bloquear.Enable(); habilitouBloquear = true; }
            if (esquivar != null)
            {
                esquivar.performed += AoEsquivar;
                if (!esquivar.enabled) { esquivar.Enable(); habilitouEsquivar = true; }
            }
            if (pular != null)
            {
                pular.performed += AoPular;
                if (!pular.enabled) { pular.Enable(); habilitouPular = true; }
            }
            if (correr != null && !correr.enabled) { correr.Enable(); habilitouCorrer = true; }
        }

        void OnDisable()
        {
            if (atacar != null) atacar.performed -= AoAtacar;
            if (interagir != null) interagir.started -= AoInteragir;
            if (executar != null) executar.performed -= AoExecutar;
            if (especial != null) especial.performed -= AoEspecial;
            if (esquivar != null) esquivar.performed -= AoEsquivar;
            if (pular != null) pular.performed -= AoPular;
            if (habilitouAtacar && atacar != null) atacar.Disable();
            if (habilitouInteragir && interagir != null) interagir.Disable();
            if (habilitouExecutar && executar != null) executar.Disable();
            if (habilitouEspecial && especial != null) especial.Disable();
            if (habilitouBloquear && bloquear != null) bloquear.Disable();
            if (habilitouEsquivar && esquivar != null) esquivar.Disable();
            if (habilitouPular && pular != null) pular.Disable();
            if (habilitouCorrer && correr != null) correr.Disable();
            if (habilitouMover && mover != null) mover.Disable();
            habilitouMover = habilitouAtacar = habilitouInteragir = habilitouExecutar =
                habilitouEspecial = habilitouBloquear = habilitouEsquivar = habilitouPular =
                habilitouCorrer = false;
            ataquesPendentes = interacoesPendentes = execucoesPendentes = especiaisPendentes =
                esquivasPendentes = pulosPendentes = 0;
        }

        void AoAtacar(InputAction.CallbackContext contexto)
        {
            ataquesPendentes = Mathf.Min(ataquesPendentes + 1, 2);
        }

        void AoInteragir(InputAction.CallbackContext contexto) => interacoesPendentes = 1;
        void AoExecutar(InputAction.CallbackContext contexto) => execucoesPendentes = 1;
        void AoEspecial(InputAction.CallbackContext contexto) => especiaisPendentes = 1;
        void AoEsquivar(InputAction.CallbackContext contexto) => esquivasPendentes = 1;
        void AoPular(InputAction.CallbackContext contexto) => pulosPendentes = 1;

        public bool ConsumirAtaque()
        {
            if (ataquesPendentes <= 0) return false;
            ataquesPendentes--;
            return true;
        }

        public bool ConsumirExecucao()
        {
            if (execucoesPendentes <= 0) return false;
            execucoesPendentes = 0;
            return true;
        }

        public bool ConsumirInteracao()
        {
            if (interacoesPendentes <= 0) return false;
            interacoesPendentes = 0;
            return true;
        }

        public void CancelarAtaquesPendentes() => ataquesPendentes = 0;

        public bool ConsumirEspecial()
        {
            if (especiaisPendentes <= 0) return false;
            especiaisPendentes = 0;
            return true;
        }

        public bool ConsumirEsquiva()
        {
            if (esquivasPendentes <= 0) return false;
            esquivasPendentes = 0;
            return true;
        }

        public bool ConsumirPulo()
        {
            if (pulosPendentes <= 0) return false;
            pulosPendentes = 0;
            return true;
        }
    }
}
