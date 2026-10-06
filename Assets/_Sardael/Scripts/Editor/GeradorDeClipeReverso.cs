using System;
using UnityEditor;
using UnityEngine;

namespace SardaelEditor
{
    /// <summary>
    /// Cria copias editor-only de AnimationClips com o tempo espelhado. Os FBXs
    /// de origem nunca sao alterados, e o caminho estavel preserva o GUID gerado.
    /// </summary>
    internal static class GeradorDeClipeReverso
    {
        const string PastaAnimacoes = "Assets/_Sardael/Animacoes";
        const string PastaRaiz = PastaAnimacoes + "/Geradas";
        const string PastaReversas = PastaRaiz + "/Reversas";

        public static string ObterAssinatura(AnimationClip fonte)
        {
            return TryObterIdentidade(fonte, out _, out _, out string assinatura)
                ? assinatura
                : string.Empty;
        }

        public static bool TryCriarOuAtualizar(
            AnimationClip fonte,
            out AnimationClip reverso,
            out string assinatura,
            out string erro)
        {
            reverso = null;
            erro = string.Empty;
            assinatura = string.Empty;

            if (fonte == null)
            {
                erro = "Escolha um clipe original ou substituto antes de inverter.";
                return false;
            }

            if (!TryObterIdentidade(
                    fonte,
                    out string guid,
                    out long fileId,
                    out assinatura))
            {
                erro = "O clipe precisa ser um asset salvo no projeto para gerar a versao reversa.";
                return false;
            }

            AnimationClip novo = null;
            try
            {
                GarantirPastas();
                string nomeDoAsset = guid + "_" + fileId + "_Reverso";
                string caminho = PastaReversas + "/" + nomeDoAsset + ".anim";
                novo = ConstruirClipe(fonte, nomeDoAsset);

                var existente = AssetDatabase.LoadAssetAtPath<AnimationClip>(caminho);
                if (existente == null)
                {
                    AssetDatabase.CreateAsset(novo, caminho);
                    reverso = novo;
                    novo = null;
                }
                else
                {
                    EditorUtility.CopySerialized(novo, existente);
                    UnityEngine.Object.DestroyImmediate(novo);
                    novo = null;
                    EditorUtility.SetDirty(existente);
                    AssetDatabase.SaveAssetIfDirty(existente);
                    reverso = existente;
                }

                return reverso != null;
            }
            catch (Exception excecao)
            {
                if (novo != null) UnityEngine.Object.DestroyImmediate(novo);
                reverso = null;
                erro = "Nao foi possivel gerar o clipe reverso: " + excecao.Message;
                Debug.LogException(excecao);
                return false;
            }
        }

        static AnimationClip ConstruirClipe(AnimationClip fonte, string nomeDoAsset)
        {
            float duracao = fonte.length;
            var resultado = new AnimationClip
            {
                name = nomeDoAsset,
                frameRate = fonte.frameRate,
                legacy = fonte.legacy,
                wrapMode = fonte.wrapMode,
                localBounds = fonte.localBounds
            };

            AnimationUtility.SetAnimationClipSettings(
                resultado,
                AnimationUtility.GetAnimationClipSettings(fonte));

            var curvas = AnimationUtility.GetCurveBindings(fonte);
            for (int i = 0; i < curvas.Length; i++)
            {
                var curva = AnimationUtility.GetEditorCurve(fonte, curvas[i]);
                if (curva == null) continue;
                AnimationUtility.SetEditorCurve(resultado, curvas[i], Inverter(curva, duracao));
            }

            var referencias = AnimationUtility.GetObjectReferenceCurveBindings(fonte);
            for (int i = 0; i < referencias.Length; i++)
            {
                var chaves = AnimationUtility.GetObjectReferenceCurve(fonte, referencias[i]);
                AnimationUtility.SetObjectReferenceCurve(
                    resultado,
                    referencias[i],
                    Inverter(chaves, duracao));
            }

            AnimationUtility.SetAnimationEvents(
                resultado,
                Inverter(AnimationUtility.GetAnimationEvents(fonte), duracao));
            resultado.EnsureQuaternionContinuity();
            return resultado;
        }

        static AnimationCurve Inverter(AnimationCurve origem, float duracao)
        {
            var antigas = origem.keys;
            var novas = new Keyframe[antigas.Length];
            for (int i = 0; i < antigas.Length; i++)
            {
                Keyframe antiga = antigas[antigas.Length - 1 - i];
                var nova = new Keyframe(
                    Mathf.Max(0f, duracao - antiga.time),
                    antiga.value,
                    -antiga.outTangent,
                    -antiga.inTangent,
                    antiga.outWeight,
                    antiga.inWeight)
                {
                    weightedMode = TrocarPesos(antiga.weightedMode)
                };
                novas[i] = nova;
            }

            return new AnimationCurve(novas)
            {
                preWrapMode = origem.postWrapMode,
                postWrapMode = origem.preWrapMode
            };
        }

        static WeightedMode TrocarPesos(WeightedMode modo)
        {
            if (modo == WeightedMode.In) return WeightedMode.Out;
            if (modo == WeightedMode.Out) return WeightedMode.In;
            return modo;
        }

        static ObjectReferenceKeyframe[] Inverter(
            ObjectReferenceKeyframe[] origem,
            float duracao)
        {
            if (origem == null) return Array.Empty<ObjectReferenceKeyframe>();
            var resultado = new ObjectReferenceKeyframe[origem.Length];
            for (int i = 0; i < origem.Length; i++)
            {
                var antiga = origem[origem.Length - 1 - i];
                resultado[i] = new ObjectReferenceKeyframe
                {
                    time = Mathf.Max(0f, duracao - antiga.time),
                    value = antiga.value
                };
            }
            return resultado;
        }

        static AnimationEvent[] Inverter(AnimationEvent[] origem, float duracao)
        {
            if (origem == null) return Array.Empty<AnimationEvent>();
            var resultado = new AnimationEvent[origem.Length];
            for (int i = 0; i < origem.Length; i++)
            {
                var antigo = origem[i];
                resultado[i] = new AnimationEvent
                {
                    functionName = antigo.functionName,
                    time = Mathf.Clamp(duracao - antigo.time, 0f, duracao),
                    floatParameter = antigo.floatParameter,
                    intParameter = antigo.intParameter,
                    stringParameter = antigo.stringParameter,
                    objectReferenceParameter = antigo.objectReferenceParameter,
                    messageOptions = antigo.messageOptions
                };
            }

            Array.Sort(resultado, (a, b) => a.time.CompareTo(b.time));
            return resultado;
        }

        static bool TryObterIdentidade(
            AnimationClip fonte,
            out string guid,
            out long fileId,
            out string assinatura)
        {
            guid = string.Empty;
            fileId = 0;
            assinatura = string.Empty;
            if (fonte == null) return false;

            string caminho = AssetDatabase.GetAssetPath(fonte);
            if (string.IsNullOrEmpty(caminho) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    fonte,
                    out guid,
                    out fileId))
                return false;

            assinatura = guid + ":" + fileId + ":" +
                AssetDatabase.GetAssetDependencyHash(caminho);
            return true;
        }

        static void GarantirPastas()
        {
            if (!AssetDatabase.IsValidFolder(PastaRaiz))
                AssetDatabase.CreateFolder(PastaAnimacoes, "Geradas");
            if (!AssetDatabase.IsValidFolder(PastaReversas))
                AssetDatabase.CreateFolder(PastaRaiz, "Reversas");
        }
    }
}

