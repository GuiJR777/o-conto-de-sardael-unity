using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Sardael;

namespace SardaelEditor
{
    /// <summary>
    /// Abre a Movimento_Teste, entra em Play e deixa o <see cref="TesteDoAparo"/> rodar sozinho.
    ///
    /// Mesma disciplina do resto: nada deste teste fica gravado na cena. A bandeira de sessao
    /// liga so' nesta viagem e o objeto nasce no Play, se apaga no fim. A cena e' dele.
    /// </summary>
    public static class RodarTesteDoAparo
    {
        const string CENA = "Assets/_Sardael/Cenas/Movimento_Teste.unity";

        [MenuItem("Sardael/Rodar Teste do Aparo")]
        public static void Rodar()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);

            var duelo = Object.FindAnyObjectByType<Duelo>();
            if (duelo == null)
            {
                Debug.LogError(
                    "[Aparo] nao achei o Duelo em " + CENA + ". Rode antes: Sardael > Montar Duelo (Movimento_Teste)");
                return;
            }

            EditorApplication.ExecuteMenuItem("Window/General/Game");
            SessionState.SetBool(TesteDoAparo.BANDEIRA, true);
            EditorApplication.EnterPlaymode();
        }
    }
}
