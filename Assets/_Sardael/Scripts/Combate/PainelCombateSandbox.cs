using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Sardael
{
    public sealed class PainelCombateSandbox : MonoBehaviour
    {
        [SerializeField] CombateDoHeroi combate;
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] ResolvedorDeImpacto resolvedor;
        [SerializeField] DiretorDeCombate diretor;
        [SerializeField] SistemaDeFlow flow;
        [SerializeField] SistemaDeExecucao execucao;
        [SerializeField] VidaDoHeroi vida;
        [SerializeField] DefesaDoHeroi defesa;
        [SerializeField] ModoDeCombate modo;
        [SerializeField] MovimentoDoHeroi movimento;

        Coroutine testeEmAndamento;

        public bool TesteEmAndamento => testeEmAndamento != null;

        public void Configurar(
            CombateDoHeroi novoCombate,
            RegistroDeCombate novoRegistro,
            ResolvedorDeImpacto novoResolvedor,
            DiretorDeCombate novoDiretor,
            SistemaDeFlow novoFlow,
            SistemaDeExecucao novaExecucao,
            VidaDoHeroi novaVida,
            DefesaDoHeroi novaDefesa,
            ModoDeCombate novoModo = null,
            MovimentoDoHeroi novoMovimento = null)
        {
            combate = novoCombate;
            registro = novoRegistro;
            resolvedor = novoResolvedor;
            diretor = novoDiretor;
            flow = novoFlow;
            execucao = novaExecucao;
            vida = novaVida;
            defesa = novaDefesa;
            modo = novoModo;
            movimento = novoMovimento;
        }

        void OnGUI()
        {
            if (combate == null) return;
            string alvo = combate.AlvoAtual == null ? "-" : combate.AlvoAtual.name;
            string distancia = combate.DistanciaDoAlvo < 0f ? "-" : combate.DistanciaDoAlvo.ToString("F2") + " m";
            GUI.Box(new Rect(10, 164, 430, 390), GUIContent.none);
            GUI.Label(new Rect(20, 174, 410, 268),
                "COMBATE SANDBOX\n" +
                "modo: " + (modo == null ? "-" : modo.Estado.ToString()) +
                "  z original/atual: " + (modo == null ? "-" : modo.ZOriginal.ToString("F2")) +
                "/" + (movimento == null ? "-" : movimento.transform.position.z.ToString("F2")) + "\n" +
                "alvo: " + alvo + "\n" +
                "elo: " + combate.EloAtual + "   direcao: " + combate.DirecaoEscolhida.ToString("F2") + "\n" +
                "distancia: " + distancia + "\n" +
                "inimigos registrados: " + (registro == null ? 0 : registro.Quantidade) + "\n" +
                "ultimo impacto: " + (resolvedor == null ? "-" : resolvedor.UltimoPadrao + " / " + resolvedor.UltimaQuantidadeDeAcertos) + "\n" +
                "acertos por elo: " + (resolvedor == null ? "-" :
                    resolvedor.AcertosDoElo(1) + "/" + resolvedor.AcertosDoElo(2) + "/" +
                    resolvedor.AcertosDoElo(3) + "/" + resolvedor.AcertosDoElo(4)) + "\n" +
                "alvos por elo: " + combate.UltimoAlvoDoElo(1) + "/" +
                    combate.UltimoAlvoDoElo(2) + "/" + combate.UltimoAlvoDoElo(3) + "/" +
                    combate.UltimoAlvoDoElo(4) + "\n" +
                "director: " + (diretor != null && diretor.Ativo ? "ATIVO" : "parado") +
                "  tokens: " + (diretor == null ? 0 : diretor.QuantidadeComToken) +
                "  dono: " + (diretor == null ? "-" : diretor.DonoDoPrimeiroToken) + "\n" +
                "flow: " + (flow == null ? "-" : flow.Atual.ToString("F0") + "/" + flow.Maximo.ToString("F0")) +
                "  gerado: " + (flow == null ? "-" : flow.TotalGerado.ToString("F0")) + "\n" +
                "vida: " + (vida == null ? "-" : vida.Atual.ToString("F0") + "/" + vida.Maxima.ToString("F0")) +
                "  bloqueio: " + (defesa != null && defesa.BloqueandoAgora ? "ATIVO" : "livre") + "\n" +
                "aparos/contra: " + (defesa == null ? "-" : defesa.Aparos + "/" + defesa.ContraAtaques) + "\n" +
                "execucoes: " + (execucao == null ? 0 : execucao.QuantidadeDeExecucoes) +
                "  especial: " + (execucao == null ? 0 : execucao.UltimoEspecialAcertos) + "\n" +
                "finalizador: " + (execucao == null ? "-" : execucao.NomeDaUltimaVariante) +
                "  opcoes: " + (execucao == null ? 0 : execucao.QuantidadeDeVariantesDisponiveis));

            GUI.enabled = testeEmAndamento == null;
            if (GUI.Button(new Rect(10, 444, 200, 28), "Testar D-D-A-D")) TestarComboCompleto();
            if (GUI.Button(new Rect(220, 444, 220, 28), "Encher Flow + Execucao")) TestarExecucao();
            if (GUI.Button(new Rect(10, 478, 200, 28), "Encher Flow + Especial")) TestarEspecial();
            if (diretor != null && GUI.Button(new Rect(220, 478, 220, 28),
                    diretor.Ativo ? "Parar Director" : "Ativar Director"))
                diretor.DefinirAtivo(!diretor.Ativo);
            if (GUI.Button(new Rect(10, 512, 200, 28), "Testar D-D-D-D")) TestarComboFrontal();
            if (GUI.Button(new Rect(220, 512, 220, 28), "Testar Bloqueio + Contra"))
                TestarBloqueioEContraAtaque();
            GUI.enabled = true;
        }

        public void TestarComboCompleto()
        {
            if (testeEmAndamento == null)
                testeEmAndamento = StartCoroutine(RotinaCombo(Key.D, Key.D, Key.A, Key.D));
        }

        public void TestarComboFrontal()
        {
            if (testeEmAndamento == null)
                testeEmAndamento = StartCoroutine(RotinaCombo(Key.D, Key.D, Key.D, Key.D));
        }

        public void TestarExecucao()
        {
            if (testeEmAndamento == null) testeEmAndamento = StartCoroutine(RotinaAcaoDeFlow(Key.Q));
        }

        public void TestarEspecial()
        {
            if (testeEmAndamento == null) testeEmAndamento = StartCoroutine(RotinaAcaoDeFlow(Key.R));
        }

        public void TestarBloqueioEContraAtaque()
        {
            if (testeEmAndamento == null)
                testeEmAndamento = StartCoroutine(RotinaBloqueioEContraAtaque());
        }

        IEnumerator RotinaBloqueioEContraAtaque()
        {
            var teclado = InputSystem.GetDevice<Keyboard>();
            if (teclado == null || defesa == null)
            {
                testeEmAndamento = null;
                yield break;
            }

            diretor?.DefinirAtivo(true);
            MotorDeCombateDoInimigo atacante = null;
            float limiteDoTurno = Time.time + 1.5f;
            while (atacante == null && Time.time < limiteDoTurno)
            {
                var motores = FindObjectsByType<MotorDeCombateDoInimigo>(FindObjectsSortMode.None);
                for (int i = 0; i < motores.Length; i++)
                    if (motores[i].PossuiTokenDeAtaque)
                    {
                        atacante = motores[i];
                        break;
                    }
                if (atacante == null) yield return null;
            }

            InputSystem.QueueStateEvent(teclado, new KeyboardState(Key.F));
            float limite = Time.time + 5f;
            while (!defesa.ContraAtaqueDisponivel && Time.time < limite) yield return null;
            if (defesa.ContraAtaqueDisponivel)
            {
                InputSystem.QueueStateEvent(teclado, new KeyboardState(Key.F, Key.Enter));
                yield return new WaitForSeconds(0.12f);
            }
            InputSystem.QueueStateEvent(teclado, new KeyboardState());
            yield return new WaitForSeconds(0.8f);
            diretor?.DefinirAtivo(false);
            testeEmAndamento = null;
        }

        IEnumerator RotinaAcaoDeFlow(Key tecla)
        {
            var teclado = InputSystem.GetDevice<Keyboard>();
            if (teclado == null || flow == null)
            {
                testeEmAndamento = null;
                yield break;
            }
            flow.Adicionar(flow.Maximo);
            yield return PressionarBotao(teclado, tecla);
            // As variantes no chao incluem a derrubada antes da finalizacao; as em pe
            // comecam direto no par da lanca. A margem cobre alinhamento e animacao.
            float limite = Time.time + 8f;
            while (execucao != null && execucao.EmExecucao && Time.time < limite) yield return null;
            testeEmAndamento = null;
        }

        IEnumerator RotinaCombo(Key primeiro, Key segundo, Key terceiro, Key quarto)
        {
            var teclado = InputSystem.GetDevice<Keyboard>();
            if (teclado == null)
            {
                testeEmAndamento = null;
                yield break;
            }

            yield return PressionarAtaque(teclado, primeiro);
            yield return EsperarElo(1);
            yield return new WaitForSeconds(0.18f);
            yield return PressionarAtaque(teclado, segundo);
            yield return EsperarElo(2);
            yield return new WaitForSeconds(0.12f);
            yield return PressionarAtaque(teclado, terceiro);
            yield return EsperarElo(3);
            yield return new WaitForSeconds(0.12f);
            yield return PressionarAtaque(teclado, quarto);
            yield return EsperarElo(4);
            testeEmAndamento = null;
        }

        IEnumerator EsperarElo(int elo)
        {
            float limite = Time.time + 3f;
            while (combate != null && combate.EloAtual != elo && Time.time < limite) yield return null;
        }

        static IEnumerator PressionarAtaque(Keyboard teclado, Key direcao)
        {
            InputSystem.QueueStateEvent(teclado, new KeyboardState(direcao, Key.Enter));
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(teclado, new KeyboardState());
        }

        static IEnumerator PressionarBotao(Keyboard teclado, Key tecla)
        {
            InputSystem.QueueStateEvent(teclado, new KeyboardState(tecla));
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(teclado, new KeyboardState());
        }
    }
}
