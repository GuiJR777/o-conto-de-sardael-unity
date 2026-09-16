using UnityEngine;
using System.Collections.Generic;
using System.Text;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// Aperta as teclas do duelo sozinho, grava a tela e escreve o que os DOIS animadores
    /// fizeram, quadro a quadro.
    ///
    /// A diferenca pro teste de movimento: aqui ha' dois corpos, e a pergunta e' se eles
    /// concordam. O registro traz o estado do heroi, o estado do orc e a distancia entre eles
    /// na mesma linha — e' assim que da' pra ver se a reacao entrou no quadro do golpe ou um
    /// pouco depois.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TesteDeCombate : MonoBehaviour
    {
        [Header("Quem")]
        public Duelo duelo;
        public MovimentoDoHeroi heroi;
        public Camera olho;

        [Header("Rodar")]
        public bool rodarAoIniciar = false;
        public bool sairAoTerminar = false;
        public float esperaInicial = 0.4f;

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
        /// Distancia em que o passo "aproximar" para de andar.
        ///
        /// Fica entre os dois alcances da cadeia — 1,166 m do elo 2 e 1,756 m dos outros tres —
        /// entao a folga de 0,70 cobre os dois a partir daqui.
        /// </summary>
        const float PERTO = 1.45f;

        static readonly Passo[] ROTEIRO = {
            new Passo(0.3f,  "nota",   0f, "=== 1 GOLPE SO (J uma vez) ==="),
            new Passo(0.4f,  "aproximar", 0f, ""),
            new Passo(3.0f,  "atacar", 0f, ""),

            new Passo(6.0f,  "nota",   0f, "=== EMENDAR 2 (J no golpe 1, durante ele) ==="),
            new Passo(6.1f,  "aproximar", 0f, ""),
            new Passo(8.5f,  "atacar", 0f, ""),
            new Passo(9.5f,  "atacar", 0f, ""),

            new Passo(13.0f, "nota",   0f, "=== EMENDAR OS 4 ==="),
            new Passo(13.1f, "aproximar", 0f, ""),
            new Passo(15.5f, "atacar", 0f, ""),
            new Passo(16.5f, "atacar", 0f, ""),
            new Passo(17.5f, "atacar", 0f, ""),
            new Passo(18.5f, "atacar", 0f, ""),

            new Passo(24.0f, "nota",   0f, "=== AFASTAR E ATACAR: tem que PASSAR LONGE ==="),
            new Passo(24.1f, "esquerda", 1.6f, ""),
            new Passo(26.0f, "atacar", 0f, ""),

            new Passo(30.0f, "nota",   0f, "=== O ORC ATACA: reacao do PLAYER ==="),
            new Passo(30.1f, "aproximar", 0f, ""),
            new Passo(32.5f, "orcAtaca", 0f, ""),

            new Passo(36.0f, "nota",   0f, "=== MARTELAR J: nao pode enlouquecer ==="),
            new Passo(36.1f, "aproximar", 0f, ""),
            new Passo(38.5f, "atacar", 0f, ""),
            new Passo(38.7f, "atacar", 0f, ""),
            new Passo(38.9f, "atacar", 0f, ""),
            new Passo(39.1f, "atacar", 0f, ""),
            new Passo(39.3f, "atacar", 0f, ""),

            new Passo(45.0f, "nota",   0f, "=== FIM ==="),
        };

        static readonly string[] ESTADOS_DO_ORC = {
            "Parado", "Levar", "LevarForte", "Golpe" };

        readonly List<string> registro = new List<string>();
        bool aproximando, recuando;
        float t0;
        int proximo;
        bool rodando;

        float eixoAte;
        int eixoInjetado;

        string heroiAnterior = "", orcAnterior = "", eventoAnterior = "";
        Transform cabecaDoOrc;

        string pasta;
        float proximoQuadro;
        int contador;
        RenderTexture alvo;
        Texture2D folha;

        void Start()
        {
            if (duelo == null) duelo = GetComponent<Duelo>();
            if (heroi == null) heroi = GetComponent<MovimentoDoHeroi>();
            if (olho == null) olho = Camera.main;
            pasta = System.IO.Path.Combine(Application.dataPath, "../gravacao");
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("sardael.combate.auto", false))
            {
                UnityEditor.SessionState.SetBool("sardael.combate.auto", false);
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
            heroiAnterior = ""; orcAnterior = ""; eventoAnterior = "";
            eixoInjetado = 0; eixoAte = 0f;

            // no teste quem manda no orc e' o roteiro, nao um relogio
            if (duelo != null) duelo.orcAtacaSozinho = false;

            // durante o teste, so' vale o que o teste injeta. Uma tecla solta do usuario
            // (Alt+Tab, por exemplo) ja' fez o Sardael arrancar no meio da medicao.
            MovimentoDoHeroi.SoInjecao = true;

            if (gravar) IniciarGravacao();
            if (duelo != null && duelo.doOrc != null)
                cabecaDoOrc = duelo.doOrc.GetBoneTransform(HumanBodyBones.Head);
            registro.Add("  tempo  heroi         orc           dist    cabecaY cabecaZ  clipe do orc     evento");
            registro.Add("  -----  ------------  ------------  ------  ------- -------  ---------------  ------");
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
                    case "atacar":   duelo.InjetarAtaque(); evento += "[J] "; break;
                    case "orcAtaca": duelo.MandarOrcAtacar(); evento += "[ORC ATACA] "; break;
                    case "aproximar": aproximando = true; evento += "[-> orc] "; break;
                    case "direita":  eixoInjetado = 1;  eixoAte = t + p.duracao; evento += "[D] "; break;
                    case "esquerda": eixoInjetado = -1; eixoAte = t + p.duracao; evento += "[A] "; break;
                    default: registro.Add(""); registro.Add("  " + p.nota); break;
                }
            }

            if (t > eixoAte) eixoInjetado = 0;

            // APROXIMAR POR DISTANCIA, NAO POR TEMPO.
            //
            // O roteiro antigo nunca andava ate' o orc: ele chegava perto porque cada golpe
            // empurrava o Sardael 1,22 m sozinho. Com os clipes novos o primeiro elo empurra
            // 0,270 m, e o teste inteiro passou a bater no ar a 3 m de distancia — oito golpes,
            // nenhum acerto, e nada disso era defeito do combate.
            //
            // Medir em vez de cronometrar tambem faz o teste sobreviver a proxima troca de clipe.
            if (aproximando && duelo != null && duelo.doOrc != null)
            {
                // MANTER a distancia, e nao so' encurtar.
                //
                // A primeira versao parava assim que |d| <= PERTO — entao, se o heroi ja' esta'
                // perto demais, ela nao fazia nada. Os golpes empurram e o tranco afasta o orc,
                // e cada bloco do roteiro comecava mais perto que o anterior: 1,27 m, 0,90 m,
                // 0,18 m. No quarto bloco o Sardael ja' entrava por dentro do orc e os quatro
                // elos erravam — sem que houvesse nada errado com o combate.
                // E TERMINA SEMPRE ANDANDO PARA O ORC.
                //
                // Recuar vira o personagem: o MovimentoDoHeroi olha pra onde anda. Na primeira
                // versao o heroi recuava ate' a distancia certa e ficava DE COSTAS — e o golpe
                // seguinte reportava "orc a -1,55 m", que e' o orc atras dele. Entao quando esta'
                // perto demais eu recuo um pouco ALEM do alvo e volto andando pra frente: o
                // ultimo passo e' sempre em direcao ao orc, e ele termina encarando.
                float d = duelo.doOrc.transform.position.x - transform.position.x;
                float lado = d >= 0f ? 1f : -1f;
                float aoOrc = Mathf.Abs(d);

                if (aoOrc < PERTO - 0.35f) recuando = true;     // perto demais: abre espaco
                if (recuando && aoOrc >= PERTO + 0.25f) recuando = false;

                if (recuando) eixoInjetado = (int)(-lado);
                else if (aoOrc > PERTO + 0.10f) eixoInjetado = (int)lado;
                else { aproximando = false; eixoInjetado = 0; }
            }

            if (heroi != null) heroi.InjetarEixo(eixoInjetado);

            string eh = NomeDoEstado(duelo.doHeroi, NOMES_DO_HEROI);
            string eo = NomeDoEstado(duelo.doOrc, ESTADOS_DO_ORC);
            if (eh != heroiAnterior) { evento += "heroi->" + eh + " "; heroiAnterior = eh; }
            if (eo != orcAnterior) { evento += "ORC->" + eo + " "; orcAnterior = eo; }

            string ue = duelo.UltimoEvento;
            if (ue != null && ue != eventoAnterior) { evento += "| " + ue; eventoAnterior = ue; }

            float dist = duelo.doOrc == null ? 0f
                : duelo.doOrc.transform.position.x - transform.position.x;

            // a cabeca do orc no referencial DELE: se a reacao esta' rodando de verdade, ela
            // desce (leva o golpe) e avanca (o corpo dobra). Se ficar parada, o clipe nao
            // esta' chegando no corpo — e o nome do estado no registro mente.
            float cy = 0f, cz = 0f;
            if (cabecaDoOrc != null)
            {
                var local = duelo.doOrc.transform.InverseTransformPoint(cabecaDoOrc.position);
                cy = local.y; cz = local.z;
            }

            // O NOME DO ESTADO NAO PROVA NADA. Isto aqui prova: qual clipe o Animator esta'
            // realmente aplicando neste quadro, e com que peso. Os clipes tem duracoes
            // diferentes (parado 1,33 / golpe 1,10 / reacoes 1,70 1,67 1,90), entao a duracao
            // identifica qual e'.
            string clipe = "-";
            if (duelo.doOrc != null)
            {
                var info = duelo.doOrc.GetCurrentAnimatorClipInfo(0);
                if (info.Length > 0 && info[0].clip != null)
                    clipe = string.Format("{0:F2}s x{1:F2}", info[0].clip.length, info[0].weight);
            }

            registro.Add(string.Format("  {0,5:F2}  {1,-12}  {2,-12}  {3,6:F2}  {4,7:F3} {5,7:F3}  {6,-15}  {7}",
                t, eh, eo, dist, cy, cz, clipe, evento));

            if (gravar) Gravar();
            if (proximo >= ROTEIRO.Length) { rodando = false; Despejar(); }
        }

        static readonly string[] NOMES_DO_HEROI = {
            "Locomocao", "PuloSaida", "PuloNoAr", "PuloQueda", "Esquiva", "Rolar", "Arranco",
            "Golpe1", "Golpe2", "Golpe3", "Atingido" };

        static string NomeDoEstado(Animator a, string[] nomes)
        {
            if (a == null) return "-";
            var e = a.GetCurrentAnimatorStateInfo(0);
            for (int i = 0; i < nomes.Length; i++) if (e.IsName(nomes[i])) return nomes[i];
            return "?";
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
            sb.AppendLine("===== TESTE DE COMBATE =====");
            foreach (var l in registro) sb.AppendLine(l);
            sb.AppendLine(string.Format("acertou {0}   passou longe {1}",
                duelo.GolpesQueAcertaram, duelo.GolpesQuePassaramLonge));
            sb.AppendLine("===== FIM =====");
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Application.dataPath, "../teste-de-combate.txt"), sb.ToString());
            }
            catch (System.Exception e) { Debug.LogWarning("[Teste] " + e.Message); }
            Debug.Log("[Teste] combate pronto: " + contador + " quadros");
#if UNITY_EDITOR
            if (sairAoTerminar) UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
