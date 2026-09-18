using System.IO;
using Sardael;
using UnityEditor;
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

        static readonly float[] Alcances = { 1.756f, 1.166f, 1.756f, 1.756f };
        static readonly float[] Distancias = { 1.45f, 1.0f, 1.45f, 1.45f };
        static readonly float[] Impactos = { 0.42f, 0.45f, 0.44f, 0.48f };

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
            var controllerHeroi = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SardaelController);
            var controllerOrc = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OrcController);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActions);

            if (prefabHeroi == null || prefabOrc == null || controllerHeroi == null ||
                controllerOrc == null || input == null)
            {
                Debug.LogError("[Combate Sandbox] faltam prefabs, controllers ou Input Actions obrigatorios.");
                return;
            }

            Directory.CreateDirectory(PastaDefinicoes);
            DefinicaoDeAtaque[] definicoes = CriarDefinicoes();
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CriarLuz();
            CriarArena();

            var sistemas = new GameObject("SistemasDeCombate");
            var registro = sistemas.AddComponent<RegistroDeCombate>();
            var resolvedor = sistemas.AddComponent<ResolvedorDeImpacto>();

            var heroi = (GameObject)PrefabUtility.InstantiatePrefab(prefabHeroi);
            heroi.name = "Sardael";
            heroi.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            RemoverLegado(heroi);

            var animatorHeroi = heroi.GetComponent<Animator>();
            if (animatorHeroi == null) animatorHeroi = heroi.AddComponent<Animator>();
            animatorHeroi.runtimeAnimatorController = controllerHeroi;
            animatorHeroi.applyRootMotion = true;
            animatorHeroi.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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
            MontarMovimento.EncaixarLanca(heroi);

            CriarInimigo(prefabOrc, controllerOrc, registro, "E2", -5.2f);
            CriarInimigo(prefabOrc, controllerOrc, registro, "E1", -2.6f);
            CriarInimigo(prefabOrc, controllerOrc, registro, "E3", 2.6f);
            CriarInimigo(prefabOrc, controllerOrc, registro, "E4", 5.2f);

            CriarCamera(heroi.transform);
            sistemas.AddComponent<PainelCombateSandbox>().Configurar(combate, registro);

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
                definicao.alcanceMagnetismo = Mathf.Max(Alcances[i] + 1.5f, 4.75f);
                definicao.distanciaDesejada = Distancias[i];
                definicao.velocidadeAproximacao = 10f;
                definicao.dano = 20f;
                definicao.poise = 25f;
                definicao.padraoDeAlvo = PadraoDeAlvo.Single;
                definicao.deslocamento = TipoDeDeslocamento.Nenhum;
                definicao.flowGerado = 10f;
                definicao.momentoDoImpacto = Impactos[i];
                EditorUtility.SetDirty(definicao);
                resultado[i] = definicao;
            }
            return resultado;
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
            chao.transform.localScale = new Vector3(36f, 1f, 4f);

            CriarLimite("LimiteEsquerdo", -18f);
            CriarLimite("LimiteDireito", 18f);

            for (int x = -16; x <= 16; x += 2)
            {
                if (x == 0) continue;
                var marco = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marco.name = "Marco_" + x;
                marco.transform.position = new Vector3(x, 0.05f, 1.8f);
                marco.transform.localScale = new Vector3(0.05f, 0.1f, 0.4f);
                Object.DestroyImmediate(marco.GetComponent<Collider>());
            }
        }

        static void CriarLimite(string nome, float x)
        {
            var limite = GameObject.CreatePrimitive(PrimitiveType.Cube);
            limite.name = nome;
            limite.transform.position = new Vector3(x, 1.5f, 0f);
            limite.transform.localScale = new Vector3(0.4f, 4f, 4f);
        }

        static void CriarInimigo(
            GameObject prefab,
            RuntimeAnimatorController controller,
            RegistroDeCombate registro,
            string nome,
            float x)
        {
            var inimigo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inimigo.name = nome;
            inimigo.transform.SetPositionAndRotation(
                new Vector3(x, 0f, 0f),
                Quaternion.Euler(0f, x < 0f ? 90f : -90f, 0f));
            RemoverLegado(inimigo);

            var animator = inimigo.GetComponent<Animator>();
            if (animator == null) animator = inimigo.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (inimigo.GetComponent<Collider>() == null)
            {
                var colisor = inimigo.AddComponent<CapsuleCollider>();
                colisor.center = new Vector3(0f, 0.9f, 0f);
                colisor.height = 1.8f;
                colisor.radius = 0.35f;
            }

            var reacao = inimigo.AddComponent<ReacaoDeCombate>();
            reacao.Configurar(animator);
            var alvo = inimigo.AddComponent<AlvoDeCombate>();
            alvo.Configurar(registro, reacao);
        }

        static void CriarCamera(Transform heroi)
        {
            var objeto = new GameObject("CameraLateral");
            objeto.tag = "MainCamera";
            var camera = objeto.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            objeto.AddComponent<AudioListener>();
            var lateral = objeto.AddComponent<CameraLateral>();
            lateral.alvo = heroi;
            lateral.acharSozinho = false;
            lateral.distancia = 12.5f;
            lateral.altura = 3.2f;
            lateral.olharAFrente = 0.8f;
            lateral.xMinimo = -14f;
            lateral.xMaximo = 14f;
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
