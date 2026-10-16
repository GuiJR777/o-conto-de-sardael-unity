using System.Collections.Generic;
using Sardael;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SardaelEditor
{
    public static class IntegrarCombateTutorialFazenda
    {
        const string Cena = "Assets/_Sardael/Cenas/Tutorial_Fazenda.unity";
        const string ControllerHeroi =
            "Assets/_Sardael/Animacoes/Controladores/Sardael_Combate.controller";
        const string ControllerOrc =
            "Assets/_Sardael/Animacoes/Controladores/Orc_Combate.controller";
        const string InputActions = "Assets/Settings/InputSystem_Actions.inputactions";
        const string FxSangue =
            "Assets/_Pacotes/Synty/PolygonGeneric/Prefabs/FX/FX_Blood_Splatter_01.prefab";
        const string CabecaDecepada =
            "Assets/_Sardael/Personagens/Gore/Cabeca_Orc_Decepada.prefab";

        [MenuItem("Sardael/Combate/Integrar combate no Tutorial Fazenda")]
        public static void ExecutarPeloMenu()
        {
            if (!Executar()) Debug.LogError("[Fazenda] a integracao nao foi concluida.");
        }

        [CliCommand(
            "integrar_combate_fazenda",
            "Configura o encontro real de combate na cena Tutorial_Fazenda",
            MainThreadRequired = true)]
        public static string ExecutarPeloCli()
        {
            if (!Executar()) throw new System.InvalidOperationException(
                "Nao foi possivel integrar o combate no Tutorial_Fazenda.");
            return "Tutorial_Fazenda integrado ao combate real.";
        }

        static bool Executar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Fazenda] pare o Play Mode antes de integrar a cena.");
                return false;
            }

            var controllerHeroi = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerHeroi);
            var controllerOrc = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerOrc);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActions);
            var definicoes = CarregarDefinicoes();
            var derrubadaHeroi = MontarCombateSandbox.CarregarControladorDaDerrubada(false);
            var derrubadaVitima = MontarCombateSandbox.CarregarControladorDaDerrubada(true);
            var variantes = MontarCombateSandbox.CarregarVariantesDeExecucao();
            var fxSangue = AssetDatabase.LoadAssetAtPath<GameObject>(FxSangue);
            var cabeca = AssetDatabase.LoadAssetAtPath<GameObject>(CabecaDecepada);
            if (controllerHeroi == null || controllerOrc == null || input == null ||
                definicoes == null || derrubadaHeroi == null || derrubadaVitima == null ||
                variantes == null)
            {
                Debug.LogError("[Fazenda] faltam controllers, ataques, Input Actions ou execucoes.");
                return false;
            }

            var cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
            var roteiro = Object.FindAnyObjectByType<DiretorDoCapitao>(FindObjectsInactive.Include);
            if (roteiro == null || roteiro.sardael == null || roteiro.cameraDoJogo == null ||
                roteiro.orcsDoDesafio == null || roteiro.orcsDoDesafio.Length == 0)
            {
                Debug.LogError("[Fazenda] DiretorDoCapitao ou referencias narrativas incompletas.");
                return false;
            }

            var sistemas = AcharOuCriar("SistemasDeCombate_TutorialFazenda");
            var registro = ObterOuAdicionar<RegistroDeCombate>(sistemas);
            var flow = ObterOuAdicionar<SistemaDeFlow>(sistemas);
            var resolvedor = ObterOuAdicionar<ResolvedorDeImpacto>(sistemas);
            resolvedor.Configurar(registro, flow);
            var diretor = ObterOuAdicionar<DiretorDeCombate>(sistemas);
            var modo = ObterOuAdicionar<ModoDeCombate>(sistemas);

            var cameraDoJogo = roteiro.cameraDoJogo;
            var cameraDeCombate = ObterOuAdicionar<CameraDeCombate>(cameraDoJogo.gameObject);
            var heroi = ConfiguracaoCompartilhadaDeCombate.ConfigurarHeroi(
                roteiro.sardael.gameObject,
                controllerHeroi,
                input,
                registro,
                resolvedor,
                flow,
                diretor,
                definicoes,
                true);
            if (heroi == null) return false;

            cameraDeCombate.Configurar(
                roteiro.sardael, registro, heroi.combate, cameraDoJogo);
            cameraDeCombate.enabled = false;
            diretor.Configurar(registro, roteiro.sardael);
            diretor.DefinirAtivo(false);

            var execucao = ConfiguracaoCompartilhadaDeCombate.ConfigurarExecucao(
                roteiro.sardael.gameObject,
                heroi,
                registro,
                flow,
                diretor,
                cameraDeCombate,
                derrubadaHeroi,
                derrubadaVitima,
                variantes);

            var alvos = new List<AlvoDeCombate>(roteiro.orcsDoDesafio.Length);
            var mortes = MontarCombateSandbox.CarregarAnimacoesDeMorte();
            foreach (var orc in roteiro.orcsDoDesafio)
            {
                if (orc == null) continue;
                var alvo = ConfiguracaoCompartilhadaDeCombate.ConfigurarInimigo(
                    orc.gameObject,
                    controllerOrc,
                    registro,
                    roteiro.sardael,
                    fxSangue,
                    cabeca,
                    mortes,
                    false,
                    false);
                if (alvo != null) alvos.Add(alvo);
            }
            if (alvos.Count == 0)
            {
                Debug.LogError("[Fazenda] nenhum orc do desafio pode ser preparado.");
                return false;
            }

            DesativarCombatenteNarrativo(roteiro.capitao);
            DesativarCombatenteNarrativo(roteiro.troll);

            var encontroObjeto = AcharOuCriar("EncontroDeCombate_TutorialFazenda");
            encontroObjeto.transform.SetParent(sistemas.transform, true);
            ConfigurarVolumeDaArena(encontroObjeto, roteiro.troll, roteiro.orcsDoDesafio);
            var encontro = ObterOuAdicionar<EncontroDeCombate>(encontroObjeto);
            encontro.Configurar(modo, alvos.ToArray(), false);

            modo.Configurar(
                heroi.movimento,
                registro,
                diretor,
                cameraDeCombate,
                heroi.vida,
                execucao,
                heroi.combate,
                heroi.defesa);
            roteiro.encontroDeCombate = encontro;

            Marcar(
                sistemas, registro, flow, resolvedor, diretor, modo,
                cameraDeCombate, encontroObjeto, encontro, roteiro);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena, Cena);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = encontroObjeto;
            Debug.Log("[Fazenda] combate real integrado aos orcs existentes.");
            return true;
        }

        static DefinicaoDeAtaque[] CarregarDefinicoes()
        {
            var resultado = new DefinicaoDeAtaque[4];
            for (int i = 0; i < resultado.Length; i++)
            {
                resultado[i] = AssetDatabase.LoadAssetAtPath<DefinicaoDeAtaque>(
                    "Assets/_Sardael/Combate/Definicoes/Golpe" + (i + 1) + ".asset");
                if (resultado[i] == null) return null;
            }
            return resultado;
        }

        static void ConfigurarVolumeDaArena(
            GameObject objeto,
            Transform troll,
            Transform[] orcs)
        {
            bool iniciou = false;
            Bounds conteudo = default;
            if (troll != null)
            {
                conteudo = new Bounds(troll.position, Vector3.zero);
                iniciou = true;
            }
            foreach (var orc in orcs)
            {
                if (orc == null) continue;
                if (!iniciou)
                {
                    conteudo = new Bounds(orc.position, Vector3.zero);
                    iniciou = true;
                }
                else conteudo.Encapsulate(orc.position);
            }

            Vector3 centro = iniciou ? conteudo.center : objeto.transform.position;
            centro.y = 1.5f;
            objeto.transform.position = centro;
            objeto.transform.rotation = Quaternion.identity;
            objeto.transform.localScale = Vector3.one;
            var volume = ObterOuAdicionar<BoxCollider>(objeto);
            volume.isTrigger = true;
            volume.center = Vector3.zero;
            volume.size = new Vector3(
                // Sardael recebe a ordem alguns metros antes do Troll. A largura minima
                // inclui esse ponto de entrada sem recuperar a antiga jaula 1D da camera.
                Mathf.Max(28f, conteudo.size.x + 20f),
                4f,
                Mathf.Max(11f, conteudo.size.z + 8f));
        }

        static void DesativarCombatenteNarrativo(Transform ator)
        {
            if (ator == null) return;
            var alvo = ator.GetComponent<AlvoDeCombate>();
            if (alvo != null) alvo.enabled = false;
            var motor = ator.GetComponent<MotorDeCombateDoInimigo>();
            if (motor != null) motor.enabled = false;
        }

        static GameObject AcharOuCriar(string nome)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include))
                if (transform.name == nome) return transform.gameObject;
            return new GameObject(nome);
        }

        static T ObterOuAdicionar<T>(GameObject alvo) where T : Component
        {
            var componente = alvo.GetComponent<T>();
            return componente != null ? componente : alvo.AddComponent<T>();
        }

        static void Marcar(params Object[] objetos)
        {
            foreach (var objeto in objetos)
                if (objeto != null) EditorUtility.SetDirty(objeto);
        }
    }
}
