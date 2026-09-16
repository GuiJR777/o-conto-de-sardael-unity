using UnityEditor;
using UnityEditor.SceneManagement;
using Sardael;

namespace SardaelEditor
{
    /// <summary>
    /// Abre o menu principal, entra em Play e deixa o <see cref="TesteDaPausa"/> jogar sozinho.
    ///
    /// Comeca no Menu_Principal de proposito: o bug da pausa so' aparece em cena alcancada A
    /// PARTIR do menu, porque o EventSystem de verdade morava na cena do menu. Quem da' Play
    /// direto em Halu nunca ve o defeito — foi por isso que ele passou tanto tempo em pe'.
    /// </summary>
    public static class RodarTesteDaPausa
    {
        const string CENA = "Assets/_Sardael/Cenas/Menu_Principal.unity";

        [MenuItem("Sardael/Testar a Pausa")]
        public static void Rodar()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);

            // a janela do Game na frente: o teste clica por coordenada de tela e o relatorio so'
            // vale se der pra assistir a mesma coisa que ele mediu
            EditorApplication.ExecuteMenuItem("Window/General/Game");

            SessionState.SetBool(TesteDaPausa.BANDEIRA, true);
            EditorApplication.EnterPlaymode();
        }
    }
}
