using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Em que ponto da historia o jogador esta'.
    ///
    /// Mora no disco, e nao numa variavel estatica, por um motivo pratico: a cena de Halu e'
    /// carregada duas vezes — antes e depois do tutorial — e precisa saber qual das duas vezes
    /// e' esta. Estatico ja' resolveria dentro de uma sessao, mas morre quando o jogo fecha.
    ///
    /// ONDE no disco depende de haver partida escolhida. Com pergaminho escolhido no menu, vai
    /// pra ficha daquela partida (<see cref="Salvao"/>) — e' o que faz os tres progressos nao
    /// se misturarem. Sem pergaminho, que e' o caso de abrir uma cena direto no editor pra
    /// testar, cai no <see cref="PlayerPrefs"/> como sempre foi: teste solto nao suja save de
    /// partida, e o fluxo continua de onde parou entre um Play e outro.
    ///
    /// Os numeros sao espacados de 10 pra caber etapa nova no meio sem renumerar o que ja'
    /// esta' salvo no disco de quem esta' testando.
    /// </summary>
    public static class Progresso
    {
        public const string CHAVE = "sardael.progresso";

        public enum Etapa
        {
            Comeco = 0,               // nunca falou com ninguem
            FalouComOChefe = 10,      // recebeu a missao da fazenda
            VoltouDoTutorial = 20,    // o tutorial acabou, o chefe manda pra Bael
            IndoParaBael = 30,        // a segunda missao ja' foi dada
            MorreuEmBael = 40,        // o orc da floresta o matou; o Veu esta' com ele
            VoltouDoVeu = 50,         // acordou em Halu achando que sonhou, e foi mandado de novo
        }

        /// <summary>
        /// O laco do Veu: 50 -> (entra na floresta e morre) -> 40 -> (acorda diante do chefe) -> 50.
        ///
        /// Ele anda pra tras de proposito. Enquanto o combate nao existir, a floresta mata
        /// sempre, e o unico jeito de a Halu saber que ela tem de abrir com a conversa do sonho
        /// e' esse numero voltar pra 40. Quem le' ordem (<see cref="Passou"/>) nao se incomoda:
        /// 40 e 50 sao ambos depois do tutorial, entao os portoes continuam certos.
        /// </summary>
        public static bool NoLacoDoVeu { get { return Onde == Etapa.MorreuEmBael || Onde == Etapa.VoltouDoVeu; } }

        public static Etapa Onde
        {
            get
            {
                if (Salvao.TemPartida) return (Etapa)Salvao.Ficha.etapa;
                return (Etapa)PlayerPrefs.GetInt(CHAVE, 0);
            }
            set
            {
                if (Salvao.TemPartida)
                {
                    Salvao.Ficha.etapa = (int)value;
                    Salvao.Gravar();                       // avanco de historia e' ponto de salvar
                    return;
                }
                PlayerPrefs.SetInt(CHAVE, (int)value); PlayerPrefs.Save();
            }
        }

        public static bool Passou(Etapa e) { return (int)Onde >= (int)e; }

        /// <summary>Volta tudo ao comeco. E' o que o teste precisa pra repetir o fluxo.</summary>
        public static void Zerar() { Onde = Etapa.Comeco; }
    }
}
