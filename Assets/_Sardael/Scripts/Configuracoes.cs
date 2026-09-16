using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// As preferencias da maquina: volume, tela e qualidade.
    ///
    /// Vao pro <see cref="PlayerPrefs"/> e NAO pra ficha da partida, de proposito. Isto e' do
    /// computador, nao da historia: quem apaga um pergaminho nao quer o volume de volta no
    /// maximo, e quem joga os tres pergaminhos quer a mesma resolucao nos tres.
    ///
    /// <see cref="Aplicar"/> roda sozinha quando o jogo comeca, antes de qualquer cena. Sem
    /// isso a configuracao so' valeria depois de alguem abrir a tela de opcoes uma vez.
    ///
    /// Sobre os volumes: hoje so' o GERAL tem efeito, porque o jogo ainda nao tem som nenhum.
    /// Musica e efeitos ja' ficam guardados e expostos aqui pra que, quando o primeiro
    /// AudioSource entrar, ele so' precise multiplicar pelo canal — e a tela de opcoes nao
    /// precise ser refeita.
    /// </summary>
    public static class Configuracoes
    {
        public enum Canal { Geral, Musica, Efeitos }

        const string V_GERAL   = "sardael.vol.geral";
        const string V_MUSICA  = "sardael.vol.musica";
        const string V_EFEITOS = "sardael.vol.efeitos";
        const string TELA_CHEIA = "sardael.telacheia";
        const string LARGURA    = "sardael.largura";
        const string ALTURA     = "sardael.altura";
        const string QUALIDADE  = "sardael.qualidade";

        /// <summary>Avisa quem estiver tocando som que o volume mudou.</summary>
        public static event System.Action Mudou;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Aplicar()
        {
            AudioListener.volume = VolumeGeral;
            int q = PlayerPrefs.GetInt(QUALIDADE, -1);
            if (q >= 0 && q < QualitySettings.names.Length) QualitySettings.SetQualityLevel(q, true);

            int l = PlayerPrefs.GetInt(LARGURA, 0), a = PlayerPrefs.GetInt(ALTURA, 0);
            bool cheia = PlayerPrefs.GetInt(TELA_CHEIA, Screen.fullScreen ? 1 : 0) == 1;
            if (l > 0 && a > 0) Screen.SetResolution(l, a, cheia);
            else Screen.fullScreen = cheia;

            if (Mudou != null) Mudou();
        }

        public static float VolumeGeral
        {
            get { return PlayerPrefs.GetFloat(V_GERAL, 0.8f); }
            set
            {
                PlayerPrefs.SetFloat(V_GERAL, Mathf.Clamp01(value));
                AudioListener.volume = Mathf.Clamp01(value);
                Avisar();
            }
        }

        public static float VolumeDaMusica
        {
            get { return PlayerPrefs.GetFloat(V_MUSICA, 0.7f); }
            set { PlayerPrefs.SetFloat(V_MUSICA, Mathf.Clamp01(value)); Avisar(); }
        }

        public static float VolumeDosEfeitos
        {
            get { return PlayerPrefs.GetFloat(V_EFEITOS, 0.9f); }
            set { PlayerPrefs.SetFloat(V_EFEITOS, Mathf.Clamp01(value)); Avisar(); }
        }

        /// <summary>Quanto um som deste canal deve tocar. Use ao ligar um AudioSource.</summary>
        public static float Volume(Canal canal)
        {
            if (canal == Canal.Musica) return VolumeDaMusica;
            if (canal == Canal.Efeitos) return VolumeDosEfeitos;
            return VolumeGeral;
        }

        public static bool TelaCheia
        {
            get { return PlayerPrefs.GetInt(TELA_CHEIA, Screen.fullScreen ? 1 : 0) == 1; }
            set
            {
                PlayerPrefs.SetInt(TELA_CHEIA, value ? 1 : 0);
                Screen.fullScreen = value;
                PlayerPrefs.Save();
            }
        }

        public static int Qualidade
        {
            get { return PlayerPrefs.GetInt(QUALIDADE, QualitySettings.GetQualityLevel()); }
            set
            {
                int q = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
                PlayerPrefs.SetInt(QUALIDADE, q);
                QualitySettings.SetQualityLevel(q, true);
                PlayerPrefs.Save();
            }
        }

        /// <summary>As resolucoes do monitor, sem repetir tamanho por causa da taxa de tela.</summary>
        public static Vector2Int[] Resolucoes()
        {
            var cruas = Screen.resolutions;
            var lista = new System.Collections.Generic.List<Vector2Int>();
            for (int i = 0; i < cruas.Length; i++)
            {
                var v = new Vector2Int(cruas[i].width, cruas[i].height);
                if (!lista.Contains(v)) lista.Add(v);
            }
            if (lista.Count == 0) lista.Add(new Vector2Int(Screen.width, Screen.height));
            return lista.ToArray();
        }

        public static Vector2Int Resolucao
        {
            get
            {
                int l = PlayerPrefs.GetInt(LARGURA, 0), a = PlayerPrefs.GetInt(ALTURA, 0);
                if (l > 0 && a > 0) return new Vector2Int(l, a);
                return new Vector2Int(Screen.width, Screen.height);
            }
            set
            {
                PlayerPrefs.SetInt(LARGURA, value.x);
                PlayerPrefs.SetInt(ALTURA, value.y);
                PlayerPrefs.Save();
                Screen.SetResolution(value.x, value.y, TelaCheia);
            }
        }

        static void Avisar()
        {
            PlayerPrefs.Save();
            if (Mudou != null) Mudou();
        }
    }
}
