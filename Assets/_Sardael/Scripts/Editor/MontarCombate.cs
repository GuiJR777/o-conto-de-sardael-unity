using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Sardael;

namespace SardaelEditor
{
    /// <summary>
    /// Constroi o combate: dois AnimatorControllers e a cena do duelo.
    ///
    /// A movimentacao aprovada NAO e' reescrita. Este construtor chama o mesmo
    /// <see cref="MontarMovimento.ConstruirControlador"/> e pendura os golpes em cima.
    ///
    /// Nenhuma duracao de golpe e' escolhida por mim: toda saida de estado e' Exit Time 1,0,
    /// ou seja, "a animacao acabou". Os unicos numeros meus sao as duracoes de MISTURA entre
    /// dois estados (0,04 a 0,12 s).
    /// </summary>
    public static class MontarCombate
    {
        const string IP  = "Assets/SpearCombatAnimationV2/Animation/IP/A_SpearCombatAnimationV2_";
        const string RM  = "Assets/SpearCombatAnimationV2/Animation/RM/A_SpearCombatAnimationV2_";
        const string KEV = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/";

        const string PASTA      = "Assets/_Sardael/Combate";
        const string CTRL_HEROI = PASTA + "/Sardael_Combate.controller";
        const string CTRL_ORC   = PASTA + "/Orc_Combate.controller";
        const string CENA       = "Assets/_Sardael/Cenas/Combate_Duelo.unity";
        const string SARDAEL    = "Assets/_Sardael/Personagens/Sardael.prefab";
        const string ORC        = "Assets/_Sardael/Personagens/Orc.prefab";

        const string TAG = MovimentoDoHeroi.TAG_ACAO;

        // os tres elos do Attack4: o ataque da lanca com 3 estagios E reacao pareada em todos
        static readonly string[] ELOS = {
            "Attack4_Stage1_Complete", "Attack4_Stage2_Complete", "Attack4_Stage3_Complete" };

        const string GOLPE_DO_ORC  = KEV + "1H/HumanM@Attack1H01_R.fbx";
        const string PARADO_DO_ORC = KEV + "1H/HumanM@CombatIdle1H01.fbx";

        [MenuItem("Sardael/Montar Combate (controllers + cena)")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Combate] pare o Play."); return; }

            ArrumarImportacao();

            float impacto = MedirImpactoDoOrc();

            var doHeroi = ControladorDoHeroi();
            if (doHeroi == null) return;
            var doOrc = ControladorDoOrc(impacto);
            if (doOrc == null) return;

            ConstruirCena(doHeroi, doOrc);
        }

        // ------------------------------------------------------------------ importacao

        static void ArrumarImportacao()
        {
            // ATENCAO: RM, nao IP.
            //
            // Os clipes _IP sao "In Place": a vitima leva o golpe de pe, sem sair do lugar.
            // Servem pra demo, onde o par fica parado lado a lado. Em jogo nao lem como golpe.
            // Os mesmos 35 ataques existem em _RM, onde atacante e alvo andam.
            //
            // E o pacote entrega os dois com a RAIZ TRAVADA (Bake Into Pose). Com a trava, o
            // deslocamento existe dentro do FBX e o importador joga fora — foi o mesmo defeito
            // que zerou a esquiva. Destravar aqui e' o que faz o golpe empurrar.
            for (int i = 0; i < ELOS.Length; i++)
            {
                Destravar(RM + ELOS[i] + "_RM.FBX");
                Destravar(RM + ELOS[i] + "_React_RM.FBX");
            }
            Loop(RM + "Hit_Fw_RM.FBX", false);
            Loop(RM + "Hit_Bw_RM.FBX", false);
            Loop(GOLPE_DO_ORC, false);
            Loop(PARADO_DO_ORC, true);
        }

        /// <summary>
        /// Tira o loop e a trava da raiz: sem isso o clipe nunca "termina" e nunca desloca.
        /// </summary>
        static void Destravar(string caminho)
        {
            var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[Combate] nao achei " + caminho); return; }
            var clipes = imp.clipAnimations;
            if (clipes == null || clipes.Length == 0) clipes = imp.defaultClipAnimations;
            if (clipes == null || clipes.Length == 0) return;

            bool mudou = false;
            foreach (var c in clipes)
            {
                if (c.loopTime) { c.loopTime = false; mudou = true; }
                if (c.lockRootPositionXZ) { c.lockRootPositionXZ = false; mudou = true; }
                if (c.lockRootRotation) { c.lockRootRotation = false; mudou = true; }
                if (!c.keepOriginalPositionXZ) { c.keepOriginalPositionXZ = true; mudou = true; }
                if (!c.keepOriginalOrientation) { c.keepOriginalOrientation = true; mudou = true; }
            }
            if (!mudou) return;
            imp.clipAnimations = clipes;
            imp.SaveAndReimport();
            Debug.Log("[Combate] raiz destravada em " + System.IO.Path.GetFileNameWithoutExtension(caminho));
        }

        static void Loop(string caminho, bool querLoop)
        {
            var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[Combate] nao achei " + caminho); return; }

            var clipes = imp.clipAnimations;
            if (clipes == null || clipes.Length == 0) clipes = imp.defaultClipAnimations;
            if (clipes == null || clipes.Length == 0) return;

            bool mudou = false;
            foreach (var c in clipes)
                if (c.loopTime != querLoop) { c.loopTime = querLoop; mudou = true; }

            if (!mudou) return;
            imp.clipAnimations = clipes;
            imp.SaveAndReimport();
            Debug.Log("[Combate] loop " + (querLoop ? "ligado" : "desligado") + " em "
                      + System.IO.Path.GetFileNameWithoutExtension(caminho));
        }

        // ------------------------------------------------------------------ medir o impacto

        /// <summary>
        /// Onde, dentro do golpe do orc, a lamina chega mais longe pra frente.
        ///
        /// Amostra o clipe de ponta a ponta e olha a mao da arma no referencial do corpo. O
        /// quadro em que ela esta' mais avancada e' o impacto. Isto e' MEDIDO: se eu chutasse
        /// "metade do clipe", o orc acertaria no ar.
        /// </summary>
        static float MedirImpactoDoOrc()
        {
            var molde = AssetDatabase.LoadAssetAtPath<GameObject>(ORC);
            var clipe = MontarMovimento.Clipe(GOLPE_DO_ORC);
            if (molde == null || clipe == null)
            {
                Debug.LogWarning("[Combate] nao deu pra medir o impacto; usando meio do clipe");
                return 0.5f;
            }

            var corpo = (GameObject)PrefabUtility.InstantiatePrefab(molde);
            float melhor = float.NegativeInfinity, quando = 0.5f;
            try
            {
                var anim = corpo.GetComponent<Animator>();
                var mao = anim == null ? null : anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (mao == null) { Debug.LogWarning("[Combate] orc sem mao direita"); return 0.5f; }

                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                const int PASSOS = 90;
                for (int i = 0; i <= PASSOS; i++)
                {
                    float f = (float)i / PASSOS;
                    AnimationMode.SampleAnimationClip(corpo, clipe, clipe.length * f);
                    float aFrente = corpo.transform.InverseTransformPoint(mao.position).z;
                    if (aFrente > melhor) { melhor = aFrente; quando = f; }
                }
                AnimationMode.EndSampling();
                AnimationMode.StopAnimationMode();
            }
            finally { Object.DestroyImmediate(corpo); }

            Debug.Log(string.Format("[Combate] impacto do orc MEDIDO em {0:P0} do clipe "
                                    + "(mao a {1:F3} m a frente, clipe de {2:F3} s)",
                                    quando, melhor, clipe.length));
            return quando;
        }

        // ------------------------------------------------------------------ heroi

        static AnimatorController ControladorDoHeroi()
        {
            var ctrl = MontarMovimento.ConstruirControlador(CTRL_HEROI);
            if (ctrl == null) return null;

            ctrl.AddParameter(Duelo.P_ATACAR,   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(Duelo.P_ATINGIDO, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var locomocao = Estado(sm, "Locomocao");
            if (locomocao == null) { Debug.LogError("[Combate] nao achei Locomocao"); return null; }

            var golpes = new AnimatorState[ELOS.Length];
            for (int i = 0; i < ELOS.Length; i++)
            {
                var s = sm.AddState("Golpe" + (i + 1));
                s.motion = MontarMovimento.Clipe(RM + ELOS[i] + "_RM.FBX");
                if (s.motion == null) { Debug.LogError("[Combate] falta o golpe " + ELOS[i]); return null; }
                s.tag = TAG;
                var aviso = s.AddStateMachineBehaviour<AvisoDoAnimator>();
                aviso.aviso = "golpe" + (i + 1);
                golpes[i] = s;
            }

            // atacar so' sai da locomocao: nao se ataca no meio de um rolamento
            var t = locomocao.AddTransition(golpes[0]);
            t.hasExitTime = false; t.duration = 0.05f; t.hasFixedDuration = true;
            t.AddCondition(AnimatorConditionMode.If, 0f, Duelo.P_ATACAR);

            for (int i = 0; i < golpes.Length; i++)
            {
                // ORDEM IMPORTA: o Unity testa as transicoes na ordem em que foram criadas.
                // Emendar no proximo elo tem que vir ANTES de voltar pra locomocao.
                if (i + 1 < golpes.Length)
                {
                    t = golpes[i].AddTransition(golpes[i + 1]);
                    t.hasExitTime = true; t.exitTime = 1f;      // A ANIMACAO ACABOU
                    t.duration = 0.08f; t.hasFixedDuration = true;
                    t.AddCondition(AnimatorConditionMode.If, 0f, Duelo.P_ATACAR);
                }
                t = golpes[i].AddTransition(locomocao);
                t.hasExitTime = true; t.exitTime = 1f;          // acabou e ninguem pediu mais
                t.duration = 0.12f; t.hasFixedDuration = true;
            }

            var atingido = sm.AddState("Atingido");
            atingido.motion = MontarMovimento.Clipe(RM + "Hit_Fw_RM.FBX");
            atingido.tag = TAG;
            var ent = sm.AddAnyStateTransition(atingido);
            ent.hasExitTime = false; ent.duration = 0.06f; ent.hasFixedDuration = true;
            ent.canTransitionToSelf = false;
            ent.AddCondition(AnimatorConditionMode.If, 0f, Duelo.P_ATINGIDO);
            t = atingido.AddTransition(locomocao);
            t.hasExitTime = true; t.exitTime = 1f; t.duration = 0.12f; t.hasFixedDuration = true;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[Combate] controller do heroi: locomocao + 3 elos + atingido");
            return ctrl;
        }

        // ------------------------------------------------------------------ orc

        static AnimatorController ControladorDoOrc(float impacto)
        {
            System.IO.Directory.CreateDirectory(PASTA);
            AssetDatabase.DeleteAsset(CTRL_ORC);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CTRL_ORC);

            ctrl.AddParameter(Duelo.P_ORC_ATACA, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            var parado = sm.AddState("Parado");
            parado.motion = MontarMovimento.Clipe(PARADO_DO_ORC);
            sm.defaultState = parado;
            // avisa o Duelo que a reacao pareada acabou e o orc pode voltar a encarar o heroi
            var voltou = parado.AddStateMachineBehaviour<AvisoDoAnimator>();
            voltou.aviso = "orcParado";

            // UM estado de apanhar, com o clipe generico de dano — o mesmo do heroi.
            // As reacoes pareadas do pacote (Attack4_StageN_React) ficam guardadas pra
            // finalizacao, onde um encaixe cinematografico e' aceitavel. Em troca de golpe
            // elas obrigam os dois a ficar na mesma rotacao, que e' um de costas pro outro.
            // DOIS trancos, os dois do proprio pacote, escolhidos pelo quanto deslocam:
            //   Hit_Bw_RM  desloca 0,15 m  -> elos do meio: o orc cambaleia e continua ao alcance
            //   Hit_Fw_RM  desloca 1,32 m  -> ultimo elo: arremessa
            // Com 1,32 m em todo golpe, o orc saia do alcance e o terceiro passava longe.
            var levar = sm.AddState("Levar");
            levar.motion = MontarMovimento.Clipe(RM + "Hit_Bw_RM.FBX");
            var voltaLevar = levar.AddTransition(parado);
            voltaLevar.hasExitTime = true; voltaLevar.exitTime = 1f;
            voltaLevar.duration = 0.12f; voltaLevar.hasFixedDuration = true;

            var levarForte = sm.AddState("LevarForte");
            levarForte.motion = MontarMovimento.Clipe(RM + "Hit_Fw_RM.FBX");
            if (levar.motion == null || levarForte.motion == null)
            { Debug.LogError("[Combate] falta clipe de dano"); return null; }
            var voltaForte = levarForte.AddTransition(parado);
            voltaForte.hasExitTime = true; voltaForte.exitTime = 1f;
            voltaForte.duration = 0.12f; voltaForte.hasFixedDuration = true;

            var golpe = sm.AddState("Golpe");
            golpe.motion = MontarMovimento.Clipe(GOLPE_DO_ORC);
            var no = golpe.AddStateMachineBehaviour<AvisoNoImpacto>();
            no.quando = impacto;
            no.aviso = "impactoDoOrc";

            var entG = sm.AddAnyStateTransition(golpe);
            entG.hasExitTime = false; entG.duration = 0.06f; entG.hasFixedDuration = true;
            entG.canTransitionToSelf = false;
            entG.AddCondition(AnimatorConditionMode.If, 0f, Duelo.P_ORC_ATACA);
            var v = golpe.AddTransition(parado);
            v.hasExitTime = true; v.exitTime = 1f; v.duration = 0.12f; v.hasFixedDuration = true;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[Combate] controller do orc: parado + 3 reacoes + golpe");
            return ctrl;
        }

        static void LimparTocadores(GameObject alvo)
        {
            var sobras = alvo.GetComponentsInChildren<TocadorDeClipe>(true);
            for (int i = 0; i < sobras.Length; i++)
            {
                Debug.LogWarning("[Combate] TocadorDeClipe removido de " + alvo.name
                                 + " — ele sequestra a saida do Animator");
                Object.DestroyImmediate(sobras[i]);
            }
        }

        static AnimatorState Estado(AnimatorStateMachine sm, string nome)
        {
            foreach (var c in sm.states) if (c.state.name == nome) return c.state;
            return null;
        }

        // ------------------------------------------------------------------ cena

        static void ConstruirCena(AnimatorController doHeroi, AnimatorController doOrc)
        {
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var luz = new GameObject("Sol");
            luz.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = luz.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.1f; l.shadows = LightShadows.Soft;

            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Chao";
            chao.transform.localScale = new Vector3(6f, 1f, 2f);

            // marcos de 2 m: da' pra ler o afastamento do par direto na imagem
            for (int x = -10; x <= 10; x += 2)
            {
                if (x == 0) continue;
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m.name = "marco " + x;
                m.transform.position = new Vector3(x, 0.35f, 2.6f);
                m.transform.localScale = new Vector3(0.06f, 0.7f, 0.06f);
                Object.DestroyImmediate(m.GetComponent<Collider>());
            }

            var heroi = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(SARDAEL));
            heroi.name = "Sardael";
            heroi.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            var cc = heroi.GetComponent<CharacterController>();
            if (cc == null) cc = heroi.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f); cc.height = 1.8f; cc.radius = 0.3f;

            var animH = heroi.GetComponent<Animator>();
            if (animH == null) animH = heroi.AddComponent<Animator>();
            animH.runtimeAnimatorController = doHeroi;
            animH.applyRootMotion = true;
            // CullUpdateTransforms faz a maquina de estados andar e o CORPO parar quando o
            // Unity acha que ninguem esta' vendo. Num duelo isso e' veneno: o registro mostra
            // o estado certo e a tela mostra um boneco parado.
            animH.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (heroi.GetComponent<MovimentoDoHeroi>() == null) heroi.AddComponent<MovimentoDoHeroi>();
            MontarMovimento.EncaixarLanca(heroi);

            var orc = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(ORC));
            orc.name = "Orc";
            // nasce exatamente no afastamento que o criador animou
            orc.transform.SetPositionAndRotation(
                new Vector3(Duelo.ALCANCE, 0f, -Duelo.DESVIO), Quaternion.Euler(0f, -90f, 0f));
            var animO = orc.GetComponent<Animator>();
            if (animO == null) animO = orc.AddComponent<Animator>();
            animO.runtimeAnimatorController = doOrc;
            animO.applyRootMotion = true;   // e' o clipe que joga o orc pra tras
            animO.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (orc.GetComponent<CorpoDoOrc>() == null) orc.AddComponent<CorpoDoOrc>();

            // O TocadorDeClipe da bancada de animacao monta um PlayableGraph proprio e liga a
            // saida NESTE Animator — o que substitui inteiro o que o AnimatorController manda
            // pros ossos. A maquina de estados continua trocando de estado (o registro mostra
            // "Reacao1" direitinho) e o corpo fica no clipe unico da bancada, com deltaPosition
            // zerado. Veio de carona quando salvei o prefab a partir da bancada.
            LimparTocadores(heroi);
            LimparTocadores(orc);

            var duelo = heroi.AddComponent<Duelo>();
            duelo.doHeroi = animH;
            duelo.doOrc = animO;

            var cam = new GameObject("CameraLateral");
            var c = cam.AddComponent<Camera>();
            c.fieldOfView = 55f;
            cam.AddComponent<AudioListener>();
            var lateral = cam.AddComponent<CameraLateral>();
            lateral.alvo = heroi.transform;
            cam.transform.position = new Vector3(0f, 2.1f, -7f);

            EditorSceneManager.SaveScene(cena, CENA);
            PlayerSettings.runInBackground = true;
            Debug.Log("[Combate] cena criada: " + CENA);
        }
    }
}
