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
        [SerializeField, Min(0.2f)] float intervaloEntreAtaques = 2.8f;
        [SerializeField, Min(0.1f)] float duracaoDoAviso = 0.85f;
        [SerializeField, Min(0.1f)] float alcanceDoGolpe = 1.9f;
        [SerializeField, Min(0f)] float danoDoGolpe = 18f;

        CharacterController controlador;
        AlvoDeCombate alvo;
        Animator animator;
        Transform jogador;
        VidaDoHeroi vidaDoJogador;
        AvisoDeAtaqueInimigo aviso;
        Collider colisorIgnorado;
        RuntimeAnimatorController controladorComParametroDeFila;
        float direcao;
        float distanciaRestante;
        float velocidade;
        float velocidadeVertical;
        float posicaoDaFila;
        float fimDoAviso;
        float fimDoAtaque;
        float fimDoStun;
        float proximoAtaque;
        float semTurnoAte;
        bool possuiPosicaoNaFila;
        bool deslocando;
        bool colisaoEmCadeiaResolvida;
        bool possuiTokenDeAtaque;
        bool avisandoAtaque;
        bool ataqueEmAndamento;
        bool atordoado;
        bool possuiParametroDeFila;

        static readonly int Atacar = Animator.StringToHash("atacar");
        static readonly int MovimentoFila = Animator.StringToHash("movimentoFila");

        public bool Deslocando => deslocando;
        public float DistanciaRestante => distanciaRestante;
        public int ColisoesEmCadeiaGeradas { get; private set; }
        public bool PossuiTokenDeAtaque => possuiTokenDeAtaque;
        public bool AvisandoAtaque => avisandoAtaque;
        public bool AtaqueEmAndamento => ataqueEmAndamento;
        public bool Atordoado => atordoado;
        public bool PodeReceberToken => Time.time >= semTurnoAte && !atordoado && alvo != null &&
            alvo.Valido && !alvo.Reservado;
        public bool AndandoNaFila { get; private set; }
        public float PosicaoDaFila => posicaoDaFila;
        public int OrdemNaFila { get; private set; }
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
                proximoAtaque = Mathf.Max(proximoAtaque, Time.time + 0.5f);
            if (!possui) InterromperAtaque();
            possuiTokenDeAtaque = possui;
        }

        public void DefinirPosicaoNaFila(float x, int ordem, Transform novoJogador)
        {
            posicaoDaFila = x;
            OrdemNaFila = ordem;
            possuiPosicaoNaFila = true;
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

            float paraFora = transform.position.x >= atacante.position.x ? 1f : -1f;
            direcao = paraFora;
            distanciaRestante = ataque.distanciaDeslocamento;
            velocidade = Mathf.Max(0.1f, ataque.velocidadeDeslocamento);
            velocidadeVertical = 0f;
            colisaoEmCadeiaResolvida = false;

            switch (ataque.deslocamento)
            {
                case TipoDeDeslocamento.Pull:
                    direcao = -paraFora;
                    break;
                case TipoDeDeslocamento.Launch:
                    velocidadeVertical = Mathf.Sqrt(2f * Mathf.Max(0f, ataque.alturaLancamento) * -Gravidade);
                    break;
                case TipoDeDeslocamento.CrossSide:
                    direcao = -paraFora;
                    distanciaRestante += Mathf.Abs(transform.position.x - atacante.position.x);
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
            AtualizarAnimacaoDeFila(0f);
            if (controlador == null || !controlador.enabled || !controlador.gameObject.activeInHierarchy)
            {
                EncerrarDeslocamento();
                return;
            }

            float passoHorizontal = Mathf.Min(distanciaRestante, velocidade * Time.deltaTime);
            float y = velocidadeVertical * Time.deltaTime;
            Vector3 antes = transform.position;
            CollisionFlags flags = controlador.Move(new Vector3(direcao * passoHorizontal, y, 0f));
            float percorrido = Mathf.Abs(transform.position.x - antes.x);
            distanciaRestante = Mathf.Max(0f, distanciaRestante - percorrido);

            if (velocidadeVertical != 0f || (flags & CollisionFlags.Below) == 0)
                velocidadeVertical += Gravidade * Time.deltaTime;
            else
                velocidadeVertical = -2f;

            bool bloqueadoDeLado = (flags & CollisionFlags.Sides) != 0 && percorrido < passoHorizontal * 0.5f;
            bool terminouHorizontal = distanciaRestante <= 0.001f;
            bool terminouVertical = velocidadeVertical <= 0f && (flags & CollisionFlags.Below) != 0;
            if (bloqueadoDeLado || (terminouHorizontal && terminouVertical)) EncerrarDeslocamento();
        }

        void AtualizarCombate()
        {
            if (jogador == null || alvo == null || !alvo.Valido || alvo.Reservado ||
                controlador == null || !controlador.enabled || !controlador.gameObject.activeInHierarchy)
            {
                AtualizarAnimacaoDeFila(0f);
                InterromperAtaque();
                return;
            }

            float deltaJogador = jogador.position.x - transform.position.x;
            int ladoJogador = deltaJogador < 0f ? -1 : 1;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.Euler(0f, ladoJogador > 0 ? 90f : -90f, 0f),
                480f * Time.deltaTime);

            if (avisandoAtaque)
            {
                AtualizarAnimacaoDeFila(0f);
                if (Time.time >= fimDoAviso) IniciarGolpe();
                return;
            }

            if (ataqueEmAndamento)
            {
                AtualizarAnimacaoDeFila(0f);
                if (Time.time >= fimDoAtaque) FinalizarGolpe();
                return;
            }

            if (EstaEmReacao())
            {
                AtualizarAnimacaoDeFila(0f);
                return;
            }

            if (possuiPosicaoNaFila)
            {
                float deltaFila = posicaoDaFila - transform.position.x;
                if (Mathf.Abs(deltaFila) > 0.06f)
                {
                    float passo = Mathf.Min(Mathf.Abs(deltaFila), velocidadeDePressao * Time.deltaTime);
                    float sentidoLocal = Mathf.Sign(deltaFila) * Mathf.Sign(transform.forward.x);
                    AtualizarAnimacaoDeFila(sentidoLocal);
                    controlador.Move(new Vector3(Mathf.Sign(deltaFila) * passo, 0f, 0f));
                    return;
                }
            }

            AtualizarAnimacaoDeFila(0f);
            if (!possuiTokenDeAtaque || vidaDoJogador == null || !vidaDoJogador.Vivo) return;
            float distancia = Mathf.Abs(deltaJogador);
            if (distancia <= distanciaDeAtaque + 0.25f && Time.time >= proximoAtaque)
                ComecarAviso();
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
                Mathf.Abs(jogador.position.x - transform.position.x) <= alcanceDoGolpe &&
                vidaDoJogador.ReceberAtaque(this, danoDoGolpe);
            if (acertou) AcertosNoHeroi++;
            else if (vidaDoJogador != null && vidaDoJogador.Vivo) GolpesBloqueados++;
            FinalizarGolpe();
            return true;
        }

        void FinalizarGolpe()
        {
            ataqueEmAndamento = false;
            proximoAtaque = Time.time + intervaloEntreAtaques;
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
            TurnosPerdidos++;
        }

        public void Atordoar(float duracao)
        {
            if (alvo == null || !alvo.Valido) return;
            InterromperAtaque();
            EncerrarDeslocamento();
            atordoado = true;
            fimDoStun = Time.time + Mathf.Max(0.1f, duracao);
        }

        void AtualizarAnimacaoDeFila(float movimento)
        {
            AndandoNaFila = Mathf.Abs(movimento) > 0.05f;
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
                   estado.IsName("Atordoado") || estado.IsName("Morte") || estado.IsName("Golpe");
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!deslocando || colisaoEmCadeiaResolvida || hit == null || hit.collider == null) return;
            var outro = hit.collider.GetComponentInParent<AlvoDeCombate>();
            if (outro == null || outro == alvo || !outro.Valido) return;

            float sentidoDoContato = Mathf.Sign(outro.transform.position.x - transform.position.x);
            if (!Mathf.Approximately(sentidoDoContato, direcao)) return;

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
            possuiPosicaoNaFila = false;
            atordoado = false;
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
            possuiTokenDeAtaque = false;
            possuiPosicaoNaFila = false;
            atordoado = false;
            InterromperAtaque();
            EncerrarDeslocamento();
        }
    }
}
