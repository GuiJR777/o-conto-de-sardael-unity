using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    [Serializable]
    public sealed class VarianteDeExecucao
    {
        [SerializeField] string nome;
        [SerializeField] RuntimeAnimatorController controladorDoExecutor;
        [SerializeField] RuntimeAnimatorController controladorDaVitima;
        [SerializeField, Min(0.5f)] float distanciaDeAlinhamento = 1.756f;
        [SerializeField, Min(0.05f)] float instanteDoImpacto = 0.8f;
        [SerializeField, Min(0.2f)] float duracaoTotal = 1.8f;
        [SerializeField] bool decapitar;

        public string Nome => nome;
        public RuntimeAnimatorController ControladorDoExecutor => controladorDoExecutor;
        public RuntimeAnimatorController ControladorDaVitima => controladorDaVitima;
        public float DistanciaDeAlinhamento => distanciaDeAlinhamento;
        public float InstanteDoImpacto => instanteDoImpacto;
        public float DuracaoTotal => duracaoTotal;
        public bool Decapitar => decapitar;
        public bool Valida => controladorDoExecutor != null && controladorDaVitima != null;

        public VarianteDeExecucao(
            string novoNome,
            RuntimeAnimatorController novoControladorDoExecutor,
            RuntimeAnimatorController novoControladorDaVitima,
            float novaDistanciaDeAlinhamento,
            float novoInstanteDoImpacto,
            float novaDuracaoTotal,
            bool deveDecapitar = false)
        {
            nome = novoNome;
            controladorDoExecutor = novoControladorDoExecutor;
            controladorDaVitima = novoControladorDaVitima;
            distanciaDeAlinhamento = Mathf.Max(0.5f, novaDistanciaDeAlinhamento);
            instanteDoImpacto = Mathf.Max(0.05f, novoInstanteDoImpacto);
            duracaoTotal = Mathf.Max(instanteDoImpacto, novaDuracaoTotal);
            decapitar = deveDecapitar;
        }
    }

    public sealed class SistemaDeExecucao : MonoBehaviour
    {
        [SerializeField] MovimentoDoHeroi movimento;
        [SerializeField] EntradaDeCombate entrada;
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] SistemaDeFlow flow;
        [SerializeField] DiretorDeCombate diretor;
        [SerializeField] Animator animatorHeroi;
        [SerializeField] RuntimeAnimatorController controladorDaDerrubadaDoExecutor;
        [SerializeField] RuntimeAnimatorController controladorDaDerrubadaDaVitima;
        [SerializeField] VarianteDeExecucao[] variantes = Array.Empty<VarianteDeExecucao>();
        [SerializeField, Min(0.5f)] float alcanceDeReserva = 4.5f;
        [SerializeField, Min(0.1f)] float velocidadeDeAlinhamento = 7f;
        [SerializeField, Min(0.2f)] float duracaoDaDerrubada = 1.62f;
        [SerializeField, Min(0.1f)] float toleranciaDoComandoDeInteracao = 0.75f;
        [SerializeField, Min(1f)] float raioDoEspecial = 5.5f;

        readonly List<AlvoDeCombate> alvos = new List<AlvoDeCombate>();
        readonly List<int> indicesValidos = new List<int>(4);
        Coroutine rotina;
        int indiceDaUltimaVariante = -1;
        float interacaoPendenteAte = -1f;

        public bool EmExecucao => rotina != null;
        public int QuantidadeDeExecucoes { get; private set; }
        public int UltimoEspecialAcertos { get; private set; }
        public int QuantidadeDeVariantesDisponiveis => ContarVariantesValidas();
        public int IndiceDaUltimaVariante => indiceDaUltimaVariante;
        public string NomeDaUltimaVariante =>
            variantes != null && indiceDaUltimaVariante >= 0 && indiceDaUltimaVariante < variantes.Length
                ? variantes[indiceDaUltimaVariante].Nome
                : "-";

        public void Configurar(
            MovimentoDoHeroi novoMovimento,
            EntradaDeCombate novaEntrada,
            RegistroDeCombate novoRegistro,
            SistemaDeFlow novoFlow,
            DiretorDeCombate novoDiretor,
            Animator novoAnimatorHeroi,
            RuntimeAnimatorController novoControladorDaDerrubadaDoExecutor,
            RuntimeAnimatorController novoControladorDaDerrubadaDaVitima,
            VarianteDeExecucao[] novasVariantes)
        {
            movimento = novoMovimento;
            entrada = novaEntrada;
            registro = novoRegistro;
            flow = novoFlow;
            diretor = novoDiretor;
            animatorHeroi = novoAnimatorHeroi;
            controladorDaDerrubadaDoExecutor = novoControladorDaDerrubadaDoExecutor;
            controladorDaDerrubadaDaVitima = novoControladorDaDerrubadaDaVitima;
            variantes = novasVariantes ?? Array.Empty<VarianteDeExecucao>();
            indiceDaUltimaVariante = -1;
        }

        void Update()
        {
            if (entrada == null || EmExecucao) return;
            if (entrada.ConsumirEspecial()) TentarEspecial();
            if (entrada.ConsumirInteracao() && ExisteAlvoAtordoadoExecutavel())
                interacaoPendenteAte = Time.unscaledTime + toleranciaDoComandoDeInteracao;

            if (Time.unscaledTime <= interacaoPendenteAte && TentarExecutarAtordoado())
            {
                interacaoPendenteAte = -1f;
                return;
            }

            if (entrada.ConsumirExecucao()) TentarExecutar();
        }

        public bool TentarExecutarAtordoado()
        {
            if (EmExecucao || registro == null || movimento == null || movimento.EmAcao ||
                ContarVariantesValidas() == 0)
                return false;
            registro.PreencherTodos(transform.position, alvos);

            float eixo = entrada == null ? 0f : entrada.MoveX;
            int ladoPreferido = Mathf.Abs(eixo) > 0.15f ? (eixo < 0f ? -1 : 1) : 0;
            AlvoDeCombate escolhido = EncontrarAtordoado(ladoPreferido);
            if (escolhido == null && ladoPreferido != 0) escolhido = EncontrarAtordoado(0);

            if (escolhido == null || !escolhido.TentarReservar(this)) return false;
            rotina = StartCoroutine(RotinaDeExecucao(escolhido, SortearVariante()));
            return true;
        }

        bool ExisteAlvoAtordoadoExecutavel()
        {
            if (registro == null) return false;
            registro.PreencherTodos(transform.position, alvos);
            return EncontrarAtordoado(0) != null;
        }

        AlvoDeCombate EncontrarAtordoado(int ladoPreferido)
        {
            AlvoDeCombate escolhido = null;
            float menorDistancia = float.MaxValue;
            for (int i = 0; i < alvos.Count; i++)
            {
                var candidato = alvos[i];
                if (candidato == null || !candidato.Valido || !candidato.Atordoado || candidato.Reservado)
                    continue;
                float delta = candidato.transform.position.x - transform.position.x;
                float distancia = Mathf.Abs(delta);
                if (distancia > alcanceDeReserva) continue;
                if (ladoPreferido != 0 && (delta < 0f ? -1 : 1) != ladoPreferido) continue;
                if (distancia >= menorDistancia) continue;
                escolhido = candidato;
                menorDistancia = distancia;
            }
            return escolhido;
        }

        public bool TentarExecutar()
        {
            if (EmExecucao || flow == null || !flow.Cheio || registro == null || movimento == null ||
                movimento.EmAcao || ContarVariantesValidas() == 0)
                return false;

            int direcao = Mathf.Abs(entrada == null ? 0f : entrada.MoveX) > 0.15f
                ? (entrada.MoveX < 0f ? -1 : 1)
                : movimento.Olhando;
            var alvo = registro.PrimeiroNoLado(transform.position, direcao);
            if (alvo == null || Mathf.Abs(alvo.transform.position.x - transform.position.x) > alcanceDeReserva ||
                !alvo.TentarReservar(this))
                return false;

            if (!flow.ConsumirCheio())
            {
                alvo.LiberarReserva(this);
                return false;
            }

            rotina = StartCoroutine(RotinaDeExecucao(alvo, SortearVariante()));
            return true;
        }

        public bool TentarEspecial()
        {
            if (EmExecucao || flow == null || !flow.Cheio || registro == null) return false;
            registro.PreencherTodos(transform.position, alvos);
            UltimoEspecialAcertos = 0;

            for (int i = 0; i < alvos.Count; i++)
                if (Mathf.Abs(alvos[i].transform.position.x - transform.position.x) <= raioDoEspecial)
                    UltimoEspecialAcertos++;
            if (UltimoEspecialAcertos == 0 || !flow.ConsumirCheio()) return false;

            var empurrao = ScriptableObject.CreateInstance<DefinicaoDeAtaque>();
            empurrao.deslocamento = TipoDeDeslocamento.Push;
            empurrao.distanciaDeslocamento = 2.2f;
            empurrao.velocidadeDeslocamento = 9f;
            empurrao.dano = 0f;
            empurrao.poise = 0f;

            for (int i = 0; i < alvos.Count; i++)
            {
                var alvo = alvos[i];
                if (Mathf.Abs(alvo.transform.position.x - transform.position.x) > raioDoEspecial) continue;
                alvo.ReceberColisaoEmCadeia(true);
                alvo.Motor?.AplicarDeslocamento(empurrao, transform);
            }
            Destroy(empurrao);
            return UltimoEspecialAcertos > 0;
        }

        IEnumerator RotinaDeExecucao(AlvoDeCombate alvo, VarianteDeExecucao variante)
        {
            if (variante == null)
            {
                alvo?.LiberarReserva(this);
                rotina = null;
                yield break;
            }

            bool diretorEstavaAtivo = diretor != null && diretor.Ativo;
            diretor?.DefinirAtivo(false);
            alvo.Motor?.DefinirTokenDeAtaque(false, transform);

            float limite = Time.time + 1.25f;
            while (alvo != null && alvo.Valido && Time.time < limite)
            {
                float delta = alvo.transform.position.x - transform.position.x;
                int lado = delta < 0f ? -1 : 1;
                float xDesejado = alvo.transform.position.x - lado * variante.DistanciaDeAlinhamento;
                float erro = xDesejado - transform.position.x;
                if (Mathf.Abs(erro) <= 0.02f) break;
                movimento.DefinirDirecaoDeCombate(lado);
                float passo = Mathf.Clamp(erro,
                    -velocidadeDeAlinhamento * Time.deltaTime,
                    velocidadeDeAlinhamento * Time.deltaTime);
                if (!movimento.AdicionarDeslocamentoDeCombate(passo)) break;
                yield return null;
            }

            if (alvo == null || !alvo.Valido)
            {
                Encerrar(alvo, diretorEstavaAtivo);
                yield break;
            }

            int direcao = alvo.transform.position.x < transform.position.x ? -1 : 1;
            movimento.DefinirDirecaoDeCombate(direcao);
            movimento.Travar(this, true);
            alvo.transform.rotation = Quaternion.Euler(0f, direcao > 0 ? -90f : 90f, 0f);

            RuntimeAnimatorController originalHeroi = animatorHeroi == null ? null : animatorHeroi.runtimeAnimatorController;
            Animator animatorAlvo = alvo.GetComponent<Animator>();
            CharacterController controladorAlvo = alvo.GetComponent<CharacterController>();
            if (controladorAlvo != null) controladorAlvo.enabled = false;

            if (controladorDaDerrubadaDoExecutor != null && controladorDaDerrubadaDaVitima != null)
            {
                AplicarControlador(animatorHeroi, controladorDaDerrubadaDoExecutor);
                AplicarControlador(animatorAlvo, controladorDaDerrubadaDaVitima);
                yield return new WaitForSeconds(duracaoDaDerrubada);

                if (alvo == null || !alvo.Valido)
                {
                    RestaurarHeroi(originalHeroi);
                    Encerrar(alvo, diretorEstavaAtivo);
                    yield break;
                }

                // Os finalizadores Full Mount da bancada partem do mesmo pivô. A queda termina
                // com os corpos sobrepostos visualmente; igualar os pivôs evita o salto lateral
                // quando o par sorteado assume o Animator.
                Vector3 posicaoNoChao = transform.position;
                posicaoNoChao.y = alvo.transform.position.y;
                alvo.transform.position = posicaoNoChao;
            }

            AplicarControlador(animatorHeroi, variante.ControladorDoExecutor);
            AplicarControlador(animatorAlvo, variante.ControladorDaVitima);

            yield return new WaitForSeconds(variante.InstanteDoImpacto);
            if (alvo != null && alvo.Valido)
            {
                var efeito = alvo.GetComponent<EfeitoDeFinalizacao>();
                if (efeito != null)
                {
                    efeito.decapitar = variante.Decapitar;
                    efeito.DispararAgora();
                }
                alvo.Morrer();
                QuantidadeDeExecucoes++;
            }

            yield return new WaitForSeconds(
                Mathf.Max(0f, variante.DuracaoTotal - variante.InstanteDoImpacto));
            if (animatorAlvo != null) animatorAlvo.speed = 0f;
            RestaurarHeroi(originalHeroi);
            Encerrar(alvo, diretorEstavaAtivo);
        }

        static void AplicarControlador(Animator animator, RuntimeAnimatorController controlador)
        {
            if (animator == null || controlador == null) return;
            animator.speed = 1f;
            animator.runtimeAnimatorController = controlador;
            animator.Rebind();
            animator.Update(0f);
        }

        void RestaurarHeroi(RuntimeAnimatorController controladorOriginal)
        {
            if (animatorHeroi == null || controladorOriginal == null) return;
            AplicarControlador(animatorHeroi, controladorOriginal);
        }

        void Encerrar(AlvoDeCombate alvo, bool reativarDiretor)
        {
            movimento?.Travar(this, false);
            alvo?.LiberarReserva(this);
            if (diretor != null) diretor.DefinirAtivo(reativarDiretor);
            rotina = null;
        }

        int ContarVariantesValidas()
        {
            if (variantes == null) return 0;
            int quantidade = 0;
            for (int i = 0; i < variantes.Length; i++)
                if (variantes[i] != null && variantes[i].Valida) quantidade++;
            return quantidade;
        }

        VarianteDeExecucao SortearVariante()
        {
            if (variantes == null) return null;
            indicesValidos.Clear();
            for (int i = 0; i < variantes.Length; i++)
            {
                if (variantes[i] == null || !variantes[i].Valida) continue;
                if (i != indiceDaUltimaVariante) indicesValidos.Add(i);
            }

            // Com uma unica opcao valida, repetir e' preferivel a impedir a execucao.
            if (indicesValidos.Count == 0 && indiceDaUltimaVariante >= 0 &&
                indiceDaUltimaVariante < variantes.Length && variantes[indiceDaUltimaVariante].Valida)
                indicesValidos.Add(indiceDaUltimaVariante);
            if (indicesValidos.Count == 0) return null;

            indiceDaUltimaVariante = indicesValidos[UnityEngine.Random.Range(0, indicesValidos.Count)];
            return variantes[indiceDaUltimaVariante];
        }

        void OnDisable()
        {
            movimento?.Travar(this, false);
            interacaoPendenteAte = -1f;
            rotina = null;
        }
    }
}
