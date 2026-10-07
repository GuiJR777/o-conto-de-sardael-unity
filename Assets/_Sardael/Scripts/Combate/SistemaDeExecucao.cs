using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    public enum PosturaDeExecucao
    {
        NoChao,
        EmPe
    }

    [Serializable]
    public sealed class VarianteDeExecucao
    {
        [SerializeField] string nome;
        [SerializeField] RuntimeAnimatorController controladorDoExecutor;
        [SerializeField] RuntimeAnimatorController controladorDaVitima;
        [SerializeField] PosturaDeExecucao postura = PosturaDeExecucao.NoChao;
        [SerializeField, Min(0.5f)] float distanciaDeAlinhamento = 1.756f;
        [SerializeField] float desvioLateralDeAlinhamento;
        [SerializeField, Min(0.05f)] float instanteDoImpacto = 0.8f;
        [SerializeField, Min(0.2f)] float duracaoTotal = 1.8f;
        [SerializeField] bool decapitar;

        public string Nome => nome;
        public RuntimeAnimatorController ControladorDoExecutor => controladorDoExecutor;
        public RuntimeAnimatorController ControladorDaVitima => controladorDaVitima;
        public PosturaDeExecucao Postura => postura;
        public bool ExigeDerrubada => postura == PosturaDeExecucao.NoChao;
        public float DistanciaDeAlinhamento => distanciaDeAlinhamento;
        public float DesvioLateralDeAlinhamento => desvioLateralDeAlinhamento;
        public float InstanteDoImpacto => instanteDoImpacto;
        public float DuracaoTotal => duracaoTotal;
        public bool Decapitar => decapitar;
        public bool Valida => controladorDoExecutor != null && controladorDaVitima != null;

        public VarianteDeExecucao(
            string novoNome,
            RuntimeAnimatorController novoControladorDoExecutor,
            RuntimeAnimatorController novoControladorDaVitima,
            PosturaDeExecucao novaPostura,
            float novaDistanciaDeAlinhamento,
            float novoDesvioLateralDeAlinhamento,
            float novoInstanteDoImpacto,
            float novaDuracaoTotal,
            bool deveDecapitar = false)
        {
            nome = novoNome;
            controladorDoExecutor = novoControladorDoExecutor;
            controladorDaVitima = novoControladorDaVitima;
            postura = novaPostura;
            distanciaDeAlinhamento = Mathf.Max(0.5f, novaDistanciaDeAlinhamento);
            desvioLateralDeAlinhamento = novoDesvioLateralDeAlinhamento;
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
        [SerializeField, Min(0f)] float invulnerabilidadeAposExecucao = 1.25f;

        readonly List<AlvoDeCombate> alvos = new List<AlvoDeCombate>();
        // Baralho de variantes: todas aparecem uma vez antes de o conjunto ser embaralhado de novo.
        // Assim a aleatoriedade nao fica parecendo "sempre a mesma" por puro azar.
        readonly List<int> sacoDeVariantes = new List<int>(8);
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
            ConfigurarVariantes(novasVariantes);
        }

        public void ConfigurarVariantes(VarianteDeExecucao[] novasVariantes)
        {
            variantes = novasVariantes ?? Array.Empty<VarianteDeExecucao>();
            indiceDaUltimaVariante = -1;
            sacoDeVariantes.Clear();
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

            Vector2 eixo = entrada == null ? Vector2.zero : entrada.Movimento;
            AlvoDeCombate escolhido = EncontrarAtordoado(
                new Vector3(eixo.x, 0f, eixo.y));

            if (escolhido == null || !escolhido.TentarReservar(this)) return false;
            rotina = StartCoroutine(RotinaDeExecucao(escolhido, SortearVariante()));
            return true;
        }

        bool ExisteAlvoAtordoadoExecutavel()
        {
            if (registro == null) return false;
            registro.PreencherTodos(transform.position, alvos);
            return EncontrarAtordoado(Vector3.zero) != null;
        }

        AlvoDeCombate EncontrarAtordoado(Vector3 direcaoPreferida)
        {
            AlvoDeCombate escolhido = null;
            float melhorPontuacao = float.NegativeInfinity;
            direcaoPreferida.y = 0f;
            bool temDirecao = direcaoPreferida.sqrMagnitude > 0.0225f;
            if (temDirecao) direcaoPreferida.Normalize();
            for (int i = 0; i < alvos.Count; i++)
            {
                var candidato = alvos[i];
                if (candidato == null || !candidato.Valido || !candidato.Atordoado || candidato.Reservado)
                    continue;
                Vector3 delta = candidato.transform.position - transform.position;
                delta.y = 0f;
                float distancia = delta.magnitude;
                if (distancia > alcanceDeReserva) continue;
                float pontuacao = -distancia;
                if (temDirecao && distancia > 0.001f)
                    pontuacao += Vector3.Dot(direcaoPreferida, delta / distancia) * 5f;
                if (pontuacao <= melhorPontuacao) continue;
                escolhido = candidato;
                melhorPontuacao = pontuacao;
            }
            return escolhido;
        }

        public bool TentarExecutar()
        {
            if (EmExecucao || flow == null || !flow.Cheio || registro == null || movimento == null ||
                movimento.EmAcao || ContarVariantesValidas() == 0)
                return false;

            Vector2 eixo = entrada == null ? Vector2.zero : entrada.Movimento;
            Vector3 direcao = new Vector3(eixo.x, 0f, eixo.y);
            var alvo = registro.MelhorAlvoNaDirecao(
                transform.position, direcao, transform.forward, null, alcanceDeReserva);
            if (alvo == null ||
                RegistroDeCombate.DistanciaPlanar(alvo.transform.position, transform.position) > alcanceDeReserva ||
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
                if (RegistroDeCombate.DistanciaPlanar(
                    alvos[i].transform.position, transform.position) <= raioDoEspecial)
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
                if (RegistroDeCombate.DistanciaPlanar(
                    alvo.transform.position, transform.position) > raioDoEspecial) continue;
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
                Vector3 direcao = alvo.transform.position - transform.position;
                direcao.y = 0f;
                if (direcao.sqrMagnitude <= 0.0001f) direcao = transform.forward;
                direcao.Normalize();
                Vector3 direita = Vector3.Cross(Vector3.up, direcao).normalized;
                Vector3 desejada = alvo.transform.position -
                    direcao * variante.DistanciaDeAlinhamento -
                    direita * variante.DesvioLateralDeAlinhamento;
                desejada.y = transform.position.y;
                Vector3 erro = desejada - transform.position;
                erro.y = 0f;
                if (erro.sqrMagnitude <= 0.0004f) break;
                movimento.DefinirDirecaoDeCombate(direcao);
                Vector3 passo = Vector3.ClampMagnitude(
                    erro, velocidadeDeAlinhamento * Time.deltaTime);
                if (!movimento.AdicionarDeslocamentoDeCombate(passo)) break;
                yield return null;
            }

            if (alvo == null || !alvo.Valido)
            {
                Encerrar(alvo, diretorEstavaAtivo);
                yield break;
            }

            Vector3 direcaoFinal = alvo.transform.position - transform.position;
            direcaoFinal.y = 0f;
            if (direcaoFinal.sqrMagnitude <= 0.0001f) direcaoFinal = transform.forward;
            direcaoFinal.Normalize();
            movimento.DefinirDirecaoDeCombate(direcaoFinal);
            movimento.Travar(this, true);
            Quaternion rotacaoDoExecutor = Quaternion.LookRotation(direcaoFinal, Vector3.up);
            transform.rotation = rotacaoDoExecutor;
            alvo.transform.rotation = variante.Postura == PosturaDeExecucao.EmPe
                ? rotacaoDoExecutor
                : Quaternion.LookRotation(-direcaoFinal, Vector3.up);

            RuntimeAnimatorController originalHeroi = animatorHeroi == null ? null : animatorHeroi.runtimeAnimatorController;
            Animator animatorAlvo = alvo.GetComponent<Animator>();
            CharacterController controladorAlvo = alvo.GetComponent<CharacterController>();
            if (controladorAlvo != null) controladorAlvo.enabled = false;

            if (variante.ExigeDerrubada &&
                controladorDaDerrubadaDoExecutor != null && controladorDaDerrubadaDaVitima != null)
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
            Encerrar(alvo, diretorEstavaAtivo, true);
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

        void Encerrar(AlvoDeCombate alvo, bool reativarDiretor, bool execucaoConcluida = false)
        {
            movimento?.Travar(this, false);
            movimento?.CancelarDeslocamentoDeCombate();
            alvo?.LiberarReserva(this);

            VidaDoHeroi vida = movimento == null ? null : movimento.GetComponent<VidaDoHeroi>();
            if (execucaoConcluida && vida != null)
                vida.ConcederInvulnerabilidade(invulnerabilidadeAposExecucao);

            bool heroiPodeContinuar = vida == null || vida.Vivo;
            bool encontroContinua = movimento != null && movimento.EmModoCombate &&
                registro != null && registro.Quantidade > 0;
            if (diretor != null)
                diretor.DefinirAtivo(heroiPodeContinuar && (reativarDiretor || encontroContinua));
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
            if (sacoDeVariantes.Count == 0) ReabastecerSacoDeVariantes();
            if (sacoDeVariantes.Count == 0) return null;

            int ultimo = sacoDeVariantes.Count - 1;
            indiceDaUltimaVariante = sacoDeVariantes[ultimo];
            sacoDeVariantes.RemoveAt(ultimo);
            return variantes[indiceDaUltimaVariante];
        }

        void ReabastecerSacoDeVariantes()
        {
            sacoDeVariantes.Clear();
            for (int i = 0; i < variantes.Length; i++)
                if (variantes[i] != null && variantes[i].Valida) sacoDeVariantes.Add(i);

            for (int i = sacoDeVariantes.Count - 1; i > 0; i--)
            {
                int outro = UnityEngine.Random.Range(0, i + 1);
                (sacoDeVariantes[i], sacoDeVariantes[outro]) =
                    (sacoDeVariantes[outro], sacoDeVariantes[i]);
            }

            // O proximo item e' retirado do fim. Impede repeticao tambem na emenda de dois ciclos.
            int proximo = sacoDeVariantes.Count - 1;
            if (proximo > 0 && sacoDeVariantes[proximo] == indiceDaUltimaVariante)
                (sacoDeVariantes[proximo], sacoDeVariantes[0]) =
                    (sacoDeVariantes[0], sacoDeVariantes[proximo]);
        }

        void OnDisable()
        {
            movimento?.Travar(this, false);
            interacaoPendenteAte = -1f;
            rotina = null;
        }
    }
}
