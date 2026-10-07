using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraDeCombate : MonoBehaviour
    {
        enum EstadoCamera { Inativa, Entrando, Acompanhando, Saindo }

        [SerializeField] Transform jogador;
        [SerializeField] RegistroDeCombate registro;
        [SerializeField] CombateDoHeroi combate;
        [SerializeField] CameraLateral cameraLateral;
        [SerializeField] Vector3 afastamento = new Vector3(-3.8f, 6.2f, -8.4f);
        [SerializeField, Min(0.1f)] float duracaoDoBlend = 0.65f;
        [SerializeField, Min(0.1f)] float resposta = 5.5f;
        [SerializeField, Range(35f, 80f)] float campoDeVisao = 56f;
        [SerializeField, Min(1f)] float raioDeEnquadramento = 8f;

        readonly List<AlvoDeCombate> alvos = new List<AlvoDeCombate>();
        Camera cameraDoJogo;
        EstadoCamera estado;
        Vector3 inicioDaPosicao;
        Quaternion inicioDaRotacao;
        float inicioDoBlend;
        float fovLateral;

        public bool Ativa => estado != EstadoCamera.Inativa;
        public bool SaidaConcluida => estado == EstadoCamera.Inativa;

        public void Configurar(
            Transform novoJogador,
            RegistroDeCombate novoRegistro,
            CombateDoHeroi novoCombate,
            CameraLateral novaCameraLateral)
        {
            jogador = novoJogador;
            registro = novoRegistro;
            combate = novoCombate;
            cameraLateral = novaCameraLateral;
            cameraDoJogo = GetComponent<Camera>();
        }

        void Awake()
        {
            cameraDoJogo = GetComponent<Camera>();
        }

        public void Ativar()
        {
            if (jogador == null) return;
            cameraDoJogo ??= GetComponent<Camera>();
            fovLateral = cameraDoJogo == null ? campoDeVisao : cameraDoJogo.fieldOfView;
            inicioDaPosicao = transform.position;
            inicioDaRotacao = transform.rotation;
            inicioDoBlend = Time.unscaledTime;
            estado = EstadoCamera.Entrando;
            if (cameraLateral != null) cameraLateral.enabled = false;
            enabled = true;
        }

        public void IniciarSaida()
        {
            if (estado == EstadoCamera.Inativa) return;
            inicioDaPosicao = transform.position;
            inicioDaRotacao = transform.rotation;
            inicioDoBlend = Time.unscaledTime;
            estado = EstadoCamera.Saindo;
        }

        public void ForcarLateral()
        {
            estado = EstadoCamera.Inativa;
            if (cameraLateral != null)
            {
                cameraLateral.enabled = true;
                cameraLateral.SincronizarAgora();
            }
            enabled = false;
        }

        void LateUpdate()
        {
            if (estado == EstadoCamera.Inativa || jogador == null) return;

            Vector3 destino;
            Quaternion rotacao;
            float fovDestino;
            if (estado == EstadoCamera.Saindo && cameraLateral != null)
            {
                cameraLateral.ObterPoseDesejada(out destino, out rotacao);
                fovDestino = fovLateral;
            }
            else
            {
                CalcularPoseDeCombate(out destino, out rotacao);
                fovDestino = campoDeVisao;
            }

            if (estado == EstadoCamera.Entrando || estado == EstadoCamera.Saindo)
            {
                float t = duracaoDoBlend <= 0f
                    ? 1f
                    : Mathf.Clamp01((Time.unscaledTime - inicioDoBlend) / duracaoDoBlend);
                t = t * t * (3f - 2f * t);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(inicioDaPosicao, destino, t),
                    Quaternion.Slerp(inicioDaRotacao, rotacao, t));
                if (cameraDoJogo != null)
                    cameraDoJogo.fieldOfView = Mathf.Lerp(
                        estado == EstadoCamera.Saindo ? campoDeVisao : fovLateral, fovDestino, t);

                if (t < 1f) return;
                if (estado == EstadoCamera.Entrando) estado = EstadoCamera.Acompanhando;
                else
                {
                    estado = EstadoCamera.Inativa;
                    if (cameraLateral != null)
                    {
                        cameraLateral.enabled = true;
                        cameraLateral.SincronizarAgora();
                    }
                    enabled = false;
                }
                return;
            }

            float peso = 1f - Mathf.Exp(-resposta * Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, destino, peso),
                Quaternion.Slerp(transform.rotation, rotacao, peso));
            if (cameraDoJogo != null)
                cameraDoJogo.fieldOfView = Mathf.Lerp(cameraDoJogo.fieldOfView, fovDestino, peso);
        }

        void CalcularPoseDeCombate(out Vector3 posicao, out Quaternion rotacao)
        {
            Vector3 foco = jogador.position;
            AlvoDeCombate alvoAtual = combate == null ? null : combate.AlvoAtual;
            if (alvoAtual != null && alvoAtual.Valido)
                foco = Vector3.Lerp(foco, alvoAtual.transform.position, 0.42f);
            else if (registro != null)
            {
                registro.PreencherTodos(jogador.position, alvos);
                Vector3 soma = Vector3.zero;
                int quantidade = 0;
                for (int i = 0; i < alvos.Count; i++)
                {
                    Vector3 delta = alvos[i].transform.position - jogador.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > raioDeEnquadramento * raioDeEnquadramento) continue;
                    soma += alvos[i].transform.position;
                    quantidade++;
                }
                if (quantidade > 0)
                {
                    Vector3 centro = soma / quantidade;
                    Vector3 deslocamento = Vector3.ClampMagnitude(centro - jogador.position, 3f);
                    foco += deslocamento * 0.3f;
                }
            }

            foco.y = jogador.position.y + 1.1f;
            posicao = foco + afastamento;
            rotacao = Quaternion.LookRotation(foco - posicao, Vector3.up);
        }
    }
}
