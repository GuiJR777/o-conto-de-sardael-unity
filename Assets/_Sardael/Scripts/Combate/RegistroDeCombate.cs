using System.Collections.Generic;
using UnityEngine;

namespace Sardael
{
    public sealed class RegistroDeCombate : MonoBehaviour
    {
        readonly List<AlvoDeCombate> alvos = new List<AlvoDeCombate>();
        readonly List<AlvoDeCombate> consulta = new List<AlvoDeCombate>();

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

        public AlvoDeCombate PrimeiroNoLado(Vector3 origem, int lado)
        {
            PreencherLado(origem, lado, consulta);
            return consulta.Count == 0 ? null : consulta[0];
        }

        public int PreencherLado(Vector3 origem, int lado, List<AlvoDeCombate> resultado)
        {
            resultado.Clear();
            lado = lado < 0 ? -1 : 1;
            LimparInvalidos();

            for (int i = 0; i < alvos.Count; i++)
            {
                var alvo = alvos[i];
                float delta = alvo.transform.position.x - origem.x;
                if (delta * lado > 0.001f) resultado.Add(alvo);
            }

            resultado.Sort((a, b) =>
                Mathf.Abs(a.transform.position.x - origem.x)
                    .CompareTo(Mathf.Abs(b.transform.position.x - origem.x)));
            return resultado.Count;
        }

        void LimparInvalidos()
        {
            for (int i = alvos.Count - 1; i >= 0; i--)
                if (alvos[i] == null || !alvos[i].Valido) alvos.RemoveAt(i);
        }
    }
}
