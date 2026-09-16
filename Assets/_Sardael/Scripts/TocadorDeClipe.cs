using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sardael
{
    /// <summary>
    /// Toca UM clipe de animacao em repeticao, sem precisar de AnimatorController.
    ///
    /// Numa bancada de teste com dezenas de estacoes, criar um controller por clipe seria
    /// dezenas de assets so' pra ver a animacao rodar. O Playables API liga o clipe direto
    /// no Animator: e' o mesmo caminho que o Animator usa por dentro, so' que sem a maquina
    /// de estados no meio.
    ///
    /// O GRAFO E' UM SO' E NUNCA E' DESTRUIDO EM TROCA DE CLIPE. A versao antiga refazia o
    /// grafo a cada <see cref="Trocar"/>, e no quadro em que o Animator ficava sem saida ele
    /// caia na pose de bind: dava um piscar de T-pose de um quadro em TODA troca de animacao.
    /// Aqui o grafo tem um mixer de duas entradas e a troca so' passa o peso de uma pra
    /// outra — sem buraco, e de quebra com mistura.
    ///
    /// Clipe que nao e' de loop (um ataque, uma morte) volta ao inicio depois de uma pausa,
    /// pra dar tempo de ler a pose final antes de repetir.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class TocadorDeClipe : MonoBehaviour
    {
        public AnimationClip clipe;
        [Range(0.1f, 2f)] public float velocidade = 1f;
        [Tooltip("Segundos parado na pose final antes de recomecar (so' pra clipe sem loop).")]
        public float pausaNoFim = 0.6f;
        [Tooltip("Para no ultimo quadro e nao volta mais. E' o que um corpo caido precisa: "
               + "repetir a queda em laco faz o morto levantar e cair de novo.")]
        public bool congelarNoFim = false;
        [Tooltip("Em que segundo do clipe este comeca. Um campo cheio de gente com o mesmo "
               + "clipe vira um corpo de baile se todos partirem do quadro zero.")]
        public float atraso = 0f;
        [Tooltip("Segundos de mistura ao trocar de clipe. ZERO CORTA SECO — e' o que uma "
               + "cadeia de clipes pareados precisa, porque neles a raiz salta junto com a "
               + "troca e misturar faria o corpo escorregar.")]
        public float suavidadeDaTroca = 0.14f;

        PlayableGraph grafo;
        AnimationMixerPlayable mistura;
        readonly AnimationClipPlayable[] canais = new AnimationClipPlayable[2];
        int vivo;
        float peso, duracaoDaTroca;

        void OnEnable() { Montar(); }

        void OnDisable() { if (grafo.IsValid()) grafo.Destroy(); }

        void Montar()
        {
            if (clipe == null) return;
            var anim = GetComponent<Animator>();
            if (anim == null) return;

            if (grafo.IsValid()) grafo.Destroy();
            grafo = PlayableGraph.Create("Tocador_" + name);
            grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var saida = AnimationPlayableOutput.Create(grafo, "saida", anim);
            mistura = AnimationMixerPlayable.Create(grafo, 2);
            saida.SetSourcePlayable(mistura);

            vivo = 0;
            canais[0] = AnimationClipPlayable.Create(grafo, clipe);
            canais[0].SetSpeed(velocidade);
            if (atraso > 0f) canais[0].SetTime(atraso % Mathf.Max(0.01f, clipe.length));
            grafo.Connect(canais[0], 0, mistura, 0);
            peso = 1f; duracaoDaTroca = 0f;
            AplicarPeso();
            grafo.Play();
        }

        void AplicarPeso()
        {
            mistura.SetInputWeight(vivo, peso);
            mistura.SetInputWeight(1 - vivo, 1f - peso);
        }

        /// <summary>Trocar o clipe em tempo de execucao, com a mistura padrao.</summary>
        public void Trocar(AnimationClip novo) { Trocar(novo, suavidadeDaTroca); }

        /// <summary>
        /// Trocar o clipe dizendo quanto misturar. <paramref name="suavidade"/> zero e' corte
        /// seco: e' o que a finalizacao usa, porque nela a raiz anda a emenda medida no mesmo
        /// instante da troca, e misturar faria o corpo deslizar durante a transicao.
        /// </summary>
        public void Trocar(AnimationClip novo, float suavidade)
        {
            if (novo == null) return;
            clipe = novo;
            if (!grafo.IsValid()) { Montar(); return; }

            int prox = 1 - vivo;
            if (canais[prox].IsValid())
            {
                mistura.DisconnectInput(prox);
                canais[prox].Destroy();
            }
            canais[prox] = AnimationClipPlayable.Create(grafo, novo);
            canais[prox].SetSpeed(velocidade);
            canais[prox].SetTime(0d);
            grafo.Connect(canais[prox], 0, mistura, prox);

            vivo = prox;
            duracaoDaTroca = Mathf.Max(0f, suavidade);
            peso = duracaoDaTroca <= 0f ? 1f : 0f;
            AplicarPeso();
        }

        /// <summary>
        /// Acerta o ritmo sem remontar o grafo. E' pra quem muda de velocidade a cada quadro:
        /// mexer na <see cref="velocidade"/> e chamar <see cref="Trocar"/> destruiria e
        /// recriaria o clipe sessenta vezes por segundo.
        /// </summary>
        public void Ritmo(float quanto)
        {
            velocidade = quanto;
            if (grafo.IsValid() && canais[vivo].IsValid()) canais[vivo].SetSpeed(quanto);
        }

        void Update()
        {
            if (!grafo.IsValid() || clipe == null) return;

            if (peso < 1f)
            {
                peso = duracaoDaTroca <= 0f ? 1f
                     : Mathf.MoveTowards(peso, 1f, Time.deltaTime / duracaoDaTroca);
                AplicarPeso();
            }

            var atual = canais[vivo];
            if (!atual.IsValid()) return;

            if (congelarNoFim)
            {
                // segura no ultimo quadro. Zerar a velocidade sozinho nao basta: o Playable
                // ja' pode ter passado do fim no quadro em que percebemos, e a pose voltaria
                // pro comeco do clipe. Entao fixa o tempo no fim e trava.
                //
                // E FIXA UM POUCO ANTES DO FIM, nao no fim exato. Clipe marcado como loop —
                // e todos os pareados do Full Mount sao — avalia a pose em `tempo % duracao`,
                // e duracao % duracao da' ZERO: travar no fim exato mostra o PRIMEIRO quadro.
                // Foi isto que fazia os dois aparecerem de pe' no meio da finalizacao.
                if (atual.GetTime() >= clipe.length)
                {
                    atual.SetTime(Mathf.Max(0f, clipe.length - Mathf.Min(0.01f, clipe.length * 0.02f)));
                    atual.SetSpeed(0d);
                }
                return;
            }

            if (clipe.isLooping) return;
            if (atual.GetTime() >= clipe.length + pausaNoFim) atual.SetTime(0d);
        }
    }
}
