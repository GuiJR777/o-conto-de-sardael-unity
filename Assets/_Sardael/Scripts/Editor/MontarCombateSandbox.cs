using System.IO;
using Sardael;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SardaelEditor
{
    public static class MontarCombateSandbox
    {
        const string Cena = "Assets/_Sardael/Cenas/Combate_Sandbox.unity";
        const string PastaCombate = "Assets/_Sardael/Combate";
        const string PastaDefinicoes = PastaCombate + "/Definicoes";
        const string SardaelPrefab = "Assets/_Sardael/Personagens/Sardael.prefab";
        const string OrcPrefab = "Assets/_Sardael/Personagens/Orc.prefab";
        const string SardaelController = "Assets/_Sardael/Animacoes/Controladores/Sardael_Combate.controller";
        const string OrcController = "Assets/_Sardael/Animacoes/Controladores/Orc_Combate.controller";
        const string InputActions = "Assets/Settings/InputSystem_Actions.inputactions";
        const string PastaControllersDeExecucao =
            "Assets/_Pacotes/Full_Mount_Attacks/Art/AnimatorControllers/Paired_FullMount_";
        const string PastaControllersDeExecucaoEmPe =
            "Assets/_Pacotes/SpearCombatAnimationV2/Controller/RM/A_SpearCombatAnimationV2_";
        const string DerrubadaEscolhida = "Takedown_DoubleLeg_Start";
        const string FxSangue =
            "Assets/_Pacotes/Synty/PolygonGeneric/Prefabs/FX/FX_Blood_Splatter_01.prefab";
        const string CabecaDecepada = "Assets/_Sardael/Personagens/Gore/Cabeca_Orc_Decepada.prefab";
        const string ClipBloqueio =
            "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/Polearm/HumanM@ParryPolearm01 - Loop.fbx";
        const string ClipStun =
            "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Stun01.fbx";
        const string ClipMorte =
            "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDeath01.fbx";
        const string ClipAparo =
            "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/Polearm/HumanM@ParryPolearm01 - Hit.fbx";
        const string ClipAndarFrente =
            "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        const string ClipAndarTras =
            "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Backward.fbx";

        static readonly float[] Alcances = { 1.756f, 1.166f, 1.756f, 1.756f };
        static readonly float[] Distancias = { 1.45f, 1.0f, 1.45f, 1.45f };
        static readonly float[] Impactos = { 0.42f, 0.45f, 0.44f, 0.48f };
        static readonly float[] AlcancesMultiAlvo = { 0f, 3.5f, 4f, 4f };
        static readonly TipoDeDeslocamento[] Deslocamentos =
        {
            TipoDeDeslocamento.Push,
            TipoDeDeslocamento.Push,
            TipoDeDeslocamento.Push,
            TipoDeDeslocamento.Push
        };
        static readonly float[] DistanciasDeslocamento = { 0.2f, 0.3f, 0.35f, 0.5f };
        static readonly string[] ExecucoesNoChao =
        {
            "1HM_SlashNeck",
            "H2H_HeadSmash"
        };
        // O Double Leg da bancada comeca com 0,991 m entre os pivôs. Os pares de chao
        // partem sobrepostos e recebem o reposicionamento ao fim da derrubada.
        static readonly float[] ImpactosNoChao = { 0.68f, 0.72f };
        static readonly float[] DuracoesNoChao = { 1.25f, 1.42f };
        static readonly bool[] DecapitacoesNoChao = { true, false };
        static readonly string[] ExecucoesEmPe =
        {
            "Attack7_Stage3_Complete",
            "Attack8_Stage4_Complete"
        };
        // Os pares RM da lanca foram animados a 1,756 m e 0,041 m de lado, com os dois
        // personagens na mesma rotacao. A duracao termina pouco antes do loop do controller.
        static readonly float[] ImpactosEmPe = { 1.30f, 1.18f };
        static readonly float[] DuracoesEmPe = { 2.25f, 2.05f };
        static readonly PadraoDeAlvo[] Padroes =
        {
            PadraoDeAlvo.Single,
            PadraoDeAlvo.FrontTwo,
            PadraoDeAlvo.FrontTwo,
            PadraoDeAlvo.FrontThree
        };

        [MenuItem("Sardael/Combate/Abrir ou reconstruir Sandbox")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Combate Sandbox] pare o Play Mode antes de reconstruir a cena.");
                return;
            }

            var prefabHeroi = AssetDatabase.LoadAssetAtPath<GameObject>(SardaelPrefab);
            var prefabOrc = AssetDatabase.LoadAssetAtPath<GameObject>(OrcPrefab);
            var controllerHeroi = AssetDatabase.LoadAssetAtPath<AnimatorController>(SardaelController);
            var controllerOrc = AssetDatabase.LoadAssetAtPath<AnimatorController>(OrcController);
            var controllerDerrubadaHeroi = CarregarControllerDeExecucao(DerrubadaEscolhida, "_Att");
            var controllerDerrubadaVitima = CarregarControllerDeExecucao(DerrubadaEscolhida, "_Vic");
            var variantesDeExecucao = CarregarVariantesDeExecucao();
            var fxSangue = AssetDatabase.LoadAssetAtPath<GameObject>(FxSangue);
            var cabecaDecepada = AssetDatabase.LoadAssetAtPath<GameObject>(CabecaDecepada);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActions);

            if (prefabHeroi == null || prefabOrc == null || controllerHeroi == null ||
                controllerOrc == null || controllerDerrubadaHeroi == null ||
                controllerDerrubadaVitima == null || variantesDeExecucao == null || input == null)
            {
                Debug.LogError("[Combate Sandbox] faltam prefabs, controllers ou Input Actions obrigatorios.");
                return;
            }
            input = GarantirAcoesEspeciais(input);
            GarantirAnimacoesCombateReal(controllerHeroi, controllerOrc);

            Directory.CreateDirectory(PastaDefinicoes);
            DefinicaoDeAtaque[] definicoes = CriarDefinicoes();
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CriarLuz();
            CriarArena();

            var sistemas = new GameObject("SistemasDeCombate");
            var registro = sistemas.AddComponent<RegistroDeCombate>();
            var flow = sistemas.AddComponent<SistemaDeFlow>();
            var resolvedor = sistemas.AddComponent<ResolvedorDeImpacto>();
            resolvedor.Configurar(registro, flow);
            var diretor = sistemas.AddComponent<DiretorDeCombate>();
            var modo = sistemas.AddComponent<ModoDeCombate>();

            var heroi = (GameObject)PrefabUtility.InstantiatePrefab(prefabHeroi);
            heroi.name = "Sardael";
            heroi.transform.SetPositionAndRotation(
                new Vector3(-9f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            RemoverLegado(heroi);

            var animatorHeroi = heroi.GetComponent<Animator>();
            if (animatorHeroi == null) animatorHeroi = heroi.AddComponent<Animator>();
            animatorHeroi.runtimeAnimatorController = controllerHeroi;
            animatorHeroi.applyRootMotion = true;
            animatorHeroi.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var trocador = heroi.GetComponent<TrocadorDeAnimacoes>();
            if (trocador == null) trocador = heroi.AddComponent<TrocadorDeAnimacoes>();
            trocador.Configurar(animatorHeroi);
            TrocadorDeAnimacoesEditor.Sincronizar(trocador);
            var clipeAparo = CarregarClip(ClipAparo);
            TrocadorDeAnimacoesEditor.DefinirSubstituicao(trocador, "Aparo", clipeAparo);
            TrocadorDeAnimacoesEditor.DefinirReversao(trocador, "Aparo", true);

            var cc = heroi.GetComponent<CharacterController>();
            if (cc == null) cc = heroi.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.3f;

            var movimento = heroi.GetComponent<MovimentoDoHeroi>();
            if (movimento == null) movimento = heroi.AddComponent<MovimentoDoHeroi>();
            movimento.zDaLinha = 0f;
            movimento.xMinimo = -17f;
            movimento.xMaximo = 17f;
            var entrada = heroi.GetComponent<EntradaDeCombate>();
            if (entrada == null) entrada = heroi.AddComponent<EntradaDeCombate>();
            entrada.Configurar(input);
            var combate = heroi.GetComponent<CombateDoHeroi>();
            if (combate == null) combate = heroi.AddComponent<CombateDoHeroi>();
            combate.Configurar(movimento, entrada, registro, resolvedor, animatorHeroi, definicoes);
            var defesa = heroi.GetComponent<DefesaDoHeroi>();
            if (defesa == null) defesa = heroi.AddComponent<DefesaDoHeroi>();
            defesa.Configurar(entrada, combate, movimento, animatorHeroi, flow, diretor);
            var vida = heroi.GetComponent<VidaDoHeroi>();
            if (vida == null) vida = heroi.AddComponent<VidaDoHeroi>();
            vida.Configurar(movimento, animatorHeroi, flow, diretor, defesa);
            MontarMovimento.EncaixarLanca(heroi);

            var inimigos = new[]
            {
                CriarInimigo(prefabOrc, controllerOrc, registro, heroi.transform, fxSangue, cabecaDecepada, "E1", new Vector3(-2.8f, 0f, 2.6f)),
                CriarInimigo(prefabOrc, controllerOrc, registro, heroi.transform, fxSangue, cabecaDecepada, "E2", new Vector3(0f, 0f, 4.1f)),
                CriarInimigo(prefabOrc, controllerOrc, registro, heroi.transform, fxSangue, cabecaDecepada, "E3", new Vector3(3.2f, 0f, 2.2f)),
                CriarInimigo(prefabOrc, controllerOrc, registro, heroi.transform, fxSangue, cabecaDecepada, "E4", new Vector3(3.4f, 0f, -2.3f)),
                CriarInimigo(prefabOrc, controllerOrc, registro, heroi.transform, fxSangue, cabecaDecepada, "E5", new Vector3(0f, 0f, -4.0f)),
                CriarInimigo(prefabOrc, controllerOrc, registro, heroi.transform, fxSangue, cabecaDecepada, "E6", new Vector3(-2.8f, 0f, -2.5f))
            };

            CriarCamera(heroi.transform, registro, combate, out var cameraLateral, out var cameraCombate);
            diretor.Configurar(registro, heroi.transform);
            diretor.DefinirAtivo(false);
            var execucao = heroi.AddComponent<SistemaDeExecucao>();
            execucao.Configurar(
                movimento, entrada, registro, flow, diretor, animatorHeroi,
                controllerDerrubadaHeroi, controllerDerrubadaVitima,
                variantesDeExecucao);
            modo.Configurar(movimento, registro, diretor, cameraCombate);
            CriarEncontro(modo, inimigos);
            sistemas.AddComponent<PainelCombateSandbox>().Configurar(
                combate, registro, resolvedor, diretor, flow, execucao, vida, defesa, modo, movimento);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena, Cena);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = heroi;
            Debug.Log("[Combate Sandbox] criada e aberta: " + Cena);
        }

        [MenuItem("Sardael/Combate/Abrir Sandbox")]
        public static void Abrir()
        {
            if (File.Exists(Cena)) EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
            else Executar();
        }

        [MenuItem("Sardael/Combate/Atualizar quatro finalizacoes na cena aberta")]
        public static void AtualizarFinalizacoesNaCenaAberta()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Combate Sandbox] pare o Play Mode antes de atualizar as finalizacoes.");
                return;
            }

            var sistema = Object.FindAnyObjectByType<SistemaDeExecucao>(FindObjectsInactive.Include);
            var novasVariantes = CarregarVariantesDeExecucao();
            if (sistema == null || novasVariantes == null)
            {
                Debug.LogError("[Combate Sandbox] sistema ou pares de finalizacao ausentes na cena aberta.");
                return;
            }

            sistema.ConfigurarVariantes(novasVariantes);
            EditorUtility.SetDirty(sistema);
            EditorSceneManager.MarkSceneDirty(sistema.gameObject.scene);
            EditorSceneManager.SaveScene(sistema.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Combate Sandbox] finalizacoes atualizadas: 2 no chao + 2 em pe.");
        }

        static DefinicaoDeAtaque[] CriarDefinicoes()
        {
            var resultado = new DefinicaoDeAtaque[4];
            for (int i = 0; i < resultado.Length; i++)
            {
                string caminho = PastaDefinicoes + "/Golpe" + (i + 1) + ".asset";
                var definicao = AssetDatabase.LoadAssetAtPath<DefinicaoDeAtaque>(caminho);
                if (definicao == null)
                {
                    definicao = ScriptableObject.CreateInstance<DefinicaoDeAtaque>();
                    AssetDatabase.CreateAsset(definicao, caminho);
                }

                definicao.id = "golpe_" + (i + 1);
                definicao.eloCombo = i + 1;
                definicao.alcance = Alcances[i];
                definicao.alcanceMagnetismo = Mathf.Max(Alcances[i] + 1.5f, 8.5f);
                definicao.alcanceMultiAlvo = AlcancesMultiAlvo[i];
                definicao.distanciaDesejada = Distancias[i];
                definicao.velocidadeAproximacao = 14f;
                definicao.dano = 20f;
                definicao.poise = 25f;
                definicao.padraoDeAlvo = Padroes[i];
                definicao.deslocamento = Deslocamentos[i];
                definicao.distanciaDeslocamento = DistanciasDeslocamento[i];
                definicao.velocidadeDeslocamento = 6f;
                definicao.alturaLancamento = 1.1f;
                definicao.flowGerado = 10f;
                definicao.momentoDoImpacto = Impactos[i];
                EditorUtility.SetDirty(definicao);
                resultado[i] = definicao;
            }
            return resultado;
        }

        public static VarianteDeExecucao[] CarregarVariantesDeExecucao()
        {
            var resultado = new VarianteDeExecucao[ExecucoesNoChao.Length + ExecucoesEmPe.Length];
            for (int i = 0; i < ExecucoesNoChao.Length; i++)
            {
                string baseDoNome = ExecucoesNoChao[i];
                var executor = CarregarControllerDeExecucao(baseDoNome, "_Att");
                var vitima = CarregarControllerDeExecucao(baseDoNome, "_Vic");
                if (executor == null || vitima == null)
                {
                    Debug.LogError("[Combate Sandbox] par de execucao ausente: " + baseDoNome);
                    return null;
                }

                resultado[i] = new VarianteDeExecucao(
                    baseDoNome,
                    executor,
                    vitima,
                    PosturaDeExecucao.NoChao,
                    0.991f,
                    0f,
                    ImpactosNoChao[i],
                    DuracoesNoChao[i],
                    DecapitacoesNoChao[i]);
            }

            for (int i = 0; i < ExecucoesEmPe.Length; i++)
            {
                string baseDoNome = ExecucoesEmPe[i];
                var executor = CarregarControllerDeExecucaoEmPe(baseDoNome, false);
                var vitima = CarregarControllerDeExecucaoEmPe(baseDoNome, true);
                if (executor == null || vitima == null)
                {
                    Debug.LogError("[Combate Sandbox] par de execucao em pe ausente: " + baseDoNome);
                    return null;
                }

                int indice = ExecucoesNoChao.Length + i;
                resultado[indice] = new VarianteDeExecucao(
                    baseDoNome,
                    executor,
                    vitima,
                    PosturaDeExecucao.EmPe,
                    1.756f,
                    0.041f,
                    ImpactosEmPe[i],
                    DuracoesEmPe[i]);
            }
            return resultado;
        }

        static RuntimeAnimatorController CarregarControllerDeExecucao(string nome, string papel)
        {
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                PastaControllersDeExecucao + nome + papel + ".controller");
        }

        static RuntimeAnimatorController CarregarControllerDeExecucaoEmPe(string nome, bool reacao)
        {
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                PastaControllersDeExecucaoEmPe + nome +
                (reacao ? "_React_RM.controller" : "_RM.controller"));
        }

        static AnimationClip[] CarregarAnimacoesDeMorte()
        {
            // As quatro mortes do pacote, pra demonstrar o sorteio. Da' pra enxugar a lista
            // no Inspector de cada inimigo.
            return new[]
            {
                CarregarClip("Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDeath01.fbx"),
                CarregarClip("Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDeath02.fbx"),
                CarregarClip("Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDeath03.fbx"),
                CarregarClip("Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDeath04.fbx")
            };
        }

        static void CriarLuz()
        {
            var sol = new GameObject("Sol");
            sol.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var luz = sol.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.1f;
            luz.shadows = LightShadows.Soft;
        }

        static void CriarArena()
        {
            var chao = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chao.name = "Chao";
            chao.transform.position = new Vector3(0f, -0.5f, 0f);
            chao.transform.localScale = new Vector3(24f, 1f, 14f);

            CriarLimite("LimiteEsquerdo", new Vector3(-12f, 1.5f, 0f), new Vector3(0.4f, 4f, 14f));
            CriarLimite("LimiteDireito", new Vector3(12f, 1.5f, 0f), new Vector3(0.4f, 4f, 14f));
            CriarLimite("LimiteFundo", new Vector3(0f, 1.5f, 7f), new Vector3(24f, 4f, 0.4f));
            CriarLimite("LimiteFrente", new Vector3(0f, 1.5f, -7f), new Vector3(24f, 4f, 0.4f));

            for (int x = -16; x <= 16; x += 2)
            {
                if (x == 0) continue;
                var marco = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marco.name = "Marco_" + x;
                marco.transform.position = new Vector3(x, 0.05f, 5.8f);
                marco.transform.localScale = new Vector3(0.05f, 0.1f, 0.4f);
                Object.DestroyImmediate(marco.GetComponent<Collider>());
            }
        }

        static void CriarLimite(string nome, Vector3 posicao, Vector3 escala)
        {
            var limite = GameObject.CreatePrimitive(PrimitiveType.Cube);
            limite.name = nome;
            limite.transform.position = posicao;
            limite.transform.localScale = escala;
        }

        static AlvoDeCombate CriarInimigo(
            GameObject prefab,
            RuntimeAnimatorController controller,
            RegistroDeCombate registro,
            Transform jogador,
            GameObject fxSangue,
            GameObject cabecaDecepada,
            string nome,
            Vector3 posicao)
        {
            var inimigo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inimigo.name = nome;
            Vector3 paraJogador = jogador.position - posicao;
            paraJogador.y = 0f;
            inimigo.transform.SetPositionAndRotation(posicao, Quaternion.LookRotation(paraJogador));
            RemoverLegado(inimigo);

            var animator = inimigo.GetComponent<Animator>();
            if (animator == null) animator = inimigo.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var controlador = inimigo.GetComponent<CharacterController>();
            if (controlador == null) controlador = inimigo.AddComponent<CharacterController>();
            controlador.center = new Vector3(0f, 0.9f, 0f);
            controlador.height = 1.8f;
            controlador.radius = 0.35f;
            controlador.skinWidth = 0.04f;
            foreach (var colisor in inimigo.GetComponents<Collider>())
                if (colisor != controlador) Object.DestroyImmediate(colisor);

            var reacao = inimigo.AddComponent<ReacaoDeCombate>();
            reacao.Configurar(animator);
            var motor = inimigo.AddComponent<MotorDeCombateDoInimigo>();
            motor.Configurar(reacao);
            var alvo = inimigo.AddComponent<AlvoDeCombate>();
            alvo.Configurar(registro, reacao, motor);
            alvo.AnimacoesDeMorte = CarregarAnimacoesDeMorte();
            alvo.SumirAposMorte = true;
            alvo.SegundosAteSumir = 5f;
            inimigo.AddComponent<BarraDeVidaDoInimigo>();
            inimigo.AddComponent<IndicadorDeExecucao>().Configurar(alvo, jogador);
            var efeito = inimigo.AddComponent<EfeitoDeFinalizacao>();
            efeito.automaticoPorAnimacao = false;
            efeito.repetirNoLoop = false;
            efeito.instante = 0.62f;
            efeito.fxSangue = fxSangue;
            efeito.decapitar = cabecaDecepada != null;
            efeito.prefabDaCabeca = cabecaDecepada;
            efeito.quantidade = 12f;
            efeito.tamanho = 3.2f;
            efeito.alcance = 2f;
            return alvo;
        }

        static InputActionAsset GarantirAcoesEspeciais(InputActionAsset input)
        {
            // Com Domain Reload desligado, o asset pode continuar habilitado depois do Play Mode.
            // O Input System proibe alterar mapas nesse estado, mesmo ja estando no Edit Mode.
            input.Disable();
            var mapa = input.FindActionMap("Player", true);
            if (mapa.FindAction("Execution", false) == null)
            {
                var acao = mapa.AddAction("Execution", InputActionType.Button);
                acao.AddBinding("<Keyboard>/q", groups: "Keyboard&Mouse");
                acao.AddBinding("<Gamepad>/rightShoulder", groups: "Gamepad");
            }
            if (mapa.FindAction("FlowSpecial", false) == null)
            {
                var acao = mapa.AddAction("FlowSpecial", InputActionType.Button);
                acao.AddBinding("<Keyboard>/r", groups: "Keyboard&Mouse");
                acao.AddBinding("<Gamepad>/rightTrigger", groups: "Gamepad");
            }
            if (mapa.FindAction("Block", false) == null)
            {
                var acao = mapa.AddAction("Block", InputActionType.Button);
                acao.AddBinding("<Keyboard>/f", groups: "Keyboard&Mouse");
                acao.AddBinding("<Mouse>/rightButton", groups: "Keyboard&Mouse");
                acao.AddBinding("<Gamepad>/leftShoulder", groups: "Gamepad");
            }
            if (mapa.FindAction("Dodge", false) == null)
            {
                var acao = mapa.AddAction("Dodge", InputActionType.Button);
                acao.AddBinding("<Keyboard>/leftCtrl", groups: "Keyboard&Mouse");
                acao.AddBinding("<Gamepad>/buttonEast", groups: "Gamepad");
            }
            File.WriteAllText(InputActions, input.ToJson());
            AssetDatabase.ImportAsset(InputActions, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActions);
        }

        static void GarantirAnimacoesCombateReal(
            AnimatorController controllerHeroi,
            AnimatorController controllerOrc)
        {
            var bloqueio = CarregarClip(ClipBloqueio);
            var stun = CarregarClip(ClipStun);
            var morte = CarregarClip(ClipMorte);
            var andarFrente = CarregarClip(ClipAndarFrente);
            var andarTras = CarregarClip(ClipAndarTras);
            if (bloqueio == null || stun == null || morte == null || andarFrente == null || andarTras == null)
            {
                Debug.LogError("[Combate Sandbox] faltam clipes de bloqueio, stun, morte ou caminhada.");
                return;
            }

            GarantirParametro(controllerHeroi, "bloqueando", AnimatorControllerParameterType.Bool);
            GarantirParametro(controllerHeroi, "contraAtacar", AnimatorControllerParameterType.Trigger);
            GarantirParametro(controllerHeroi, "morrer", AnimatorControllerParameterType.Trigger);
            MontarEsquivaDirecional.Configurar(controllerHeroi);

            var maquinaHeroi = controllerHeroi.layers[0].stateMachine;
            var locomocao = EncontrarEstado(maquinaHeroi, "Locomocao");
            var golpe1 = EncontrarEstado(maquinaHeroi, "Golpe1");
            var estadoBloqueio = GarantirEstado(maquinaHeroi, "Bloqueio", bloqueio, "acao");
            var contraAtaque = GarantirEstado(
                maquinaHeroi, "ContraAtaque", golpe1 == null ? null : golpe1.motion, "acao");
            var morteHeroi = GarantirEstado(maquinaHeroi, "Morte", morte, "acao");
            if (locomocao != null)
            {
                GarantirTransicao(locomocao, estadoBloqueio, "bloqueando", AnimatorConditionMode.If, false, 0.08f);
                GarantirTransicao(estadoBloqueio, locomocao, "bloqueando", AnimatorConditionMode.IfNot, false, 0.08f);
                GarantirTransicao(contraAtaque, locomocao, null, AnimatorConditionMode.If, true, 0.1f);
            }
            GarantirTransicaoAny(maquinaHeroi, contraAtaque, "contraAtacar", AnimatorConditionMode.If, 0.04f);
            GarantirTransicaoAny(maquinaHeroi, morteHeroi, "morrer", AnimatorConditionMode.If, 0.04f);

            var maquinaOrc = controllerOrc.layers[0].stateMachine;
            var parado = EncontrarEstado(maquinaOrc, "Parado");
            GarantirParametro(controllerOrc, "movimentoFila", AnimatorControllerParameterType.Float);
            var andarParaFrente = GarantirEstado(maquinaOrc, "AndarFrente", andarFrente, string.Empty);
            var andarParaTras = GarantirEstado(maquinaOrc, "AndarTras", andarTras, string.Empty);
            var atordoado = GarantirEstado(maquinaOrc, "Atordoado", stun, "acao");
            GarantirEstado(maquinaOrc, "Morte", morte, "acao");
            if (parado != null)
            {
                GarantirTransicao(parado, andarParaFrente, "movimentoFila", AnimatorConditionMode.Greater, false, 0.1f, 0.05f);
                GarantirTransicao(parado, andarParaTras, "movimentoFila", AnimatorConditionMode.Less, false, 0.1f, -0.05f);
                GarantirTransicao(andarParaFrente, parado, "movimentoFila", AnimatorConditionMode.Less, false, 0.1f, 0.05f);
                GarantirTransicao(andarParaTras, parado, "movimentoFila", AnimatorConditionMode.Greater, false, 0.1f, -0.05f);
                GarantirTransicao(atordoado, parado, null, AnimatorConditionMode.If, true, 0.12f);
            }

            EditorUtility.SetDirty(controllerHeroi);
            EditorUtility.SetDirty(controllerOrc);
        }

        static AnimationClip CarregarClip(string caminho)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(caminho))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        static void GarantirParametro(
            AnimatorController controller,
            string nome,
            AnimatorControllerParameterType tipo)
        {
            foreach (var parametro in controller.parameters)
                if (parametro.name == nome) return;
            controller.AddParameter(nome, tipo);
        }

        static AnimatorState EncontrarEstado(AnimatorStateMachine maquina, string nome)
        {
            foreach (var filho in maquina.states)
                if (filho.state != null && filho.state.name == nome) return filho.state;
            return null;
        }

        static AnimatorState GarantirEstado(
            AnimatorStateMachine maquina,
            string nome,
            Motion movimento,
            string tag)
        {
            var estado = EncontrarEstado(maquina, nome);
            if (estado == null) estado = maquina.AddState(nome);
            estado.motion = movimento;
            estado.tag = tag;
            return estado;
        }

        static void GarantirTransicao(
            AnimatorState origem,
            AnimatorState destino,
            string parametro,
            AnimatorConditionMode modo,
            bool comSaida,
            float duracao)
        {
            GarantirTransicao(origem, destino, parametro, modo, comSaida, duracao, 0f);
        }

        static void GarantirTransicao(
            AnimatorState origem,
            AnimatorState destino,
            string parametro,
            AnimatorConditionMode modo,
            bool comSaida,
            float duracao,
            float limiar)
        {
            if (origem == null || destino == null) return;
            foreach (var existente in origem.transitions)
            {
                if (existente.destinationState != destino) continue;
                if (string.IsNullOrEmpty(parametro) && existente.conditions.Length == 0) return;
                foreach (var condicao in existente.conditions)
                    if (condicao.parameter == parametro) return;
            }

            var transicao = origem.AddTransition(destino);
            transicao.hasExitTime = comSaida;
            transicao.exitTime = comSaida ? 0.92f : 0f;
            transicao.duration = duracao;
            if (!string.IsNullOrEmpty(parametro)) transicao.AddCondition(modo, limiar, parametro);
        }

        static void GarantirTransicaoAny(
            AnimatorStateMachine maquina,
            AnimatorState destino,
            string parametro,
            AnimatorConditionMode modo,
            float duracao)
        {
            if (destino == null) return;
            foreach (var existente in maquina.anyStateTransitions)
                if (existente.destinationState == destino) return;
            var transicao = maquina.AddAnyStateTransition(destino);
            transicao.hasExitTime = false;
            transicao.duration = duracao;
            transicao.canTransitionToSelf = false;
            transicao.AddCondition(modo, 0f, parametro);
        }

        static void CriarCamera(
            Transform heroi,
            RegistroDeCombate registro,
            CombateDoHeroi combate,
            out CameraLateral lateral,
            out CameraDeCombate cameraDeCombate)
        {
            var objeto = new GameObject("CameraLateral");
            objeto.tag = "MainCamera";
            var camera = objeto.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            objeto.AddComponent<AudioListener>();
            lateral = objeto.AddComponent<CameraLateral>();
            lateral.alvo = heroi;
            lateral.acharSozinho = false;
            lateral.distancia = 12.5f;
            lateral.altura = 3.2f;
            lateral.olharAFrente = 0.8f;
            lateral.xMinimo = -14f;
            lateral.xMaximo = 14f;
            cameraDeCombate = objeto.AddComponent<CameraDeCombate>();
            cameraDeCombate.Configurar(heroi, registro, combate, lateral);
            cameraDeCombate.enabled = false;
        }

        static void CriarEncontro(ModoDeCombate modo, AlvoDeCombate[] inimigos)
        {
            var objeto = new GameObject("Encontro_FreeFlow");
            objeto.transform.position = new Vector3(-5.5f, 1.5f, 0f);
            var volume = objeto.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(1f, 3f, 12f);
            objeto.AddComponent<EncontroDeCombate>().Configurar(modo, inimigos);
        }

        static void RemoverLegado(GameObject raiz)
        {
            foreach (var duelo in raiz.GetComponentsInChildren<Duelo>(true)) Object.DestroyImmediate(duelo);
            foreach (var teste in raiz.GetComponentsInChildren<TesteDeCombate>(true)) Object.DestroyImmediate(teste);
            foreach (var corpo in raiz.GetComponentsInChildren<CorpoDoOrc>(true)) Object.DestroyImmediate(corpo);
            foreach (var tocador in raiz.GetComponentsInChildren<TocadorDeClipe>(true)) Object.DestroyImmediate(tocador);
        }
    }
}
