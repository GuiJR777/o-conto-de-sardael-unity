using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sardael
{
    /// <summary>O que fica guardado de uma partida.</summary>
    [Serializable]
    public class FichaDeJogo
    {
        public bool existe;
        public int etapa;             // vale o mesmo que Progresso.Etapa
        public string cena = "";      // onde o jogador parou
        public double segundos;       // tempo de jogo somado
        public int mortesNoVeu;       // quantas vezes o orc de Bael ja' o matou
        public string salvoEm = "";
        public string criadoEm = "";
    }

    /// <summary>
    /// Os tres pergaminhos: as tres partidas que o jogo guarda.
    ///
    /// Cada uma e' um arquivo JSON em <c>Application.persistentDataPath</c>, e nao um punhado
    /// de chaves no PlayerPrefs. Motivo: o jogador pode APAGAR uma partida, e apagar um arquivo
    /// e' uma linha; apagar meia duzia de chaves espalhadas e' o tipo de coisa que deixa lixo
    /// pra tras e faz a partida "apagada" voltar do tumulo com metade dos dados.
    ///
    /// A gravacao e' automatica e acontece em dois momentos: quando a historia avanca
    /// (<see cref="Progresso.Onde"/>) e quando entra uma cena nova. Nao ha' botao de salvar
    /// porque o jogo nao tem ponto de salvar — quem sai no meio volta no comeco da cena em
    /// que estava, que e' o comportamento que o dono pediu: "sair e o progresso fica".
    ///
    /// <b>Slot -1</b> quer dizer "ninguem escolheu pergaminho": e' o que acontece quando se
    /// abre Halu direto no editor pra testar. Nesse caso o progresso vai pro PlayerPrefs, como
    /// era antes do menu existir, e nenhum arquivo de partida e' tocado. Teste de cena solta
    /// nao suja o save de ninguem.
    /// </summary>
    public static class Salvao
    {
        public const int QUANTOS = 3;
        public const string CHAVE_SLOT = "sardael.slot";
        public const string CENA_DO_MENU = "Menu_Principal";
        public const string CENA_INICIAL = "Halu";

        static int slot = -1;
        static FichaDeJogo ficha;
        static float marca;               // realtimeSinceStartup da ultima contagem
        static bool ouvindo;

        // Sem Domain Reload o estatico atravessa partidas. Limpa antes da cena carregar.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoComecarAPartida()
        {
            slot = -1; ficha = null; ouvindo = false; marca = 0f;
        }

        public static int SlotAtual { get { return slot; } }
        public static bool TemPartida { get { return slot >= 0; } }

        /// <summary>A ficha da partida em curso. Fora de partida, uma de rascunho.</summary>
        public static FichaDeJogo Ficha
        {
            get
            {
                if (ficha == null) ficha = new FichaDeJogo();
                return ficha;
            }
        }

        public static string Caminho(int qual)
        {
            return Path.Combine(Application.persistentDataPath, "sardael_partida_" + qual + ".json");
        }

        /// <summary>Le a ficha do disco sem entrar na partida. E' o que o menu mostra nos cartoes.</summary>
        public static FichaDeJogo Ler(int qual)
        {
            try
            {
                string p = Caminho(qual);
                if (!File.Exists(p)) return new FichaDeJogo();
                var f = JsonUtility.FromJson<FichaDeJogo>(File.ReadAllText(p));
                return f ?? new FichaDeJogo();
            }
            catch (Exception e)
            {
                // arquivo corrompido nao pode derrubar o menu; o cartao aparece vazio
                Debug.LogWarning("Salvao: nao consegui ler a partida " + qual + ": " + e.Message);
                return new FichaDeJogo();
            }
        }

        public static void Apagar(int qual)
        {
            try { if (File.Exists(Caminho(qual))) File.Delete(Caminho(qual)); }
            catch (Exception e) { Debug.LogWarning("Salvao: nao consegui apagar: " + e.Message); }

            if (slot == qual) { slot = -1; ficha = null; PlayerPrefs.DeleteKey(CHAVE_SLOT); }
        }

        /// <summary>Comeca do zero neste pergaminho. Sobrescreve o que houver.</summary>
        public static void ComecarNovo(int qual)
        {
            slot = qual;
            ficha = new FichaDeJogo
            {
                existe = true,
                etapa = (int)Progresso.Etapa.Comeco,
                cena = CENA_INICIAL,
                segundos = 0.0,
                criadoEm = Agora()
            };
            marca = Time.realtimeSinceStartup;
            PlayerPrefs.SetInt(CHAVE_SLOT, qual); PlayerPrefs.Save();
            Ouvir();
            Gravar();
        }

        /// <summary>Retoma um pergaminho gravado. Devolve a cena onde o jogador parou.</summary>
        public static string Continuar(int qual)
        {
            var f = Ler(qual);
            if (!f.existe) { ComecarNovo(qual); return CENA_INICIAL; }

            slot = qual;
            ficha = f;
            marca = Time.realtimeSinceStartup;
            PlayerPrefs.SetInt(CHAVE_SLOT, qual); PlayerPrefs.Save();
            Ouvir();
            return string.IsNullOrEmpty(f.cena) ? CENA_INICIAL : f.cena;
        }

        /// <summary>Sai da partida sem apagar nada. O menu volta a nao ter dono.</summary>
        public static void Largar()
        {
            Gravar();
            slot = -1;
            ficha = null;
        }

        public static void Gravar()
        {
            if (slot < 0 || ficha == null) return;       // teste de cena solta nao grava arquivo

            float agora = Time.realtimeSinceStartup;
            if (marca > 0f && agora > marca) ficha.segundos += agora - marca;
            marca = agora;

            ficha.existe = true;
            ficha.salvoEm = Agora();
            try { File.WriteAllText(Caminho(slot), JsonUtility.ToJson(ficha, true)); }
            catch (Exception e) { Debug.LogWarning("Salvao: nao consegui gravar: " + e.Message); }
        }

        static void Ouvir()
        {
            if (ouvindo) return;
            ouvindo = true;
            // com o Domain Reload desligado a assinatura da partida ANTERIOR pode ter
            // sobrevivido; tira antes de por, senao acumula uma a cada Play
            SceneManager.sceneLoaded -= AoCarregarCena;
            SceneManager.sceneLoaded += AoCarregarCena;
        }

        static void AoCarregarCena(Scene cena, LoadSceneMode modo)
        {
            if (slot < 0 || ficha == null) return;
            if (cena.name == CENA_DO_MENU) return;       // o menu nao e' lugar de voltar
            ficha.cena = cena.name;
            Gravar();
        }

        static string Agora() { return DateTime.Now.ToString("dd/MM/yyyy HH:mm"); }

        /// <summary>"2 h 14 min" — do jeito que cabe no cartao.</summary>
        public static string TempoEmTexto(double segundos)
        {
            int t = Mathf.Max(0, Mathf.RoundToInt((float)segundos));
            int h = t / 3600, m = (t % 3600) / 60;
            if (h > 0) return h + " h " + m + " min";
            if (m > 0) return m + " min";
            return "recem-comecado";
        }

        /// <summary>Onde o jogador esta' na historia, em palavras.</summary>
        public static string EtapaEmTexto(int etapa)
        {
            if (etapa >= (int)Progresso.Etapa.VoltouDoVeu)       return "de novo a caminho de Bael";
            if (etapa >= (int)Progresso.Etapa.MorreuEmBael)      return "nas mãos do Véu";
            if (etapa >= (int)Progresso.Etapa.IndoParaBael)      return "a caminho da Floresta de Bael";
            if (etapa >= (int)Progresso.Etapa.VoltouDoTutorial)  return "de volta a Halu";
            if (etapa >= (int)Progresso.Etapa.FalouComOChefe)    return "rumo a fazenda do leste";
            return "o comeco, em Halu";
        }

        /// <summary>Conta mais uma morte no Veu. E' o que diferencia a primeira vez das outras.</summary>
        public static int ContarMorteNoVeu()
        {
            if (slot < 0 || ficha == null) return 1;      // teste de cena solta: sempre a primeira
            ficha.mortesNoVeu++;
            Gravar();
            return ficha.mortesNoVeu;
        }

        public static int MortesNoVeu { get { return ficha != null ? ficha.mortesNoVeu : 0; } }
    }
}
