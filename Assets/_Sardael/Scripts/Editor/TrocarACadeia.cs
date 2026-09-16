using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Sardael;
using Sardael.EditorFerramentas;

namespace SardaelEditor
{
    /// <summary>
    /// Troca a cadeia de golpes do Sardael pelos clipes que o dono escolheu OLHANDO, arruma a
    /// importacao de cada um conforme o PAPEL que ele vai ter, e mede antes de ligar.
    ///
    /// POR QUE ISTO EXISTE: o que estava na tecla J era o Attack4, e o Attack4 nao e' golpe — e'
    /// o Sardael EMPURRANDO com a lanca. Isso contaminou tudo que foi medido em cima dele,
    /// inclusive o alcance de 1,756 m. E resolveu o outro buraco de graca: esse empurrao e'
    /// exatamente um aparo, entao o Sardael armado passa a ser 100% SpearCombatAnimationV2.
    ///
    /// A ESCOLHA DO DONO:
    ///   golpe 1  Attack1_Stage1_Complete   1,50 s
    ///   golpe 2  Attack3_Stage2_Complete   1,17 s
    ///   golpe 3  Attack6_Stage2_Complete   1,67 s
    ///   golpe 4  Attack3_Stage3_Complete   2,03 s — o mais longo, por ultimo de proposito
    ///   aparo    Attack4_Stage1_Complete   o empurrao que estava fazendo papel de golpe
    ///
    /// A RAIZ, E POR QUE ELA VAI PROS DOIS LADOS AQUI
    /// ----------------------------------------------
    /// O pacote entrega todo clipe com a raiz TRAVADA, e com a trava o importador joga o
    /// deslocamento fora — o golpe nao empurra o heroi. O velho MontarCombate destravava
    /// exatamente os tres elos do Attack4 e mais nenhum, entao os quatro clipes novos chegaram
    /// aqui travados e mediram avanco ZERO.
    ///
    /// Mas a trava nao e' defeito: e' ajuste, e depende do PAPEL do clipe.
    ///   POSICAO XZ, e depende do papel:
    ///     golpe  SOLTA. O avanco e' o que faz o golpe empurrar e casar com o tranco do alvo.
    ///     aparo  TRAVADA. Aparo e' no lugar; solto ele escorrega 1,6 m e vira investida.
    ///   ROTACAO, e nos cinco igual:
    ///     TRAVADA. Destravar a posicao solta a rotacao junto, e ai' o Sardael gira pra fora da
    ///     linha no meio do combo — varios clipes deste pacote golpeiam em Z, de frente pra
    ///     tela, porque o pacote nao foi feito pra jogo de linha. Quem manda na direcao que ele
    ///     encara e' o codigo, nunca a animacao.
    /// Por isso esta ferramenta trava um e destrava os outros quatro. O ajuste mora em codigo
    /// nosso, e nao numa edicao manual dentro de _Pacotes que se perde na reimportacao.
    ///
    /// O QUE ELA MEDE, e por que cada numero importa:
    ///   duracao   quanto tempo o heroi fica preso no estado.
    ///   alcance   a PONTA DA LANCA mais a frente, NO REFERENCIAL DO HEROI. E' o ALCANCE novo do
    ///             Duelo — o 1,756 m de hoje foi medido no par do Attack4 e nao vale mais.
    ///   curso     quanto a ponta anda do inicio ao fim. Se der quase zero, a lanca nao esta'
    ///             acompanhando a pose e a medida inteira nao vale.
    ///   impacto   a fracao do clipe em que a ponta chega mais longe. Vai pro aviso do Animator.
    ///   avanco    quanto o root motion empurra o heroi. Decide se o orc leva o tranco curto
    ///             (Levar) ou o longo (LevarForte).
    ///
    /// Rode quantas vezes quiser. Nao salva cena, e devolve os ossos no finally.
    /// </summary>
    public static class TrocarACadeia
    {
        const string CENA = "Assets/_Sardael/Cenas/Movimento_Teste.unity";
        const string CTRL = "Assets/_Sardael/Animacoes/Controladores/Sardael_Combate.controller";
        const string RM   = "Assets/_Pacotes/SpearCombatAnimationV2/Animation/RM/A_SpearCombatAnimationV2_";
        const string SAIDA = "_cadeia_nova.txt";

        // A ORDEM E DO DONO, e ela tem motivo: o Attack3_Stage3 dura 2,03 s, o dobro dos
        // outros. Como a emenda so abre no fim do clipe, elo longo no MEIO da cadeia faz o
        // jogador esperar dois segundos pelo golpe seguinte e parece travamento. No fim, pesa
        // menos: ninguem espera emenda depois do ultimo.
        static readonly string[] ELOS = {
            "Attack1_Stage1_Complete",   // 1,50 s
            "Attack3_Stage2_Complete",   // 1,17 s
            "Attack6_Stage2_Complete",   // 1,67 s
            "Attack3_Stage3_Complete",   // 2,03 s — o mais longo, por ultimo
        };
        const string APARO = "Attack4_Stage1_Complete";

        class Medida
        {
            public string estado, arquivo;
            public float duracao, alcance, curso, impacto, avanco, lado;
            public bool avanca, gira;
        }

        [MenuItem("Sardael/Trocar a Cadeia de Golpes")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Cadeia] pare o Play primeiro."); return; }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL);
            if (ctrl == null) { Debug.LogError("[Cadeia] nao achei " + CTRL); return; }

            // 1 ---------------------------------------- a raiz, conforme o papel de cada clipe
            // ANTES de carregar os clipes: SaveAndReimport invalida qualquer referencia ja' lida.
            foreach (var n in ELOS) ArrumarRaiz(RM + n + "_RM.FBX", true);
            ArrumarRaiz(RM + APARO + "_RM.FBX", false);
            // a rotacao fica travada nos cinco — ver ArrumarRaiz

            // 2 ---------------------------------------- carrega tudo ANTES de mexer no controller
            var clipes = new AnimationClip[ELOS.Length];
            for (int i = 0; i < ELOS.Length; i++)
            {
                clipes[i] = Clipe(RM + ELOS[i] + "_RM.FBX");
                if (clipes[i] == null) { Debug.LogError("[Cadeia] nao achei " + ELOS[i]); return; }
            }
            var clipeAparo = Clipe(RM + APARO + "_RM.FBX");
            if (clipeAparo == null) { Debug.LogError("[Cadeia] nao achei " + APARO); return; }

            // 3 ---------------------------------------- mede na cena, com a lanca na mao
            var medidas = Medir(clipes, clipeAparo);
            if (medidas == null) return;

            // 4 ---------------------------------------- troca no controller
            var camada = ctrl.layers[0].stateMachine;
            var locomocao = Achar(camada, "Locomocao");
            if (locomocao == null) { Debug.LogError("[Cadeia] nao achei o estado Locomocao."); return; }

            var golpes = new AnimatorState[ELOS.Length];
            for (int i = 0; i < ELOS.Length; i++)
            {
                string nome = "Golpe" + (i + 1);
                var st = Achar(camada, nome) ?? camada.AddState(nome);
                st.motion = clipes[i];
                st.speed = 1f;
                st.tag = MovimentoDoHeroi.TAG_ACAO;
                st.writeDefaultValues = true;
                golpes[i] = st;
            }

            for (int i = 0; i < golpes.Length; i++)
            {
                while (golpes[i].transitions.Length > 0)
                    golpes[i].RemoveTransition(golpes[i].transitions[0]);

                // PRIMEIRO o proximo elo: a ordem da lista e' a ordem de teste
                if (i + 1 < golpes.Length)
                {
                    var emenda = golpes[i].AddTransition(golpes[i + 1]);
                    emenda.AddCondition(AnimatorConditionMode.If, 0f, Duelo.P_ATACAR);
                    emenda.hasExitTime = true; emenda.exitTime = 1f;
                    emenda.hasFixedDuration = true; emenda.duration = 0.08f;
                }
                // DEPOIS a volta pra guarda, sem condicao
                var volta = golpes[i].AddTransition(locomocao);
                volta.hasExitTime = true; volta.exitTime = 1f;
                volta.hasFixedDuration = true; volta.duration = 0.12f;

                // Reaproveita o aviso em vez de destruir e recriar: StateMachineBehaviour e'
                // sub-asset do .controller, e DestroyImmediate com re-adicao na mesma passada e'
                // o caminho classico de deixar sub-asset orfao dentro do arquivo.
                AvisoDoAnimator aviso = null;
                foreach (var b in golpes[i].behaviours)
                    if (b is AvisoDoAnimator) { aviso = (AvisoDoAnimator)b; break; }
                if (aviso == null) aviso = golpes[i].AddStateMachineBehaviour<AvisoDoAnimator>();
                aviso.aviso = "golpe" + (i + 1);
            }

            bool temEntrada = false;
            foreach (var t in locomocao.transitions) if (t.destinationState == golpes[0]) temEntrada = true;
            if (!temEntrada)
            {
                var entra = locomocao.AddTransition(golpes[0]);
                entra.AddCondition(AnimatorConditionMode.If, 0f, Duelo.P_ATACAR);
                entra.hasExitTime = false;
                entra.hasFixedDuration = true; entra.duration = 0.05f;
            }

            var aparoSt = Achar(camada, Duelo.ESTADO_APARO);
            if (aparoSt != null) aparoSt.motion = clipeAparo;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            Relatar(medidas, aparoSt != null);
        }

        // ------------------------------------------------------------------ a importacao

        /// <summary>
        /// Ajusta a raiz do clipe: se ele AVANCA, e se ele pode GIRAR.
        ///
        /// Sao duas coisas separadas, e foi por trata-las como uma so que eu errei antes.
        ///
        /// POSICAO XZ — depende do papel:
        ///   golpe  solta. O avanco e' o que faz o golpe empurrar e casar com o tranco do alvo.
        ///   aparo  travada. Aparo e' no lugar; solto ele vira investida de 1,6 m.
        ///
        /// ROTACAO — TRAVADA NOS CINCO, sem excecao.
        ///   Este jogo e' 2.5D: o heroi anda numa linha e a camera nunca gira. Se a rotacao da
        ///   raiz vier do clipe, ele gira pra fora do plano no meio do combo — e varios destes
        ///   clipes golpeiam em Z, de frente pra tela, porque o pacote nao foi feito pra linha.
        ///   Quem manda na direcao que o Sardael encara e' o codigo, nunca a animacao.
        ///
        /// Os nomes da API nao batem com os do YAML: lockRootPositionXZ aqui e'
        /// loopBlendPositionXZ la'. Mexer pela API e' o certo — e' ela que sabe a traducao.
        /// </summary>
        static void ArrumarRaiz(string caminho, bool avanca)
        {
            var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[Cadeia] nao achei " + caminho); return; }

            var clipes = imp.clipAnimations;
            if (clipes == null || clipes.Length == 0) clipes = imp.defaultClipAnimations;
            if (clipes == null || clipes.Length == 0) return;

            bool mudou = false;
            foreach (var c in clipes)
            {
                if (c.loopTime) { c.loopTime = false; mudou = true; }   // acao nenhuma repete
                if (c.lockRootPositionXZ != !avanca) { c.lockRootPositionXZ = !avanca; mudou = true; }
                if (!c.lockRootRotation) { c.lockRootRotation = true; mudou = true; }
                if (!c.keepOriginalPositionXZ) { c.keepOriginalPositionXZ = true; mudou = true; }
                if (!c.keepOriginalOrientation) { c.keepOriginalOrientation = true; mudou = true; }
            }
            if (!mudou) return;
            imp.clipAnimations = clipes;
            imp.SaveAndReimport();
            Debug.Log("[Cadeia] " + System.IO.Path.GetFileNameWithoutExtension(caminho)
                    + ": posicao " + (avanca ? "SOLTA" : "travada") + ", rotacao TRAVADA");
        }

        static bool RaizAvanca(string caminho)
        {
            var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
            if (imp == null) return false;
            var cs = imp.clipAnimations;
            if (cs == null || cs.Length == 0) cs = imp.defaultClipAnimations;
            return cs != null && cs.Length > 0 && !cs[0].lockRootPositionXZ;
        }

        static bool RaizGira(string caminho)
        {
            var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
            if (imp == null) return false;
            var cs = imp.clipAnimations;
            if (cs == null || cs.Length == 0) cs = imp.defaultClipAnimations;
            return cs != null && cs.Length > 0 && !cs[0].lockRootRotation;
        }

        // ------------------------------------------------------------------ a medicao

        static Medida[] Medir(AnimationClip[] clipes, AnimationClip aparo)
        {
            EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);

            var heroi = Object.FindAnyObjectByType<MovimentoDoHeroi>();
            if (heroi == null) { Debug.LogError("[Cadeia] nao achei o Sardael em " + CENA); return null; }
            var anim = heroi.GetComponent<Animator>();

            var naMao = heroi.GetComponent<LancaNaMao>();
            Transform lanca = naMao != null ? naMao.lancaOriginal : null;
            if (lanca == null)
            {
                Debug.LogError("[Cadeia] nao achei a lanca (LancaNaMao.lancaOriginal). "
                             + "Sem ela nao da' pra medir alcance de ponta.");
                return null;
            }
            if (!lanca.IsChildOf(heroi.transform))
                Debug.LogWarning("[Cadeia] a lanca nao esta' dentro do Sardael na hierarquia. "
                               + "Se o 'curso' vier perto de zero, e' por isso: ela nao acompanha a pose.");

            var todos = new AnimationClip[clipes.Length + 1];
            System.Array.Copy(clipes, todos, clipes.Length);
            todos[clipes.Length] = aparo;

            var saida = new Medida[todos.Length];
            GravadorDeQuadros.Guardar(heroi.transform);
            try
            {
                for (int c = 0; c < todos.Length; c++)
                {
                    var clipe = todos[c];
                    bool ehAparo = c == clipes.Length;
                    string arq = ehAparo ? APARO : ELOS[c];
                    EditorUtility.DisplayProgressBar("Medindo a cadeia", arq, (float)c / todos.Length);

                    // PELA LINHA, e nao "longe do corpo".
                    //
                    // Dois erros meus antes deste, e os dois davam numero plausivel:
                    //   1) medi a caixa da lanca no espaco DA LANCA: deu 0,035 m constante nos
                    //      cinco clipes, que era a espessura dela.
                    //   2) medi a distancia da ponta ao corpo em QUALQUER direcao, e peguei o
                    //      ponto mais afastado. Numa lanca comprida esse ponto e' quase sempre a
                    //      ARMADA, com a haste atravessada no corpo — nao o contato. Deu golpe a
                    //      -159 graus, ou seja apontando pra tras, que nao existe.
                    //
                    // A pergunta certa num jogo de linha e': quanto a ponta AVANCA, e quanto ela
                    // esta' fora do plano NESSE MESMO INSTANTE. E' o avanco que decide se o golpe
                    // encosta no orc; e' o desvio que diz se ele ainda parece certo de perfil.
                    const int PASSOS = 90;
                    float maisLonge = float.NegativeInfinity, maisPerto = float.PositiveInfinity;
                    float quando = 0f, desvio = 0f;
                    for (int i = 0; i <= PASSOS; i++)
                    {
                        float f = (float)i / PASSOS;
                        GravadorDeQuadros.Pousar(anim, clipe, clipe.length * f);

                        Vector2 ponta = PontaNoHeroi(heroi.transform, lanca);   // x = lado, y = frente
                        if (ponta.y > maisLonge)
                        {
                            maisLonge = ponta.y; quando = f;
                            desvio = Mathf.Abs(ponta.x);
                        }
                        if (ponta.y < maisPerto) maisPerto = ponta.y;
                    }

                    var v = clipe.averageSpeed;
                    saida[c] = new Medida {
                        estado  = ehAparo ? Duelo.ESTADO_APARO : ("Golpe" + (c + 1)),
                        arquivo = arq,
                        duracao = clipe.length,
                        alcance = maisLonge,
                        curso   = maisLonge - maisPerto,
                        impacto = quando,
                        lado    = desvio,
                        avanco  = new Vector2(v.x, v.z).magnitude * clipe.length,
                        avanca  = RaizAvanca(RM + arq + "_RM.FBX"),
                        gira    = RaizGira(RM + arq + "_RM.FBX"),
                    };
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                GravadorDeQuadros.Devolver();      // os ossos voltam. Sempre.
            }
            return saida;
        }

        /// <summary>
        /// O ponto da lanca que vai MAIS A FRENTE no referencial do heroi, e quanto ele esta'
        /// pro lado. Devolve (lado, frente).
        ///
        /// "Mais a frente" e nao "mais longe": numa lanca de 1,6 m o ponto mais afastado do corpo
        /// costuma ser a armada, com a haste atravessada — e o que decide se o golpe encosta no
        /// orc, que esta' na linha, e' o quanto a ponta avanca.
        /// </summary>
        static Vector2 PontaNoHeroi(Transform heroi, Transform lanca)
        {
            var paraHeroi = heroi.worldToLocalMatrix;
            Vector2 melhor = new Vector2(0f, float.NegativeInfinity);

            foreach (var f in lanca.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null)
                    Cantos(f.sharedMesh.bounds, paraHeroi * f.transform.localToWorldMatrix, ref melhor);

            foreach (var s in lanca.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null)
                    Cantos(s.sharedMesh.bounds, paraHeroi * s.transform.localToWorldMatrix, ref melhor);

            return melhor;
        }

        static void Cantos(Bounds caixa, Matrix4x4 m, ref Vector2 melhor)
        {
            var c = caixa.center; var e = caixa.extents;
            for (int i = 0; i < 8; i++)
            {
                var p = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                        (i & 2) == 0 ? -e.y : e.y,
                                        (i & 4) == 0 ? -e.z : e.z);
                var q = m.MultiplyPoint3x4(p);
                if (q.z > melhor.y) melhor = new Vector2(q.x, q.z);   // x = lado, z = frente
            }
        }

        // ------------------------------------------------------------------ o relatorio

        static void Relatar(Medida[] m, bool trocouOAparo)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== CADEIA NOVA =====");
            sb.AppendLine("Unity " + Application.unityVersion + "   " + System.DateTime.Now);
            sb.AppendLine();
            sb.AppendLine("Clipes escolhidos pelo dono, olhando as animacoes rodando.");
            sb.AppendLine("A ponta da lanca amostrada em 90 passos por clipe, no referencial do Sardael.");
            sb.AppendLine();
            sb.AppendLine("  estado   avanca  gira   duracao   alcance   lado     curso    impacto   avanco   clipe");
            sb.AppendLine("  -------  ------  -----  --------  --------  -------  -------  --------  -------  -----");
            foreach (var x in m)
                sb.AppendLine(string.Format(
                    "  {0,-7}  {1,-6}  {2,-5}  {3,6:F3} s  {4,6:F3} m  {5,5:F3} m  {6,5:F3} m  {7,6:P1}  {8,5:F3} m  {9}",
                    x.estado, x.avanca ? "sim" : "nao", x.gira ? "SIM" : "nao",
                    x.duracao, x.alcance, x.lado, x.curso, x.impacto, x.avanco, x.arquivo));

            sb.AppendLine();
            sb.AppendLine("COMO LER");
            sb.AppendLine("  alcance  o quanto a ponta da lanca AVANCA, no referencial do Sardael. E' este");
            sb.AppendLine("           numero que decide se o golpe encosta no orc, que esta' na linha.");
            sb.AppendLine("  lado     o quanto ela esta' fora do plano NESSE MESMO instante. Pequeno = o");
            sb.AppendLine("           golpe termina na linha. Grande = ele termina pra dentro da tela, e");
            sb.AppendLine("           de perfil isso le' como golpe que passou ao lado do inimigo.");
            sb.AppendLine("  impacto  a fracao do clipe em que isso acontece. Vai pro aviso do Animator.");
            sb.AppendLine();
            sb.AppendLine("  curso perto de zero = a lanca nao acompanhou a pose; a linha nao vale.");
            sb.AppendLine();
            sb.AppendLine("  'gira' TEM que sair 'nao' nos cinco. Rotacao vinda do clipe faz o Sardael");
            sb.AppendLine("  virar pra fora da linha no meio do combo — quem manda na direcao e' o codigo.");
            sb.AppendLine();
            sb.AppendLine("O QUE EU FACO COM ELES DEPOIS");
            sb.AppendLine("  - ALCANCE do Duelo sai da coluna 'alcance' (hoje 1,756 m, do par do Attack4).");
            sb.AppendLine("  - o aviso de impacto de cada elo sai da coluna 'impacto'.");
            sb.AppendLine("  - tranco do orc por elo: avanco grande pede LevarForte (Hit_Fw_RM); avanco");
            sb.AppendLine("    curto pede Levar (Hit_Bw_RM).");
            sb.AppendLine();
            sb.AppendLine(trocouOAparo ? "O estado Aparo foi repontado pro clipe da lanca."
                                       : "AVISO: nao achei o estado Aparo — rode antes o Montar Aparo.");

            string arq = Application.dataPath + "/../" + SAIDA;
            try { System.IO.File.WriteAllText(arq, sb.ToString()); } catch { }
            Debug.Log(sb.ToString() + "\ngravado em " + arq);
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

        static AnimatorState Achar(AnimatorStateMachine camada, string nome)
        {
            foreach (var c in camada.states) if (c.state.name == nome) return c.state;
            return null;
        }
    }
}
