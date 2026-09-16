using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sardael
{
    /// <summary>
    /// Um lutador de cena: fica em guarda e, quando mandam, executa UM golpe ou UMA defesa
    /// e volta pra guarda.
    ///
    /// Nao usa AnimatorController porque o duelo aqui e' coreografia, nao maquina de estados:
    /// quem decide o que vem depois e' o <see cref="DueloEncenado"/>, no relogio dele. Com
    /// Playables o clipe entra e sai por peso, entao a troca e' macia sem precisar de
    /// transicoes desenhadas a mao para cada par de clipes.
    ///
    /// Ninguem morre: nao existe clipe de morte nesta lista, de proposito.
    ///
    /// MORA NO PROPRIO ARQUIVO de proposito. Quando esta classe dividia arquivo com o
    /// DueloEncenado, as duas ficavam com o mesmo GUID de script e o Unity trocava os layouts
    /// de serializacao entre elas ("Read 116 bytes but expected 184"); a cada recompilacao
    /// alguns componentes viravam "missing" e os bonecos apareciam em T no Play.
    /// </summary>
    [DisallowMultipleComponent]   // dois grafos no mesmo Animator se anulam e o boneco fica em T
    [RequireComponent(typeof(Animator))]
    public class Combatente : MonoBehaviour
    {
        [Header("Clipes")]
        public AnimationClip guarda;
        [Tooltip("Sorteados a cada golpe, pra luta nao virar metronomo.")]
        public AnimationClip[] golpes;
        public AnimationClip defesa;

        [Header("Mistura")]
        [Tooltip("Tempo pra entrar e sair do clipe de acao.")]
        public float suavidade = 0.14f;

        PlayableGraph grafo;
        AnimationMixerPlayable mistura;
        AnimationClipPlayable pGuarda, pAcao;
        float duracaoAcao, decorrido;
        bool emAcao;

        public bool EmAcao { get { return emAcao; } }

        void OnEnable() { Montar(); }

        // se o grafo nao subiu no OnEnable (campo ainda vazio, ordem de execucao, recompilacao
        // no meio do caminho), o boneco fica em T e ninguem fica sabendo. Este Start pega isso.
        void Start()
        {
            if (!grafo.IsValid())
            {
                Montar();
                if (!grafo.IsValid())
                    Debug.LogWarning("Combatente sem animacao em '" + name + "': "
                        + (GetComponent<Animator>() == null ? "sem Animator. " : "")
                        + (guarda == null ? "clipe de guarda vazio. " : "")
                        + "Ele vai ficar em T ate' isso ser resolvido.", this);
            }
        }

        void Montar()
        {
            var anim = GetComponent<Animator>();
            if (anim == null || guarda == null) return;
            anim.applyRootMotion = false;

            if (grafo.IsValid()) grafo.Destroy();
            grafo = PlayableGraph.Create("Duelo_" + name);
            grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var saida = AnimationPlayableOutput.Create(grafo, "saida", anim);
            mistura = AnimationMixerPlayable.Create(grafo, 2);
            saida.SetSourcePlayable(mistura);

            pGuarda = AnimationClipPlayable.Create(grafo, guarda);
            grafo.Connect(pGuarda, 0, mistura, 0);
            mistura.SetInputWeight(0, 1f);
            mistura.SetInputWeight(1, 0f);
            // cada um entra na guarda num ponto diferente do ciclo, senao a fileira
            // inteira respira junto e denuncia que e' o mesmo clipe
            pGuarda.SetTime(Random.value * guarda.length);
            grafo.Play();
        }

        void OnDisable() { if (grafo.IsValid()) grafo.Destroy(); }

        /// <summary>Executa um clipe uma vez e volta pra guarda.</summary>
        public void Executar(AnimationClip clipe)
        {
            if (clipe == null || !grafo.IsValid()) return;
            if (pAcao.IsValid())
            {
                mistura.DisconnectInput(1);
                pAcao.Destroy();
            }
            pAcao = AnimationClipPlayable.Create(grafo, clipe);
            pAcao.SetTime(0d);
            grafo.Connect(pAcao, 0, mistura, 1);
            duracaoAcao = clipe.length;
            decorrido = 0f;
            emAcao = true;
        }

        public AnimationClip SortearGolpe()
        {
            if (golpes == null || golpes.Length == 0) return null;
            return golpes[Random.Range(0, golpes.Length)];
        }

        void Update()
        {
            if (!grafo.IsValid()) return;
            if (!emAcao) return;

            decorrido += Time.deltaTime;
            float s = Mathf.Max(0.01f, suavidade);
            float peso;
            if (decorrido < s) peso = decorrido / s;                                   // entrando
            else if (decorrido > duracaoAcao - s) peso = (duracaoAcao - decorrido) / s;  // saindo
            else peso = 1f;
            peso = Mathf.Clamp01(peso);

            mistura.SetInputWeight(1, peso);
            mistura.SetInputWeight(0, 1f - peso);

            if (decorrido >= duracaoAcao)
            {
                mistura.SetInputWeight(1, 0f);
                mistura.SetInputWeight(0, 1f);
                emAcao = false;
            }
        }
    }
}
