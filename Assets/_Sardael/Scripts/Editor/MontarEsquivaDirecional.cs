using System;
using Sardael;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SardaelEditor
{
    /// <summary>Configura a esquiva 3D sem fazer Sardael perder o alvo de vista.</summary>
    public static class MontarEsquivaDirecional
    {
        const string Controller =
            "Assets/_Sardael/Animacoes/Controladores/Sardael_Combate.controller";
        const string Pasta =
            "Assets/_Pacotes/SpearCombatAnimationV2/Animation/RM/A_SpearCombatAnimationV2_";
        const string NomeDaArvore = "EsquivaDirecional";

        static readonly string[] Caminhos =
        {
            Pasta + "Dodge_L_RM.FBX",
            Pasta + "Dodge_R_RM.FBX",
            Pasta + "Dodge_Bw_RM.FBX",
            Pasta + "Roll_Fw_RM.FBX"
        };

        [MenuItem("Sardael/Combate/Montar Esquiva Direcional")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Esquiva] pare o Play Mode antes de alterar o Animator.");
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null)
            {
                Debug.LogError("[Esquiva] controller nao encontrado: " + Controller);
                return;
            }

            if (Configurar(controller, true))
                Debug.Log("[Esquiva] direcional pronta: esquerda, direita, tras e frente; diagonais interpoladas.");
        }

        public static bool Configurar(AnimatorController controller, bool salvar = false)
        {
            if (controller == null || controller.layers.Length == 0) return false;

            var clipes = new AnimationClip[Caminhos.Length];
            for (int i = 0; i < Caminhos.Length; i++)
            {
                GarantirUmaExecucao(Caminhos[i]);
                clipes[i] = CarregarClip(Caminhos[i]);
                if (clipes[i] == null)
                {
                    Debug.LogError("[Esquiva] clipe nao encontrado: " + Caminhos[i]);
                    return false;
                }
            }

            GarantirParametro(
                controller, MovimentoDoHeroi.P_ESQUIVA_HORIZONTAL,
                AnimatorControllerParameterType.Float);
            GarantirParametro(
                controller, MovimentoDoHeroi.P_ESQUIVA_VERTICAL,
                AnimatorControllerParameterType.Float);

            AnimatorState estado = EncontrarEstado(
                controller.layers[0].stateMachine, "Esquiva");
            if (estado == null)
            {
                Debug.LogError("[Esquiva] estado Esquiva nao encontrado no controller.");
                return false;
            }

            BlendTree arvore = estado.motion as BlendTree;
            if (arvore == null || arvore.name != NomeDaArvore)
                arvore = EncontrarOuCriarArvore(controller);

            arvore.name = NomeDaArvore;
            arvore.blendType = BlendTreeType.SimpleDirectional2D;
            arvore.blendParameter = MovimentoDoHeroi.P_ESQUIVA_HORIZONTAL;
            arvore.blendParameterY = MovimentoDoHeroi.P_ESQUIVA_VERTICAL;
            arvore.useAutomaticThresholds = false;
            arvore.children = Array.Empty<ChildMotion>();
            arvore.AddChild(clipes[0], Vector2.left);
            arvore.AddChild(clipes[1], Vector2.right);
            arvore.AddChild(clipes[2], Vector2.down);
            // O pacote nao tem Dodge_Fw. O Roll_Fw e' o clipe frontal com root motion real.
            arvore.AddChild(clipes[3], Vector2.up);

            estado.motion = arvore;
            estado.tag = MovimentoDoHeroi.TAG_ACAO;
            EditorUtility.SetDirty(arvore);
            EditorUtility.SetDirty(estado);
            EditorUtility.SetDirty(controller);
            if (salvar) AssetDatabase.SaveAssets();
            return true;
        }

        static BlendTree EncontrarOuCriarArvore(AnimatorController controller)
        {
            string caminho = AssetDatabase.GetAssetPath(controller);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(caminho))
                if (asset is BlendTree arvore && arvore.name == NomeDaArvore) return arvore;

            var nova = new BlendTree { name = NomeDaArvore };
            AssetDatabase.AddObjectToAsset(nova, controller);
            return nova;
        }

        static AnimatorState EncontrarEstado(AnimatorStateMachine maquina, string nome)
        {
            foreach (ChildAnimatorState filho in maquina.states)
                if (filho.state.name == nome) return filho.state;
            foreach (ChildAnimatorStateMachine filha in maquina.stateMachines)
            {
                AnimatorState estado = EncontrarEstado(filha.stateMachine, nome);
                if (estado != null) return estado;
            }
            return null;
        }

        static AnimationClip CarregarClip(string caminho)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(caminho))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        static void GarantirParametro(
            AnimatorController controller,
            string nome,
            AnimatorControllerParameterType tipo)
        {
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                AnimatorControllerParameter parametro = controller.parameters[i];
                if (parametro.name != nome) continue;
                if (parametro.type == tipo) return;
                controller.RemoveParameter(i);
                break;
            }
            controller.AddParameter(nome, tipo);
        }

        static void GarantirUmaExecucao(string caminho)
        {
            var importador = AssetImporter.GetAtPath(caminho) as ModelImporter;
            if (importador == null) return;

            ModelImporterClipAnimation[] clipes = importador.clipAnimations;
            if (clipes == null || clipes.Length == 0) clipes = importador.defaultClipAnimations;
            bool alterou = false;
            foreach (ModelImporterClipAnimation clipe in clipes)
            {
                if (!clipe.loopTime) continue;
                clipe.loopTime = false;
                alterou = true;
            }
            if (!alterou) return;
            importador.clipAnimations = clipes;
            importador.SaveAndReimport();
        }
    }
}
