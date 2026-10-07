using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace Sardael
{
    public sealed class ResolvedorDeImpacto : MonoBehaviour
    {
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] SistemaDeFlow flow;
        [SerializeField, Min(0f)] float tolerancia = 0.35f;

        readonly List<AlvoDeCombate> candidatos = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> atingidos = new List<AlvoDeCombate>();
        readonly int[] acertosPorElo = new int[5];
        Coroutine pushDoJogador;

        public int UltimaQuantidadeDeAcertos { get; private set; }
        public PadraoDeAlvo UltimoPadrao { get; private set; }

        public int AcertosDoElo(int elo) => elo >= 1 && elo < acertosPorElo.Length ? acertosPorElo[elo] : 0;

        public void Configurar(RegistroDeCombate novoRegistro, SistemaDeFlow novoFlow = null)
        {
            registro = novoRegistro;
            flow = novoFlow;
        }

        public bool Resolver(DefinicaoDeAtaque ataque, Transform atacante, AlvoDeCombate alvo)
        {
            atingidos.Clear();
            UltimaQuantidadeDeAcertos = 0;
            if (ataque == null || atacante == null || alvo == null || !alvo.Valido) return false;
            float distancia = RegistroDeCombate.DistanciaPlanar(
                alvo.transform.position, atacante.position);
            if (distancia > ataque.alcance + tolerancia) return false;

            UltimoPadrao = ataque.padraoDeAlvo;
            Adicionar(alvo, ataque, atacante.position, false);

            switch (ataque.padraoDeAlvo)
            {
                case PadraoDeAlvo.FrontTwo:
                    AdicionarNoCone(ataque, atacante.position, alvo, 2, false);
                    break;
                case PadraoDeAlvo.FrontThree:
                    AdicionarNoCone(ataque, atacante.position, alvo, 3, false);
                    break;
                case PadraoDeAlvo.BothSides:
                    AdicionarNoCone(ataque, atacante.position, alvo, 2, true);
                    break;
                case PadraoDeAlvo.Piercing:
                    AdicionarNoCone(ataque, atacante.position, alvo, int.MaxValue, false);
                    break;
            }

            for (int i = 0; i < atingidos.Count; i++) atingidos[i].ReceberImpacto(ataque, atacante);
            if (ataque.deslocamento == TipoDeDeslocamento.PushPlayer)
                EmpurrarJogador(ataque, atacante, alvo.transform);
            UltimaQuantidadeDeAcertos = atingidos.Count;
            flow?.RegistrarAcerto(ataque, UltimaQuantidadeDeAcertos);
            if (ataque.eloCombo >= 1 && ataque.eloCombo < acertosPorElo.Length)
                acertosPorElo[ataque.eloCombo] = UltimaQuantidadeDeAcertos;
            return UltimaQuantidadeDeAcertos > 0;
        }

        void AdicionarNoCone(
            DefinicaoDeAtaque ataque,
            Vector3 origem,
            AlvoDeCombate alvoPrimario,
            int maximo,
            bool ladoOposto)
        {
            if (registro == null || atingidos.Count >= maximo) return;
            Vector3 direcao = alvoPrimario.transform.position - origem;
            direcao.y = 0f;
            if (ladoOposto) direcao = -direcao;
            registro.PreencherNoCone(
                origem, direcao, ataque.alcanceMultiAlvo + tolerancia, 0.15f, candidatos);
            for (int i = 0; i < candidatos.Count && atingidos.Count < maximo; i++)
                Adicionar(candidatos[i], ataque, origem, true);
        }

        void Adicionar(
            AlvoDeCombate alvo,
            DefinicaoDeAtaque ataque,
            Vector3 origem,
            bool secundario)
        {
            if (alvo == null || !alvo.Valido || atingidos.Contains(alvo)) return;
            float limite = secundario ? ataque.alcanceMultiAlvo : ataque.alcance;
            if (RegistroDeCombate.DistanciaPlanar(alvo.transform.position, origem) > limite + tolerancia)
                return;
            atingidos.Add(alvo);
        }

        void EmpurrarJogador(DefinicaoDeAtaque ataque, Transform atacante, Transform alvo)
        {
            var movimento = atacante.GetComponent<MovimentoDoHeroi>();
            if (movimento == null || ataque.distanciaDeslocamento <= 0f) return;
            if (pushDoJogador != null) StopCoroutine(pushDoJogador);
            Vector3 direcao = atacante.position - alvo.position;
            direcao.y = 0f;
            if (direcao.sqrMagnitude <= 0.0001f) direcao = -atacante.forward;
            direcao.Normalize();
            pushDoJogador = StartCoroutine(RotinaPushDoJogador(
                movimento, direcao, ataque.distanciaDeslocamento, ataque.velocidadeDeslocamento));
        }

        IEnumerator RotinaPushDoJogador(
            MovimentoDoHeroi movimento,
            Vector3 direcao,
            float distancia,
            float velocidade)
        {
            float restante = distancia;
            while (movimento != null && restante > 0.001f)
            {
                float passo = Mathf.Min(restante, Mathf.Max(0.1f, velocidade) * Time.deltaTime);
                if (!movimento.AdicionarDeslocamentoDeCombate(direcao * passo)) break;
                restante -= passo;
                yield return null;
            }
            pushDoJogador = null;
        }
    }
}
