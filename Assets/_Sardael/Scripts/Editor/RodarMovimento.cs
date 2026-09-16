using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Sardael;

namespace SardaelEditor
{
    /// <summary>
    /// Liga o teste automatico na cena de movimento e entra em Play.
    ///
    /// POR QUE ISTO EXISTE: "da' play e testa" foi validado varias vezes e nenhuma delas
    /// estava certo. Quem escreve o codigo precisa ver o jogo rodando antes de dizer que
    /// funciona. Daqui sai um arquivo de numeros por quadro e uma pasta de imagens.
    /// </summary>
    public static class RodarMovimento
    {
        const string CENA = "Assets/_Sardael/Cenas/Movimento_Teste.unity";

        [MenuItem("Sardael/Rodar Teste de Movimento")]
        public static void Rodar()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }

            var cena = EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);

            var heroi = Object.FindAnyObjectByType<MovimentoDoHeroi>();
            if (heroi == null) { Debug.LogError("[Rodar] nao achei o MovimentoDoHeroi na cena"); return; }

            var teste = heroi.GetComponent<TesteDeMovimento>();
            if (teste == null) teste = heroi.gameObject.AddComponent<TesteDeMovimento>();
            teste.heroi = heroi;
            teste.olho = Object.FindAnyObjectByType<Camera>();

            // Guardado DESLIGADO. Quem liga e' a bandeira de sessao, so' nesta viagem.
            teste.rodarAoIniciar = false;
            teste.sairAoTerminar = false;
            EditorUtility.SetDirty(teste);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);

            SessionState.SetBool("sardael.teste.auto", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
