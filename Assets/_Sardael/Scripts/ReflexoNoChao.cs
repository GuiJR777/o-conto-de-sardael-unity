using UnityEngine;
using System.Collections.Generic;

namespace Sardael
{
    /// <summary>
    /// Reflexo de verdade num chao espelhado, sem shader nenhum.
    ///
    /// URP nao traz reflexo planar pronto, e Reflection Probe num cenario todo preto nao tem o
    /// que refletir — o cubemap volta preto. Entao o reflexo aqui e o truque antigo do teatro:
    /// uma COPIA do ator, virada de cabeca pra baixo embaixo do chao, e um piso translucido por
    /// cima. O que controla "pouca opacidade" e o alfa do piso, nao um parametro de shader.
    ///
    /// A copia nao tem Animator proprio. Ela copia osso por osso do original a cada LateUpdate
    /// e so' a RAIZ leva a escala -1 em Y. Assim o reflexo nunca dessincroniza da animacao —
    /// dois Animators tocando o mesmo clipe derivam em poucos segundos.
    ///
    /// Escala negativa inverte o sentido dos triangulos: os materiais da copia precisam de
    /// Cull Off, senao o Unity descarta justamente as faces que aparecem.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class ReflexoNoChao : MonoBehaviour
    {
        [Tooltip("O objeto de verdade, o que tem Animator.")]
        public Transform original;
        [Tooltip("Altura do plano do espelho em Y.")]
        public float alturaDoChao = 0f;

        Transform[] deLa, daqui;

        void Awake() { Casar(); }

        /// <summary>
        /// Casa as duas hierarquias POR CAMINHO, nao por indice.
        ///
        /// Por indice era fragil demais: bastou eu pendurar um objeto vazio de mira no corpo
        /// pra copia ficar com um transform a menos, o reflexo se desligar e o Sardael refletido
        /// aparecer de pe, em pose de bind, no meio da cena. Casando por caminho relativo, o
        /// que existe nos dois lados e' espelhado e o que so' existe num lado e' ignorado.
        /// </summary>
        void Casar()
        {
            if (original == null) return;

            var origem = original.GetComponentsInChildren<Transform>(true);
            var copia = GetComponentsInChildren<Transform>(true);

            var porCaminho = new Dictionary<string, Transform>();
            foreach (var t in copia)
            {
                var c = Caminho(t, transform);
                if (!porCaminho.ContainsKey(c)) porCaminho[c] = t;
            }

            var a = new List<Transform>();
            var b = new List<Transform>();
            foreach (var t in origem)
            {
                if (t == original) continue;              // a raiz e' espelhada, nao copiada
                var c = Caminho(t, original);
                if (!porCaminho.ContainsKey(c)) continue;
                a.Add(t);
                b.Add(porCaminho[c]);
            }

            deLa = a.ToArray();
            daqui = b.ToArray();

            if (deLa.Length == 0)
                Debug.LogWarning("ReflexoNoChao: nenhum osso casou entre " + original.name
                    + " e " + name + ", reflexo vai ficar em pose de bind");
            else if (deLa.Length < origem.Length - 1)
                Debug.Log("ReflexoNoChao: " + deLa.Length + " de " + (origem.Length - 1)
                    + " ossos casados em " + name + " (o resto so' existe no original)");
        }

        static string Caminho(Transform t, Transform raiz)
        {
            var s = t.name;
            var p = t.parent;
            while (p != null && p != raiz) { s = p.name + "/" + s; p = p.parent; }
            return s;
        }

        void LateUpdate()
        {
            if (original == null || deLa == null || daqui == null) return;

            // ossos: copia crua, sem espelhar. O espelho e' so' na raiz.
            for (int i = 0; i < daqui.Length; i++)
            {
                daqui[i].localPosition = deLa[i].localPosition;
                daqui[i].localRotation = deLa[i].localRotation;
                daqui[i].localScale = deLa[i].localScale;
            }

            var p = original.position;
            transform.position = new Vector3(p.x, 2f * alturaDoChao - p.y, p.z);

            var e = original.eulerAngles;
            transform.rotation = Quaternion.Euler(-e.x, e.y, -e.z);

            var s = original.localScale;
            transform.localScale = new Vector3(s.x, -s.y, s.z);
        }
    }
}
