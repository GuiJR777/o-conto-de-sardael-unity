using System.Collections;
using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// A emboscada da Floresta de Bael.
    ///
    /// Sardael entra com o comando livre. Passados alguns metros, um orc surge a frente, vem
    /// em cima e o mata. Nao ha' luta: a morte e' roteiro, e acontece TODA VEZ que se entra
    /// aqui. E' de proposito — enquanto o combate nao existir, Bael e' uma parede, e a parede
    /// faz parte da historia: quem mata Sardael entrega ele ao Veu, que o devolve.
    ///
    /// Quando o combate existir, e' so' desligar <see cref="matarSempre"/> e o roteiro para de
    /// rodar; o resto da cena continua de pe'.
    ///
    /// A MORTE DO HEROI desliga o <see cref="MovimentoDoHeroi"/> e TIRA o AnimatorController
    /// antes de tocar o clipe. Um controller e um grafo de Playables no mesmo Animator se
    /// anulam e o boneco vai pra T-pose; o controller tem de sair de cena primeiro.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiretorDeBael : MonoBehaviour
    {
        public enum Etapa { Andando, OrcChega, Golpe, Morto, Indo }

        [Header("Atores")]
        public Transform sardael;
        public MovimentoDoHeroi comandoDoHeroi;
        [Tooltip("O orc da emboscada. Comeca desligado; o roteiro o acende.")]
        public Transform orc;
        public FalaNaTela caixaDeFala;

        [Header("Clipes do orc")]
        public AnimationClip orcParado;
        public AnimationClip orcCorrendo;
        public AnimationClip orcGolpe;
        [Tooltip("Passada do clipe de correr, medida no [RM] dele.")]
        public float velocidadeDoOrc = 3.6f;

        [Header("Clipe da morte de Sardael")]
        public AnimationClip morteDeSardael;

        [Header("Quando")]
        [Tooltip("Quantos metros ele anda antes de o orc aparecer.")]
        public float distanciaDaEmboscada = 10f;
        [Tooltip("De quantos metros a frente o orc entra. Tem de ser fora do quadro.")]
        public float distanciaDeEntrada = 9f;
        [Tooltip("A que distancia do Sardael o orc para' pra golpear.")]
        public float distanciaDoGolpe = 1.5f;
        [Tooltip("Do inicio do golpe ate' o corpo cair. Sai do clipe do orc.")]
        public float tempoAteOImpacto = 0.45f;
        [Tooltip("Quanto tempo o corpo fica na tela antes de o Veu levar a cena.")]
        public float pausaDepoisDaMorte = 2.6f;

        [Header("Fala")]
        public string nomeDoOrc = "Orc";
        [TextArea(2, 3)] public string grito = "Carne de Valdória!";
        public float duracaoDoGrito = 1.6f;

        [Header("Regra")]
        [Tooltip("Desligue quando o combate existir: o roteiro da emboscada para de rodar.")]
        public bool matarSempre = true;

        [Header("Depuracao")]
        public bool mostrarPainel = true;

        public Etapa Agora { get; private set; }

        Terrain chao;
        float xDePartida;
        int rumo = 1;
        TocadorDeClipe tocadorDoOrc;

        void Start()
        {
            chao = Terrain.activeTerrain;
            if (caixaDeFala == null) caixaDeFala = FalaNaTela.Instancia;
            if (comandoDoHeroi == null) comandoDoHeroi = FindAnyObjectByType<MovimentoDoHeroi>();
            if (sardael == null && comandoDoHeroi != null) sardael = comandoDoHeroi.transform;

            if (sardael == null) { Aviso("sardael"); return; }
            if (orc == null) { Aviso("orc"); return; }

            tocadorDoOrc = orc.GetComponent<TocadorDeClipe>();
            if (tocadorDoOrc == null) tocadorDoOrc = orc.gameObject.AddComponent<TocadorDeClipe>();
            orc.gameObject.SetActive(false);

            xDePartida = sardael.position.x;
            Agora = Etapa.Andando;
            if (matarSempre) StartCoroutine(Roteiro());
        }

        void Aviso(string oQue)
        {
            Debug.LogWarning("DiretorDeBael sem " + oQue + ". A emboscada nao vai rodar.", this);
            enabled = false;
        }

        IEnumerator Roteiro()
        {
            // ---------- 1: ele anda solto ate' o ponto da emboscada
            while (Mathf.Abs(sardael.position.x - xDePartida) < distanciaDaEmboscada)
                yield return null;

            rumo = sardael.position.x >= xDePartida ? +1 : -1;

            // ---------- 2: o orc entra de fora do quadro, correndo
            Agora = Etapa.OrcChega;
            Travar(true);
            orc.gameObject.SetActive(true);
            Plantar(orc, new Vector3(sardael.position.x + rumo * distanciaDeEntrada,
                                     0f, comandoDoHeroi != null ? comandoDoHeroi.zDaLinha : sardael.position.z));
            Encarar(orc, sardael);
            Encarar(sardael, orc);
            Tocar(orcCorrendo, false);

            float xParada = sardael.position.x + rumo * distanciaDoGolpe;
            while ((orc.position.x - xParada) * rumo > 0.05f)
            {
                float anda = Mathf.Min(velocidadeDoOrc * Time.deltaTime, Mathf.Abs(orc.position.x - xParada));
                Plantar(orc, new Vector3(orc.position.x - rumo * anda, 0f, orc.position.z));
                yield return null;
            }
            Plantar(orc, new Vector3(xParada, 0f, orc.position.z));
            Encarar(orc, sardael);

            if (!string.IsNullOrEmpty(grito))
            {
                Tocar(orcParado, false);
                Dizer(nomeDoOrc, grito);
                yield return new WaitForSeconds(duracaoDoGrito);
                Calar();
            }

            // ---------- 3: o golpe, e o corpo cai no meio dele
            Agora = Etapa.Golpe;
            Tocar(orcGolpe, true);
            yield return new WaitForSeconds(tempoAteOImpacto);
            Matar();

            Agora = Etapa.Morto;
            yield return new WaitForSeconds(pausaDepoisDaMorte);

            // ---------- 4: o Veu assume
            Agora = Etapa.Indo;
            Salvao.ContarMorteNoVeu();
            Progresso.Onde = Progresso.Etapa.MorreuEmBael;
            TelaDeCarregamento.Ir("Morte_Do_Veu", "O Véu se abre...");
        }

        /// <summary>Tira o controle e o AnimatorController de Sardael e toca a queda.</summary>
        void Matar()
        {
            var animador = sardael.GetComponent<Animator>();
            if (comandoDoHeroi != null) comandoDoHeroi.enabled = false;

            if (animador != null)
            {
                // sem isto sao dois grafos no mesmo Animator, e o resultado e' T-pose
                animador.runtimeAnimatorController = null;

                // quem recolhia o root motion era o OnAnimatorMove do MovimentoDoHeroi, que
                // acabou de ser desligado. Deixar ligado faria o corpo escorregar sozinho
                // enquanto cai, porque o deslocamento do clipe iria direto pro transform.
                animador.applyRootMotion = false;
            }

            if (morteDeSardael == null) return;
            var t = sardael.GetComponent<TocadorDeClipe>();
            if (t == null) t = sardael.gameObject.AddComponent<TocadorDeClipe>();
            t.congelarNoFim = true;
            t.clipe = morteDeSardael;
            t.enabled = true;
            t.Trocar(morteDeSardael, 0f);
        }

        void Tocar(AnimationClip clipe, bool segurarOFim)
        {
            if (tocadorDoOrc == null || clipe == null) return;
            tocadorDoOrc.congelarNoFim = segurarOFim;
            tocadorDoOrc.Trocar(clipe);
            tocadorDoOrc.Ritmo(1f);
        }

        /// <summary>
        /// Poe alguem em pe' na linha de jogo.
        ///
        /// Bael nao tem Terrain — o chao e' malha montada a mao. Sem terreno pra consultar, a
        /// altura vem do PE DO SARDAEL, e nao da altura que o objeto ja' tinha: o orc nasce
        /// fora do quadro, e se ele guardasse a propria altura entraria voando ou enterrado.
        /// </summary>
        void Plantar(Transform quem, Vector3 xz)
        {
            if (quem == null) return;
            float y;
            if (chao != null) y = chao.SampleHeight(xz) + chao.transform.position.y;
            else if (sardael != null) y = sardael.position.y;
            else y = quem.position.y;
            quem.position = new Vector3(xz.x, y, xz.z);
        }

        void Encarar(Transform quem, Transform paraQuem)
        {
            if (quem == null || paraQuem == null) return;
            var d = paraQuem.position - quem.position; d.y = 0f;
            if (d.sqrMagnitude < 0.0004f) return;
            quem.rotation = Quaternion.LookRotation(d.normalized);
        }

        void Travar(bool quanto)
        {
            if (comandoDoHeroi != null) comandoDoHeroi.Travar(this, quanto);
        }

        void Dizer(string quem, string oQue) { if (caixaDeFala != null) caixaDeFala.Dizer(quem, oQue); }
        void Calar() { if (caixaDeFala != null) caixaDeFala.Calar(); }

        void OnGUI()
        {
            if (!mostrarPainel) return;
            float andou = sardael != null ? Mathf.Abs(sardael.position.x - xDePartida) : 0f;
            GUI.Box(new Rect(10, 10, 320, 44), "");
            GUI.Label(new Rect(20, 18, 310, 30),
                "Bael: " + Agora + "   andou " + andou.ToString("F1") + " m de " + distanciaDaEmboscada);
        }
    }
}
