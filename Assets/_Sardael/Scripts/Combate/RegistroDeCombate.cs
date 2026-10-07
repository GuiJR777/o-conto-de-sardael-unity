using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    public sealed class RegistroDeCombate : MonoBehaviour
    {
        readonly List<AlvoDeCombate> alvos = new List<AlvoDeCombate>();

        public int Quantidade
        {
            get
            {
                LimparInvalidos();
                return alvos.Count;
            }
        }

        public void Registrar(AlvoDeCombate alvo)
        {
            if (alvo != null && !alvos.Contains(alvo)) alvos.Add(alvo);
        }

        public void Desregistrar(AlvoDeCombate alvo) => alvos.Remove(alvo);

        public AlvoDeCombate MaisProximo(Vector3 origem, float raioMaximo = float.PositiveInfinity)
        {
            LimparInvalidos();
            AlvoDeCombate melhor = null;
            float melhorDistancia = raioMaximo * raioMaximo;
            for (int i = 0; i < alvos.Count; i++)
            {
                var candidato = alvos[i];
                if (candidato.Reservado) continue;
                float distancia = DistanciaPlanarQuadrada(origem, candidato.transform.position);
                if (distancia >= melhorDistancia) continue;
                melhor = candidato;
                melhorDistancia = distancia;
            }
            return melhor;
        }

        public AlvoDeCombate MelhorAlvoNaDirecao(
            Vector3 origem,
            Vector3 direcaoDeEntrada,
            Vector3 direcaoAtual,
            AlvoDeCombate alvoAnterior = null,
            float raioMaximo = float.PositiveInfinity)
        {
            LimparInvalidos();
            direcaoDeEntrada.y = 0f;
            direcaoAtual.y = 0f;
            bool temEntrada = direcaoDeEntrada.sqrMagnitude >= 0.04f;
            Vector3 referencia = temEntrada
                ? direcaoDeEntrada.normalized
                : (direcaoAtual.sqrMagnitude > 0.0001f ? direcaoAtual.normalized : Vector3.right);

            AlvoDeCombate melhor = null;
            float melhorPontuacao = float.NegativeInfinity;
            float raioQuadrado = raioMaximo * raioMaximo;
            for (int i = 0; i < alvos.Count; i++)
            {
                var candidato = alvos[i];
                if (candidato.Reservado) continue;
                Vector3 delta = candidato.transform.position - origem;
                delta.y = 0f;
                float distanciaQuadrada = delta.sqrMagnitude;
                if (distanciaQuadrada > raioQuadrado || distanciaQuadrada < 0.0001f) continue;
                float distancia = Mathf.Sqrt(distanciaQuadrada);
                float alinhamento = Vector3.Dot(referencia, delta / distancia);
                if (temEntrada && alinhamento < -0.2f) continue;
                float pontuacao = alinhamento * (temEntrada ? 5f : 1.8f) - distancia * 0.22f;
                if (!temEntrada && candidato == alvoAnterior) pontuacao += 1.35f;
                if (pontuacao <= melhorPontuacao) continue;
                melhor = candidato;
                melhorPontuacao = pontuacao;
            }
            return melhor ?? MaisProximo(origem, raioMaximo);
        }

        public int PreencherDentroDoRaio(
            Vector3 origem,
            float raio,
            List<AlvoDeCombate> resultado,
            bool incluirReservados = false)
        {
            resultado.Clear();
            LimparInvalidos();
            float raioQuadrado = raio * raio;
            for (int i = 0; i < alvos.Count; i++)
            {
                var alvo = alvos[i];
                if (!incluirReservados && alvo.Reservado) continue;
                if (DistanciaPlanarQuadrada(origem, alvo.transform.position) <= raioQuadrado)
                    resultado.Add(alvo);
            }
            resultado.Sort((a, b) =>
                DistanciaPlanarQuadrada(origem, a.transform.position)
                    .CompareTo(DistanciaPlanarQuadrada(origem, b.transform.position)));
            return resultado.Count;
        }

        public int PreencherNoCone(
            Vector3 origem,
            Vector3 direcao,
            float raio,
            float cossenoMinimo,
            List<AlvoDeCombate> resultado)
        {
            PreencherDentroDoRaio(origem, raio, resultado);
            direcao.y = 0f;
            if (direcao.sqrMagnitude <= 0.0001f) return resultado.Count;
            direcao.Normalize();
            for (int i = resultado.Count - 1; i >= 0; i--)
            {
                Vector3 delta = resultado[i].transform.position - origem;
                delta.y = 0f;
                if (delta.sqrMagnitude <= 0.0001f || Vector3.Dot(direcao, delta.normalized) < cossenoMinimo)
                    resultado.RemoveAt(i);
            }
            resultado.Sort((a, b) =>
            {
                Vector3 da = a.transform.position - origem; da.y = 0f;
                Vector3 db = b.transform.position - origem; db.y = 0f;
                return Vector3.Dot(da, direcao).CompareTo(Vector3.Dot(db, direcao));
            });
            return resultado.Count;
        }

        public int PreencherTodos(Vector3 origem, List<AlvoDeCombate> resultado)
        {
            resultado.Clear();
            LimparInvalidos();
            resultado.AddRange(alvos);
            resultado.Sort((a, b) =>
                DistanciaPlanarQuadrada(origem, a.transform.position)
                    .CompareTo(DistanciaPlanarQuadrada(origem, b.transform.position)));
            return resultado.Count;
        }

        public static float DistanciaPlanar(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(DistanciaPlanarQuadrada(a, b));
        }

        public static float DistanciaPlanarQuadrada(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }

        void LimparInvalidos()
        {
            for (int i = alvos.Count - 1; i >= 0; i--)
                if (alvos[i] == null || !alvos[i].Valido) alvos.RemoveAt(i);
        }
    }
}
