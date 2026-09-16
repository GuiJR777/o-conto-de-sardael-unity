using UnityEngine;
using System.Collections.Generic;
using System.Text;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// Aperta as teclas sozinho, grava a tela e escreve o que a maquina de estados fez.
    ///
    /// POR QUE EXISTE: quem escreve o codigo nao enxerga o jogo rodando, e "validar" em cima
    /// de registro de estado deu tudo verde enquanto a tela estava quebrada. As duas coisas
    /// juntas — quadros em disco pra OLHAR, e numeros por quadro pra CONFERIR — foi o unico
    /// jeito que achou defeito de verdade.
    ///
    /// T roda o roteiro. Fica desligado ao iniciar, pra ninguem achar que o jogo se mexe
    /// sozinho.
    /// </summary>
    // Roda ANTES do heroi: assim a tecla injetada chega no mesmo quadro, igual a tecla real.
    [DefaultExecutionOrder(-100)]
    public class TesteDeMovimento : MonoBehaviour
    {
        [Header("Quem")]
        public MovimentoDoHeroi heroi;
        public Camera olho;

        [Header("Rodar")]
        [Tooltip("DESLIGADO: o roteiro toma o controle e parece que o jogo se mexe sozinho. Aperte T.")]
        public bool rodarAoIniciar = false;
        public float esperaInicial = 0.4f;
        [Tooltip("Sai do Play sozinho no fim. So para a rodada automatica.")]
        public bool sairAoTerminar = false;

        [Header("Gravacao")]
        public bool gravar = true;
        public float quadrosPorSegundo = 30f;
        public int largura = 640, altura = 360;

        struct Passo
        {
            public float quando; public string acao; public float duracao; public string nota;
            public Passo(float q, string a, float d, string n) { quando = q; acao = a; duracao = d; nota = n; }
        }

        /// <summary>
        /// Cada habilidade isolada, com folga depois. A folga e' o que revela acao nascendo
        /// sozinha — se ninguem apertar nada e o estado mudar, esta' no registro.
        /// </summary>
        static readonly Passo[] ROTEIRO = {
            new Passo(0.2f,  "nota",    0f,    "=== ANDAR pra direita 2s ==="),
            new Passo(0.3f,  "direita", 2.0f,  ""),
            new Passo(2.8f,  "nota",    0f,    "=== CORRER pra esquerda 2s ==="),
            new Passo(2.9f,  "esquerda+correr", 2.0f, ""),
            new Passo(5.4f,  "nota",    0f,    "=== PULAR parado ==="),
            new Passo(5.5f,  "pular",   0f,    ""),
            new Passo(7.5f,  "nota",    0f,    "=== PULAR andando ==="),
            new Passo(7.6f,  "direita", 2.6f,  ""),
            new Passo(8.2f,  "pular",   0f,    ""),
            new Passo(10.5f, "nota",    0f,    "=== ESQUIVA (parado) ==="),
            new Passo(10.6f, "esquiva", 0f,    ""),
            new Passo(13.0f, "nota",    0f,    "=== ROLAR (parado) ==="),
            new Passo(13.1f, "rolar",   0f,    ""),
            new Passo(15.5f, "nota",    0f,    "=== ARRANCO (parado) ==="),
            new Passo(15.6f, "arranco", 0f,    ""),

            // daqui pra baixo: as combinacoes que o primeiro teste nao cobriu.
            // e' onde mora o "muda de lado e teleporta" que ele relata desde o primeiro dia.
            new Passo(18.0f, "nota",    0f,    "=== VIRAR CORRENDO (direita -> esquerda, sem soltar) ==="),
            new Passo(18.1f, "direita+correr",  1.4f, ""),
            new Passo(19.5f, "esquerda+correr", 1.4f, ""),
            new Passo(21.2f, "nota",    0f,    "=== ESQUIVA CORRENDO ==="),
            new Passo(21.3f, "direita+correr", 1.7f, ""),
            new Passo(22.3f, "esquiva", 0f,    ""),
            new Passo(24.0f, "nota",    0f,    "=== ROLAR CORRENDO ==="),
            new Passo(24.1f, "direita+correr", 1.7f, ""),
            new Passo(25.1f, "rolar",   0f,    ""),
            new Passo(27.0f, "nota",    0f,    "=== PULAR CORRENDO ==="),
            new Passo(27.1f, "direita+correr", 2.4f, ""),
            new Passo(28.0f, "pular",   0f,    ""),
            new Passo(30.5f, "nota",    0f,    "=== FIM ==="),
        };

        readonly List<string> registro = new List<string>();
        float t0;
        int proximo;
        bool rodando;

        // entrada injetada
        float eixoAte, correrAte;
        int eixoInjetado;

        string estadoAnterior = "";
        float xAnterior;

        // gravacao
        string pasta;
        float proximoQuadro;
        int contador;
        RenderTexture alvo;
        Texture2D folha;

        void Start()
        {
            if (heroi == null) heroi = GetComponent<MovimentoDoHeroi>();
            if (olho == null) olho = Camera.main;
            pasta = System.IO.Path.Combine(Application.dataPath, "../gravacao");
#if UNITY_EDITOR
            // Bandeira de uma viagem so': o menu "Rodar Teste" a levanta, o Start a derruba.
            // A cena no disco continua com o teste DESLIGADO — quem apertar Play ve' o jogo,
            // nao um boneco se mexendo sozinho.
            if (UnityEditor.SessionState.GetBool("sardael.teste.auto", false))
            {
                UnityEditor.SessionState.SetBool("sardael.teste.auto", false);
                rodarAoIniciar = true; sairAoTerminar = true;
            }
#endif
            if (rodarAoIniciar) Comecar();
        }

        public void Comecar()
        {
            registro.Clear();
            proximo = 0;
            t0 = Time.time + esperaInicial;
            rodando = true;
            estadoAnterior = "";
            xAnterior = transform.position.x;
            eixoInjetado = 0; eixoAte = 0f; correrAte = 0f;

            // teclado fisico fora: so' vale o que o teste injeta. Sem isto, um Alt+Tab no meio
            // da medicao vira arranco e o registro mede outra coisa sem avisar.
            MovimentoDoHeroi.SoInjecao = true;

            if (gravar) IniciarGravacao();

            registro.Add("  tempo  estado        acao?   x       passo    vel     chao  evento");
            registro.Add("  -----  ------------  ------  ------  -------  ------  ----  ------");
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var tec = Keyboard.current;
            if (!rodando && tec != null && tec.tKey.wasPressedThisFrame) Comecar();
#endif
            if (!rodando) return;
            float t = Time.time - t0;
            if (t < 0f) return;

            string evento = "";

            while (proximo < ROTEIRO.Length && t >= ROTEIRO[proximo].quando)
            {
                var p = ROTEIRO[proximo++];
                switch (p.acao)
                {
                    case "direita":  eixoInjetado = 1;  eixoAte = t + p.duracao; evento += "[D] "; break;
                    case "esquerda": eixoInjetado = -1; eixoAte = t + p.duracao; evento += "[A] "; break;
                    case "esquerda+correr":
                        eixoInjetado = -1; eixoAte = t + p.duracao; correrAte = t + p.duracao;
                        evento += "[A+SHIFT] "; break;
                    case "direita+correr":
                        eixoInjetado = 1; eixoAte = t + p.duracao; correrAte = t + p.duracao;
                        evento += "[D+SHIFT] "; break;
                    case "pular":   heroi.InjetarPulo(); evento += "[ESPACO] "; break;
                    case "esquiva": heroi.InjetarEsquiva(); evento += "[CTRL] "; break;
                    case "rolar":   heroi.InjetarRolar(); evento += "[C] "; break;
                    case "arranco": heroi.InjetarArranco(); evento += "[ALT] "; break;
                    default: registro.Add(""); registro.Add("  " + p.nota); break;
                }
            }

            if (t > eixoAte) eixoInjetado = 0;
            heroi.InjetarEixo(eixoInjetado);
            heroi.InjetarCorrer(t <= correrAte);

            string est = heroi.EstadoDaAnimacao;
            if (est != estadoAnterior) { evento += "-> " + est + " "; estadoAnterior = est; }

            float x = transform.position.x;
            registro.Add(string.Format("  {0,5:F2}  {1,-12}  {2,-6}  {3,6:F2}  {4,7:F4}  {5,6:F2}  {6,-4}  {7}",
                t, est, heroi.EmAcao ? "ACAO" : "", x, x - xAnterior,
                heroi.VelocidadeAtual, heroi.NoChao ? "sim" : "NAO", evento));
            xAnterior = x;

            if (gravar) Gravar();

            if (proximo >= ROTEIRO.Length) { rodando = false; Despejar(); }
        }

        // ------------------------------------------------------------------ gravacao

        void IniciarGravacao()
        {
            if (olho == null) { gravar = false; return; }
            try
            {
                if (System.IO.Directory.Exists(pasta))
                    foreach (var f in System.IO.Directory.GetFiles(pasta, "*.png")) System.IO.File.Delete(f);
                else System.IO.Directory.CreateDirectory(pasta);
            }
            catch { gravar = false; return; }

            alvo = new RenderTexture(largura, altura, 24);
            folha = new Texture2D(largura, altura, TextureFormat.RGB24, false);
            contador = 0; proximoQuadro = 0f;
        }

        void Gravar()
        {
            proximoQuadro -= Time.deltaTime;
            if (proximoQuadro > 0f) return;
            proximoQuadro = 1f / Mathf.Max(1f, quadrosPorSegundo);

            var antes = olho.targetTexture; var ativo = RenderTexture.active;
            olho.targetTexture = alvo; olho.Render();
            RenderTexture.active = alvo;
            folha.ReadPixels(new Rect(0, 0, largura, altura), 0, 0); folha.Apply(false);
            olho.targetTexture = antes; RenderTexture.active = ativo;

            try
            {
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(pasta, "q" + contador.ToString("D4") + ".png"),
                    folha.EncodeToPNG());
            }
            catch { gravar = false; }
            contador++;
        }

        void Despejar()
        {
            if (alvo != null) { alvo.Release(); Destroy(alvo); alvo = null; }

            var sb = new StringBuilder();
            sb.AppendLine("===== TESTE DE MOVIMENTO =====");
            foreach (var l in registro) sb.AppendLine(l);
            sb.AppendLine("===== FIM =====");
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Application.dataPath, "../teste-de-movimento.txt"), sb.ToString());
            }
            catch (System.Exception e) { Debug.LogWarning("[Teste] " + e.Message); }
            Debug.Log("[Teste] pronto: " + contador + " quadros");
#if UNITY_EDITOR
            if (sairAoTerminar) UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
