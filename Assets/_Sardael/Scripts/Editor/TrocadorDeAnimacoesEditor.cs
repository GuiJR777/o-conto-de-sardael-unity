using System;
using System.Collections.Generic;
using Sardael;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SardaelEditor
{
    [CustomEditor(typeof(TrocadorDeAnimacoes))]
    public sealed class TrocadorDeAnimacoesEditor : Editor
    {
        sealed class SlotEncontrado
        {
            public string caminho;
            public string estado;
            public AnimationClip original;
        }

        sealed class ConfiguracaoAnterior
        {
            public AnimationClip substituto;
            public bool tocarAoContrario;
            public AnimationClip reverso;
            public string assinatura;
            public string erro;
        }

        static readonly GUIContent TituloAnimator = new GUIContent(
            "Animator", "Animator do heroi que recebera as substituicoes.");
        static readonly GUIContent TituloSubstituto = new GUIContent(
            "Usar no lugar", "Vazio mantem o clipe original do Animator Controller.");
        static readonly GUIContent TituloTocarAoContrario = new GUIContent(
            "Tocar ao contrario",
            "Gera uma copia do clipe efetivo com o tempo invertido somente para este slot.");
        static readonly GUIContent TituloReverso = new GUIContent(
            "Clipe reverso gerado",
            "Asset gerado automaticamente sem modificar o FBX original.");

        SerializedProperty animator;
        SerializedProperty controladorSincronizado;
        SerializedProperty substituicoes;
        bool mostrarSlots = true;

        void OnEnable()
        {
            animator = serializedObject.FindProperty("animator");
            controladorSincronizado = serializedObject.FindProperty("controladorSincronizado");
            substituicoes = serializedObject.FindProperty("substituicoes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(animator, TituloAnimator);

            var componente = (TrocadorDeAnimacoes)target;
            var animatorAtual = animator.objectReferenceValue as Animator;
            var controllerAtual = ControllerBase(animatorAtual == null
                ? null
                : animatorAtual.runtimeAnimatorController);
            var sincronizado = ControllerBase(
                controladorSincronizado.objectReferenceValue as RuntimeAnimatorController);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Controller sincronizado", sincronizado,
                    typeof(RuntimeAnimatorController), false);

            if (animatorAtual == null)
                EditorGUILayout.HelpBox("Atribua o Animator do heroi para listar as animacoes.",
                    MessageType.Warning);
            else if (!(controllerAtual is AnimatorController))
                EditorGUILayout.HelpBox(
                    "O controller-base precisa ser um Animator Controller editavel.",
                    MessageType.Warning);
            else if (sincronizado != null && sincronizado != controllerAtual)
                EditorGUILayout.HelpBox(
                    "O Animator usa outro controller. Clique em Sincronizar com Animator.",
                    MessageType.Warning);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!(controllerAtual is AnimatorController)))
                {
                    if (GUILayout.Button("Sincronizar com Animator"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        Sincronizar(componente);
                        serializedObject.Update();
                    }
                }

                using (new EditorGUI.DisabledScope(substituicoes.arraySize == 0))
                {
                    if (GUILayout.Button("Limpar substituicoes"))
                    {
                        Undo.RecordObject(componente, "Limpar substituicoes de animacao");
                        for (int i = 0; i < substituicoes.arraySize; i++)
                        {
                            var slot = substituicoes.GetArrayElementAtIndex(i);
                            slot.FindPropertyRelative("clipeSubstituto").objectReferenceValue = null;
                            slot.FindPropertyRelative("tocarAoContrario").boolValue = false;
                            slot.FindPropertyRelative("clipeReversoGerado").objectReferenceValue = null;
                            slot.FindPropertyRelative("assinaturaDaFonteReversa").stringValue = string.Empty;
                            slot.FindPropertyRelative("erroDoClipeReverso").stringValue = string.Empty;
                        }
                    }
                }
            }

            EditorGUILayout.Space(4f);
            mostrarSlots = EditorGUILayout.Foldout(
                mostrarSlots,
                "Animacoes (" + substituicoes.arraySize + ")",
                true);
            if (mostrarSlots)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < substituicoes.arraySize; i++)
                    DesenharSlot(substituicoes.GetArrayElementAtIndex(i));
                EditorGUI.indentLevel--;
            }

            bool mudou = serializedObject.ApplyModifiedProperties();
            if (mudou || PrecisaAtualizarReversos(componente))
                AtualizarReversosAtivos(componente, false);
            if (mudou && Application.isPlaying)
                componente.AplicarConfiguracao();
        }

        static void DesenharSlot(SerializedProperty slot)
        {
            var caminho = slot.FindPropertyRelative("caminhoDoEstado");
            var original = slot.FindPropertyRelative("clipeOriginal");
            var substituto = slot.FindPropertyRelative("clipeSubstituto");
            var tocarAoContrario = slot.FindPropertyRelative("tocarAoContrario");
            var reverso = slot.FindPropertyRelative("clipeReversoGerado");
            var erro = slot.FindPropertyRelative("erroDoClipeReverso");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(caminho.stringValue, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(
                        "Original", original.objectReferenceValue, typeof(AnimationClip), false);
                EditorGUILayout.PropertyField(substituto, TituloSubstituto);
                EditorGUILayout.PropertyField(tocarAoContrario, TituloTocarAoContrario);
                if (tocarAoContrario.boolValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(
                            TituloReverso,
                            reverso.objectReferenceValue,
                            typeof(AnimationClip),
                            false);
                    if (!string.IsNullOrEmpty(erro.stringValue))
                        EditorGUILayout.HelpBox(erro.stringValue, MessageType.Error);
                }
                if (caminho.stringValue.Contains(" | "))
                    EditorGUILayout.HelpBox(
                        "Este mesmo clipe-base e compartilhado. A substituicao afeta todos os caminhos listados.",
                        MessageType.Info);
            }
        }

        public static void Sincronizar(TrocadorDeAnimacoes componente)
        {
            if (componente == null) return;
            var animator = componente.Animator != null
                ? componente.Animator
                : componente.GetComponent<Animator>();
            var controller = ControllerBase(animator == null
                ? null
                : animator.runtimeAnimatorController) as AnimatorController;
            if (animator == null || controller == null) return;

            var objetoSerializado = new SerializedObject(componente);
            var animatorProp = objetoSerializado.FindProperty("animator");
            var controllerProp = objetoSerializado.FindProperty("controladorSincronizado");
            var listaProp = objetoSerializado.FindProperty("substituicoes");

            var anteriores = new Dictionary<AnimationClip, ConfiguracaoAnterior>();
            for (int i = 0; i < listaProp.arraySize; i++)
            {
                var item = listaProp.GetArrayElementAtIndex(i);
                var original = item.FindPropertyRelative("clipeOriginal").objectReferenceValue
                    as AnimationClip;
                if (original == null) continue;
                anteriores[original] = new ConfiguracaoAnterior
                {
                    substituto = item.FindPropertyRelative("clipeSubstituto").objectReferenceValue
                        as AnimationClip,
                    tocarAoContrario = item.FindPropertyRelative("tocarAoContrario").boolValue,
                    reverso = item.FindPropertyRelative("clipeReversoGerado").objectReferenceValue
                        as AnimationClip,
                    assinatura = item.FindPropertyRelative("assinaturaDaFonteReversa").stringValue,
                    erro = item.FindPropertyRelative("erroDoClipeReverso").stringValue
                };
            }

            var encontrados = EncontrarSlots(controller);
            Undo.RecordObject(componente, "Sincronizar animacoes do heroi");
            objetoSerializado.Update();
            animatorProp.objectReferenceValue = animator;
            controllerProp.objectReferenceValue = controller;
            listaProp.arraySize = encontrados.Count;
            for (int i = 0; i < encontrados.Count; i++)
            {
                var destino = listaProp.GetArrayElementAtIndex(i);
                destino.FindPropertyRelative("caminhoDoEstado").stringValue = encontrados[i].caminho;
                destino.FindPropertyRelative("nomeDoEstado").stringValue = encontrados[i].estado;
                destino.FindPropertyRelative("clipeOriginal").objectReferenceValue = encontrados[i].original;
                if (anteriores.TryGetValue(encontrados[i].original, out var anterior))
                {
                    destino.FindPropertyRelative("clipeSubstituto").objectReferenceValue =
                        anterior.substituto;
                    destino.FindPropertyRelative("tocarAoContrario").boolValue =
                        anterior.tocarAoContrario;
                    destino.FindPropertyRelative("clipeReversoGerado").objectReferenceValue =
                        anterior.reverso;
                    destino.FindPropertyRelative("assinaturaDaFonteReversa").stringValue =
                        anterior.assinatura;
                    destino.FindPropertyRelative("erroDoClipeReverso").stringValue =
                        anterior.erro;
                }
                else
                {
                    destino.FindPropertyRelative("clipeSubstituto").objectReferenceValue = null;
                    destino.FindPropertyRelative("tocarAoContrario").boolValue = false;
                    destino.FindPropertyRelative("clipeReversoGerado").objectReferenceValue = null;
                    destino.FindPropertyRelative("assinaturaDaFonteReversa").stringValue = string.Empty;
                    destino.FindPropertyRelative("erroDoClipeReverso").stringValue = string.Empty;
                }
            }
            objetoSerializado.ApplyModifiedProperties();
            EditorUtility.SetDirty(componente);
            AtualizarReversosAtivos(componente, false);
        }

        public static bool DefinirSubstituicao(
            TrocadorDeAnimacoes componente,
            string nomeDoEstado,
            AnimationClip substituto)
        {
            if (componente == null || string.IsNullOrEmpty(nomeDoEstado)) return false;
            var objetoSerializado = new SerializedObject(componente);
            var lista = objetoSerializado.FindProperty("substituicoes");
            bool encontrou = false;
            Undo.RecordObject(componente, "Definir substituicao de animacao");
            for (int i = 0; i < lista.arraySize; i++)
            {
                var item = lista.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("nomeDoEstado").stringValue != nomeDoEstado) continue;
                item.FindPropertyRelative("clipeSubstituto").objectReferenceValue = substituto;
                encontrou = true;
            }
            if (!encontrou) return false;
            objetoSerializado.ApplyModifiedProperties();
            EditorUtility.SetDirty(componente);
            AtualizarReversosAtivos(componente, true);
            return true;
        }

        public static bool DefinirReversao(
            TrocadorDeAnimacoes componente,
            string nomeDoEstado,
            bool tocarAoContrario)
        {
            if (componente == null || string.IsNullOrEmpty(nomeDoEstado)) return false;
            var objetoSerializado = new SerializedObject(componente);
            var lista = objetoSerializado.FindProperty("substituicoes");
            bool encontrou = false;
            Undo.RecordObject(componente, "Definir reproducao reversa de animacao");
            for (int i = 0; i < lista.arraySize; i++)
            {
                var item = lista.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("nomeDoEstado").stringValue != nomeDoEstado) continue;

                item.FindPropertyRelative("tocarAoContrario").boolValue = tocarAoContrario;
                if (!tocarAoContrario)
                {
                    item.FindPropertyRelative("clipeReversoGerado").objectReferenceValue = null;
                    item.FindPropertyRelative("assinaturaDaFonteReversa").stringValue = string.Empty;
                    item.FindPropertyRelative("erroDoClipeReverso").stringValue = string.Empty;
                }
                encontrou = true;
            }

            if (!encontrou) return false;
            objetoSerializado.ApplyModifiedProperties();
            EditorUtility.SetDirty(componente);
            AtualizarReversosAtivos(componente, tocarAoContrario);
            return true;
        }

        static bool PrecisaAtualizarReversos(TrocadorDeAnimacoes componente)
        {
            if (componente == null) return false;
            var objetoSerializado = new SerializedObject(componente);
            var lista = objetoSerializado.FindProperty("substituicoes");
            for (int i = 0; i < lista.arraySize; i++)
            {
                var item = lista.GetArrayElementAtIndex(i);
                bool ativo = item.FindPropertyRelative("tocarAoContrario").boolValue;
                var reverso = item.FindPropertyRelative("clipeReversoGerado");
                var assinatura = item.FindPropertyRelative("assinaturaDaFonteReversa");
                var erro = item.FindPropertyRelative("erroDoClipeReverso");
                if (!ativo)
                {
                    if (reverso.objectReferenceValue != null ||
                        !string.IsNullOrEmpty(assinatura.stringValue) ||
                        !string.IsNullOrEmpty(erro.stringValue))
                        return true;
                    continue;
                }

                var fonte = item.FindPropertyRelative("clipeSubstituto").objectReferenceValue
                    as AnimationClip;
                if (fonte == null)
                    fonte = item.FindPropertyRelative("clipeOriginal").objectReferenceValue
                        as AnimationClip;
                string assinaturaAtual = GeradorDeClipeReverso.ObterAssinatura(fonte);
                if (assinatura.stringValue != assinaturaAtual) return true;
                if (reverso.objectReferenceValue == null && string.IsNullOrEmpty(erro.stringValue))
                    return true;
            }
            return false;
        }

        static void AtualizarReversosAtivos(
            TrocadorDeAnimacoes componente,
            bool forcar)
        {
            if (componente == null) return;
            var objetoSerializado = new SerializedObject(componente);
            var lista = objetoSerializado.FindProperty("substituicoes");
            bool alterou = false;
            bool registrouUndo = false;

            for (int i = 0; i < lista.arraySize; i++)
            {
                var item = lista.GetArrayElementAtIndex(i);
                bool ativo = item.FindPropertyRelative("tocarAoContrario").boolValue;
                var reversoProp = item.FindPropertyRelative("clipeReversoGerado");
                var assinaturaProp = item.FindPropertyRelative("assinaturaDaFonteReversa");
                var erroProp = item.FindPropertyRelative("erroDoClipeReverso");

                if (!ativo)
                {
                    if (reversoProp.objectReferenceValue == null &&
                        string.IsNullOrEmpty(assinaturaProp.stringValue) &&
                        string.IsNullOrEmpty(erroProp.stringValue))
                        continue;

                    RegistrarUndoUmaVez(componente, ref registrouUndo);
                    reversoProp.objectReferenceValue = null;
                    assinaturaProp.stringValue = string.Empty;
                    erroProp.stringValue = string.Empty;
                    alterou = true;
                    continue;
                }

                var fonte = item.FindPropertyRelative("clipeSubstituto").objectReferenceValue
                    as AnimationClip;
                if (fonte == null)
                    fonte = item.FindPropertyRelative("clipeOriginal").objectReferenceValue
                        as AnimationClip;

                string assinaturaAtual = GeradorDeClipeReverso.ObterAssinatura(fonte);
                bool falhaJaRegistrada = reversoProp.objectReferenceValue == null &&
                    !string.IsNullOrEmpty(erroProp.stringValue) &&
                    assinaturaProp.stringValue == assinaturaAtual;
                if (!forcar &&
                    assinaturaProp.stringValue == assinaturaAtual &&
                    (reversoProp.objectReferenceValue != null || falhaJaRegistrada))
                    continue;

                bool sucesso = GeradorDeClipeReverso.TryCriarOuAtualizar(
                    fonte,
                    out var clipeGerado,
                    out var assinaturaNova,
                    out var erroDaGeracao);
                RegistrarUndoUmaVez(componente, ref registrouUndo);
                reversoProp.objectReferenceValue = sucesso ? clipeGerado : null;
                assinaturaProp.stringValue = assinaturaNova;
                erroProp.stringValue = sucesso ? string.Empty : erroDaGeracao;
                alterou = true;
            }

            if (!alterou) return;
            objetoSerializado.ApplyModifiedProperties();
            EditorUtility.SetDirty(componente);
            if (Application.isPlaying) componente.AplicarConfiguracao();
        }

        static void RegistrarUndoUmaVez(
            TrocadorDeAnimacoes componente,
            ref bool registrado)
        {
            if (registrado) return;
            Undo.RecordObject(componente, "Atualizar clipes reversos");
            registrado = true;
        }

        static List<SlotEncontrado> EncontrarSlots(AnimatorController controller)
        {
            var porClipe = new Dictionary<AnimationClip, SlotEncontrado>();
            for (int i = 0; i < controller.layers.Length; i++)
            {
                var layer = controller.layers[i];
                ColetarMaquina(layer.stateMachine, layer.name, porClipe);
            }

            var resultado = new List<SlotEncontrado>(porClipe.Values);
            resultado.Sort((a, b) => string.Compare(
                a.caminho, b.caminho, StringComparison.OrdinalIgnoreCase));
            return resultado;
        }

        static void ColetarMaquina(
            AnimatorStateMachine maquina,
            string caminho,
            Dictionary<AnimationClip, SlotEncontrado> porClipe)
        {
            foreach (var filho in maquina.states)
            {
                if (filho.state == null) continue;
                string caminhoDoEstado = caminho + "/" + filho.state.name;
                ColetarMotion(filho.state.motion, caminhoDoEstado, filho.state.name, porClipe);
            }

            foreach (var filha in maquina.stateMachines)
            {
                if (filha.stateMachine == null) continue;
                ColetarMaquina(filha.stateMachine,
                    caminho + "/" + filha.stateMachine.name, porClipe);
            }
        }

        static void ColetarMotion(
            Motion motion,
            string caminho,
            string estado,
            Dictionary<AnimationClip, SlotEncontrado> porClipe)
        {
            if (motion is AnimationClip clip)
            {
                if (porClipe.TryGetValue(clip, out var existente))
                {
                    if (!existente.caminho.Contains(caminho))
                        existente.caminho += " | " + caminho;
                    return;
                }

                porClipe.Add(clip, new SlotEncontrado
                {
                    caminho = caminho,
                    estado = estado,
                    original = clip
                });
                return;
            }

            if (!(motion is BlendTree arvore)) return;
            var filhos = arvore.children;
            for (int i = 0; i < filhos.Length; i++)
                ColetarMotion(filhos[i].motion,
                    caminho + "/" + arvore.name + " [" + (i + 1) + "]",
                    estado,
                    porClipe);
        }

        static RuntimeAnimatorController ControllerBase(RuntimeAnimatorController controller)
        {
            while (controller is AnimatorOverrideController overrideController &&
                   overrideController.runtimeAnimatorController != null)
                controller = overrideController.runtimeAnimatorController;
            return controller;
        }
    }
}
