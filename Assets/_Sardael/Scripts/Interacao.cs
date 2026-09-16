using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// O botao de interagir do jogador: acha o <see cref="Interagivel"/> mais perto, mostra o
    /// aviso sobre a cabeca dele e conduz a conversa.
    ///
    /// Durante a conversa o movimento fica travado por um CAMPO do MovimentoDoHeroi, e nao
    /// desligando o componente. Desligado, ele para de aplicar gravidade — e um Sardael que
    /// conversa em cima de um degrau ficaria pendurado no ar ate' a conversa acabar.
    ///
    /// A mesma tecla avanca a fala. Nao ha' um segundo botao de "continuar": o jogador ja'
    /// esta' com o dedo nesse.
    /// </summary>
    public class Interacao : MonoBehaviour
    {
        [Header("Tecla")]
        [Tooltip("Alem do alcance do interagivel, exige que ele esteja na frente de Sardael.")]
        public bool exigirDeFrente = false;

        [Header("Quests resolvidas")]
        public List<string> resolvidas = new List<string>();

        [Header("Aviso")]
        public bool mostrarAviso = true;
        [Tooltip("Altura do balao acima do pe do NPC.")]
        public float alturaDoAviso = 2.1f;

        [Header("Animacao")]
        [Tooltip("Gatilho disparado quando a conversa comeca. Vazio = nao anima.")]
        public string gatilhoDeInteragir = "interagir";

        Interagivel alvo;
        Interagivel falandoCom;
        int falaAtual = -1;
        MovimentoDoHeroi mov;
        Animator anim;
        Camera olho;

        public bool EmConversa { get { return falandoCom != null; } }

        void Awake()
        {
            mov = GetComponent<MovimentoDoHeroi>();
            anim = GetComponent<Animator>();
        }

        void Update()
        {
            if (falandoCom == null) alvo = MaisPerto();

            if (Apertou())
            {
                if (falandoCom == null) { if (alvo != null) Comecar(alvo); }
                else Avancar();
            }

            // Em NOME DESTE componente, e nao no booleano solto: o diretor da cutscene
            // tambem segura o heroi, e escrever o booleano todo quadro apagava a trava dele.
            if (mov != null) mov.Travar(this, EmConversa);

            // se o jogador for arrastado pra longe (ou o NPC sumir), a conversa fecha sozinha
            if (falandoCom != null &&
                (!falandoCom.isActiveAndEnabled ||
                 Vector3.Distance(transform.position, falandoCom.transform.position) > falandoCom.alcance * 1.8f))
                Fechar();
        }

        Interagivel MaisPerto()
        {
            Interagivel melhor = null;
            float menor = float.MaxValue;
            var p = transform.position;
            foreach (var i in Interagivel.Todos)
            {
                if (i == null || !i.isActiveAndEnabled) continue;
                float d = Vector3.Distance(p, i.transform.position);
                if (d > i.alcance || d >= menor) continue;
                if (exigirDeFrente &&
                    Vector3.Dot(transform.forward, (i.transform.position - p).normalized) < 0.2f) continue;
                menor = d; melhor = i;
            }
            return melhor;
        }

        void Comecar(Interagivel i)
        {
            falandoCom = i;
            falaAtual = 0;

            // o gesto de interagir dispara UMA vez, no comeco da conversa. Nas falas seguintes
            // ele fica parado ouvindo: repetir o gesto a cada tecla daria um tique nervoso.
            if (anim != null && !string.IsNullOrEmpty(gatilhoDeInteragir))
                anim.SetTrigger(gatilhoDeInteragir);

            if (i.aoInteragir != null) i.aoInteragir.Invoke();
        }

        void Avancar()
        {
            var falas = falandoCom.FalasDeAgora;
            falaAtual++;
            if (falas != null && falaAtual < falas.Length) return;

            if (falandoCom.resolveAoFalar && !string.IsNullOrEmpty(falandoCom.quest)
                && !resolvidas.Contains(falandoCom.quest))
            {
                resolvidas.Add(falandoCom.quest);
                falandoCom.Concluido = true;
            }
            Fechar();
        }

        void Fechar() { falandoCom = null; falaAtual = -1; }

        public bool QuestResolvida(string q) { return resolvidas.Contains(q); }

        bool Apertou()
        {
#if ENABLE_INPUT_SYSTEM
            var t = Keyboard.current;
            if (t != null && (t.eKey.wasPressedThisFrame || t.enterKey.wasPressedThisFrame)) return true;
            var g = Gamepad.current;
            if (g != null && g.buttonWest.wasPressedThisFrame) return true;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return);
#else
            return false;
#endif
        }

        // ------------------------------------------------------------------------ desenho
        static Texture2D fundo;
        static Texture2D Fundo()
        {
            if (fundo == null)
            {
                fundo = new Texture2D(1, 1);
                fundo.SetPixel(0, 0, Color.white);
                fundo.Apply();
                fundo.hideFlags = HideFlags.HideAndDontSave;
            }
            return fundo;
        }

        static void Caixa(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Fundo());
            GUI.color = antes;
        }

        void OnGUI()
        {
            if (!mostrarAviso) return;
            if (olho == null) olho = Camera.main;

            if (falandoCom != null) { Dialogo(); return; }
            if (alvo != null) Balao(alvo);
        }

        void Balao(Interagivel i)
        {
            if (olho == null) return;
            var mundo = i.transform.position + Vector3.up * alturaDoAviso;
            var tela = olho.WorldToScreenPoint(mundo);
            if (tela.z <= 0f) return;

            string texto = "E   " + i.verbo;
            var estilo = new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            estilo.normal.textColor = Color.white;

            float larg = Mathf.Max(96f, estilo.CalcSize(new GUIContent(texto)).x + 26f);
            var r = new Rect(tela.x - larg / 2f, Screen.height - tela.y - 30f, larg, 28f);
            Caixa(r, new Color(0f, 0f, 0f, 0.66f));
            GUI.Label(r, texto, estilo);

            var sub = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            sub.normal.textColor = new Color(1f, 0.86f, 0.45f);
            GUI.Label(new Rect(r.x, r.yMax + 1f, r.width, 16f), i.nome, sub);
        }

        void Dialogo()
        {
            var falas = falandoCom.FalasDeAgora;
            if (falas == null || falaAtual < 0 || falaAtual >= falas.Length) return;

            float larg = Mathf.Min(760f, Screen.width - 80f);
            var caixa = new Rect((Screen.width - larg) / 2f, Screen.height - 196f, larg, 136f);
            Caixa(caixa, new Color(0.04f, 0.03f, 0.02f, 0.86f));
            Caixa(new Rect(caixa.x, caixa.y, caixa.width, 2f), new Color(1f, 0.78f, 0.3f, 0.9f));

            var nome = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            nome.normal.textColor = new Color(1f, 0.85f, 0.42f);
            GUI.Label(new Rect(caixa.x + 20, caixa.y + 12, caixa.width - 40, 22), falandoCom.nome, nome);

            var fala = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            fala.normal.textColor = Color.white;
            GUI.Label(new Rect(caixa.x + 20, caixa.y + 40, caixa.width - 40, 66), falas[falaAtual], fala);

            var rodape = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleRight };
            rodape.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            string fim = (falaAtual + 1 >= falas.Length) ? "E para encerrar" : "E para continuar";
            GUI.Label(new Rect(caixa.x + 20, caixa.yMax - 26, caixa.width - 40, 18),
                      (falaAtual + 1) + " / " + falas.Length + "     " + fim, rodape);
        }
    }
}
