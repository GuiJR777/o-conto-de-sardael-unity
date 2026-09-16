using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Sardael;
using Sardael.EditorFerramentas;

namespace SardaelEditor
{
    /// <summary>
    /// Uma tira de fotos por clipe, pra escolher animacao OLHANDO, e nao pelo nome.
    ///
    /// POR QUE ISTO EXISTE: o golpe que esta' na tecla J hoje e' o Attack4, e o dono viu na tela
    /// que ele nao e' golpe — e' o Sardael EMPURRANDO com a lanca. O nome dizia "Attack"; o corpo
    /// diz outra coisa. Com 13 ataques base e 80 elos de cadeia no pacote, escolher pelo nome e'
    /// chute caro: cada troca custa remedir o impacto e refazer o par com a reacao do alvo.
    ///
    /// Cada clipe vira UMA imagem com oito poses lado a lado, do comeco ao fim. Numa tira da' pra
    /// ver na hora se e' estocada, corte de cima, varrida ou empurrao — e da' pra ver se algum
    /// deles serve de APARO, que e' o buraco do pacote.
    ///
    /// DE QUE ANGULO: o mesmo da camera do jogo. Um golpe lido de tres quartos engana; o jogo e'
    /// 2.5D de perfil, e o que importa e' o que aparece DESSE lado. A camera aqui copia o
    /// enquadramento da cena de bancada.
    ///
    /// NAO SUJA NADA: quem posa e devolve os ossos e' o <see cref="GravadorDeQuadros"/>, que ja'
    /// existe e ja' resolve as tres armadilhas disso — a pose que fica gravada na cena, o
    /// controller que ignora o grafo de fora, e a matriz de pele que trava no primeiro quadro.
    /// A cena NAO e' salva em nenhum momento.
    /// </summary>
    public static class FolhaDeContato
    {
        const string CENA = "Assets/_Sardael/Cenas/Movimento_Teste.unity";
        const string RM   = "Assets/_Pacotes/SpearCombatAnimationV2/Animation/RM/A_SpearCombatAnimationV2_";
        const string SAIDA = "folha_de_contato";

        const int POSES  = 8;
        const int LARG   = 320;      // por pose. Largo o bastante pra lanca deitada (2,24 m).
        const int ALT    = 400;      // a lanca tambem sobe muito

        /// <summary>De onde a camera olha, em relacao ao QUADRIL da pose. Ver Fotografar.</summary>
        static readonly Vector3 OLHO = new Vector3(0.45f, 0f, -4.5f);
        const float FOV = 40f;

        /// <summary>
        /// Quantos elos de cadeia cada ataque tem, CONTADO nos arquivos do pacote.
        ///
        /// Indice = numero do ataque. Zero quer dizer golpe unico, sem cadeia. Nao e' chute: sai
        /// de listar os _Complete que existem em Animation/RM. Gerar nome que nao existe so'
        /// enche o relatorio de "nao achei" e esconde o que falta de verdade.
        /// </summary>
        static readonly int[] ELOS_POR_ATAQUE = {
            0,  // [0] nao existe Attack0
            2,  // Attack1
            2,  // Attack2
            3,  // Attack3
            3,  // Attack4
            0,  // Attack5   golpe unico
            3,  // Attack6
            3,  // Attack7
            4,  // Attack8   a cadeia mais longa do pacote
            0,  // Attack9   golpe unico
            2,  // Attack10
            0,  // Attack11  golpe unico
            0,  // Attack12  golpe unico
            0,  // Attack13  golpe unico
        };

        /// <summary>
        /// Os 13 golpes unicos: os que se joga sozinho, sem preparacao.
        ///
        /// Numa mao de cartas, sao as cartas que valem por si. Esta folha responde a pergunta que
        /// a contagem nao responde: QUANTOS DELES O JOGADOR ENXERGA COMO DIFERENTES? Treze no
        /// codigo pode ser cinco na tela, se forem todos estocada parecida.
        /// </summary>
        [MenuItem("Sardael/Folha de Contato 1 - os 13 golpes unicos")]
        public static void Unicos()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Folha] pare o Play primeiro."); return; }

            var alvos = new List<string>();
            for (int i = 1; i < ELOS_POR_ATAQUE.Length; i++) alvos.Add("Attack" + i + "_RM");
            Fotografar(alvos, SAIDA + "_unicos");
        }

        /// <summary>
        /// Os 22 elos de cadeia que ACERTAM (_Complete), na ordem em que se encadeiam.
        ///
        /// Cada um so' existe como o 2o, 3o ou 4o de uma sequencia — e' a gramatica que o pacote
        /// traz pronta. O nome do arquivo de saida comeca pelo ataque e pelo elo, entao a pasta
        /// ja' sai na ordem de jogar.
        /// </summary>
        [MenuItem("Sardael/Folha de Contato 2 - os 22 elos de cadeia")]
        public static void Elos()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Folha] pare o Play primeiro."); return; }

            var alvos = new List<string>();
            for (int a = 1; a < ELOS_POR_ATAQUE.Length; a++)
                for (int s = 1; s <= ELOS_POR_ATAQUE[a]; s++)
                    alvos.Add("Attack" + a + "_Stage" + s + "_Complete_RM");

            Fotografar(alvos, SAIDA + "_elos");
        }

        // ------------------------------------------------------------------ o trabalho

        static void Fotografar(List<string> nomes, string pasta)
        {
            EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);

            var heroi = Object.FindAnyObjectByType<MovimentoDoHeroi>();
            if (heroi == null) { Debug.LogError("[Folha] nao achei o Sardael em " + CENA); return; }
            var anim = heroi.GetComponent<Animator>();
            if (anim == null) { Debug.LogError("[Folha] o Sardael nao tem Animator."); return; }

            string destino = Application.dataPath + "/../" + pasta;
            try { System.IO.Directory.CreateDirectory(destino); } catch { }

            // O QUADRIL, e por que a camera tem que seguir ELE e nao o objeto
            // -----------------------------------------------------------------
            // Os 13 ataques do pacote chegam com loopBlendPositionXZ: 1 — a raiz TRAVADA. Com a
            // trava o importador nao move o objeto: ele assa o deslocamento na POSE, e quem anda
            // e' o corpo, para longe da origem do objeto. Camera fixa na origem = o Sardael sai
            // pela beirada no meio do golpe, que e' justamente a parte que interessa.
            //
            // Entao a camera se pendura no quadril, em X e Z. Em Y nao: se ela subisse junto, um
            // salto ficaria igual a um golpe no chao, e altura e' leitura de golpe.
            var quadril = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (quadril == null) { Debug.LogError("[Folha] o Sardael nao tem quadril mapeado."); return; }
            float chao = heroi.transform.position.y;

            int feitos = 0, faltando = 0;
            GravadorDeQuadros.Guardar(heroi.transform);
            GravadorDeQuadros.AbrirCamera(LARG, ALT);
            try
            {
                for (int c = 0; c < nomes.Count; c++)
                {
                    var clipe = Clipe(RM + nomes[c] + ".FBX");
                    if (clipe == null) { faltando++; continue; }

                    EditorUtility.DisplayProgressBar("Folha de contato", clipe.name, (float)c / nomes.Count);

                    var tiras = new Texture2D[POSES];
                    for (int i = 0; i < POSES; i++)
                    {
                        // A ULTIMA POSE NAO E' clipe.length.
                        //
                        // Estes clipes vem com loopTime: 1, e num clipe em laco o instante final
                        // E' o instante zero. Amostrando em length eu gravava a pose de guarda de
                        // novo e jogava fora uma das oito fotos — a tira acabava mostrando o
                        // comeco duas vezes e escondendo o fim do golpe.
                        float t = Mathf.Min(clipe.length * i / (POSES - 1),
                                            Mathf.Max(0f, clipe.length - 1f / 30f));
                        GravadorDeQuadros.Pousar(anim, clipe, t);

                        // a camera so' agora, DEPOIS de posar: o quadril desta pose e' que manda
                        var q = quadril.position;
                        var alvo = new Vector3(q.x + OLHO.x, chao + 1.00f, q.z);
                        var olho = new Vector3(q.x + OLHO.x, chao + 1.15f, q.z + OLHO.z);

                        string tmp = destino + "/_tmp_" + i + ".png";
                        GravadorDeQuadros.Quadro(tmp, olho,
                            Quaternion.LookRotation(alvo - olho, Vector3.up), FOV, LARG, ALT);
                        tiras[i] = Ler(tmp);
                        try { System.IO.File.Delete(tmp); } catch { }
                    }

                    Costurar(tiras, destino + "/" + nomes[c].Replace("_RM", "") + ".png");
                    foreach (var t2 in tiras) if (t2 != null) Object.DestroyImmediate(t2);
                    feitos++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                GravadorDeQuadros.FecharCamera();
                GravadorDeQuadros.Devolver();       // os ossos voltam pro lugar. Sempre.
            }

            Debug.Log(string.Format(
                "[Folha] {0} tiras gravadas em {1}{2}\n"
              + "  cada tira tem {3} poses, do comeco ao fim do clipe, vistas do angulo do jogo.\n"
              + "  A cena NAO foi salva e os ossos foram devolvidos.",
                feitos, destino, faltando > 0 ? ("  (" + faltando + " clipe(s) nao encontrado(s))") : "", POSES));
        }

        // ------------------------------------------------------------------ pecas

        static AnimationClip Clipe(string caminho)
        {
            var tudo = AssetDatabase.LoadAllAssetsAtPath(caminho);
            if (tudo == null) return null;
            foreach (var a in tudo)
            {
                var c = a as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
        }

        static Texture2D Ler(string arquivo)
        {
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(System.IO.File.ReadAllBytes(arquivo));
                return t;
            }
            catch { return null; }
        }

        /// <summary>Cola as poses lado a lado numa tira so, com um fio claro separando.</summary>
        static void Costurar(Texture2D[] poses, string arquivo)
        {
            int n = poses.Length;
            var tira = new Texture2D(LARG * n, ALT, TextureFormat.RGBA32, false);
            var risco = new Color(1f, 1f, 1f, 0.25f);

            for (int i = 0; i < n; i++)
            {
                if (poses[i] == null) continue;
                tira.SetPixels(i * LARG, 0, LARG, ALT, poses[i].GetPixels());
                if (i > 0)
                    for (int y = 0; y < ALT; y++) tira.SetPixel(i * LARG, y, risco);
            }
            tira.Apply(false);
            try { System.IO.File.WriteAllBytes(arquivo, tira.EncodeToPNG()); } catch { }
            Object.DestroyImmediate(tira);
        }
    }
}
