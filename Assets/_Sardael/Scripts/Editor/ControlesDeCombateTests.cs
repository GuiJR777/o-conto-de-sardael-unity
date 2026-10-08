using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sardael.Tests
{
    public sealed class ControlesDeCombateTests
    {
        const string CaminhoDasAcoes = "Assets/Settings/InputSystem_Actions.inputactions";

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
