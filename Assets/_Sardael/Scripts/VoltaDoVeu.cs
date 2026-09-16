using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// O que acontece quando a cena do Veu acaba: Sardael volta pra Halu.
    ///
    /// Fica num objeto da cena <c>Morte_Do_Veu</c> e e' chamado pelo <c>aoAcabar</c> da
    /// <see cref="CenaDaMorte"/>. Existe separado de proposito: a cutscene nao precisa saber
    /// nada da historia — ela sabe mostrar um veado, uma luz e um branco. Quem decide o que
    /// vem depois e' este componente, e trocar o destino nao encosta na cutscene.
    ///
    /// O progresso ja' foi marcado como <see cref="Progresso.Etapa.MorreuEmBael"/> la' na
    /// floresta, antes de esta cena carregar. E' isso que faz a Halu abrir com a conversa do
    /// sonho em vez de repetir a segunda missao.
    /// </summary>
    public class VoltaDoVeu : MonoBehaviour
    {
        [Tooltip("Pra onde ele acorda. A cena PRECISA estar no Build Settings.")]
        public string cenaDeDestino = "Halu";
        [TextArea] public string recadoDaTela = "...";
        [Tooltip("Respiro depois do branco, antes de a tela de carregamento entrar.")]
        public float esperaAntes = 0.4f;

        bool jaFoi;

        /// <summary>Ligue no <c>aoAcabar</c> da CenaDaMorte.</summary>
        public void Voltar()
        {
            if (jaFoi) return;
            jaFoi = true;

            // se ele chegou aqui sem ter morrido em Bael (teste da cena solta), nao inventa
            // progresso: so' leva pra Halu, que decide sozinha o que mostrar
            if (Progresso.Onde == Progresso.Etapa.Comeco)
                Debug.Log("VoltaDoVeu: sem morte registrada — Halu vai abrir pelo progresso atual.");

            Invoke("Ir", Mathf.Max(0f, esperaAntes));
        }

        void Ir() { TelaDeCarregamento.Ir(cenaDeDestino, recadoDaTela); }
    }
}
