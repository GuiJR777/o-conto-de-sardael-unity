using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MotorDeCombateDoInimigo : MonoBehaviour
    {
        const float Gravidade = -24f;

        [SerializeField] ReacaoDeCombate reacao;
        [SerializeField, Min(0.5f)] float distanciaDeAtaque = 1.75f;
        [SerializeField, Min(0.1f)] float velocidadeDePressao = 2.4f;
        [SerializeField, Min(0.1f)] float velocidadeDeStrafe = 1.8f;
        [SerializeField, Min(0.2f)] float intervaloEntreAtaques = 2.8f;
        [SerializeField, Min(0.1f)] float duracaoDoAviso = 0.85f;
        [SerializeField, Min(0.1f)] float alcanceDoGolpe = 1.9f;
        [SerializeField, Min(0f)] float danoDoGolpe = 18f;
        [SerializeField, Min(90f)] float giroPorSegundo = 540f;
        [Header("Movimento tatico")]
        [SerializeField, Min(1f)] float distanciaSeguraSemTurno = 2.8f;
        [SerializeField, Min(0.5f)] float distanciaMinimaEntreInimigos = 1.45f;
        [SerializeField, Range(0f, 3f)] float pesoDaSeparacao = 1.6f;
        [SerializeField, Range(0f, 1f)] float chanceDePausaTatica = 0.45f;
        [SerializeField] Vector2 intervaloEntrePausas = new Vector2(1.4f, 3.2f);
        [SerializeField] Vector2 duracaoDaPausa = new Vector2(0.65f, 1.5f);
        [SerializeField, Min(0.1f)] float toleranciaDoSlotParaPausar = 1.1f;

        CharacterController controlador;
        AlvoDeCombate alvo;
        Animator animator;
        Transform jogador;
        VidaDoHeroi vidaDoJogador;
        AvisoDeAtaqueInimigo aviso;
        Collider colisorIgnorado;
        RuntimeAnimatorController controladorComParametroDeFila;
        Vector3 direcaoDoDeslocamento;
        Vector3 posicaoDesejadaNoAnel;
        float distanciaRestante;
        float velocidade;
        float velocidadeVertical;
        float fimDoAviso;
        float fimDoAtaque;
        float fimDoStun;
        float proximoAtaque;
        float semTurnoAte;
        float pausaTaticaAte;
        float proximaDecisaoTatica;
        bool possuiPosicaoNoAnel;
        bool deslocando;
        bool colisaoEmCadeiaResolvida;
        bool possuiTokenDeAtaque;
        bool avisandoAtaque;
        bool ataqueEmAndamento;
        bool atordoado;
        bool possuiParametroDeFila;

        static readonly List<MotorDeCombateDoInimigo> MotoresAtivos =
            new List<MotorDeCombateDoInimigo>();

        static readonly int Atacar = Animator.StringToHash("atacar");
        static readonly int MovimentoFila = Animator.StringToHash("movimentoFila");

        public bool Deslocando => deslocando;
        public float DistanciaRestante => distanciaRestante;
        public int ColisoesEmCadeiaGeradas { get; private set; }
        public bool PossuiTokenDeAtaque => possuiTokenDeAtaque;
        public bool AvisandoAtaque => avisandoAtaque;
        public bool AtaqueEmAndamento => ataqueEmAndamento;
        public bool Atordoado => atordoado;
        public float TempoAteImpacto => ataqueEmAndamento
            ? 0f
            : (avisandoAtaque ? Mathf.Max(0f, fimDoAviso - Time.time) : float.PositiveInfinity);
        public bool PodeReceberToken => Time.time >= semTurnoAte && !atordoado && alvo != null &&
            alvo.Valido && !alvo.Reservado;
        public bool EmMovimentoTatico { get; private set; }
        public bool EmPausaTatica => !possuiTokenDeAtaque && Time.time < pausaTaticaAte;
        public int SlotDoAnel { get; private set; }
        public Vector3 PosicaoDesejadaNoAnel => posicaoDesejadaNoAnel;
        public int AtaquesAvisados { get; private set; }
        public int AtaquesIniciados { get; private set; }
        public int ImpactosExecutados { get; private set; }
        public int AcertosNoHeroi { get; private set; }
        public int GolpesBloqueados { get; private set; }
        public int TurnosPerdidos { get; private set; }
        public AlvoDeCombate Alvo => alvo;

        public void Configurar(ReacaoDeCombate novaReacao)
        {
            reacao = novaReacao;
            PrepararReferencias();
        }

        void Awake() => PrepararReferencias();

        void OnEnable()
        {
            if (!MotoresAtivos.Contains(this)) MotoresAtivos.Add(this);
            AgendarProximaDecisaoTatica(0.35f);
        }

        void PrepararReferencias()
        {
            if (controlador == null) controlador = GetComponent<CharacterController>();
            if (alvo == null) alvo = GetComponent<AlvoDeCombate>();
            if (reacao == null) reacao = GetComponent<ReacaoDeCombate>();
            if (animator == null) animator = GetComponent<Animator>();
            if (aviso == null)
            {
                aviso = GetComponent<AvisoDeAtaqueInimigo>();
                if (aviso == null) aviso = gameObject.AddComponent<AvisoDeAtaqueInimigo>();
            }
        }

        public void DefinirTokenDeAtaque(bool possui, Transform novoJogador)
        {
            jogador = novoJogador;
            if (jogador != null) vidaDoJogador = jogador.GetComponent<VidaDoHeroi>();
            if (possui && !PodeReceberToken) possui = false;
            if (possui && !possuiTokenDeAtaque)
            {
                proximoAtaque = Mathf.Max(proximoAtaque, Time.time + 0.35f);
                pausaTaticaAte = 0f;
            }
            if (!possui) InterromperAtaque();
            possuiTokenDeAtaque = possui;
        }

        public void DefinirPosicaoNoAnel(Vector3 posicao, int ordem, Transform novoJogador)
        {
            posicao.y = transform.position.y;
            posicaoDesejadaNoAnel = posicao;
            SlotDoAnel = ordem;
            possuiPosicaoNoAnel = true;
            jogador = novoJogador;
            if (jogador != null) vidaDoJogador = jogador.GetComponent<VidaDoHeroi>();
        }

        public void AplicarDeslocamento(DefinicaoDeAtaque ataque, Transform atacante)
        {
            if (ataque == null || atacante == null || ataque.distanciaDeslocamento <= 0f ||
                ataque.deslocamento == TipoDeDeslocamento.Nenhum ||
                ataque.deslocamento == TipoDeDeslocamento.PushPlayer)
                return;

            PrepararReferencias();
            InterromperAtaque();
            RestaurarColisaoIgnorada();

            Vector3 paraFora = transform.position - atacante.position;
            paraFora.y = 0f;
            if (paraFora.sqrMagnitude <= 0.0001f) paraFora = transform.forward;
            paraFora.Normalize();
            direcaoDoDeslocamento = paraFora;
            distanciaRestante = ataque.distanciaDeslocamento;
            velocidade = Mathf.Max(0.1f, ataque.velocidadeDeslocamento);
            velocidadeVertical = 0f;
            colisaoEmCadeiaResolvida = false;

            switch (ataque.deslocamento)
            {
                case TipoDeDeslocamento.Pull:
                    direcaoDoDeslocamento = -paraFora;
                    break;
                case TipoDeDeslocamento.Launch:
                    velocidadeVertical = Mathf.Sqrt(
                        2f * Mathf.Max(0f, ataque.alturaLancamento) * -Gravidade);
                    break;
                case TipoDeDeslocamento.CrossSide:
                    direcaoDoDeslocamento = -paraFora;
                    distanciaRestante += RegistroDeCombate.DistanciaPlanar(
                        transform.position, atacante.position);
                    IgnorarAtacanteDuranteTravessia(atacante);
                    break;
                case TipoDeDeslocamento.KnockThrough:
                    velocidade *= 1.15f;
                    break;
            }
            deslocando = true;
        }

        void Update()
        {
            if (atordoado)
            {
                AtualizarAnimacaoDeFila(0f);
                AplicarGravidade();
                if (Time.time >= fimDoStun)
                {
                    atordoado = false;
                    alvo?.RecuperarPoise();
                }
                else return;
            }

            if (!deslocando)
            {
                AtualizarCombate();
                return;
            }
            AtualizarDeslocamento();
        }

        void AtualizarDeslocamento()
        {
            AtualizarAnimacaoDeFila(0f);
            if (!ControladorDisponivel())
            {
                EncerrarDeslocamento();
                return;
            }

            float passoPlanar = Mathf.Min(distanciaRestante, velocidade * Time.deltaTime);
            Vector3 antes = transform.position;
            Vector3 passo = direcaoDoDeslocamento * passoPlanar;
            passo.y = velocidadeVertical * Time.deltaTime;
            CollisionFlags flags = controlador.Move(passo);
            float percorrido = RegistroDeCombate.DistanciaPlanar(transform.position, antes);
            distanciaRestante = Mathf.Max(0f, distanciaRestante - percorrido);

            if (velocidadeVertical != 0f || (flags & CollisionFlags.Below) == 0)
                velocidadeVertical += Gravidade * Time.deltaTime;
            else
                velocidadeVertical = -2f;

            bool bloqueadoDeLado = (flags & CollisionFlags.Sides) != 0 &&
                percorrido < passoPlanar * 0.5f;
            bool terminouHorizontal = distanciaRestante <= 0.001f;
            bool terminouVertical = velocidadeVertical <= 0f && (flags & CollisionFlags.Below) != 0;
            if (bloqueadoDeLado || (terminouHorizontal && terminouVertical)) EncerrarDeslocamento();
        }

        void AtualizarCombate()
        {
            if (jogador == null || alvo == null || !alvo.Valido || alvo.Reservado || !ControladorDisponivel())
            {
                AtualizarAnimacaoDeFila(0f);
                InterromperAtaque();
                AplicarGravidade();
                return;
            }

            Vector3 paraJogador = jogador.position - transform.position;
            paraJogador.y = 0f;
            float distanciaDoJogador = paraJogador.magnitude;
            if (distanciaDoJogador > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(paraJogador / distanciaDoJogador, Vector3.up),
                    giroPorSegundo * Time.deltaTime);
            }

            if (avisandoAtaque)
            {
                AtualizarAnimacaoDeFila(0f);
                AplicarGravidade();
                if (Time.time >= fimDoAviso) IniciarGolpe();
                return;
            }

            if (ataqueEmAndamento)
            {
                if (distanciaDoJogador > distanciaDeAtaque * 0.78f)
                    MoverNaDirecao(paraJogador, velocidadeDePressao * 1.35f);
                else
                {
                    AtualizarAnimacaoDeFila(0f);
                    AplicarGravidade();
                }
                if (Time.time >= fimDoAtaque) FinalizarGolpe();
                return;
            }

            if (EstaEmReacao())
            {
                AtualizarAnimacaoDeFila(0f);
                AplicarGravidade();
                return;
            }

            Vector3 destino = posicaoDesejadaNoAnel;
            float velocidadeDesejada = velocidadeDeStrafe;
            bool forcarRetirada = false;
            if (possuiTokenDeAtaque)
            {
                Vector3 radial = distanciaDoJogador <= 0.001f
                    ? -transform.forward
                    : -paraJogador / distanciaDoJogador;
                destino = jogador.position + radial * (distanciaDeAtaque * 0.92f);
                velocidadeDesejada = velocidadeDePressao;
            }
            else if (distanciaDoJogador < distanciaSeguraSemTurno)
            {
                // So' quem tem o token ocupa a zona de ataque. Os demais recuam pela
                // radial mais curta, em vez de cruzarem por cima do protagonista.
                Vector3 radialParaFora = distanciaDoJogador <= 0.001f
                    ? transform.forward
                    : -paraJogador / distanciaDoJogador;
                destino = jogador.position + radialParaFora * distanciaSeguraSemTurno;
                velocidadeDesejada = velocidadeDePressao * 1.15f;
                pausaTaticaAte = 0f;
                forcarRetirada = true;
            }

            if (possuiPosicaoNoAnel)
            {
                Vector3 delta = destino - transform.position;
                delta.y = 0f;
                Vector3 separacao = CalcularSeparacao();
                bool precisaSeparar = separacao.sqrMagnitude > 0.0025f;

                if (!possuiTokenDeAtaque && !forcarRetirada &&
                    DeveFazerPausaTatica(delta, separacao))
                {
                    AtualizarAnimacaoDeFila(0f);
                    AplicarGravidade();
                    return;
                }

                if (delta.sqrMagnitude > 0.01f || precisaSeparar)
                {
                    MoverNaDirecao(delta, velocidadeDesejada, separacao);
                    return;
                }
            }

            AtualizarAnimacaoDeFila(0f);
            AplicarGravidade();
            if (!possuiTokenDeAtaque || vidaDoJogador == null || !vidaDoJogador.Vivo) return;
            if (distanciaDoJogador <= distanciaDeAtaque + 0.25f && Time.time >= proximoAtaque)
                ComecarAviso();
        }

        void MoverNaDirecao(Vector3 direcao, float velocidadeDoMovimento)
        {
            MoverNaDirecao(direcao, velocidadeDoMovimento, CalcularSeparacao());
        }

        void MoverNaDirecao(
            Vector3 direcao,
            float velocidadeDoMovimento,
            Vector3 separacao)
        {
            direcao.y = 0f;
            float distancia = direcao.magnitude;
            Vector3 normalizada = distancia > 0.001f ? direcao / distancia : Vector3.zero;
            // Abaixo de aproximadamente 60% da distancia pessoal, separar tem
            // prioridade total sobre perseguir o slot. Isto desfaz engarrafamentos
            // em vez de deixar dois CharacterControllers se empurrando lado a lado.
            Vector3 direcaoComSeparacao = separacao.sqrMagnitude >= 0.16f
                ? separacao
                : normalizada + separacao * pesoDaSeparacao;
            direcaoComSeparacao.y = 0f;
            if (direcaoComSeparacao.sqrMagnitude <= 0.0001f)
            {
                AtualizarAnimacaoDeFila(0f);
                AplicarGravidade();
                return;
            }

            direcaoComSeparacao.Normalize();
            if (normalizada.sqrMagnitude > 0.001f &&
                Vector3.Dot(direcaoComSeparacao, normalizada) < 0.2f)
                direcaoComSeparacao = Vector3.Slerp(normalizada, direcaoComSeparacao, 0.45f).normalized;

            float alcanceDoPasso = distancia > 0.001f
                ? Mathf.Max(distancia, distanciaMinimaEntreInimigos * 0.25f)
                : distanciaMinimaEntreInimigos * 0.25f;
            float passo = Mathf.Min(alcanceDoPasso, velocidadeDoMovimento * Time.deltaTime);
            float sinalLocal = Vector3.Dot(transform.forward, direcaoComSeparacao);
            if (Mathf.Abs(sinalLocal) < 0.05f) sinalLocal = 0.45f;
            AtualizarAnimacaoDeFila(sinalLocal);
            Vector3 movimento = direcaoComSeparacao * passo;
            movimento.y = velocidadeVertical * Time.deltaTime;
            CollisionFlags flags = controlador.Move(movimento);
            AtualizarVelocidadeVertical(flags);
        }

        Vector3 CalcularSeparacao()
        {
            Vector3 resultado = Vector3.zero;
            float distanciaQuadrada = distanciaMinimaEntreInimigos * distanciaMinimaEntreInimigos;
            for (int i = MotoresAtivos.Count - 1; i >= 0; i--)
            {
                MotorDeCombateDoInimigo outro = MotoresAtivos[i];
                if (outro == null)
                {
                    MotoresAtivos.RemoveAt(i);
                    continue;
                }
                if (outro == this || !outro.isActiveAndEnabled || outro.alvo == null ||
                    !outro.alvo.Valido)
                    continue;

                Vector3 delta = transform.position - outro.transform.position;
                delta.y = 0f;
                float quadrado = delta.sqrMagnitude;
                if (quadrado >= distanciaQuadrada) continue;
                if (quadrado <= 0.0001f)
                {
                    float lado = SlotDoAnel <= outro.SlotDoAnel ? -1f : 1f;
                    delta = transform.right * lado;
                    quadrado = 1f;
                }

                float distancia = Mathf.Sqrt(quadrado);
                resultado += delta / distancia *
                    (1f - Mathf.Clamp01(distancia / distanciaMinimaEntreInimigos));
            }
            return Vector3.ClampMagnitude(resultado, 1f);
        }

        bool DeveFazerPausaTatica(Vector3 distanciaDoSlot, Vector3 separacao)
        {
            float toleranciaQuadrada = toleranciaDoSlotParaPausar * toleranciaDoSlotParaPausar;
            if (distanciaDoSlot.sqrMagnitude > toleranciaQuadrada || separacao.sqrMagnitude > 0.04f)
            {
                pausaTaticaAte = 0f;
                return false;
            }

            if (Time.time < pausaTaticaAte) return true;
            if (Time.time < proximaDecisaoTatica) return false;

            AgendarProximaDecisaoTatica();
            if (Random.value > chanceDePausaTatica) return false;
            pausaTaticaAte = Time.time + SortearIntervalo(duracaoDaPausa, 0.2f);
            proximaDecisaoTatica = pausaTaticaAte + SortearIntervalo(intervaloEntrePausas, 0.2f);
            return true;
        }

        void AgendarProximaDecisaoTatica(float atrasoMinimo = 0f)
        {
            proximaDecisaoTatica = Time.time + Mathf.Max(
                atrasoMinimo, SortearIntervalo(intervaloEntrePausas, 0.2f));
        }

        static float SortearIntervalo(Vector2 intervalo, float minimo)
        {
            float menor = Mathf.Max(minimo, Mathf.Min(intervalo.x, intervalo.y));
            float maior = Mathf.Max(menor, Mathf.Max(intervalo.x, intervalo.y));
            return Random.Range(menor, maior);
        }

        void AplicarGravidade()
        {
            if (!ControladorDisponivel()) return;
            CollisionFlags flags = controlador.Move(new Vector3(0f, velocidadeVertical * Time.deltaTime, 0f));
            AtualizarVelocidadeVertical(flags);
        }

        void AtualizarVelocidadeVertical(CollisionFlags flags)
        {
            if ((flags & CollisionFlags.Below) != 0 && velocidadeVertical <= 0f)
                velocidadeVertical = -2f;
            else
                velocidadeVertical += Gravidade * Time.deltaTime;
        }

        bool ControladorDisponivel()
        {
            return controlador != null && controlador.enabled && controlador.gameObject.activeInHierarchy;
        }

        void ComecarAviso()
        {
            avisandoAtaque = true;
            fimDoAviso = Time.time + duracaoDoAviso;
            AtaquesAvisados++;
            aviso?.Mostrar(duracaoDoAviso);
        }

        void IniciarGolpe()
        {
            avisandoAtaque = false;
            aviso?.Ocultar();
            if (!possuiTokenDeAtaque || atordoado || alvo == null || !alvo.Valido) return;
            ataqueEmAndamento = true;
            fimDoAtaque = Time.time + 1.8f;
            AtaquesIniciados++;
            if (animator != null) animator.SetTrigger(Atacar);
        }

        public bool AoAvisoDeImpacto(string nomeDoAviso)
        {
            if (nomeDoAviso != "impactoDoOrc") return false;
            if (!ataqueEmAndamento) return true;

            ImpactosExecutados++;
            bool acertou = vidaDoJogador != null && vidaDoJogador.Vivo &&
                jogador != null &&
                RegistroDeCombate.DistanciaPlanar(jogador.position, transform.position) <= alcanceDoGolpe &&
                vidaDoJogador.ReceberAtaque(this, danoDoGolpe);
            if (acertou) AcertosNoHeroi++;
            else if (vidaDoJogador != null && vidaDoJogador.Vivo) GolpesBloqueados++;
            FinalizarGolpe();
            return true;
        }

        void FinalizarGolpe()
        {
            ataqueEmAndamento = false;
            possuiTokenDeAtaque = false;
            proximoAtaque = Time.time + intervaloEntreAtaques;
            semTurnoAte = proximoAtaque;
            pausaTaticaAte = 0f;
            AgendarProximaDecisaoTatica(0.5f);
        }

        public void InterromperAtaque()
        {
            avisandoAtaque = false;
            ataqueEmAndamento = false;
            aviso?.Ocultar();
            proximoAtaque = Mathf.Max(proximoAtaque, Time.time + 0.65f);
        }

        public void PerderVezDeAtaque(float impedimento = 1.25f)
        {
            bool tinhaVez = possuiTokenDeAtaque || avisandoAtaque || ataqueEmAndamento;
            InterromperAtaque();
            if (!tinhaVez) return;
            possuiTokenDeAtaque = false;
            semTurnoAte = Mathf.Max(semTurnoAte, Time.time + Mathf.Max(0.1f, impedimento));
            pausaTaticaAte = 0f;
            AgendarProximaDecisaoTatica(0.5f);
            TurnosPerdidos++;
        }

        public void Atordoar(float duracao)
        {
            if (alvo == null || !alvo.Valido) return;
            InterromperAtaque();
            EncerrarDeslocamento();
            pausaTaticaAte = 0f;
            atordoado = true;
            fimDoStun = Time.time + Mathf.Max(0.1f, duracao);
        }

        void AtualizarAnimacaoDeFila(float movimento)
        {
            EmMovimentoTatico = Mathf.Abs(movimento) > 0.05f;
            if (animator == null) return;

            var controladorAtual = animator.runtimeAnimatorController;
            if (controladorAtual != controladorComParametroDeFila)
            {
                controladorComParametroDeFila = controladorAtual;
                possuiParametroDeFila = false;
                foreach (var parametro in animator.parameters)
                {
                    if (parametro.nameHash != MovimentoFila) continue;
                    possuiParametroDeFila = true;
                    break;
                }
            }

            if (possuiParametroDeFila) animator.SetFloat(MovimentoFila, movimento);
        }

        bool EstaEmReacao()
        {
            if (animator == null) return false;
            var estado = animator.GetCurrentAnimatorStateInfo(0);
            return estado.IsName("Levar") || estado.IsName("LevarForte") ||
                   estado.IsName("Atordoado") || estado.IsName("Morte");
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!deslocando || colisaoEmCadeiaResolvida || hit == null || hit.collider == null) return;
            var outro = hit.collider.GetComponentInParent<AlvoDeCombate>();
            if (outro == null || outro == alvo || !outro.Valido) return;

            Vector3 contato = outro.transform.position - transform.position;
            contato.y = 0f;
            if (contato.sqrMagnitude <= 0.0001f ||
                Vector3.Dot(contato.normalized, direcaoDoDeslocamento) < 0.35f) return;

            colisaoEmCadeiaResolvida = true;
            ColisoesEmCadeiaGeradas++;
            alvo?.ReceberColisaoEmCadeia(true);
            outro.ReceberColisaoEmCadeia(false);
            EncerrarDeslocamento();
        }

        void IgnorarAtacanteDuranteTravessia(Transform atacante)
        {
            var outro = atacante.GetComponent<CharacterController>();
            if (controlador == null || outro == null) return;
            Physics.IgnoreCollision(controlador, outro, true);
            colisorIgnorado = outro;
        }

        void EncerrarDeslocamento()
        {
            deslocando = false;
            distanciaRestante = 0f;
            RestaurarColisaoIgnorada();
        }

        public void Interromper()
        {
            possuiTokenDeAtaque = false;
            possuiPosicaoNoAnel = false;
            atordoado = false;
            pausaTaticaAte = 0f;
            InterromperAtaque();
            EncerrarDeslocamento();
        }

        void RestaurarColisaoIgnorada()
        {
            if (controlador != null && colisorIgnorado != null)
                Physics.IgnoreCollision(controlador, colisorIgnorado, false);
            colisorIgnorado = null;
        }

        void OnDisable()
        {
            MotoresAtivos.Remove(this);
            possuiTokenDeAtaque = false;
            possuiPosicaoNoAnel = false;
            atordoado = false;
            pausaTaticaAte = 0f;
            InterromperAtaque();
            EncerrarDeslocamento();
        }
    }
}
