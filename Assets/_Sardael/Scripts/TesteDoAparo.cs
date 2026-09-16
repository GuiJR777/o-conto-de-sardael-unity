using System.Collections;
using System.Text;
using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Manda o orc golpear e aperta o aparo em instantes escolhidos, pra ver quais valem.
    ///
    /// O aparo entra pela MESMA variavel que a tecla K do jogador
    /// (<see cref="Duelo.InjetarAparo"/>), nunca por um atalho: se este teste passa, e' porque o
    /// caminho do jogador passa.
    ///
    /// A PRIMEIRA COISA E' MEDIR O TELEGRAFO — quanto tempo existe entre o orc comecar o golpe e
    /// o golpe chegar. Esse numero nao esta' escrito em lugar nenhum em segundos: e' a fracao
    /// 0,3222 (medida, gravada no AvisoNoImpacto) vezes a duracao do estado, que so' existe em
    /// Play. E' ele que decide se a janela do aparo e' justa ou decorativa, entao vai no alto do
    /// relatorio. Se a medicao falhar, o teste PARA: numero inventado nao prova nada.
    ///
    /// OS CASOS, E O QUE CADA UM EXISTE PRA IMPEDIR:
    ///   1. sem aparo           -> o heroi apanha. E' o controle; sem ele nada abaixo significa nada.
    ///   2. aparo no tempo      -> nao apanha, a animacao roda, e o heroi SAI dela depois.
    ///   3. aparo cedo demais   -> apanha. E erra a janela por 0,15 s cronometrados, nao por um
    ///                             segundo inteiro: uma janela tres vezes maior que a declarada
    ///                             tem que REPROVAR aqui.
    ///   4. tamborilar a tecla  -> apanha. Um aparo que vale so' pelo relogio daria
    ///                             invulnerabilidade a quem martela K, com o heroi andando de
    ///                             bracos baixos. Este caso existe pra esse exploit nao nascer.
    ///   5. K depois de apanhar -> o heroi NAO sai do tranco. A transicao do aparo vem de
    ///                             AnyState e nao respeita estado nenhum; sem a porteira do
    ///                             Duelo, K seria o botao de cancelar o castigo.
    ///
    /// O caso 2 e' o unico que prova o aparo. Os outros quatro existem pra provar que ele nao
    /// passou por acidente — um aparo que vale sempre nao e' aparo, e' invulnerabilidade.
    /// </summary>
    public class TesteDoAparo : MonoBehaviour
    {
        public const string BANDEIRA = "sardael.teste.aparo";

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Nascer()
        {
            if (!UnityEditor.SessionState.GetBool(BANDEIRA, false)) return;
            UnityEditor.SessionState.SetBool(BANDEIRA, false);
            var go = new GameObject("TesteDoAparo");
            go.AddComponent<TesteDoAparo>();
        }
#endif

        /// <summary>Distancia em que o teste para de andar, com folga dentro do alcance do orc.</summary>
        const float PERTO = 1.9f;
        /// <summary>Teto de quadros de qualquer laco. Nenhum laco deste teste pode pendurar o Unity.</summary>
        const int TETO = 900;

        Duelo duelo;
        MovimentoDoHeroi heroi;
        Animator doHeroi, doOrc;

        string arquivo, pasta;
        readonly StringBuilder relatorio = new StringBuilder();
        int falhas, foto;
        float telegrafo = -1f;

        IEnumerator Start()
        {
            arquivo = Application.dataPath + "/../_teste_aparo.txt";
            pasta = Application.dataPath + "/../gravacao_aparo";
            try
            {
                if (System.IO.Directory.Exists(pasta))
                    foreach (var f in System.IO.Directory.GetFiles(pasta, "*.png")) System.IO.File.Delete(f);
                else System.IO.Directory.CreateDirectory(pasta);
            }
            catch { }

            // o relogio ja' ficou gravado em 0 no disco deste projeto uma vez. Se estiver assim
            // agora, TODOS os lacos daqui seriam infinitos e o teste penduraria sem relatorio.
            if (Time.timeScale <= 0f) Time.timeScale = 1f;

            duelo = Object.FindAnyObjectByType<Duelo>();
            if (duelo == null) { Anotar("achar o Duelo na cena", false, "rode antes: Sardael > Montar Duelo"); Despejar(); yield break; }
            heroi   = duelo.GetComponent<MovimentoDoHeroi>();
            doHeroi = duelo.doHeroi;
            doOrc   = duelo.doOrc;
            if (heroi == null || doHeroi == null || doOrc == null)
            {
                Anotar("achar heroi, animador e orc", false,
                       "heroi=" + (heroi != null) + " doHeroi=" + (doHeroi != null) + " doOrc=" + (doOrc != null));
                Despejar(); yield break;
            }

            duelo.orcAtacaSozinho = false;      // quem manda o orc atacar neste teste sou eu
            MovimentoDoHeroi.SoInjecao = true;  // teclado fisico fora: so' vale o que eu injeto
            yield return Esperar(0.6f);

            // ---------------------------------------------------------- o aparo existe mesmo?
            bool temEstado = doHeroi.HasState(0, Animator.StringToHash("Aparo"))
                          || doHeroi.HasState(0, Animator.StringToHash("Base Layer.Aparo"));
            Anotar("o controller do heroi tem o estado Aparo", temEstado,
                   "sem ele a tecla K nao tem pra onde ir; rode Sardael > Montar Aparo");

            // esta e' a checagem que separa APARO de INVULNERABILIDADE: sem o parametro, o
            // SetTrigger so' reclama no console e o jogo fica 0,30 s imune numa tecla que nao
            // anima nada. O teste tem que gritar, nao seguir em frente.
            bool temGatilho = false;
            foreach (var p in doHeroi.parameters)
                if (p.name == Duelo.P_APARAR && p.type == AnimatorControllerParameterType.Trigger) temGatilho = true;
            Anotar("o controller do heroi tem o gatilho \"" + Duelo.P_APARAR + "\"", temGatilho,
                   "sem ele o SetTrigger falha calado e o aparo vira invulnerabilidade sem animacao");

            int achei;
            bool emLoop = ClipeDoAparoEmLoop(out achei);
            Anotar("achei o clipe do aparo no controller", achei == 1,
                   achei == 0 ? "nenhum clipe com \"Parry\" no nome" : achei + " clipes com \"Parry\"");
            if (achei == 1)
                Anotar("o clipe do aparo nao esta' em loop", !emLoop,
                       "clipe em loop nunca termina, e a saida do estado depende de terminar");

            if (!temEstado || !temGatilho) { Despejar(); yield break; }

            // ---------------------------------------------------------- 1. controle, e a medida
            yield return Aproximar();
            yield return UmGolpe("1. sem aparo (controle)", -1f, true, false);

            if (telegrafo <= 0f)
            {
                Anotar("medir o telegrafo do orc", false,
                       "sem esse numero os casos 2, 3 e 4 seriam apertados na hora errada. Parando aqui "
                     + "em vez de inventar um valor.");
                Despejar(); yield break;
            }
            Anotar("medi o telegrafo do orc", true,
                   telegrafo.ToString("F3") + " s entre o golpe comecar e chegar");

            // ---------------------------------------------------------- 2. aparo no tempo
            // 0,12 s antes do impacto: dentro da janela de 0,30 com sobra dos dois lados
            yield return Aproximar();
            yield return UmGolpe("2. aparo no tempo", Mathf.Max(0f, telegrafo - 0.12f), false, true);

            // ---------------------------------------------------------- 3. cedo demais, medido
            yield return Aproximar();
            yield return CedoDemais();

            // ---------------------------------------------------------- 4. tamborilar a tecla
            yield return Aproximar();
            yield return Tamborilar();

            // ---------------------------------------------------------- 5. cancelar o tranco
            yield return Aproximar();
            yield return CancelarOTranco();

            Despejar();
        }

        // ------------------------------------------------------------------ um golpe do orc

        /// <summary>
        /// Manda o orc golpear. Se <paramref name="quandoApertar"/> for &gt;= 0, aperta o aparo
        /// tantos segundos depois da ordem.
        /// </summary>
        IEnumerator UmGolpe(string nome, float quandoApertar, bool temQueApanhar, bool temQueAparar)
        {
            int certosAntes = duelo.AparosCertos;
            bool apanhou = false, aparouNaTela = false, voltou = false, apertei = false;
            bool queriaApertar = quandoApertar >= 0f;

            duelo.MandarOrcAtacar();
            float comecou = Time.time;
            int quadros = TETO;

            while (Time.time - comecou < 1.8f && quadros-- > 0)
            {
                MedirTelegrafo();

                if (quandoApertar >= 0f && Time.time - comecou >= quandoApertar)
                {
                    duelo.InjetarAparo();
                    apertei = true;
                    quandoApertar = -1f;                        // uma vez so'
                }

                if (duelo.Aparando) aparouNaTela = true;
                if (doHeroi.GetCurrentAnimatorStateInfo(0).IsName("Atingido")) apanhou = true;
                if (aparouNaTela && doHeroi.GetCurrentAnimatorStateInfo(0).IsName("Locomocao")) voltou = true;
                yield return null;
            }

            bool aparou = duelo.AparosCertos > certosAntes;
            string detalhe = Retrato(apertei, aparou, aparouNaTela, apanhou);

            if (queriaApertar)
                Anotar(nome + ": a tecla foi mesmo apertada", apertei,
                       "sem isto o caso passaria sem encostar no aparo");

            if (temQueApanhar)
            {
                Anotar(nome + ": o heroi apanha", apanhou, detalhe);
                Anotar(nome + ": o aparo NAO conta", !aparou, detalhe);
            }
            if (temQueAparar)
            {
                Anotar(nome + ": o heroi NAO apanha", !apanhou, detalhe);
                Anotar(nome + ": o aparo conta", aparou, detalhe);
                Anotar(nome + ": a animacao de aparo rodou", aparouNaTela, detalhe);
                Anotar(nome + ": e o heroi SAI dela sozinho", voltou,
                       "se o clipe estivesse em loop ele ficaria preso na guarda. " + detalhe);
            }
            yield return Foto(nome);
        }

        /// <summary>
        /// Aperta o aparo de forma que o impacto caia EXATAMENTE janela+0,15 s depois do pedido.
        /// Assim o caso reprova qualquer janela maior que a declarada, e nao so' janelas absurdas.
        /// </summary>
        IEnumerator CedoDemais()
        {
            float erroDeProposito = duelo.janelaDoAparo + 0.15f;
            int certosAntes = duelo.AparosCertos, cedoAntes = duelo.AparosCedoDemais;
            bool apanhou = false;

            if (telegrafo > erroDeProposito)
            {
                // da' pra errar POR DENTRO do golpe: aperta cedo, ainda no comeco do ataque
                yield return UmGolpe("3. aparo cedo demais (erra a janela por 0,15 s)",
                                     telegrafo - erroDeProposito, true, false);
                yield break;
            }

            // a janela e' quase do tamanho do telegrafo: pra errar, aperta ANTES do golpe sair
            float antes = erroDeProposito - telegrafo;
            duelo.InjetarAparo();
            yield return Esperar(antes);

            duelo.MandarOrcAtacar();
            float comecou = Time.time;
            int quadros = TETO;
            while (Time.time - comecou < 1.8f && quadros-- > 0)
            {
                if (doHeroi.GetCurrentAnimatorStateInfo(0).IsName("Atingido")) apanhou = true;
                yield return null;
            }

            bool aparou = duelo.AparosCertos > certosAntes;
            string detalhe = "apertei " + erroDeProposito.ToString("F2") + " s antes do impacto "
                           + "(janela " + duelo.janelaDoAparo.ToString("F2") + " s)  |  " + duelo.UltimoEvento;
            Anotar("3. aparo cedo demais: o heroi apanha", apanhou, detalhe);
            Anotar("3. aparo cedo demais: o aparo NAO conta", !aparou, detalhe);
            Anotar("3. aparo cedo demais: contou como cedo", duelo.AparosCedoDemais > cedoAntes, detalhe);
            yield return Foto("3. cedo demais");
        }

        /// <summary>
        /// Martela K sem parar. Se o aparo valesse so' pelo relogio, isto daria invulnerabilidade
        /// com o heroi andando de bracos baixos: bastaria recarimbar o instante a cada toque.
        /// </summary>
        IEnumerator Tamborilar()
        {
            int certosAntes = duelo.AparosCertos, recusadosAntes = duelo.AparosRecusados;
            bool apanhou = false;

            // um segundo inteiro tamborilando ANTES do golpe sair
            float t0 = Time.time;
            int quadros = TETO;
            float proximo = 0f;
            while (Time.time - t0 < 1.0f && quadros-- > 0)
            {
                if (Time.time - t0 >= proximo) { duelo.InjetarAparo(); proximo += 0.2f; }
                yield return null;
            }

            duelo.MandarOrcAtacar();
            float comecou = Time.time;
            quadros = TETO;
            proximo = 0f;
            while (Time.time - comecou < 1.8f && quadros-- > 0)
            {
                if (Time.time - comecou >= proximo) { duelo.InjetarAparo(); proximo += 0.2f; }
                if (doHeroi.GetCurrentAnimatorStateInfo(0).IsName("Atingido")) apanhou = true;
                yield return null;
            }

            bool aparou = duelo.AparosCertos > certosAntes;
            string detalhe = "recusados nesta rodada: " + (duelo.AparosRecusados - recusadosAntes)
                           + "  |  " + duelo.UltimoEvento;
            Anotar("4. tamborilar K: o heroi apanha assim mesmo", apanhou, detalhe);
            Anotar("4. tamborilar K: nao vira invulnerabilidade", !aparou, detalhe);
            Anotar("4. tamborilar K: a porteira recusou os toques de dentro da acao",
                   duelo.AparosRecusados > recusadosAntes,
                   "se nenhum foi recusado, a guarda do Duelo nao esta' funcionando. " + detalhe);
            yield return Foto("4. tamborilar");
        }

        /// <summary>
        /// Apanha primeiro, aperta K depois. O heroi NAO pode ser arrancado do tranco: a
        /// transicao do aparo vem de AnyState com hasExitTime 0 e passaria por cima do Atingido.
        /// </summary>
        IEnumerator CancelarOTranco()
        {
            bool apanhou = false, saiuDoTranco = false, entrouNoAparo = false;
            int recusadosAntes = duelo.AparosRecusados;

            duelo.MandarOrcAtacar();
            float comecou = Time.time;
            int quadros = TETO;
            float apertouEm = -1f;

            while (Time.time - comecou < 2.2f && quadros-- > 0)
            {
                bool noTranco = doHeroi.GetCurrentAnimatorStateInfo(0).IsName("Atingido");
                if (noTranco) apanhou = true;

                // assim que ele apanha, tenta cancelar
                if (apanhou && apertouEm < 0f) { duelo.InjetarAparo(); apertouEm = Time.time; }

                if (apertouEm > 0f && Time.time - apertouEm > 0.05f)
                {
                    if (duelo.Aparando) entrouNoAparo = true;
                    if (apanhou && !noTranco && Time.time - apertouEm < 0.25f) saiuDoTranco = true;
                }
                yield return null;
            }

            string detalhe = "apanhou=" + apanhou + " entrouNoAparo=" + entrouNoAparo
                           + " recusados nesta rodada: " + (duelo.AparosRecusados - recusadosAntes);
            Anotar("5. cancelar o tranco: o heroi apanhou primeiro", apanhou, detalhe);
            Anotar("5. cancelar o tranco: K NAO tira o heroi do Atingido", !entrouNoAparo && !saiuDoTranco,
                   "se entrar no aparo aqui, K virou botao de cancelar o castigo. " + detalhe);
            Anotar("5. cancelar o tranco: a porteira recusou o toque", duelo.AparosRecusados > recusadosAntes,
                   detalhe);
            yield return Foto("5. cancelar o tranco");
        }

        // ------------------------------------------------------------------ medir e chegar perto

        /// <summary>
        /// Le' o telegrafo do orc: a fracao medida (AvisoNoImpacto.quando) vezes a duracao do
        /// estado. So' marca como medido depois de conseguir — se a leitura falhar num quadro,
        /// tenta no proximo.
        ///
        /// Cuidado conhecido: GetBehaviour varre o controller inteiro e devolve o PRIMEIRO
        /// AvisoNoImpacto. Hoje o Orc_Combate tem exatamente um; se um dia tiver dois, este
        /// numero passa a sair do estado errado, calado.
        /// </summary>
        void MedirTelegrafo()
        {
            if (telegrafo > 0f) return;
            var info = doOrc.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName("Golpe")) return;
            // pelo AVISO, nao pelo primeiro que aparecer: GetBehaviour<T> varre o controller
            // inteiro e devolve o primeiro. No dia em que o orc ganhar um segundo AvisoNoImpacto
            // — por exemplo um que anuncie o COMECO do golpe — o telegrafo passaria a ser medido
            // com a fracao do estado errado, calado.
            AvisoNoImpacto aviso = null;
            foreach (var a in doOrc.GetBehaviours<AvisoNoImpacto>())
                if (a != null && a.aviso == "impactoDoOrc") aviso = a;
            if (aviso == null || info.length <= 0f) return;
            telegrafo = aviso.quando * info.length;
        }

        /// <summary>Anda ate' o orc pelo mesmo eixo que o teclado usa.</summary>
        IEnumerator Aproximar()
        {
            int quadros = TETO;
            while (quadros-- > 0)
            {
                float d = doOrc.transform.position.x - duelo.transform.position.x;
                if (Mathf.Abs(d) <= PERTO) break;
                heroi.InjetarEixo(d > 0f ? 1f : -1f);
                yield return null;
            }
            heroi.InjetarEixo(0f);
            yield return Esperar(0.5f);         // deixa a desaceleracao acabar

            float agora = Mathf.Abs(doOrc.transform.position.x - duelo.transform.position.x);
            if (agora > Duelo.ALCANCE + duelo.folga)
                Anotar("chegar perto do orc", false,
                       "parei a " + agora.ToString("F2") + " m e o golpe do orc so' alcanca "
                     + (Duelo.ALCANCE + duelo.folga).ToString("F2") + " m — o caso a seguir nao vale");
        }

        /// <summary>Quantos clipes com "Parry" no nome o controller do heroi carrega, e se o unico esta' em loop.</summary>
        bool ClipeDoAparoEmLoop(out int quantos)
        {
            quantos = 0;
            bool loop = false;
            foreach (var c in doHeroi.runtimeAnimatorController.animationClips)
                if (c != null && c.name.Contains("Parry")) { quantos++; loop = c.isLooping; }
            return loop;
        }

        // ------------------------------------------------------------------ o que fica gravado

        static string Retrato(bool apertei, bool aparou, bool naTela, bool apanhou)
        {
            return "apertei=" + apertei + " aparou=" + aparou
                 + " animacaoDeAparo=" + naTela + " apanhou=" + apanhou;
        }

        static IEnumerator Esperar(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Foto(string nome)
        {
            yield return new WaitForEndOfFrame();
            Texture2D t = null;
            try
            {
                t = ScreenCapture.CaptureScreenshotAsTexture();
                string limpo = nome.Replace(" ", "_").Replace(".", "").Replace(",", "").Replace(":", "");
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(pasta, foto.ToString("D2") + "_" + limpo + ".png"),
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
            if (!string.IsNullOrEmpty(detalhe)) relatorio.Append("\n             ").Append(detalhe);
            relatorio.Append('\n');
        }

        void Despejar()
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== TESTE DO APARO =====");
            sb.AppendLine("Unity " + Application.unityVersion + "   " + System.DateTime.Now);
            sb.AppendLine();
            sb.AppendLine("OS NUMEROS");
            sb.AppendLine("  telegrafo do orc (golpe comeca -> golpe chega): "
                        + (telegrafo > 0f ? telegrafo.ToString("F3") + " s   MEDIDO" : "nao medido"));
            sb.AppendLine("  janela do aparo: "
                        + (duelo != null ? duelo.janelaDoAparo.ToString("F2") + " s   ESCOLHIDO" : "?"));
            if (telegrafo > 0f && duelo != null)
            {
                float cobre = Mathf.Min(duelo.janelaDoAparo, telegrafo) / telegrafo * 100f;
                sb.AppendLine("  a janela cobre " + cobre.ToString("F0") + "% do telegrafo — sobram "
                            + Mathf.Max(0f, telegrafo - duelo.janelaDoAparo).ToString("F3")
                            + " s em que apertar ainda e' cedo demais");
                sb.AppendLine("  reacao humana a estimulo visual fica perto de 0,25 s: compare com o telegrafo");
            }
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
