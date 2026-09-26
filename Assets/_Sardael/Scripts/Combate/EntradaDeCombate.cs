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

        InputAction mover;
        InputAction atacar;
        InputAction interagir;
        InputAction executar;
        InputAction especial;
        InputAction bloquear;
        int ataquesPendentes;
        int interacoesPendentes;
        int execucoesPendentes;
        int especiaisPendentes;
        bool habilitouMover;
        bool habilitouAtacar;
        bool habilitouInteragir;
        bool habilitouExecutar;
        bool habilitouEspecial;
        bool habilitouBloquear;

        public float MoveX => mover == null ? 0f : mover.ReadValue<Vector2>().x;
        public bool Bloqueando => bloquear != null && bloquear.IsPressed();

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
        }

        void OnDisable()
        {
            if (atacar != null) atacar.performed -= AoAtacar;
            if (interagir != null) interagir.started -= AoInteragir;
            if (executar != null) executar.performed -= AoExecutar;
            if (especial != null) especial.performed -= AoEspecial;
            if (habilitouAtacar && atacar != null) atacar.Disable();
            if (habilitouInteragir && interagir != null) interagir.Disable();
            if (habilitouExecutar && executar != null) executar.Disable();
            if (habilitouEspecial && especial != null) especial.Disable();
            if (habilitouBloquear && bloquear != null) bloquear.Disable();
            if (habilitouMover && mover != null) mover.Disable();
            habilitouMover = habilitouAtacar = habilitouInteragir = habilitouExecutar =
                habilitouEspecial = habilitouBloquear = false;
            ataquesPendentes = interacoesPendentes = execucoesPendentes = especiaisPendentes = 0;
        }

        void AoAtacar(InputAction.CallbackContext contexto)
        {
            ataquesPendentes = Mathf.Min(ataquesPendentes + 1, 2);
        }

        void AoInteragir(InputAction.CallbackContext contexto) => interacoesPendentes = 1;
        void AoExecutar(InputAction.CallbackContext contexto) => execucoesPendentes = 1;
        void AoEspecial(InputAction.CallbackContext contexto) => especiaisPendentes = 1;

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
    }
}
