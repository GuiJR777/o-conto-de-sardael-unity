using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Sardael
{
    /// <summary>
    /// Constroi o AnimatorController do movimento e a cena de teste, do zero.
    ///
    /// POR QUE POR SCRIPT: cena e grafo montados a mao nao da' pra reconstruir depois de um
    /// erro, nem pra ler o que mudou. Montados por script, da' — e este arquivo vira a
    /// documentacao executavel do que o personagem sabe fazer.
    ///
    /// O QUE O GRAFO TEM, e por que assim:
    ///
    ///   Locomocao   uma Blend Tree em 'velocidade' (parado 0 / andar 2,4 / correr 4,5).
    ///               Blend Tree existe pra isso: a mistura entre parado, andar e correr e'
    ///               continua e o Unity resolve. Aqui mistura E' desejavel — o que nao pode
    ///               ter mistura e' golpe, que e' assunto do combate, mais pra frente.
    ///
    ///   Habilidades esquiva, rolar (frente e tras) e arranco. Entram de QUALQUER estado
    ///               (AnyState), porque a mao pede esquiva no meio de qualquer coisa; e saem
    ///               por EXIT TIME 1,0 — ou seja, quando a ANIMACAO TERMINA. E' o "comeco e
    ///               fim" pedido, e quem cronometra e' o Unity, nao um numero meu.
    ///
    ///   Pulo        tres estados: sair do chao, no ar (loop), aterrissar. O arco vem da
    ///               gravidade no codigo; os clipes sao in-place e so' vestem o movimento.
    ///               Sai do ar quando 'noChao' fica verdadeiro — condicao, nao tempo.
    ///
    /// Os estados de habilidade levam a TAG "acao". E' por ela que o codigo pergunta ao
    /// Animator se pode mexer no corpo, em vez de manter um bool paralelo que sai de sincronia.
    /// </summary>
    public static class MontarMovimento
    {
        const string IP = "Assets/SpearCombatAnimationV2/Animation/IP/A_SpearCombatAnimationV2_";
        const string RM = "Assets/SpearCombatAnimationV2/Animation/RM/A_SpearCombatAnimationV2_";
        const string PASTA = "Assets/_Sardael/Movimento";
        const string CTRL = PASTA + "/Sardael_Movimento.controller";
        const string CENA = "Assets/_Sardael/Cenas/Movimento_Teste.unity";
        const string SARDAEL = "Assets/_Sardael/Personagens/Sardael.prefab";

        const string TAG = MovimentoDoHeroi.TAG_ACAO;

        [MenuItem("Sardael/Montar Movimento (controller + cena)")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Movimento] pare o Play."); return; }

            ArrumarImportacao();
            var ctrl = ConstruirControlador(CTRL);
            if (ctrl == null) return;
            ConstruirCena(ctrl);
        }

        // ------------------------------------------------------------------ importacao

        /// <summary>
        /// Um clipe de uma vez so' (sair do chao, aterrissar, esquiva, rolar, arranco) NAO
        /// pode estar marcado como loop: se repete, ele nunca "termina", e terminar e'
        /// justamente o evento que faz a maquina de estados sair do estado. O pack veio do
        /// Unreal com loopTime ligado em tudo.
        /// </summary>
        static void ArrumarImportacao()
        {
            string[] umaVezSo = {
                IP + "Jump_Start_IP.fbx", IP + "Jump_Land_IP.fbx",
                RM + "Dodge_Bw_RM.fbx", RM + "Roll_Fw_RM.fbx",
                RM + "Dash_Fw_RM.fbx",
            };

            foreach (var caminho in umaVezSo)
            {
                var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
                if (imp == null) { Debug.LogWarning("[Movimento] nao achei " + caminho); continue; }

                var clipes = imp.clipAnimations;
                if (clipes == null || clipes.Length == 0) clipes = imp.defaultClipAnimations;
                if (clipes == null || clipes.Length == 0) continue;

                bool mudou = false;
                foreach (var c in clipes)
                    if (c.loopTime) { c.loopTime = false; mudou = true; }

                if (!mudou) continue;
                imp.clipAnimations = clipes;
                imp.SaveAndReimport();
                Debug.Log("[Movimento] loop desligado em " + System.IO.Path.GetFileNameWithoutExtension(caminho));
            }
        }

        // ------------------------------------------------------------------ controlador

        internal static AnimationClip Clipe(string caminho)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(caminho))
                if (o is AnimationClip && !o.name.StartsWith("__")) return (AnimationClip)o;
            Debug.LogError("[Movimento] clipe nao encontrado: " + caminho);
            return null;
        }

        /// <summary>
        /// Monta o grafo da locomocao no caminho pedido. O combate chama este mesmo metodo e
        /// depois PENDURA os golpes em cima — a movimentacao aprovada nao e' reescrita.
        /// </summary>
        internal static AnimatorController ConstruirControlador(string caminho)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(caminho));
            AssetDatabase.DeleteAsset(caminho);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(caminho);

            ctrl.AddParameter(MovimentoDoHeroi.P_VELOCIDADE, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(MovimentoDoHeroi.P_NO_CHAO,    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter(MovimentoDoHeroi.P_PULAR,      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(MovimentoDoHeroi.P_ESQUIVAR,   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(MovimentoDoHeroi.P_ROLAR,      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(MovimentoDoHeroi.P_ARRANCAR,   AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            // ---------- locomocao: uma Blend Tree em velocidade ----------
            var parado = Clipe(IP + "Idle1_IP.fbx");
            var andar  = Clipe(IP + "Walk_Fw_IP.fbx");
            var correr = Clipe(IP + "Run_Fw_IP.fbx");
            if (parado == null || andar == null || correr == null) return null;

            BlendTree arvore;
            var locomocao = ctrl.CreateBlendTreeInController("Locomocao", out arvore, 0);
            arvore.blendParameter = MovimentoDoHeroi.P_VELOCIDADE;
            arvore.blendType = BlendTreeType.Simple1D;
            arvore.useAutomaticThresholds = false;
            arvore.AddChild(parado, 0f);
            arvore.AddChild(andar,  2.4f);
            arvore.AddChild(correr, 4.5f);
            sm.defaultState = locomocao;

            // ---------- pulo ----------
            var puloSaida = sm.AddState("PuloSaida");
            puloSaida.motion = Clipe(IP + "Jump_Start_IP.fbx");
            var puloNoAr = sm.AddState("PuloNoAr");
            puloNoAr.motion = Clipe(IP + "Jump_OnAir_IP.fbx");
            var puloQueda = sm.AddState("PuloQueda");
            puloQueda.motion = Clipe(IP + "Jump_Land_IP.fbx");

            // ---------- habilidades ----------
            var esquiva = sm.AddState("Esquiva");
            esquiva.motion = Clipe(RM + "Dodge_Bw_RM.fbx");
            esquiva.tag = TAG;

            // Rolar e' SEMPRE pra frente. O rolamento de costas (Roll_Bw) fica guardado pro
            // combate, onde o corpo fica virado pro inimigo e recuar rolando faz sentido.
            // Aqui ele seria inalcancavel: pra pedir "pra tras" a mao teria que segurar o lado
            // oposto, e segurar o lado oposto ja' vira o personagem — viraria rolar pra frente
            // no mesmo quadro. E recuar ja' e' o CTRL, que anda 1,85 m pra tras sem virar.
            var rolar = sm.AddState("Rolar");
            rolar.motion = Clipe(RM + "Roll_Fw_RM.fbx");
            rolar.tag = TAG;

            var arranco = sm.AddState("Arranco");
            arranco.motion = Clipe(RM + "Dash_Fw_RM.fbx");
            arranco.tag = TAG;

            // ---------- transicoes: pulo ----------
            // sair do chao nao espera nada: e' resposta a tecla
            var t = locomocao.AddTransition(puloSaida);
            t.hasExitTime = false; t.duration = 0.05f; t.hasFixedDuration = true;
            t.AddCondition(AnimatorConditionMode.If, 0f, MovimentoDoHeroi.P_PULAR);

            // sair do chao -> no ar: quando a animacao de impulso acaba
            t = puloSaida.AddTransition(puloNoAr);
            t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.10f; t.hasFixedDuration = true;

            // AO TOCAR O CHAO, DUAS SAIDAS — a decisao e' a velocidade, nao o tempo.
            //
            // Parado, a animacao de queda inteira e' o certo: ele amortece e levanta.
            // Em movimento, ela vira escorregao: o teste mostrou 0,58 s deslizando a 2,4 m/s
            // numa pose de aterrissagem. Quem pousa correndo volta a correr na hora.
            t = puloNoAr.AddTransition(puloQueda);
            t.hasExitTime = false; t.duration = 0.06f; t.hasFixedDuration = true;
            t.AddCondition(AnimatorConditionMode.If, 0f, MovimentoDoHeroi.P_NO_CHAO);
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, MovimentoDoHeroi.P_VELOCIDADE);

            t = puloNoAr.AddTransition(locomocao);
            t.hasExitTime = false; t.duration = 0.10f; t.hasFixedDuration = true;
            t.AddCondition(AnimatorConditionMode.If, 0f, MovimentoDoHeroi.P_NO_CHAO);
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, MovimentoDoHeroi.P_VELOCIDADE);

            // aterrissar -> locomocao: quando a animacao de queda acaba
            t = puloQueda.AddTransition(locomocao);
            t.hasExitTime = true; t.exitTime = 0.80f; t.duration = 0.12f; t.hasFixedDuration = true;

            // rede de seguranca: se cair sem pular (andou pra fora de uma borda)
            t = locomocao.AddTransition(puloNoAr);
            t.hasExitTime = false; t.duration = 0.10f; t.hasFixedDuration = true;
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, MovimentoDoHeroi.P_NO_CHAO);

            // ---------- transicoes: habilidades ----------
            // entram de QUALQUER estado, porque a mao pede esquiva no meio de qualquer coisa
            Habilidade(sm, esquiva,     MovimentoDoHeroi.P_ESQUIVAR, null, false, locomocao);
            Habilidade(sm, rolar,       MovimentoDoHeroi.P_ROLAR, null, false, locomocao);
            Habilidade(sm, arranco,     MovimentoDoHeroi.P_ARRANCAR, null, false, locomocao);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            Debug.Log("[Movimento] controller criado: " + caminho);
            Debug.Log("[Movimento] estados: Locomocao(blend) + pulo(3) + habilidades(4)");
            return ctrl;
        }

        /// <summary>
        /// Liga uma habilidade: entra de AnyState pelo gatilho, sai por EXIT TIME 1,0 — ou
        /// seja, quando a animacao termina. Nenhuma duracao chutada por mim.
        /// </summary>
        static void Habilidade(AnimatorStateMachine sm, AnimatorState estado,
                               string gatilho, string condicaoBool, bool valorDoBool,
                               AnimatorState volta)
        {
            var entrada = sm.AddAnyStateTransition(estado);
            entrada.hasExitTime = false;
            entrada.duration = 0.05f;
            entrada.hasFixedDuration = true;
            entrada.canTransitionToSelf = false;
            entrada.AddCondition(AnimatorConditionMode.If, 0f, gatilho);
            if (condicaoBool != null)
                entrada.AddCondition(valorDoBool ? AnimatorConditionMode.If
                                                 : AnimatorConditionMode.IfNot, 0f, condicaoBool);

            var saida = estado.AddTransition(volta);
            saida.hasExitTime = true;
            saida.exitTime = 1f;        // A ANIMACAO TERMINOU. E' este o evento.
            saida.duration = 0.12f;
            saida.hasFixedDuration = true;
        }

        // ------------------------------------------------------------------ cena

        static void ConstruirCena(AnimatorController ctrl)
        {
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var luz = new GameObject("Luz");
            var l = luz.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.1f; l.shadows = LightShadows.Soft;
            luz.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Chao";
            chao.transform.localScale = new Vector3(8f, 1f, 2f);   // 80 x 20 m

            // marcas de 5 em 5 metros: sem referencia no chao nao da' pra ver deslocamento
            var marcas = new GameObject("Marcas");
            for (int x = -30; x <= 30; x += 5)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m.name = "marca " + x;
                m.transform.SetParent(marcas.transform, false);
                m.transform.position = new Vector3(x, 0.02f, 2.5f);
                m.transform.localScale = new Vector3(0.12f, 0.04f, 1.6f);
                Object.DestroyImmediate(m.GetComponent<BoxCollider>());
            }

            var heroi = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(SARDAEL));
            heroi.name = "Sardael";
            heroi.transform.SetPositionAndRotation(new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

            var cc = heroi.GetComponent<CharacterController>();
            if (cc == null) cc = heroi.AddComponent<CharacterController>();
            cc.height = 1.75f; cc.radius = 0.32f; cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f; cc.stepOffset = 0.35f;

            var anim = heroi.GetComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = true;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (heroi.GetComponent<MovimentoDoHeroi>() == null)
                heroi.AddComponent<MovimentoDoHeroi>();

            EncaixarLanca(heroi);

            var cam = new GameObject("Camera");
            var c = cam.AddComponent<Camera>();
            c.fieldOfView = 42f; c.nearClipPlane = 0.1f; c.farClipPlane = 200f;
            cam.tag = "MainCamera";
            cam.AddComponent<AudioListener>();
            var lateral = cam.AddComponent<CameraLateral>();
            lateral.alvo = heroi.transform;
            lateral.acharSozinho = false;
            lateral.altura = 2.1f; lateral.distancia = 7f; lateral.inclinacao = 6f;
            lateral.olharAFrente = 1.4f; lateral.suavidade = 0.16f;
            lateral.usarLimites = true; lateral.xMinimo = -22f; lateral.xMaximo = 22f;
            cam.transform.position = new Vector3(0f, 2.1f, -7f);
            cam.transform.rotation = Quaternion.Euler(6f, 0f, 0f);

            PlayerSettings.runInBackground = true;   // senao o Play congela sem foco na janela

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CENA));
            EditorSceneManager.SaveScene(cena, CENA);
            Debug.Log("[Movimento] cena criada: " + CENA);
            Debug.Log("[Movimento] A/D andar · SHIFT correr · ESPACO pular · CTRL esquiva · C rolar · ALT arranco");
        }

        // ------------------------------------------------------------------ a lanca

        const string LANCA = "Assets/Synty/PolygonGoblinWarCamp/Prefabs/Weapons/SM_Wep_Spear_01.prefab";

        /// <summary>
        /// Poe a lanca na mao direita do Sardael.
        ///
        /// OS NUMEROS NAO SAO MEUS. Foram lidos do objeto "Lanca" que ja' esta' encaixado na
        /// bancada de Finishers_Sardael — a cena que ele aprovou. Mesmo osso (Hand_R), mesma
        /// posicao, mesma rotacao, mesma escala. Copiar o que ja' funciona vale mais do que
        /// recalcular um encaixe que ja' foi resolvido uma vez.
        ///
        /// A escala parece absurda (96x) porque os ossos da Synty carregam lossyScale 0,01:
        /// 96,269 x 0,01 = 0,963 no mundo. A lanca fica com 2,16 m de ponta a ponta.
        /// </summary>
        internal static void EncaixarLanca(GameObject heroi)
        {
            var mao = Procurar(heroi.transform, "Hand_R");
            if (mao == null) { Debug.LogWarning("[Movimento] nao achei o osso Hand_R"); return; }

            var velha = Procurar(heroi.transform, "Lanca");
            if (velha != null) Object.DestroyImmediate(velha.gameObject);

            var molde = AssetDatabase.LoadAssetAtPath<GameObject>(LANCA);
            if (molde == null) { Debug.LogWarning("[Movimento] nao achei o prefab da lanca"); return; }

            var arma = (GameObject)PrefabUtility.InstantiatePrefab(molde);
            arma.name = "Lanca";
            arma.transform.SetParent(mao, false);
            arma.transform.localPosition    = new Vector3(9.28287f, 7.41498f, -11.86496f);
            arma.transform.localEulerAngles = new Vector3(344.698f, 78.331f, 290.765f);
            arma.transform.localScale       = new Vector3(96.26907f, 96.26904f, 96.26903f);

            Debug.Log("[Movimento] lanca encaixada em Hand_R");
        }

        static Transform Procurar(Transform raiz, string nome)
        {
            if (raiz.name == nome) return raiz;
            for (int i = 0; i < raiz.childCount; i++)
            {
                var achado = Procurar(raiz.GetChild(i), nome);
                if (achado != null) return achado;
            }
            return null;
        }

    }
}
