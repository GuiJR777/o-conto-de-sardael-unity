using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Sardael.Tests
{
    public sealed class EfeitoDeFinalizacaoTests
    {
        const string CaminhoDaCabeca = "Assets/_Sardael/Personagens/Gore/Cabeca_Sardael_Decepada.prefab";

        [Test]
        public void CabecaDecepadaInstanciadaMantemColliderEmEscalaHumana()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoDaCabeca);
            Assert.That(prefab, Is.Not.Null, "Prefab da cabeca decepada nao encontrado.");

            var vitima = new GameObject("Vitima_Teste_Decapitacao");
            var osso = new GameObject("Head");
            osso.transform.SetParent(vitima.transform, false);
            var efeito = vitima.AddComponent<EfeitoDeFinalizacao>();
            efeito.automaticoPorAnimacao = false;
            efeito.decapitar = true;
            efeito.prefabDaCabeca = prefab;

            GameObject clone = null;
            try
            {
                efeito.DispararAgora();
                clone = GameObject.Find(prefab.name + "(Clone)");
                Assert.That(clone, Is.Not.Null, "A cabeca decepada nao foi instanciada.");

                var esfera = clone.GetComponent<SphereCollider>();
                Assert.That(esfera, Is.Not.Null, "A cabeca decepada precisa de SphereCollider.");
                float maiorEscala = Mathf.Max(
                    Mathf.Abs(clone.transform.lossyScale.x),
                    Mathf.Abs(clone.transform.lossyScale.y),
                    Mathf.Abs(clone.transform.lossyScale.z));
                float raioMundial = esfera.radius * maiorEscala;

                Assert.That(raioMundial, Is.LessThanOrEqualTo(0.5f),
                    "O collider da cabeca decepada nao pode englobar o personagem principal.");
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                Object.DestroyImmediate(vitima);
            }
        }
    }
}