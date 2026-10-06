using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    [Serializable]
    public sealed class SubstituicaoDeAnimacao
    {
        [SerializeField] string caminhoDoEstado;
        [SerializeField] string nomeDoEstado;
        [SerializeField] AnimationClip clipeOriginal;
        [SerializeField] AnimationClip clipeSubstituto;
        [SerializeField] bool tocarAoContrario;
        [SerializeField] AnimationClip clipeReversoGerado;
        [SerializeField, HideInInspector] string assinaturaDaFonteReversa;
        [SerializeField, HideInInspector] string erroDoClipeReverso;

        public string CaminhoDoEstado => caminhoDoEstado;
        public string NomeDoEstado => nomeDoEstado;
        public AnimationClip ClipeOriginal => clipeOriginal;
        public AnimationClip ClipeSubstituto => clipeSubstituto;
        public bool TocarAoContrario => tocarAoContrario;
        public AnimationClip ClipeReversoGerado => clipeReversoGerado;
        public AnimationClip ClipeEfetivo => tocarAoContrario && clipeReversoGerado != null
            ? clipeReversoGerado
            : (clipeSubstituto != null ? clipeSubstituto : clipeOriginal);
    }

    /// <summary>
    /// Centraliza todas as trocas de clipe de um Animator. Os slots sao sincronizados
    /// pelo Inspector usando a referencia do clipe original, nunca o nome do estado ou
    /// o nome visivel do clipe (varios FBXs usam o mesmo nome "Unreal Take").
    /// </summary>
    [AddComponentMenu("Sardael/Configuracao de Animacoes do Heroi")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(-100)]
    public sealed class TrocadorDeAnimacoes : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField, HideInInspector] RuntimeAnimatorController controladorSincronizado;
        [SerializeField, HideInInspector] List<SubstituicaoDeAnimacao> substituicoes =
            new List<SubstituicaoDeAnimacao>();

        AnimatorOverrideController sobrescrita;
        RuntimeAnimatorController controladorBase;

        public Animator Animator => animator;
        public RuntimeAnimatorController ControladorSincronizado => controladorSincronizado;
        public IReadOnlyList<SubstituicaoDeAnimacao> Substituicoes => substituicoes;

        public void Configurar(Animator novoAnimator)
        {
            animator = novoAnimator;
            if (Application.isPlaying) AplicarConfiguracao();
        }

        void Reset()
        {
            animator = GetComponent<Animator>();
        }

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            AplicarConfiguracao();
        }

        public void AplicarConfiguracao()
        {
            if (!GarantirSobrescrita()) return;

            var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            sobrescrita.GetOverrides(pares);
            for (int i = 0; i < pares.Count; i++)
            {
                var slot = EncontrarPorClipeOriginal(pares[i].Key);
                if (slot == null) continue;
                pares[i] = new KeyValuePair<AnimationClip, AnimationClip>(
                    pares[i].Key,
                    slot.ClipeEfetivo != null ? slot.ClipeEfetivo : pares[i].Key);
            }

            sobrescrita.ApplyOverrides(pares);
            if (animator.runtimeAnimatorController != sobrescrita)
                animator.runtimeAnimatorController = sobrescrita;
        }

        /// <summary>
        /// API de compatibilidade para sistemas que precisem trocar um estado durante o jogo.
        /// Resolve o estado para o clipe-base sincronizado e aplica a troca pela referencia.
        /// Clipe nulo restaura a escolha serializada no Inspector (ou o original).
        /// </summary>
        public bool Sobrescrever(string nomeDoEstado, AnimationClip clipe)
        {
            if (string.IsNullOrEmpty(nomeDoEstado) || !GarantirSobrescrita()) return false;

            bool encontrou = false;
            for (int i = 0; i < substituicoes.Count; i++)
            {
                var slot = substituicoes[i];
                if (slot == null || slot.ClipeOriginal == null ||
                    !string.Equals(slot.NomeDoEstado, nomeDoEstado, StringComparison.Ordinal))
                    continue;

                sobrescrita[slot.ClipeOriginal] = clipe != null
                    ? clipe
                    : (slot.ClipeEfetivo != null ? slot.ClipeEfetivo : slot.ClipeOriginal);
                encontrou = true;
            }
            return encontrou;
        }

        bool GarantirSobrescrita()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) return false;

            var atual = animator.runtimeAnimatorController;
            var baseDoAtual = Desembrulhar(atual);
            if (baseDoAtual == null) return false;

            var baseSincronizada = Desembrulhar(controladorSincronizado);
            if (baseSincronizada != null && baseSincronizada != baseDoAtual) return false;

            if (sobrescrita != null && controladorBase == baseDoAtual)
            {
                if (animator.runtimeAnimatorController != sobrescrita)
                    animator.runtimeAnimatorController = sobrescrita;
                return true;
            }

            var anterior = atual as AnimatorOverrideController;
            sobrescrita = new AnimatorOverrideController(baseDoAtual)
            {
                name = name + "_Animacoes"
            };
            controladorBase = baseDoAtual;

            if (anterior != null)
            {
                var paresAnteriores = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                anterior.GetOverrides(paresAnteriores);
                sobrescrita.ApplyOverrides(paresAnteriores);
            }

            animator.runtimeAnimatorController = sobrescrita;
            return true;
        }

        SubstituicaoDeAnimacao EncontrarPorClipeOriginal(AnimationClip original)
        {
            if (original == null || substituicoes == null) return null;
            for (int i = 0; i < substituicoes.Count; i++)
            {
                var slot = substituicoes[i];
                if (slot != null && slot.ClipeOriginal == original) return slot;
            }
            return null;
        }

        static RuntimeAnimatorController Desembrulhar(RuntimeAnimatorController controller)
        {
            while (controller is AnimatorOverrideController overrideController &&
                   overrideController.runtimeAnimatorController != null)
                controller = overrideController.runtimeAnimatorController;
            return controller;
        }
    }
}
