using System.Collections;
using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// A coreografia: um bate, o outro defende, e trocam. Ninguem cai.
    ///
    /// O defensor nao levanta o escudo junto com o golpe — levanta um pouco DEPOIS, no
    /// <see cref="atrasoDaDefesa"/>, pra que o bloqueio caia no quadro do impacto. Erguer
    /// junto faz parecer que ele adivinhou, e o golpe perde o peso.
    ///
    /// Fica no objeto PAI do par. Duplicar o boneco nao traz esta coreografia junto: o que se
    /// duplica e' o grupo inteiro.
    ///
    /// O <see cref="Combatente"/> mora em arquivo proprio. As duas classes juntas num .cs so'
    /// compartilhavam o GUID de script e o Unity embaralhava os layouts de serializacao — os
    /// componentes viravam "missing" na recompilacao e os duelistas apareciam em T no Play.
    /// </summary>
    [DisallowMultipleComponent]
    public class DueloEncenado : MonoBehaviour
    {
        public Combatente aliado;
        public Combatente inimigo;

        [Header("Ritmo")]
        [Tooltip("Segundos entre o inicio do golpe e o inicio da defesa.")]
        public float atrasoDaDefesa = 0.28f;
        [Tooltip("Respiro entre uma troca e a proxima.")]
        public float pausa = 0.55f;
        [Tooltip("Variacao sorteada na pausa, pra dois duelos vizinhos nao baterem no mesmo tempo.")]
        public float variacao = 0.35f;
        [Tooltip("Segundos de espera antes do primeiro golpe. Espalhar isto entre os pares "
               + "evita a salva de tiros inicial.")]
        public float atrasoInicial = 0f;

        [Header("Encarar")]
        [Tooltip("Vira um pro outro ao comecar. Deixe ligado a menos que voce ja tenha posto de frente.")]
        public bool encararNoInicio = true;

        void Start()
        {
            if (aliado == null || inimigo == null)
            {
                Debug.LogWarning("Duelo '" + name + "' sem par: "
                    + (aliado == null ? "aliado vazio. " : "")
                    + (inimigo == null ? "inimigo vazio. " : "")
                    + "Os dois vao ficar parados.", this);
                enabled = false;
                return;
            }
            if (encararNoInicio) Encarar();
            StartCoroutine(Coreografia());
        }

        void Encarar()
        {
            var a = aliado.transform; var b = inimigo.transform;
            var d = b.position - a.position; d.y = 0f;
            if (d.sqrMagnitude < 0.0004f) return;
            a.rotation = Quaternion.LookRotation(d);
            b.rotation = Quaternion.LookRotation(-d);
        }

        IEnumerator Coreografia()
        {
            if (atrasoInicial > 0f) yield return new WaitForSeconds(atrasoInicial);
            bool vezDoAliado = true;

            while (true)
            {
                var atacante = vezDoAliado ? aliado : inimigo;
                var defensor = vezDoAliado ? inimigo : aliado;

                var golpe = atacante.SortearGolpe();
                if (golpe == null)
                {
                    Debug.LogWarning("Duelo '" + name + "': " + atacante.name + " nao tem golpe. A luta parou.", this);
                    yield break;
                }
                atacante.Executar(golpe);

                float espera = Mathf.Min(atrasoDaDefesa, golpe.length * 0.6f);
                yield return new WaitForSeconds(espera);
                defensor.Executar(defensor.defesa);

                float resto = Mathf.Max(golpe.length - espera,
                                        defensor.defesa != null ? defensor.defesa.length : 0f);
                yield return new WaitForSeconds(resto + pausa + Random.Range(0f, variacao));

                vezDoAliado = !vezDoAliado;
            }
        }
    }
}
