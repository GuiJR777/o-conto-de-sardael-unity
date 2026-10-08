using System.Linq;
using NUnit.Framework;
using SardaelEditor;
using UnityEditor;
using UnityEngine;

namespace Sardael.Tests
{
    public sealed class ExecucoesEmPeTests
    {
        const float DeslocamentoPlanarMaximo = 0.15f;
        const float RotacaoDaRaizMaxima = 40f;

        [Test]
        public void ExecucoesEmPeNaoOrbitamOAlvoAntesDoImpacto()
        {
            VarianteDeExecucao[] variantes = MontarCombateSandbox.CarregarVariantesDeExecucao();
            Assert.That(variantes, Is.Not.Null);

            VarianteDeExecucao[] emPe = variantes
                .Where(variante => variante != null && variante.Postura == PosturaDeExecucao.EmPe)
                .ToArray();
            Assert.That(emPe, Has.Length.EqualTo(2));
            Assert.That(
                emPe.Select(variante => variante.Nome).ToArray(),
                Is.EqualTo(new[] { "Attack6_Stage3_Complete", "Attack11" }));

            foreach (VarianteDeExecucao variante in emPe)
            {
                AnimationClip clip = variante.ControladorDoExecutor.animationClips.FirstOrDefault();
                Assert.That(clip, Is.Not.Null, variante.Nome + " nao possui clipe de animacao.");

                float deslocamento = MaiorDeslocamentoPlanar(clip);
                Assert.That(deslocamento, Is.LessThanOrEqualTo(DeslocamentoPlanarMaximo),
                    variante.Nome + " desloca a raiz " + deslocamento.ToString("F3") +
                    " m e faz o executor orbitar o alvo.");

                float rotacao = MaiorRotacaoDaRaiz(clip);
                Assert.That(rotacao, Is.LessThanOrEqualTo(RotacaoDaRaizMaxima),
                    variante.Nome + " gira a raiz " + rotacao.ToString("F1") +
                    " graus e faz o executor contornar o alvo antes do golpe.");
            }
        }

        static float MaiorDeslocamentoPlanar(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            EditorCurveBinding xBinding = bindings.FirstOrDefault(
                binding => binding.propertyName == "RootT.x");
            EditorCurveBinding zBinding = bindings.FirstOrDefault(
                binding => binding.propertyName == "RootT.z");

            AnimationCurve x = string.IsNullOrEmpty(xBinding.propertyName)
                ? null
                : AnimationUtility.GetEditorCurve(clip, xBinding);
            AnimationCurve z = string.IsNullOrEmpty(zBinding.propertyName)
                ? null
                : AnimationUtility.GetEditorCurve(clip, zBinding);
            if (x == null && z == null) return 0f;

            Vector2 origem = new Vector2(
                x == null ? 0f : x.Evaluate(0f),
                z == null ? 0f : z.Evaluate(0f));
            float maior = 0f;
            for (int i = 0; i <= 120; i++)
            {
                float tempo = clip.length * i / 120f;
                var posicao = new Vector2(
                    x == null ? 0f : x.Evaluate(tempo),
                    z == null ? 0f : z.Evaluate(tempo));
                maior = Mathf.Max(maior, Vector2.Distance(origem, posicao));
            }
            return maior;
        }

        static float MaiorRotacaoDaRaiz(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            AnimationCurve x = CurvaDaRaiz(clip, bindings, "RootQ.x");
            AnimationCurve y = CurvaDaRaiz(clip, bindings, "RootQ.y");
            AnimationCurve z = CurvaDaRaiz(clip, bindings, "RootQ.z");
            AnimationCurve w = CurvaDaRaiz(clip, bindings, "RootQ.w");
            if (x == null || y == null || z == null || w == null) return 0f;

            Quaternion origem = new Quaternion(
                x.Evaluate(0f), y.Evaluate(0f), z.Evaluate(0f), w.Evaluate(0f));
            float maior = 0f;
            for (int i = 0; i <= 120; i++)
            {
                float tempo = clip.length * i / 120f;
                var rotacao = new Quaternion(
                    x.Evaluate(tempo), y.Evaluate(tempo), z.Evaluate(tempo), w.Evaluate(tempo));
                maior = Mathf.Max(maior, Quaternion.Angle(origem, rotacao));
            }
            return maior;
        }

        static AnimationCurve CurvaDaRaiz(
            AnimationClip clip,
            EditorCurveBinding[] bindings,
            string propriedade)
        {
            EditorCurveBinding binding = bindings.FirstOrDefault(
                candidato => candidato.propertyName == propriedade);
            return string.IsNullOrEmpty(binding.propertyName)
                ? null
                : AnimationUtility.GetEditorCurve(clip, binding);
        }
    }
}
