using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Sardael.Tests
{
    public sealed class DefesaDoHeroiTests
    {
        [Test]
        public void JanelaDeParryPermaneceAtivaPorSeisDecimos()
        {
            var janela = new JanelaDeParry();

            janela.Abrir(10f, 0.6f);

            Assert.That(janela.Ativa(10f), Is.True);
            Assert.That(janela.Ativa(10.6f), Is.True);
            Assert.That(janela.Ativa(10.601f), Is.False);
        }

        [Test]
        public void PrimeiroImpactoConsomeAJanelaDeParry()
        {
            var janela = new JanelaDeParry();
            janela.Abrir(3f, 0.6f);

            Assert.That(janela.TentarConsumir(3.2f), Is.True);
            Assert.That(janela.TentarConsumir(3.3f), Is.False);
        }

        [Test]
        public void NovoPressionamentoReabreUmaJanelaConsumida()
        {
            var janela = new JanelaDeParry();
            janela.Abrir(1f, 0.6f);
            janela.TentarConsumir(1.1f);

            janela.Abrir(2f, 0.6f);

            Assert.That(janela.TentarConsumir(2.1f), Is.True);
        }

        [Test]
        public void SelecaoDirecionalCompartilhadaEscolheOInimigoApontado()
        {
            var registroObjeto = new GameObject("Registro_Teste_Defesa");
            var heroi = new GameObject("Heroi_Teste_Defesa");
            var inimigoEsquerdo = new GameObject("Inimigo_Esquerdo_Teste_Defesa");
            var inimigoDireito = new GameObject("Inimigo_Direito_Teste_Defesa");
            try
            {
                var registro = registroObjeto.AddComponent<RegistroDeCombate>();
                var combate = heroi.AddComponent<CombateDoHeroi>();
                var movimento = heroi.GetComponent<MovimentoDoHeroi>();
                var entrada = heroi.GetComponent<EntradaDeCombate>();
                var animator = heroi.GetComponent<Animator>();
                combate.Configurar(
                    movimento, entrada, registro, null, animator, new DefinicaoDeAtaque[0]);

                inimigoEsquerdo.transform.position = Vector3.left * 3f;
                inimigoDireito.transform.position = Vector3.right * 3f;
                var alvoEsquerdo = inimigoEsquerdo.AddComponent<AlvoDeCombate>();
                alvoEsquerdo.Configurar(registro, null);
                var alvoDireito = inimigoDireito.AddComponent<AlvoDeCombate>();
                alvoDireito.Configurar(registro, null);

                AlvoDeCombate escolhido = combate.EscolherAlvoDirecional(Vector3.left);

                Assert.That(escolhido, Is.SameAs(alvoEsquerdo));
            }
            finally
            {
                Object.DestroyImmediate(inimigoDireito);
                Object.DestroyImmediate(inimigoEsquerdo);
                Object.DestroyImmediate(heroi);
                Object.DestroyImmediate(registroObjeto);
            }
        }

        [Test]
        public void BloqueioMantidoNaoRepeteOParryNoSegundoGolpe()
        {
            var heroi = new GameObject("Heroi_Teste_Parry");
            var inimigo = new GameObject("Inimigo_Teste_Parry");
            try
            {
                var combate = heroi.AddComponent<CombateDoHeroi>();
                var entrada = heroi.GetComponent<EntradaDeCombate>();
                var movimento = heroi.GetComponent<MovimentoDoHeroi>();

                var defesa = heroi.AddComponent<DefesaDoHeroi>();
                defesa.Configurar(entrada, combate, movimento, null, null);

                inimigo.transform.position = Vector3.forward * 2f;
                var alvo = inimigo.AddComponent<AlvoDeCombate>();
                var motor = inimigo.AddComponent<MotorDeCombateDoInimigo>();
                motor.Configurar(null);
                alvo.Configurar(null, null, motor);

                entrada.InjetarBloqueio(true);
                typeof(DefesaDoHeroi)
                    .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(defesa, null);

                Assert.That(entrada.Bloqueando, Is.True);
                Assert.That(motor.Alvo, Is.SameAs(alvo));
                Assert.That(defesa.TentarBloquear(motor), Is.True);
                Assert.That(defesa.Aparos, Is.EqualTo(1));
                Assert.That(alvo.AparosRecebidos, Is.EqualTo(1));

                Assert.That(defesa.TentarBloquear(motor), Is.True,
                    "O segundo golpe ainda deve ser bloqueado.");
                Assert.That(defesa.Aparos, Is.EqualTo(1),
                    "Segurar o botão não pode gerar outro parry.");
                Assert.That(alvo.AparosRecebidos, Is.EqualTo(1),
                    "Bloqueio comum não pode provocar reação de parry no inimigo.");
            }
            finally
            {
                Object.DestroyImmediate(inimigo);
                Object.DestroyImmediate(heroi);
            }
        }
    }
}
