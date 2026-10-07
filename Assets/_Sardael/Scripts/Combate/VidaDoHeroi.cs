using System.Collections;
using UnityEngine;

namespace Sardael
{
    [DefaultExecutionOrder(-60)]
    public sealed class VidaDoHeroi : MonoBehaviour
    {
        [SerializeField, Min(1f)] float vidaMaxima = 100f;
        [SerializeField] MovimentoDoHeroi movimento;
        [SerializeField] Animator animator;
        [SerializeField] SistemaDeFlow flow;
        [SerializeField] DiretorDeCombate diretor;
        [SerializeField] DefesaDoHeroi defesa;

        Coroutine reacao;
        float vidaAtual;
        float invulneravelAte;
        bool vivo = true;

        static readonly int Atingido = Animator.StringToHash("atingido");
        static readonly int Morrer = Animator.StringToHash("morrer");

        public float Atual => vidaAtual;
        public float Maxima => vidaMaxima;
        public float Normalizada => vidaMaxima <= 0f ? 0f : vidaAtual / vidaMaxima;
        public bool Vivo => vivo;
        public bool Invulneravel => vivo && Time.time < invulneravelAte;
        public int GolpesRecebidos { get; private set; }
        public int GolpesBloqueados { get; private set; }

        public void Configurar(
            MovimentoDoHeroi novoMovimento,
            Animator novoAnimator,
            SistemaDeFlow novoFlow,
            DiretorDeCombate novoDiretor,
            DefesaDoHeroi novaDefesa)
        {
            movimento = novoMovimento;
            animator = novoAnimator;
            flow = novoFlow;
            diretor = novoDiretor;
            defesa = novaDefesa;
        }

        void Awake()
        {
            if (movimento == null) movimento = GetComponent<MovimentoDoHeroi>();
            if (animator == null) animator = GetComponent<Animator>();
            if (defesa == null) defesa = GetComponent<DefesaDoHeroi>();
            vidaAtual = vidaMaxima;
        }

        public bool ReceberAtaque(MotorDeCombateDoInimigo atacante, float dano)
        {
            if (!vivo || Invulneravel) return false;
            if (defesa != null && defesa.TentarBloquear(atacante))
            {
                GolpesBloqueados++;
                return false;
            }

            GolpesRecebidos++;
            vidaAtual = Mathf.Max(0f, vidaAtual - Mathf.Max(0f, dano));
            flow?.RegistrarDanoRecebido();
            if (vidaAtual <= 0f)
            {
                MorrerAgora();
                return true;
            }

            if (animator != null) animator.SetTrigger(Atingido);
            if (reacao != null) StopCoroutine(reacao);
            reacao = StartCoroutine(TravarDuranteReacao(0.5f));
            return true;
        }

        public void Restaurar()
        {
            vivo = true;
            vidaAtual = vidaMaxima;
            invulneravelAte = 0f;
            defesa?.Reativar();
            movimento?.Travar(this, false);
            if (animator != null) animator.Play("Locomocao", 0, 0f);
        }

        public void ConcederInvulnerabilidade(float duracao)
        {
            if (!vivo) return;
            invulneravelAte = Mathf.Max(invulneravelAte, Time.time + Mathf.Max(0f, duracao));
        }

        void MorrerAgora()
        {
            if (!vivo) return;
            vivo = false;
            defesa?.Cancelar();
            movimento?.Travar(this, true);
            diretor?.DefinirAtivo(false);
            if (animator != null) animator.SetTrigger(Morrer);
        }

        IEnumerator TravarDuranteReacao(float tempo)
        {
            movimento?.Travar(this, true);
            yield return new WaitForSeconds(tempo);
            if (vivo) movimento?.Travar(this, false);
            reacao = null;
        }
    }
}
