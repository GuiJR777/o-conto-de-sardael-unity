using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Sardael
{
    /// <summary>
    /// Joga sozinho ate' a pausa e clica em cada botao dela com o mouse.
    ///
    /// POR QUE ISTO EXISTE: "da' play e testa" ja' foi validado varias vezes aqui e nenhuma
    /// delas estava certa. O relatorio que sai deste teste e as fotos sao a prova; sem elas eu
    /// nao digo que consertei nada.
    ///
    /// O clique e' clique de verdade: um evento de mouse entra na fila do Input System, na
    /// posicao do botao na tela, e segue o mesmo caminho do jogador — modulo de UI,
    /// EventSystem, GraphicRaycaster, Button. Chamar o metodo do botao a mao provaria que o
    /// metodo funciona, que nunca foi a duvida; a duvida era se o clique CHEGA nele.
    ///
    /// O teste nao encosta em pergaminho nenhum: entra em Halu pelo mesmo
    /// <see cref="TelaDeCarregamento.Ir"/> que o menu usa, mas sem escolher partida. Sem slot
    /// escolhido o <see cref="Salvao"/> nao grava arquivo, entao o jogo salvo dele fica
    /// intacto — do menu o teste so' abre e fecha painel, que nao toca em disco.
    /// </summary>
    public class TesteDaPausa : MonoBehaviour
    {
        public const string BANDEIRA = "sardael.teste.pausa";

#if UNITY_EDITOR
        /// <summary>
        /// Nasce sozinho, e so' quando o menu do editor armou a bandeira.
        ///
        /// De proposito nao ha' componente deste teste gravado em cena nenhuma: a cena e' dele,
        /// e objeto de teste esquecido la' dentro vira bug do jogo depois.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Nascer()
        {
            if (!UnityEditor.SessionState.GetBool(BANDEIRA, false)) return;
            UnityEditor.SessionState.SetBool(BANDEIRA, false);      // vale so' por esta viagem
            var go = new GameObject("TesteDaPausa");
            DontDestroyOnLoad(go);
            go.AddComponent<TesteDaPausa>();
        }
#endif

        const float ESPERA_DE_CARGA = 240f;   // Halu e' grande, e a tela de carga espera quadro bom

        string arquivo, pasta;
        readonly StringBuilder relatorio = new StringBuilder();
        int falhas, foto;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse;
        Keyboard teclado;
        InputSettings.EditorInputBehaviorInPlayMode focoDeAntes;
#endif

        IEnumerator Start()
        {
            arquivo = Application.dataPath + "/../_teste_pausa.txt";
            pasta = Application.dataPath + "/../gravacao_pausa";
            try
            {
                if (System.IO.Directory.Exists(pasta))
                    foreach (var f in System.IO.Directory.GetFiles(pasta, "*.png")) System.IO.File.Delete(f);
                else System.IO.Directory.CreateDirectory(pasta);
            }
            catch { }

#if !ENABLE_INPUT_SYSTEM
            Anotar("Input System ligado", false, "o teste clica pela fila do Input System");
            Despejar();
            yield break;
#else
            mouse = Mouse.current != null ? Mouse.current : InputSystem.AddDevice<Mouse>();
            teclado = Keyboard.current != null ? Keyboard.current : InputSystem.AddDevice<Keyboard>();

            // sem isto o editor desliga mouse e teclado quando a janela do Game perde o foco, e
            // o teste inteiro passaria a nao apertar nada sem dizer por que
            focoDeAntes = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            yield return Correr();

            InputSystem.settings.editorInputBehaviorInPlayMode = focoDeAntes;
            Despejar();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        IEnumerator Correr()
        {
            yield return Esperar(1.5f);

            // ---------------------------------------------------------- 1. o menu ainda clica
            var menu = Object.FindAnyObjectByType<MenuPrincipal>();
            if (menu == null)
            {
                Anotar("achar o menu principal", false, "comece o teste pela cena Menu_Principal");
                yield break;
            }
            Anotar("um so' EventSystem no menu", ContarEventSystem() == 1,
                   "achei " + ContarEventSystem());

            yield return Foto("menu");
            yield return Clicar(menu.botaoJogar != null ? menu.botaoJogar.gameObject : null, "JOGAR do menu");
            Anotar("o menu abriu os pergaminhos no clique",
                   menu.painelSlots != null && menu.painelSlots.activeSelf, Painel(menu));
            yield return Foto("pergaminhos");

            yield return Clicar(menu.botaoVoltar != null ? menu.botaoVoltar.gameObject : null, "VOLTAR do menu");
            Anotar("o menu voltou pra entrada",
                   menu.painelInicio != null && menu.painelInicio.activeSelf, Painel(menu));

            // ---------------------------------------------------------- 2. entrar no jogo
            // pelo mesmo caminho do menu, mas sem escolher pergaminho: nada e' gravado no disco
            TelaDeCarregamento.Ir(Salvao.CENA_INICIAL, "Testando a pausa...");
            float comecou = Time.unscaledTime;
            while ((SceneManager.GetActiveScene().name != Salvao.CENA_INICIAL
                    || TelaDeCarregamento.Carregando)
                   && Time.unscaledTime - comecou < ESPERA_DE_CARGA)
                yield return null;

            bool entrou = SceneManager.GetActiveScene().name == Salvao.CENA_INICIAL;
            Anotar("entrou em " + Salvao.CENA_INICIAL, entrou,
                   "cena agora: " + SceneManager.GetActiveScene().name
                 + ", levou " + (Time.unscaledTime - comecou).ToString("F1") + " s");
            if (!entrou) yield break;
            yield return Esperar(1.5f);

            // e' AQUI que o bug morava: o EventSystem era o do Menu_Principal e morreu com ele
            Anotar("ha' EventSystem dentro do jogo", EventSystem.current != null,
                   "sem ele nenhum clique de UI existe; so' o ESC responderia");
            Anotar("e um so'", ContarEventSystem() == 1, "achei " + ContarEventSystem());

            // ---------------------------------------------------------- 3. ESC abre a pausa
            var pausa = TelaDePausa.Instancia;
            if (pausa == null) { Anotar("achar a TelaDePausa", false, "instancia nula"); yield break; }

            yield return ApertarEsc();
            Anotar("ESC pausou", pausa.Pausado, "timeScale = " + Time.timeScale);
            Anotar("o painel da pausa apareceu", Ligado("Painel_Pausa"), Paineis());
            yield return Foto("pausa_aberta");

            // ---------------------------------------------------------- 4. os botoes da pausa
            yield return Clicar(Achar("Painel_Pausa/Botao_VOLTAR AO MENU"), "VOLTAR AO MENU");
            Anotar("VOLTAR AO MENU abriu a confirmacao", Ligado("Painel_Confirmar"), Paineis());
            yield return Foto("confirmar");

            yield return Clicar(Achar("Painel_Confirmar/Caixa/Botao_FICAR"), "FICAR");
            Anotar("FICAR voltou pra pausa", Ligado("Painel_Pausa"), Paineis());

            yield return Clicar(Achar("Painel_Pausa/Botao_CONFIGURACOES"), "CONFIGURACOES");
            Anotar("CONFIGURACOES abriu as opcoes", Ligado("Painel_Opcoes"), Paineis());
            yield return Foto("opcoes");

            yield return Clicar(Achar("Painel_Opcoes/Caixa/Botao_VOLTAR"), "VOLTAR das opcoes");
            Anotar("VOLTAR fechou as opcoes", Ligado("Painel_Pausa"), Paineis());

            yield return Clicar(Achar("Painel_Pausa/Botao_CONTINUAR"), "CONTINUAR");
            Anotar("CONTINUAR despausou", !pausa.Pausado, "timeScale = " + Time.timeScale);
            Anotar("o relogio voltou a andar", Time.timeScale > 0.99f, "timeScale = " + Time.timeScale);
            yield return Foto("despausado");

            // ---------------------------------------------------------- 5. voltar ao menu pra valer
            yield return ApertarEsc();
            Anotar("ESC pausou de novo", pausa.Pausado, "timeScale = " + Time.timeScale);
            yield return Clicar(Achar("Painel_Pausa/Botao_VOLTAR AO MENU"), "VOLTAR AO MENU (de novo)");
            yield return Clicar(Achar("Painel_Confirmar/Caixa/Botao_VOLTAR"), "VOLTAR confirmado");

            comecou = Time.unscaledTime;
            while ((SceneManager.GetActiveScene().name != Salvao.CENA_DO_MENU
                    || TelaDeCarregamento.Carregando)
                   && Time.unscaledTime - comecou < ESPERA_DE_CARGA)
                yield return null;
            Anotar("VOLTAR AO MENU levou ao menu",
                   SceneManager.GetActiveScene().name == Salvao.CENA_DO_MENU,
                   "cena agora: " + SceneManager.GetActiveScene().name);
            yield return Esperar(1.5f);
            yield return Foto("de_volta_ao_menu");

            // ---------------------------------------------------------- 6. o menu nao ficou duplo
            Anotar("continua um so' EventSystem no menu", ContarEventSystem() == 1,
                   "achei " + ContarEventSystem() + "; dois fazem o Unity desligar um deles");
            var menu2 = Object.FindAnyObjectByType<MenuPrincipal>();
            if (menu2 != null && menu2.botaoJogar != null)
            {
                yield return Clicar(menu2.botaoJogar.gameObject, "JOGAR do menu, depois de voltar");
                Anotar("o menu continua respondendo ao mouse",
                       menu2.painelSlots != null && menu2.painelSlots.activeSelf, Painel(menu2));
            }
        }

        // ------------------------------------------------------------------ o mouse e o teclado

        IEnumerator Clicar(GameObject alvo, string oQue)
        {
            if (alvo == null) { Anotar("clicar em " + oQue, false, "nao achei o botao na tela"); yield break; }

            var r = alvo.transform as RectTransform;

            // cada Canvas tem a sua conta: o da pausa e' Overlay e a posicao do objeto JA' e' a
            // posicao na tela; o do menu e' Screen Space - Camera e precisa da camera dele pra
            // projetar. Passar null pros dois mandou o clique do menu pra -7,-1, fora da tela
            var lona = alvo.GetComponentInParent<Canvas>();
            if (lona != null) lona = lona.rootCanvas;
            Camera olho = lona != null && lona.renderMode != RenderMode.ScreenSpaceOverlay
                        ? lona.worldCamera : null;
            Vector2 onde = RectTransformUtility.WorldToScreenPoint(olho, r.position);

            InputSystem.QueueStateEvent(mouse, new MouseState { position = onde });
            yield return null; yield return null;

            string embaixo = OQueEstaEmbaixo(onde);
            bool chegou = embaixo == alvo.name;

            InputSystem.QueueStateEvent(mouse, new MouseState { position = onde }
                                               .WithButton(MouseButton.Left, true));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = onde }
                                               .WithButton(MouseButton.Left, false));
            yield return null; yield return null;

            Anotar("o mouse alcanca " + oQue, chegou,
                   "em " + onde.x.ToString("F0") + "," + onde.y.ToString("F0")
                 + " o raio da UI acha: " + embaixo);
        }

        IEnumerator ApertarEsc()
        {
            InputSystem.QueueStateEvent(teclado, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(teclado, new KeyboardState());
            yield return null; yield return null;
        }

        /// <summary>O que um clique naquele ponto acertaria — a pergunta que o teste faz.</summary>
        static string OQueEstaEmbaixo(Vector2 onde)
        {
            var es = EventSystem.current;
            if (es == null) return "NADA: nao ha' EventSystem nesta cena";
            var dados = new PointerEventData(es);
            dados.position = onde;
            var achados = new List<RaycastResult>();
            es.RaycastAll(dados, achados);
            if (achados.Count == 0) return "nada";
            var g = achados[0].gameObject;
            // o raio pode parar num filho do botao (moldura, rotulo). Quem recebe o clique e' o
            // Selectable mais proximo subindo; sem isto o teste acusaria falha onde nao ha'
            for (var t = g.transform; t != null; t = t.parent)
                if (t.GetComponent<Selectable>() != null) return t.name;
            return g.name;
        }

        // ------------------------------------------------------------------ olhar a pausa

        static GameObject Achar(string caminho)
        {
            var p = TelaDePausa.Instancia;
            if (p == null) return null;
            var t = p.transform.Find("Canvas_Pausa/" + caminho);
            if (t != null) return t.gameObject;

            // "CONFIGURAÇÕES" tem cedilha e til: se o caminho exato falhar, procura o botao
            // comparando so' as letras. Assim o teste nao quebra por causa de acento.
            int barra = caminho.LastIndexOf('/');
            string nome = barra < 0 ? caminho : caminho.Substring(barra + 1);
            foreach (var b in p.GetComponentsInChildren<Button>(true))
                if (Cru(b.gameObject.name) == Cru(nome)) return b.gameObject;
            return null;
        }

        /// <summary>So' as letras, sem acento e em maiuscula: CONFIGURAÇÕES bate com CONFIGURACOES.</summary>
        static string Cru(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s.Normalize(NormalizationForm.FormD))
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToUpperInvariant(c));
            return sb.ToString();
        }

        static bool Ligado(string painel)
        {
            var p = TelaDePausa.Instancia;
            if (p == null) return false;
            var t = p.transform.Find("Canvas_Pausa/" + painel);
            return t != null && t.gameObject.activeSelf;
        }

        static string Paineis()
        {
            return "pausa=" + Ligado("Painel_Pausa")
                 + " opcoes=" + Ligado("Painel_Opcoes")
                 + " confirmar=" + Ligado("Painel_Confirmar");
        }

        static string Painel(MenuPrincipal m)
        {
            return "inicio=" + (m.painelInicio != null && m.painelInicio.activeSelf)
                 + " slots=" + (m.painelSlots != null && m.painelSlots.activeSelf);
        }

        static int ContarEventSystem()
        {
            return Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude).Length;
        }
#endif

        // ------------------------------------------------------------------ o que fica gravado

        static IEnumerator Esperar(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Foto(string nome)
        {
            yield return new WaitForEndOfFrame();
            Texture2D t = null;
            try
            {
                t = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(pasta, foto.ToString("D2") + "_" + nome + ".png"),
                    t.EncodeToPNG());
            }
            catch { }
            if (t != null) Destroy(t);
            foto++;
        }

        void Anotar(string oQue, bool passou, string detalhe)
        {
            if (!passou) falhas++;
            relatorio.Append(passou ? "  ok       " : "  FALHOU   ").Append(oQue);
            if (!string.IsNullOrEmpty(detalhe)) relatorio.Append("   [").Append(detalhe).Append(']');
            relatorio.Append('\n');
        }

        void Despejar()
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== TESTE DA PAUSA =====");
            sb.AppendLine("Unity " + Application.unityVersion + "   " + System.DateTime.Now);
            sb.AppendLine();
            sb.Append(relatorio);
            sb.AppendLine();
            sb.AppendLine(falhas == 0 ? "TUDO PASSOU" : falhas + " FALHA(S)");
            sb.AppendLine("fotos em: " + pasta);
            try { System.IO.File.WriteAllText(arquivo, sb.ToString()); } catch { }
            Debug.Log(sb.ToString());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
