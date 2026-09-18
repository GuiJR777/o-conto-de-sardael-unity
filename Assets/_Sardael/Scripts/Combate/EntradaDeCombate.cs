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

        InputAction mover;
        InputAction atacar;
        int ataquesPendentes;
        bool habilitouMover;
        bool habilitouAtacar;

        public float MoveX => mover == null ? 0f : mover.ReadValue<Vector2>().x;

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
        }

        void OnDisable()
        {
            if (atacar != null) atacar.performed -= AoAtacar;
            if (habilitouAtacar && atacar != null) atacar.Disable();
            if (habilitouMover && mover != null) mover.Disable();
            habilitouMover = habilitouAtacar = false;
            ataquesPendentes = 0;
        }

        void AoAtacar(InputAction.CallbackContext contexto)
        {
            ataquesPendentes = Mathf.Min(ataquesPendentes + 1, 2);
        }

        public bool ConsumirAtaque()
        {
            if (ataquesPendentes <= 0) return false;
            ataquesPendentes--;
            return true;
        }
    }
}
