using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>Os pedacos de tela de um cartao de partida.</summary>
    [System.Serializable]
    public class CartaoDeSlot
    {
        public Button botao;
        public TextMeshProUGUI titulo;     // I, II, III
        public TextMeshProUGUI estado;     // "Pergaminho em branco" ou onde parou
        public TextMeshProUGUI detalhe;    // tempo de jogo e data
        public Button botaoApagar;
        public GameObject marcaDeVazio;    // enfeite que so' aparece no cartao em branco
    }

    /// <summary>
    /// O menu principal: a porta de entrada do jogo.
    ///
    /// Tres telas no mesmo Canvas, e nao tres cenas: trocar cena pra mostrar tres botoes custa
    /// um carregamento inteiro e faz a arte de fundo piscar. Aqui so' se liga e desliga painel.
    ///
    /// O menu NAO sabe nada da historia. Ele escolhe o pergaminho e manda carregar a cena que
    /// a ficha aponta; quem conta a historia e' o diretor de cada cena, lendo o
    /// <see cref="Progresso"/>. Assim uma fase nova entra no jogo sem tocar no menu.
    ///
    /// Apagar pede confirmacao de proposito: e' a unica acao daqui que destroi alguma coisa, e
    /// um toque errado num cartao apagaria horas de jogo sem volta.
    /// </summary>
    public class MenuPrincipal : MonoBehaviour
    {
        [Header("Paineis")]
        public GameObject painelInicio;
        public GameObject painelSlots;
        public GameObject painelConfirmar;

        [Header("Botoes da entrada")]
        public Button botaoJogar;
        public Button botaoSair;

        [Header("Os tres pergaminhos")]
        public CartaoDeSlot[] cartoes = new CartaoDeSlot[0];
        public Button botaoVoltar;

        [Header("Confirmacao de apagar")]
        public TextMeshProUGUI perguntaDeApagar;
        public Button botaoSim;
        public Button botaoNao;

        static readonly string[] ROMANOS = { "I", "II", "III", "IV", "V" };

        int aApagar = -1;

        void Start()
        {
            // quem volta pro menu no meio de uma partida nao continua preso a ela
            if (Salvao.TemPartida) Salvao.Largar();

            if (botaoJogar != null) botaoJogar.onClick.AddListener(AbrirOsPergaminhos);
            if (botaoSair != null) botaoSair.onClick.AddListener(Sair);
            if (botaoVoltar != null) botaoVoltar.onClick.AddListener(Voltar);
            if (botaoSim != null) botaoSim.onClick.AddListener(ConfirmarApagar);
            if (botaoNao != null) botaoNao.onClick.AddListener(CancelarApagar);

            for (int i = 0; i < cartoes.Length; i++)
            {
                int qual = i;                                 // fecha sobre a copia, nao sobre i
                if (cartoes[i].botao != null)
                    cartoes[i].botao.onClick.AddListener(delegate { Escolher(qual); });
                if (cartoes[i].botaoApagar != null)
                    cartoes[i].botaoApagar.onClick.AddListener(delegate { PedirApagar(qual); });
            }

            Mostrar(painelInicio);
        }

        void Update()
        {
            bool voltou = false;
#if ENABLE_INPUT_SYSTEM
            var t = Keyboard.current;
            if (t != null) voltou = t.escapeKey.wasPressedThisFrame;
#else
            voltou = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (!voltou) return;
            if (painelConfirmar != null && painelConfirmar.activeSelf) CancelarApagar();
            else if (painelSlots != null && painelSlots.activeSelf) Voltar();
        }

        void Mostrar(GameObject qual)
        {
            if (painelInicio != null) painelInicio.SetActive(qual == painelInicio);
            if (painelSlots != null) painelSlots.SetActive(qual == painelSlots);
            if (painelConfirmar != null) painelConfirmar.SetActive(qual == painelConfirmar);
        }

        void AbrirOsPergaminhos()
        {
            Pintar();
            Mostrar(painelSlots);
        }

        void Voltar() { Mostrar(painelInicio); }

        /// <summary>Enche os tres cartoes com o que esta' gravado no disco.</summary>
        void Pintar()
        {
            for (int i = 0; i < cartoes.Length; i++)
            {
                var c = cartoes[i];
                var f = Salvao.Ler(i);

                if (c.titulo != null)
                    c.titulo.text = i < ROMANOS.Length ? ROMANOS[i] : (i + 1).ToString();

                if (c.estado != null)
                    c.estado.text = f.existe ? Salvao.EtapaEmTexto(f.etapa) : "pergaminho em branco";

                if (c.detalhe != null)
                    c.detalhe.text = f.existe
                        ? Salvao.TempoEmTexto(f.segundos) + "   •   " + f.salvoEm
                        : "comecar uma nova historia";

                if (c.botaoApagar != null) c.botaoApagar.gameObject.SetActive(f.existe);
                if (c.marcaDeVazio != null) c.marcaDeVazio.SetActive(!f.existe);
            }
        }

        void Escolher(int qual)
        {
            var f = Salvao.Ler(qual);
            string cena = f.existe ? Salvao.Continuar(qual) : NovaPartida(qual);
            TelaDeCarregamento.Ir(cena, f.existe ? "Retomando a história..." : "A lenda começa...");
        }

        string NovaPartida(int qual)
        {
            Salvao.ComecarNovo(qual);
            Progresso.Zerar();                 // ja' grava na ficha nova
            return Salvao.CENA_INICIAL;
        }

        void PedirApagar(int qual)
        {
            aApagar = qual;
            if (perguntaDeApagar != null)
                perguntaDeApagar.text = "Apagar o pergaminho <b>"
                    + (qual < ROMANOS.Length ? ROMANOS[qual] : (qual + 1).ToString())
                    + "</b>?\nO que estiver gravado nele some para sempre.";
            Mostrar(painelConfirmar);
        }

        void ConfirmarApagar()
        {
            if (aApagar >= 0) Salvao.Apagar(aApagar);
            aApagar = -1;
            Pintar();
            Mostrar(painelSlots);
        }

        void CancelarApagar() { aApagar = -1; Mostrar(painelSlots); }

        void Sair()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
