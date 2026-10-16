using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sardael.Tests
{
    public sealed class ControlesDeCombateTests
    {
        const string CaminhoDasAcoes = "Assets/Settings/InputSystem_Actions.inputactions";
        static readonly string[] CenasComSardaelJogavel =
        {
            "Assets/_Sardael/Cenas/Halu.unity",
            "Assets/_Sardael/Cenas/Tutorial_Fazenda.unity",
            "Assets/_Sardael/Cenas/Floresta_Trecho_01.unity",
            "Assets/_Sardael/Cenas/Combate_Sandbox.unity"
        };

        [Test]
        public void CenasJogaveisPossuemUmaEntradaConfiguradaPorHeroi()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(CaminhoDasAcoes);
            Assert.That(asset, Is.Not.Null);
            SceneSetup[] abertas = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string caminho in CenasComSardaelJogavel)
                {
                    Scene cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Single);
                    MovimentoDoHeroi[] herois = cena.GetRootGameObjects()
                        .SelectMany(raiz => raiz.GetComponentsInChildren<MovimentoDoHeroi>(true))
                        .ToArray();
                    Assert.That(herois, Is.Not.Empty, caminho + " precisa ter um heroi jogavel.");

                    foreach (MovimentoDoHeroi heroi in herois)
                    {
                        EntradaDeCombate[] entradas = heroi.GetComponents<EntradaDeCombate>();
                        Assert.That(entradas, Has.Length.EqualTo(1),
                            caminho + " precisa ter exatamente uma EntradaDeCombate no heroi.");

                        var serializado = new SerializedObject(entradas[0]);
                        SerializedProperty acoes = serializado.FindProperty("acoes");
                        Assert.That(acoes, Is.Not.Null);
                        Assert.That(acoes.objectReferenceValue, Is.EqualTo(asset),
                            caminho + " precisa apontar para InputSystem_Actions.");
                    }
                }
            }
            finally
            {
                if (abertas.Any(configuracao => configuracao.isLoaded))
                    EditorSceneManager.RestoreSceneManagerSetup(abertas);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void AcoesUsamOsControlesCombinados()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(CaminhoDasAcoes);
            Assert.That(asset, Is.Not.Null);
            InputActionMap mapa = asset.FindActionMap("Player", true);

            AssertBindings(mapa, "Interact", "<Keyboard>/e", "<Gamepad>/buttonNorth");
            AssertBindings(mapa, "Execution");
            AssertBindings(mapa, "Block", "<Mouse>/rightButton", "<Gamepad>/rightShoulder");
            AssertBindings(mapa, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
        }

        [Test]
        public void CorridaFicaArmadaEnquantoHaMovimentoEDesarmaAoParar()
        {
            Type tipo = typeof(EntradaDeCombate);
            MethodInfo registrar = tipo.GetMethod("RegistrarToqueDeCorrida");
            MethodInfo atualizar = tipo.GetMethod("AtualizarCorrida");
            Assert.That(registrar, Is.Not.Null, "EntradaDeCombate precisa registrar um toque de corrida.");
            Assert.That(atualizar, Is.Not.Null, "EntradaDeCombate precisa atualizar o latch pela entrada de movimento.");

            var objeto = new GameObject("EntradaDeCombate_Teste");
            try
            {
                var entrada = objeto.AddComponent<EntradaDeCombate>();
                registrar.Invoke(entrada, null);
                Assert.That(Atualizar(atualizar, entrada, Vector2.right, false), Is.True);
                Assert.That(Atualizar(atualizar, entrada, Vector2.right, false), Is.True);
                Assert.That(Atualizar(atualizar, entrada, Vector2.zero, false), Is.False);
                Assert.That(Atualizar(atualizar, entrada, Vector2.right, false), Is.False);

                registrar.Invoke(entrada, null);
                Assert.That(Atualizar(atualizar, entrada, Vector2.right, true), Is.False,
                    "Uma trava de gameplay deve desarmar a corrida.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(objeto);
            }
        }

        static bool Atualizar(MethodInfo metodo, EntradaDeCombate entrada, Vector2 movimento, bool travado)
        {
            return (bool)metodo.Invoke(entrada, new object[] { movimento, travado });
        }

        static void AssertBindings(InputActionMap mapa, string nomeDaAcao, params string[] esperados)
        {
            string[] atuais = mapa.FindAction(nomeDaAcao, true).bindings
                .Where(binding => !binding.isComposite && !binding.isPartOfComposite)
                .Select(binding => binding.path)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string[] ordenados = esperados.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(atuais, Is.EqualTo(ordenados), nomeDaAcao + " possui bindings incorretos.");
        }
    }
}
