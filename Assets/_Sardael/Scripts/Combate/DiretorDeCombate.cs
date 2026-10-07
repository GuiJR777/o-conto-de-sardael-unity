using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    public sealed class DiretorDeCombate : MonoBehaviour
    {
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] Transform jogador;
        [SerializeField, Min(0.05f)] float intervaloDeReavaliacao = 0.2f;
        [SerializeField, Min(1f)] float raioDoAnel = 4.25f;
        [SerializeField, Min(0f)] float variacaoRadialDoAnel = 0.35f;
        [SerializeField, Range(0f, 30f)] float grausDeStrafePorSegundo = 4.5f;
        [SerializeField, Range(1, 4)] int maximoAtacantes = 1;
        [SerializeField, Min(0f)] float atrasoDoPrimeiroTurno = 0.65f;
        [SerializeField, Min(0.1f)] float intervaloEntreTurnos = 1.15f;
        [SerializeField] bool ativo;

        readonly List<AlvoDeCombate> porDistancia = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> noAnel = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> ordemDoAnel = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> comToken = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> selecionados = new List<AlvoDeCombate>();
        float proximaReavaliacao;
        float proximoTurnoPermitido;
        float faseDoAnel;
        float instanteDaUltimaAtualizacaoDoAnel;
        bool anelInicializado;

        public bool Ativo => ativo;
        public int QuantidadeComToken => comToken.Count;
        public int MaximoAtacantes => maximoAtacantes;
        public string DonoDoPrimeiroToken => comToken.Count == 0 || comToken[0] == null
            ? "-"
            : comToken[0].name;

        public void Configurar(RegistroDeCombate novoRegistro, Transform novoJogador)
        {
            registro = novoRegistro;
            jogador = novoJogador;
            ReavaliarAgora();
        }

        public void DefinirAtivo(bool valor)
        {
            bool iniciando = valor && !ativo;
            ativo = valor;
            if (iniciando)
            {
                proximoTurnoPermitido = Mathf.Max(
                    proximoTurnoPermitido, Time.time + atrasoDoPrimeiroTurno);
                ordemDoAnel.Clear();
                anelInicializado = false;
            }
            ReavaliarAgora();
        }

        void Update()
        {
            if (Time.time >= proximaReavaliacao) ReavaliarAgora();
        }

        public void ReavaliarAgora()
        {
            proximaReavaliacao = Time.time + intervaloDeReavaliacao;
            if (!ativo || registro == null || jogador == null)
            {
                LimparTokens();
                return;
            }

            selecionados.Clear();
            registro.PreencherTodos(jogador.position, porDistancia);
            DistribuirAnel();

            // O turno permanece estavel desde a aproximacao ate a recuperacao. Sem isto,
            // pequenas mudancas de distancia no anel fariam o token saltar entre inimigos
            // antes de qualquer um conseguir telegrafar.
            bool turnoAnteriorTerminou = false;
            for (int i = 0; i < comToken.Count && selecionados.Count < maximoAtacantes; i++)
            {
                var atual = comToken[i];
                if (atual != null && atual.Valido && atual.Motor != null &&
                    atual.Motor.PossuiTokenDeAtaque && atual.Motor.PodeReceberToken)
                    SelecionarToken(atual);
                else if (atual != null && atual.Motor != null)
                    turnoAnteriorTerminou = true;
            }

            // Quando um golpe termina, abre-se uma janela de respiracao antes de outro
            // inimigo receber o turno. Assim a pressao continua, mas os avisos e golpes
            // deixam de parecer simultaneos.
            if (turnoAnteriorTerminou && selecionados.Count == 0)
                proximoTurnoPermitido = Mathf.Max(
                    proximoTurnoPermitido, Time.time + intervaloEntreTurnos);

            if (Time.time >= proximoTurnoPermitido)
                for (int i = 0; i < porDistancia.Count && selecionados.Count < maximoAtacantes; i++)
                    SelecionarToken(porDistancia[i]);

            for (int i = 0; i < comToken.Count; i++)
            {
                var anterior = comToken[i];
                if (anterior != null && anterior.Motor != null && !selecionados.Contains(anterior))
                    anterior.Motor.DefinirTokenDeAtaque(false, jogador);
            }
            for (int i = 0; i < selecionados.Count; i++)
            {
                var atual = selecionados[i];
                if (!comToken.Contains(atual)) atual.Motor.DefinirTokenDeAtaque(true, jogador);
            }
            comToken.Clear();
            comToken.AddRange(selecionados);
        }

        void DistribuirAnel()
        {
            noAnel.Clear();
            noAnel.AddRange(porDistancia);
            if (noAnel.Count == 0) return;

            bool ordemMudou = ordemDoAnel.Count != noAnel.Count;
            if (!ordemMudou)
                for (int i = 0; i < noAnel.Count; i++)
                    if (!ordemDoAnel.Contains(noAnel[i]))
                    {
                        ordemMudou = true;
                        break;
                    }

            if (ordemMudou || !anelInicializado)
            {
                noAnel.Sort((a, b) => AnguloAtual(a).CompareTo(AnguloAtual(b)));
                ordemDoAnel.Clear();
                ordemDoAnel.AddRange(noAnel);
                faseDoAnel = AnguloAtual(ordemDoAnel[0]) * Mathf.Rad2Deg;
                instanteDaUltimaAtualizacaoDoAnel = Time.time;
                anelInicializado = true;
            }
            else
            {
                noAnel.Clear();
                noAnel.AddRange(ordemDoAnel);
            }

            float deltaDoAnel = Mathf.Max(0f, Time.time - instanteDaUltimaAtualizacaoDoAnel);
            instanteDaUltimaAtualizacaoDoAnel = Time.time;
            faseDoAnel = Mathf.Repeat(
                faseDoAnel + grausDeStrafePorSegundo * deltaDoAnel, 360f);
            float passo = 360f / noAnel.Count;
            for (int i = 0; i < noAnel.Count; i++)
            {
                var alvo = noAnel[i];
                if (alvo == null || alvo.Motor == null) continue;
                float radianos = (faseDoAnel + passo * i) * Mathf.Deg2Rad;
                Vector3 radial = new Vector3(Mathf.Cos(radianos), 0f, Mathf.Sin(radianos));
                float raioDoSlot = raioDoAnel + (i % 2 == 0 ? 0f : variacaoRadialDoAnel);
                alvo.Motor.DefinirPosicaoNoAnel(
                    jogador.position + radial * raioDoSlot, i, jogador);
            }
        }

        float AnguloAtual(AlvoDeCombate alvo)
        {
            if (alvo == null || jogador == null) return 0f;
            Vector3 delta = alvo.transform.position - jogador.position;
            return Mathf.Atan2(delta.z, delta.x);
        }

        void SelecionarToken(AlvoDeCombate alvo)
        {
            if (alvo == null || !alvo.Valido || alvo.Reservado || selecionados.Contains(alvo) ||
                selecionados.Count >= maximoAtacantes)
                return;
            if (alvo.Motor == null || !alvo.Motor.PodeReceberToken) return;
            selecionados.Add(alvo);
        }

        public MotorDeCombateDoInimigo AmeacaMaisUrgente(
            Vector3 origem,
            Vector3 frente,
            float alcance,
            float anguloMaximo)
        {
            if (registro == null) return null;
            registro.PreencherDentroDoRaio(origem, alcance, porDistancia, true);
            frente.y = 0f;
            if (frente.sqrMagnitude <= 0.0001f) frente = Vector3.forward;
            frente.Normalize();
            float cosseno = Mathf.Cos(anguloMaximo * 0.5f * Mathf.Deg2Rad);
            MotorDeCombateDoInimigo melhor = null;
            float melhorTempo = float.PositiveInfinity;
            int melhorPrioridade = int.MaxValue;

            for (int i = 0; i < porDistancia.Count; i++)
            {
                var motor = porDistancia[i].Motor;
                if (motor == null || (!motor.AvisandoAtaque && !motor.AtaqueEmAndamento)) continue;
                Vector3 direcao = motor.transform.position - origem;
                direcao.y = 0f;
                if (direcao.sqrMagnitude <= 0.0001f || Vector3.Dot(frente, direcao.normalized) < cosseno)
                    continue;
                int prioridade = motor.AtaqueEmAndamento ? 0 : 1;
                float tempo = motor.TempoAteImpacto;
                if (prioridade > melhorPrioridade ||
                    (prioridade == melhorPrioridade && tempo >= melhorTempo)) continue;
                melhor = motor;
                melhorPrioridade = prioridade;
                melhorTempo = tempo;
            }
            return melhor;
        }

        void LimparTokens()
        {
            for (int i = 0; i < comToken.Count; i++)
                if (comToken[i] != null && comToken[i].Motor != null)
                    comToken[i].Motor.DefinirTokenDeAtaque(false, jogador);
            comToken.Clear();
        }

        void OnDisable() => LimparTokens();
    }
}
