using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    public sealed class DiretorDeCombate : MonoBehaviour
    {
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] Transform jogador;
        [SerializeField, Min(0.05f)] float intervaloDeReavaliacao = 0.25f;
        [SerializeField, Min(0.5f)] float distanciaDaPrimeiraPosicao = 1.75f;
        [SerializeField, Min(0.5f)] float espacamentoDaFila = 1.2f;
        [SerializeField] bool ativo;

        readonly List<AlvoDeCombate> ordenados = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> comToken = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> selecionados = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> ladoEsquerdo = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> ladoDireito = new List<AlvoDeCombate>();
        float proximaReavaliacao;

        public bool Ativo => ativo;
        public int QuantidadeComToken => comToken.Count;
        public int MaximoAtacantes => 1;

        public void Configurar(RegistroDeCombate novoRegistro, Transform novoJogador)
        {
            registro = novoRegistro;
            jogador = novoJogador;
            ReavaliarAgora();
        }

        public void DefinirAtivo(bool valor)
        {
            ativo = valor;
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
            registro.PreencherTodos(jogador.position, ordenados);
            AtribuirFila(-1, ladoEsquerdo);
            AtribuirFila(1, ladoDireito);

            // A vez de ataque e global: mesmo cercado, somente um inimigo pode
            // avisar ou executar um golpe. O turno atual nao troca no meio da acao.
            for (int i = 0; i < comToken.Count; i++)
            {
                var atual = comToken[i];
                if (atual != null && atual.Valido && atual.Motor != null &&
                    (atual.Motor.AvisandoAtaque || atual.Motor.AtaqueEmAndamento))
                {
                    SelecionarToken(atual);
                    break;
                }
            }

            for (int i = 0; i < ordenados.Count && selecionados.Count < 1; i++)
                SelecionarToken(ordenados[i]);

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

        void AtribuirFila(int lado, List<AlvoDeCombate> fila)
        {
            registro.PreencherLado(jogador.position, lado, fila);
            for (int i = 0; i < fila.Count; i++)
            {
                var alvo = fila[i];
                if (alvo == null || alvo.Motor == null) continue;
                float x = jogador.position.x + lado * (distanciaDaPrimeiraPosicao + espacamentoDaFila * i);
                alvo.Motor.DefinirPosicaoNaFila(x, i, jogador);
            }
        }

        void SelecionarToken(AlvoDeCombate alvo)
        {
            if (alvo == null || !alvo.Valido || alvo.Reservado || selecionados.Contains(alvo) ||
                selecionados.Count >= 1)
                return;
            if (alvo.Motor == null || !alvo.Motor.PodeReceberToken) return;
            selecionados.Add(alvo);
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
